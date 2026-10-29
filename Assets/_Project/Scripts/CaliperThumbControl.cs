using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class CaliperThumbControl : MonoBehaviour
{
    [Header("Механика губки")]
    [Tooltip("Перетащи сюда подвижную часть (#TOL0002_Movable)")]
    [SerializeField] private Transform movableJaw;

    [Tooltip("Ось скольжения В ЛОКАЛЬНЫХ осях РОДИТЕЛЯ movableJaw (не корня!). Проверяется в Scene view: при стике #TOL0002_Movable должна ехать ВДОЛЬ штанги. Едет вниз/вбок — ось неверная, подбери (1,0,0)/(0,1,0)/(0,0,1).")]
    [SerializeField] private Vector3 slideAxis = new Vector3(0, 0, 1);

    [Header("Лимиты (в метрах)")]
    [SerializeField] private float minLimit = 0f;
    [SerializeField] private float maxLimit = 0.15f;

    [Header("Скорость движения (МИРОВЫЕ м/с)")]
    [Tooltip("Полная скорость. 0.07 м/с ~ 0.8-1.1мм/кадр при 60-90 FPS.")]
    [SerializeField] private float slideSpeed = 0.07f;
    [Tooltip("Скорость доводки во внешней зоне. 0.005 м/с ~ 0.05мм/кадр при 90 FPS — вдвое точнее нониуса (0.1мм).")]
    [SerializeField] private float slowSpeed = 0.005f;

    [Header("Ввод (Стики контроллеров)")]
    [SerializeField] private InputActionProperty leftThumbstick;
    [SerializeField] private InputActionProperty rightThumbstick;

    [Header("Блокировка локомоции")]
    [Tooltip("Выключать движение/поворот пока штангель в руке, чтобы стик не вел игрока")]
    [SerializeField] private bool disableLocomotionWhileHeld = true;

    [Header("Упор в брусок (без проваливания)")]
    [Tooltip("Те же точки что в DigitalCaliper: ZeroPoint на неподвижной губке, SliderPoint на подвижной")]
    [SerializeField] private Transform fixedTip;
    [SerializeField] private Transform movingTip;

    [Header("Внутренние губки (упор при РАЗВЕДЕНИИ)")]
    [Tooltip("Пустой объект на подвижной части, строго в точке контакта ВНУТРЕННЕЙ губки (кончик/ребро, которым она упирается в стенку отверстия). Пусто — внутренний упор выключен.")]
    [SerializeField] private Transform innerMovingTip;
    [Tooltip("Радиус начала торможения у внутренней губки (мировые метры).")]
    [SerializeField] private float innerSlowRadius = 0.005f;
    [Tooltip("Радиус жёсткого упора у внутренней губки (мировые метры).")]
    [SerializeField] private float innerTipRadius = 0.0002f;
    [Tooltip("Слои брусков. Можно оставить Everything — точный отбор ниже делают теги.")]
    [SerializeField] private LayerMask blockLayers = ~0;
    [Tooltip("Теги на брусках. Должны совпадать с CaliperSocketDirector (BIG/SMALL)")]
    [SerializeField] private string bigTag = "BIG";
    [SerializeField] private string smallTag = "SMALL";
    [Tooltip("ВНЕШНЯЯ зона: радиус начала торможения (мировые метры). Должен превышать stop-зону минимум на один полный шаг (~0.8мм), иначе проскочим её за кадр. 0.005 = 5мм.")]
    [SerializeField] private float slowRadius = 0.005f;
    [Tooltip("ВНУТРЕННЯЯ зона: жёсткий контакт (мировые метры). Больше радиус — больше систематическая ошибка штангеля: 0.0002 = 0.2мм вплотную к грани.")]
    [SerializeField] private float tipRadius = 0.0002f;
    [Tooltip("Писать в Console когда сработал откат (для проверки)")]
    [SerializeField] private bool debugLog;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grabInteractable;
    private Vector3 _startLocalPos;
    private float _currentTravel = 0f;

    // Мировых метров смещения губки на единицу _currentTravel (локальную).
    // Кэш в Awake: масштаб в рантайме не меняется, поворот на модуль вектора не влияет.
    private float _worldPerLocal = 1f;

    // Знак связи «травел -> зазор», измеряем по факту движения: заранее неизвестно,
    // в какую сторону slideAxis сближает губки. Нужен чтобы предсказать сближение ДО шага.
    private float _gapPerTravel = 0f;
    private bool _hasGradient = false;

    // NonAlloc-буфер щупа: OverlapSphere без аллокаций каждый кадр (GC-фри на Quest)
    private readonly Collider[] _hits = new Collider[64];

    // Запоминаем что выключили, чтобы вернуть назад
    private readonly System.Collections.Generic.List<MonoBehaviour> _disabledProviders =
        new System.Collections.Generic.List<MonoBehaviour>();

    // Отказоустойчивость: если в Movable Jaw воткнут корень — движение губки блокируем,
    // иначе localPosition сдвинет ВЕСЬ штангенциркуль относительно руки (симптом «едет весь штангель»).
    private bool _misconfigured = false;

    private void Awake()
    {
        _grabInteractable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (movableJaw != null)
        {
            _startLocalPos = movableJaw.localPosition;
            _worldPerLocal = WorldPerLocalUnit();
        }
        if (movableJaw == transform)
        {
            Debug.LogError("[Caliper] movableJaw указывает на КОРЕНЬ штангеля — движение заблокировано. Перетащи подвижную часть (#TOL0002_Movable).", this);
            _misconfigured = true;
        }
        else if (movableJaw != null && !movableJaw.IsChildOf(transform))
        {
            Debug.LogWarning("[Caliper] movableJaw не внутри иерархии штангеля — проверь ссылку (ожидается #TOL0002_Movable).", this);
        }
    }

    // Сколько мировых метров даёт единица локального травел вдоль slideAxis.
    // Радиусы OverlapSphere это не затрагивает — они изначально мировые.
    private float WorldPerLocalUnit()
    {
        if (movableJaw == null || movableJaw.parent == null) return 1f;
        float m = movableJaw.parent.localToWorldMatrix.MultiplyVector(slideAxis.normalized).magnitude;
        return m > 1e-6f ? m : 1f;
    }

    private void OnEnable()
    {
        _grabInteractable.selectEntered.AddListener(OnGrabbed);
        _grabInteractable.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        _grabInteractable.selectEntered.RemoveListener(OnGrabbed);
        _grabInteractable.selectExited.RemoveListener(OnReleased);
        RestoreLocomotion();
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (disableLocomotionWhileHeld)
            DisableLocomotion();
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        // Второй рукой все еще держит — локомоцию не возвращаем
        if (_grabInteractable != null && _grabInteractable.isSelected) return;
        RestoreLocomotion();
    }

    private void DisableLocomotion()
    {
        _disabledProviders.Clear();
        // Ищем ВСЮ локомоцию XRIT любых версий: идём по цепочке базовых типов до LocomotionProvider.
        // Имена конкретных провайдеров между версиями меняются (Continuous/Dynamic/Snap/Teleport...),
        // поэтому матчим базовый класс, а не 4 конкретных имени.
        var all = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        foreach (var b in all)
        {
            if (b == null || !b.enabled) continue;
            if (IsLocomotionProvider(b))
            {
                b.enabled = false;
                _disabledProviders.Add(b);
            }
        }
        if (debugLog)
            Debug.Log($"[Caliper] locomotion providers disabled: {_disabledProviders.Count}", this);
    }

    private static bool IsLocomotionProvider(MonoBehaviour b)
    {
        for (var t = b.GetType(); t != null; t = t.BaseType)
        {
            string n = t.Name;
            if (n.Contains("LocomotionProvider")) return true;
            if (n.Contains("MoveProvider") || n.Contains("TurnProvider") || n.Contains("TeleportationProvider")) return true;
        }
        return false;
    }

    private void RestoreLocomotion()
    {
        foreach (var b in _disabledProviders)
        {
            if (b != null) b.enabled = true;
        }
        _disabledProviders.Clear();
    }

    private void Update()
    {
        // 1. Двигаем губку ТОЛЬКО если игрок держит штангенциркуль в руке.
        // При misconfigure (Movable Jaw = корень) стоим: иначе уехал бы весь штангенциркуль.
        if (_misconfigured || !_grabInteractable.isSelected || movableJaw == null) return;

        // 2. Считываем отклонение стиков (ось Y - вверх/вниз)
        float leftY = leftThumbstick.action?.ReadValue<Vector2>().y ?? 0f;
        float rightY = rightThumbstick.action?.ReadValue<Vector2>().y ?? 0f;

        // 3. Берем значение с того стика, который сейчас отклоняют сильнее
        float inputY = Mathf.Abs(leftY) > Mathf.Abs(rightY) ? leftY : rightY;

        // 4. Если стик сдвинут (с небольшой мертвой зоной против дрифта)
        if (Mathf.Abs(inputY) > 0.1f)
        {
            float gapBefore = Gap();

            // Предсказываем сближение ДО шага по измеренному ранее градиенту.
            // Градиента ещё нет (первый кадр) — считаем сближением, это консервативно и безопасно.
            bool closingPredicted = !_hasGradient || (inputY * _gapPerTravel < 0f);

            // Внешние губки упираются при СБЛИЖЕНИИ, внутренние — при РАЗВЕДЕНИИ
            // (у реального штангеля внутренние губки расходятся, когда внешние раскрываются).
            bool openingPredicted = innerMovingTip != null && (!_hasGradient || (inputY * _gapPerTravel > 0f));

            // Зона торможения: щуп только в том направлении, куда реально едем.
            // В обратную сторону — полная скорость (так сбрасывается замедление при реверсе).
            float worldSpeed = slideSpeed;
            bool inSlow = false;
            if (closingPredicted)
                inSlow = ProbeBlocks(movingTip, slowRadius);
            if (openingPredicted)
                inSlow |= ProbeBlocks(innerMovingTip, innerSlowRadius);
            if (inSlow) worldSpeed = slowSpeed;

            // Шаг: мировую скорость переводим в локальные единицы травел
            float prevTravel = _currentTravel;
            _currentTravel += inputY * worldSpeed * Time.deltaTime / _worldPerLocal;

            // Запрещаем выезжать за пределы
            _currentTravel = Mathf.Clamp(_currentTravel, minLimit, maxLimit);

            // Применяем новую позицию
            movableJaw.localPosition = _startLocalPos + (slideAxis.normalized * _currentTravel);

            // Замеряем факт и обновляем градиент для предсказания на следующем кадре
            float gapAfter = Gap();
            float dTravel = _currentTravel - prevTravel;
            if (Mathf.Abs(dTravel) > 1e-9f)
            {
                _gapPerTravel = (gapAfter - gapBefore) / dTravel;
                _hasGradient = true;
            }

            // Внутренняя зона: только при ФАКТИЧЕСКОМ сближении, без продавливания и толкания физики.
            // Шаг здесь уже микро (slowSpeed) — откат на prevTravel и есть микро-откат (<=0.05мм).
            bool closingActual = gapAfter < gapBefore - 0.0000001f;
            bool stop = false;
            if (closingActual)
            {
                stop = ProbeBlocks(movingTip, tipRadius);
                if (stop)
                {
                    _currentTravel = prevTravel;
                    movableJaw.localPosition = _startLocalPos + (slideAxis.normalized * _currentTravel);
                }
            }

            // Внутренний упор: только при ФАКТИЧЕСКОМ разведении
            bool openingActual = gapAfter > gapBefore + 0.0000001f;
            bool stopInner = false;
            if (openingActual && innerMovingTip != null)
            {
                stopInner = ProbeBlocks(innerMovingTip, innerTipRadius);
                if (stopInner)
                {
                    _currentTravel = prevTravel;
                    movableJaw.localPosition = _startLocalPos + (slideAxis.normalized * _currentTravel);
                }
            }

            // Лог КАЖДЫЙ активный кадр (под галкой): по нему видно, какое звено цепи молчит —
            // predicted=false (градиент врёт) / inSlow=false (щуп не видит) / closing=false (Gap сломан) / stop=false (контакта нет)
            if (debugLog)
                Debug.Log($"[Caliper] gap {gapBefore:F6}->{gapAfter:F6} predClose={closingPredicted} slow={inSlow} actClose={closingActual} stop={stop} actOpen={openingActual} stopInner={stopInner} v={worldSpeed:F4}", this);
        }
    }

    private float Gap()
    {
        if (fixedTip == null || movingTip == null) return float.MaxValue;
        return Vector3.Distance(fixedTip.position, movingTip.position);
    }

    // Универсальный щуп: слои — грубый отбор на уровне физики, теги BIG/SMALL — точный.
    // Свои коллайдеры штангеля исключаем.
    // Теги ищем ВВЕРХ от самого коллайдера (а не от Rigidbody): тег может висеть
    // на mesh-child под ригидом — старт со Steiner rigidbody его бы пропустил.
    // Радиус — мировой, масштаб иерархии на него не влияет. Ноль аллокаций (NonAlloc + готовый буфер).
    private bool ProbeBlocks(Transform tip, float worldRadius)
    {
        if (tip == null) return false;
        int n = Physics.OverlapSphereNonAlloc(tip.position, worldRadius, _hits, blockLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider h = _hits[i];
            if (h == null) continue;
            if (h.transform.IsChildOf(transform)) continue;
            Transform t = h.transform;
            while (t != null)
            {
                if (t.CompareTag(bigTag) || t.CompareTag(smallTag)) return true;
                t = t.parent;
            }
        }
        return false;
    }

    // Ловим неназначенные ссылки ещё в редакторе, а не в рантайме по симптому «остановка не работает»
    private void OnValidate()
    {
        if (movableJaw == null) Debug.LogWarning("[Caliper] movableJaw не назначен — губка не поедет.", this);
        if (movableJaw == transform) Debug.LogError("[Caliper] movableJaw указывает на КОРЕНЬ штангеля — ездить будет ВЕСЬ штангенциркуль. Нужна подвижная часть (#TOL0002_Movable).", this);
        if (fixedTip == null || movingTip == null) Debug.LogWarning("[Caliper] FixedTip/MovingTip не назначены — Gap()=inf, упор и доводка НЕ РАБОТАЮТ.", this);
        if (innerMovingTip == null) Debug.Log("[Caliper] innerMovingTip не назначен — упор внутренних губок выключен.", this);
        if (innerSlowRadius <= innerTipRadius) Debug.LogWarning("[Caliper] innerSlowRadius должен быть заметно больше innerTipRadius.", this);
        if (slowRadius <= tipRadius) Debug.LogWarning("[Caliper] slowRadius должен быть заметно больше tipRadius (минимум +1 полный шаг), иначе торможение не успеет.", this);
    }

    // Визуализация невидимого: жёлтая сфера = зона торможения, красная = зона стопа, линия = измеряемый зазор.
    // Смотреть в Scene view при ВЫБРАННОМ штангеле. Если красная сфера не на контактной грани губки — вот и «stop=False при касании».
    private void OnDrawGizmosSelected()
    {
        if (innerMovingTip != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(innerMovingTip.position, innerSlowRadius);
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(innerMovingTip.position, innerTipRadius);
        }
        if (movingTip == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(movingTip.position, slowRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(movingTip.position, tipRadius);
        if (fixedTip != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(fixedTip.position, movingTip.position);
        }
    }
}