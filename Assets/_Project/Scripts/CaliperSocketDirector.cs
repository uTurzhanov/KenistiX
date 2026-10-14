using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// Директор сокетов штангенциркуля. Без kinematic-заморозки в воздухе:
// блок садится в свой Socket и держится им — нет проваливания и зависания.
//
// Ты сам создаешь 6 сокетов (по точке на габарит):
//   Socket_BIG_L, Socket_BIG_W, Socket_BIG_H,
//   Socket_SMALL_L, Socket_SMALL_W, Socket_SMALL_H.
// Этот скрипт висит на ТРИГГЕРЕ зоны измерения и включает
// только 1 нужный сокет: по категории рядом (BIG/SMALL) + режиму UI (L/W/H).
//
// ВАЖНО: повесь скрипт на ТОТ ЖЕ объект где триггер-бокс зоны измерения.
[RequireComponent(typeof(BoxCollider))]
public class CaliperSocketDirector : MonoBehaviour
{
    public enum SizeCat { Unknown, BIG, SMALL }
    public enum MeasureMode { Length = 0, Width = 1, Height = 2 }

    [Header("Сокеты BIG: Длина / Ширина / Высота")]
    [SerializeField] private XRSocketInteractor socketBigLength;
    [SerializeField] private XRSocketInteractor socketBigWidth;
    [SerializeField] private XRSocketInteractor socketBigHeight;

    [Header("Сокеты SMALL: Длина / Ширина / Высота")]
    [SerializeField] private XRSocketInteractor socketSmallLength;
    [SerializeField] private XRSocketInteractor socketSmallWidth;
    [SerializeField] private XRSocketInteractor socketSmallHeight;

    [Header("Режим измерения — 3 кнопки TMP (вместо Dropdown)")]
    [Tooltip("Перетащи сюда Button Длина / Ширина / Высота. OnClick подпишется сам.")]
    [SerializeField] private Button buttonLength;
    [SerializeField] private Button buttonWidth;
    [SerializeField] private Button buttonHeight;
    [SerializeField] private MeasureMode mode = MeasureMode.Length;

    [Header("Штангель в руках (обязательное условие)")]
    [Tooltip("Grab со штангеля. Если пусто — найдется сам в родителях. Сокет включается только пока штангель держат.")]
    [SerializeField] private XRGrabInteractable caliperGrab;

    [Header("Фильтр по тегам")]
    [Tooltip("Теги на брусках. Большие = BIG, маленькие = SMALL")]
    [SerializeField] private string bigTag = "BIG";
    [SerializeField] private string smallTag = "SMALL";

    private readonly HashSet<XRBaseInteractable> _hovered = new HashSet<XRBaseInteractable>();
    private SizeCat _activeCat = SizeCat.Unknown;
    private bool _isHeld;

    // Залипание занятого сокета: пока сокет держит брусок — директор его не выключает.
    // Иначе петля: сокет притянул брусок к Attach → брусок вышел из зоны → категория Unknown →
    // сокет выключился → deselect → брусок упал обратно в зону → всё по кругу
    // (дёргание + toggling kinematic/gravity от XRGrabInteractable + прыжки родителя Attach<->сцена).
    private XRSocketInteractor _lockedSocket;

    private void Awake()
    {
        var col = GetComponent<BoxCollider>();
        if (col != null && !col.isTrigger) col.isTrigger = true;

        if (caliperGrab == null)
            caliperGrab = GetComponentInParent<XRGrabInteractable>();
    }

    private void OnValidate()
    {
        var col = GetComponent<BoxCollider>();
        if (col != null && !col.isTrigger) col.isTrigger = true;
    }

    private void Start()
    {
        BindButtons();
        EnsureSocketTriggers();
        RefreshSockets();
    }

    // Чтобы не назначать каждому сокету коллайдер вручную:
    // если на сокете нет своего триггера — создаем маленькую сферу на Attach.
    // Общий родительский триггер для ховера сокетов использовать нельзя:
    // XRIT смотрит триггеры только на объекте самого сокета.
    // Один общий триггер остается только здесь, на директоре, для определения BIG/SMALL.
    [Tooltip("Радиус авто-триггера сокета если своего нет")]
    [SerializeField] private float autoSocketRadius = 0.02f;
    private void EnsureSocketTriggers()
    {
        EnsureOne(socketBigLength);
        EnsureOne(socketBigWidth);
        EnsureOne(socketBigHeight);
        EnsureOne(socketSmallLength);
        EnsureOne(socketSmallWidth);
        EnsureOne(socketSmallHeight);
    }
    private void EnsureOne(XRSocketInteractor s)
    {
        if (s == null) return;
        Collider c = s.GetComponent<Collider>();
        if (c == null)
        {
            var sp = s.gameObject.AddComponent<SphereCollider>();
            sp.isTrigger = true;
            sp.radius = autoSocketRadius;
            // Центр на Attach чтобы зона была в точке фиксации
            if (s.attachTransform != null)
                sp.center = s.transform.InverseTransformPoint(s.attachTransform.position);
        }
        else if (!c.isTrigger)
        {
            c.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        BindButtons();
        SubscribeSockets();
        if (caliperGrab == null)
            caliperGrab = GetComponentInParent<XRGrabInteractable>();
        if (caliperGrab != null)
        {
            caliperGrab.selectEntered.AddListener(OnCaliperGrabbed);
            caliperGrab.selectExited.AddListener(OnCaliperReleased);
            _isHeld = caliperGrab.isSelected;
        }
    }

    private void OnDisable()
    {
        if (buttonLength != null) buttonLength.onClick.RemoveListener(SetModeLength);
        if (buttonWidth != null) buttonWidth.onClick.RemoveListener(SetModeWidth);
        if (buttonHeight != null) buttonHeight.onClick.RemoveListener(SetModeHeight);
        UnsubscribeSockets();
        if (caliperGrab != null)
        {
            caliperGrab.selectEntered.RemoveListener(OnCaliperGrabbed);
            caliperGrab.selectExited.RemoveListener(OnCaliperReleased);
        }
        _isHeld = false;
        _lockedSocket = null;
    }

    private System.Collections.Generic.IEnumerable<XRSocketInteractor> AllSockets()
    {
        yield return socketBigLength;
        yield return socketBigWidth;
        yield return socketBigHeight;
        yield return socketSmallLength;
        yield return socketSmallWidth;
        yield return socketSmallHeight;
    }

    private void SubscribeSockets()
    {
        foreach (var s in AllSockets())
        {
            if (s == null) continue;
            s.selectEntered.AddListener(OnSocketSelected);
            s.selectExited.AddListener(OnSocketReleased);
            if (s.hasSelection) _lockedSocket = s;
        }
    }

    private void UnsubscribeSockets()
    {
        foreach (var s in AllSockets())
        {
            if (s == null) continue;
            s.selectEntered.RemoveListener(OnSocketSelected);
            s.selectExited.RemoveListener(OnSocketReleased);
        }
    }

    private void OnSocketSelected(SelectEnterEventArgs args)
    {
        var s = args.interactorObject as XRSocketInteractor;
        if (s == null) return;
        _lockedSocket = s;
        RefreshSockets();
    }

    private void OnSocketReleased(SelectExitEventArgs args)
    {
        var s = args.interactorObject as XRSocketInteractor;
        if (s == null) return;
        if (_lockedSocket == s) _lockedSocket = null;
        RefreshSockets();
    }

    private void OnCaliperGrabbed(SelectEnterEventArgs args)
    {
        _isHeld = true;
        RefreshSockets();
    }

    private void OnCaliperReleased(SelectExitEventArgs args)
    {
        _isHeld = false;
        RefreshSockets();
    }

    private bool _buttonsBound;
    private void BindButtons()
    {
        if (_buttonsBound) return;
        _buttonsBound = true;
        if (buttonLength != null) buttonLength.onClick.AddListener(SetModeLength);
        if (buttonWidth != null) buttonWidth.onClick.AddListener(SetModeWidth);
        if (buttonHeight != null) buttonHeight.onClick.AddListener(SetModeHeight);
    }

    // ---------- UI: 3 Button TMP (Dropdown удален) ----------
    public void SetMode(int m)
    {
        mode = (MeasureMode)Mathf.Clamp(m, 0, 2);
        RefreshSockets();
    }
    public void SetModeLength() { SetMode(0); }
    public void SetModeWidth() { SetMode(1); }
    public void SetModeHeight() { SetMode(2); }
    public MeasureMode CurrentMode => mode;
    public string CurrentModeName => mode.ToString();

    // ---------- Триггер зоны: определяем BIG / SMALL ----------
    private void OnTriggerEnter(Collider other)
    {
        var it = FindInteractable(other);
        if (it == null) return;
        _hovered.Add(it);
        UpdateCategory();
    }

    private void OnTriggerExit(Collider other)
    {
        var it = FindInteractable(other);
        if (it == null) return;
        _hovered.Remove(it);
        UpdateCategory();
    }

    private XRBaseInteractable FindInteractable(Collider other)
    {
        // XR-интеракт на самом коллайдере или родителе (брусок целиком).
        // Бруски всегда с XRGrabInteractable, других Rigidbody в зоне не ждём.
        return other.GetComponentInParent<XRBaseInteractable>();
    }

    private void UpdateCategory()
    {
        _hovered.RemoveWhere(x => x == null);
        SizeCat cat = SizeCat.Unknown;
        foreach (var it in _hovered)
        {
            cat = CategoryOf(it.gameObject.tag);
            if (cat != SizeCat.Unknown) break;
        }
        _activeCat = cat;
        RefreshSockets();
    }

    private SizeCat CategoryOf(string tag)
    {
        if (tag == bigTag) return SizeCat.BIG;
        if (tag == smallTag) return SizeCat.SMALL;
        return SizeCat.Unknown;
    }

    // ---------- Включаем 1 сокет, остальные глушим ----------
    // Условие: штангель В РУКЕ + брусок в зоне + совпали размер и ось.
    // В начале (не держат / пустая зона) все 6 выключены.
    private void RefreshSockets()
    {
        // Пока сокет держит брусок — горит только он, остальное не трогаем.
        // Иначе выход бруска из зоны в момент притягивания гасит сокет -> петля select/deselect.
        if (_lockedSocket != null)
        {
            foreach (var s in AllSockets())
                SetSocket(s, s == _lockedSocket);
            UpdateButtonVisuals();
            return;
        }

        bool big = _isHeld && _activeCat == SizeCat.BIG;
        bool small = _isHeld && _activeCat == SizeCat.SMALL;

        SetSocket(socketBigLength, big && mode == MeasureMode.Length);
        SetSocket(socketBigWidth, big && mode == MeasureMode.Width);
        SetSocket(socketBigHeight, big && mode == MeasureMode.Height);

        SetSocket(socketSmallLength, small && mode == MeasureMode.Length);
        SetSocket(socketSmallWidth, small && mode == MeasureMode.Width);
        SetSocket(socketSmallHeight, small && mode == MeasureMode.Height);

        UpdateButtonVisuals();
    }

    // Подсветка активной кнопки: активная неинтерактивна
    private void UpdateButtonVisuals()
    {
        if (buttonLength != null) buttonLength.interactable = (mode != MeasureMode.Length);
        if (buttonWidth != null) buttonWidth.interactable = (mode != MeasureMode.Width);
        if (buttonHeight != null) buttonHeight.interactable = (mode != MeasureMode.Height);
    }

    private void SetSocket(XRSocketInteractor s, bool on)
    {
        if (s == null) return;
        // Страховка: никогда не гасим сокет, в котором прямо сейчас лежит брусок
        if (!on && s.hasSelection) return;
        s.socketActive = on;
        // enabled=false чтобы чужой размер вообще не мог заховетиться
        s.enabled = on;
    }

    // Для отладки/логгера (учитывает залипание: категория могла уже сброситься в Unknown)
    public string ActiveSocketName()
    {
        if (_lockedSocket != null)
        {
            if (_lockedSocket == socketBigLength) return "BIG_L";
            if (_lockedSocket == socketBigWidth) return "BIG_W";
            if (_lockedSocket == socketBigHeight) return "BIG_H";
            if (_lockedSocket == socketSmallLength) return "SMALL_L";
            if (_lockedSocket == socketSmallWidth) return "SMALL_W";
            if (_lockedSocket == socketSmallHeight) return "SMALL_H";
            return "LOCKED";
        }        if (_activeCat == SizeCat.BIG && mode == MeasureMode.Length) return "BIG_L";
        if (_activeCat == SizeCat.BIG && mode == MeasureMode.Width) return "BIG_W";
        if (_activeCat == SizeCat.BIG && mode == MeasureMode.Height) return "BIG_H";
        if (_activeCat == SizeCat.SMALL && mode == MeasureMode.Length) return "SMALL_L";
        if (_activeCat == SizeCat.SMALL && mode == MeasureMode.Width) return "SMALL_W";
        if (_activeCat == SizeCat.SMALL && mode == MeasureMode.Height) return "SMALL_H";
        return "NONE";
    }
}
