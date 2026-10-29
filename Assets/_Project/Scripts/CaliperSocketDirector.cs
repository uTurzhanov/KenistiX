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
// Коллизии «брусок <-> корпус штангеля» отключаются, как только брусок
// входит в зону измерения / наводится на сокет, и возвращаются только
// когда брусок покинул зону и его не держит ни один наш сокет.
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

    // Бруски в зоне измерения (счётчик коллайдеров на каждый интерактабл,
    // т.к. у бруска может быть несколько коллайдеров).
    private readonly Dictionary<XRBaseInteractable, int> _zone = new Dictionary<XRBaseInteractable, int>();
    // Бруски, у которых сейчас отключены коллизии с корпусом штангеля.
    private readonly HashSet<XRBaseInteractable> _ignored = new HashSet<XRBaseInteractable>();
    private readonly List<XRBaseInteractable> _tmp = new List<XRBaseInteractable>();

    private SizeCat _activeCat = SizeCat.Unknown;
    private bool _isHeld;

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
        _buttonsBound = false; // иначе после повторного Enable кнопки не подпишутся

        UnsubscribeSockets();
        if (caliperGrab != null)
        {
            caliperGrab.selectEntered.RemoveListener(OnCaliperGrabbed);
            caliperGrab.selectExited.RemoveListener(OnCaliperReleased);
        }

        // Вернуть все отключённые коллизии, чтобы ничего не осталось "призрачным"
        _tmp.Clear();
        _tmp.AddRange(_ignored);
        foreach (var it in _tmp)
            if (it != null) SetCollisionBetween(it, false);
        _tmp.Clear();
        _ignored.Clear();
        _zone.Clear();

        _activeCat = SizeCat.Unknown;
        _isHeld = false;
        _lockedSocket = null;
    }

    private IEnumerable<XRSocketInteractor> AllSockets()
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
            s.hoverEntered.AddListener(OnSocketHoverEntered);
            s.hoverExited.AddListener(OnSocketHoverExited);
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
            s.hoverEntered.RemoveListener(OnSocketHoverEntered);
            s.hoverExited.RemoveListener(OnSocketHoverExited);
        }
    }

    private void OnSocketHoverEntered(HoverEnterEventArgs args)
    {
        // Брусок притягивается к сокету — корпус уже не должен с ним сталкиваться
        var it = args.interactableObject as XRBaseInteractable;
        if (it == null || it == caliperGrab) return;
        IgnoreBlock(it);
    }

    private void OnSocketHoverExited(HoverExitEventArgs args)
    {
        var it = args.interactableObject as XRBaseInteractable;
        TryRestoreBlock(it, null);
    }

    private void OnSocketSelected(SelectEnterEventArgs args)
    {
        var s = args.interactorObject as XRSocketInteractor;
        if (s == null) return;
        _lockedSocket = s;

        var it = args.interactableObject as XRBaseInteractable;
        if (it != null) IgnoreBlock(it);

        RefreshSockets();
    }

    private void OnSocketReleased(SelectExitEventArgs args)
    {
        var s = args.interactorObject as XRSocketInteractor;
        if (s == null) return;
        if (_lockedSocket == s) _lockedSocket = null;

        // НЕ возвращаем коллизии сразу: брусок ещё между губками, PhysX вытолкнет его с силой.
        // Вернём, когда брусок покинет зону измерения (OnTriggerExit),
        // либо прямо сейчас, если он и так уже вне зоны.
        var it = args.interactableObject as XRBaseInteractable;
        TryRestoreBlock(it, s);

        RefreshSockets();
    }

    private void IgnoreBlock(XRBaseInteractable it)
    {
        if (it == null) return;
        if (_ignored.Add(it))
            SetCollisionBetween(it, true);
    }

    private void TryRestoreBlock(XRBaseInteractable it, XRSocketInteractor exceptSocket)
    {
        if (it == null || !_ignored.Contains(it)) return;

        // Ещё в зоне измерения — держим коллизии отключёнными
        if (_zone.ContainsKey(it)) return;

        // Всё ещё удерживается одним из наших сокетов — тоже не трогаем
        foreach (var s in AllSockets())
        {
            if (s == null || s == exceptSocket) continue;
            if (s.hasSelection && ReferenceEquals(s.firstInteractableSelected, it)) return;
        }

        _ignored.Remove(it);
        SetCollisionBetween(it, false);
    }

    // Отключаем/включаем физику между бруском и ВСЕМ корпусом штангенциркуля (от корня caliperGrab).
    private void SetCollisionBetween(XRBaseInteractable block, bool ignore)
    {
        if (block == null || caliperGrab == null) return;

        var blockColliders = block.transform.GetComponentsInChildren<Collider>(true);
        var caliperColliders = caliperGrab.transform.GetComponentsInChildren<Collider>(true);

        foreach (var bc in blockColliders)
        {
            // IgnoreCollision требует активных коллайдеров
            if (bc == null || bc.isTrigger || !bc.enabled || !bc.gameObject.activeInHierarchy) continue;

            foreach (var cc in caliperColliders)
            {
                if (cc == null || cc == bc || cc.isTrigger || !cc.enabled || !cc.gameObject.activeInHierarchy) continue;
                // Не трогаем коллайдеры самого бруска (если он вдруг лежит под корпусом в иерархии)
                if (cc.transform.IsChildOf(block.transform)) continue;

                Physics.IgnoreCollision(bc, cc, ignore);
            }
        }
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

    private void OnTriggerEnter(Collider other)
    {
        var it = FindInteractable(other);
        if (it == null || it == caliperGrab) return;

        _zone.TryGetValue(it, out int n);
        _zone[it] = n + 1;

        // Размерный брусок вошёл в зону — сразу отключаем коллизии с корпусом,
        // чтобы он не упирался в губки ещё на подлёте к сокету
        if (CategoryOf(it.gameObject.tag) != SizeCat.Unknown)
            IgnoreBlock(it);

        UpdateCategory();
    }

    private void OnTriggerExit(Collider other)
    {
        var it = FindInteractable(other);
        if (it == null) return;
        if (!_zone.TryGetValue(it, out int n)) return;

        if (n <= 1)
        {
            _zone.Remove(it);
            TryRestoreBlock(it, null);
        }
        else
        {
            _zone[it] = n - 1;
        }

        UpdateCategory();
    }

    private XRBaseInteractable FindInteractable(Collider other)
    {
        return other.GetComponentInParent<XRBaseInteractable>();
    }

    private void PruneDestroyed()
    {
        _tmp.Clear();
        foreach (var k in _zone.Keys)
            if (k == null) _tmp.Add(k);
        foreach (var k in _tmp) _zone.Remove(k);
        _tmp.Clear();

        _ignored.RemoveWhere(x => x == null);
    }

    private void UpdateCategory()
    {
        PruneDestroyed();
        SizeCat cat = SizeCat.Unknown;
        foreach (var it in _zone.Keys)
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

    private void RefreshSockets()
    {
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

    private void UpdateButtonVisuals()
    {
        if (buttonLength != null) buttonLength.interactable = (mode != MeasureMode.Length);
        if (buttonWidth != null) buttonWidth.interactable = (mode != MeasureMode.Width);
        if (buttonHeight != null) buttonHeight.interactable = (mode != MeasureMode.Height);
    }

    private void SetSocket(XRSocketInteractor s, bool on)
    {
        if (s == null) return;

        // Защита от выключения занятого сокета (hasSelection)
        // И притягивающегося в данный момент объекта (hasHover)
        bool isBusy = s.hasSelection || s.hasHover;

        if (!on && isBusy) return;

        s.socketActive = on;
        s.enabled = on;
    }

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
        }
        if (_activeCat == SizeCat.BIG && mode == MeasureMode.Length) return "BIG_L";
        if (_activeCat == SizeCat.BIG && mode == MeasureMode.Width) return "BIG_W";
        if (_activeCat == SizeCat.BIG && mode == MeasureMode.Height) return "BIG_H";
        if (_activeCat == SizeCat.SMALL && mode == MeasureMode.Length) return "SMALL_L";
        if (_activeCat == SizeCat.SMALL && mode == MeasureMode.Width) return "SMALL_W";
        if (_activeCat == SizeCat.SMALL && mode == MeasureMode.Height) return "SMALL_H";
        return "NONE";
    }
}