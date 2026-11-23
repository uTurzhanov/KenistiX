using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Провод / кабель / верёвка в одном скрипте. Положить на пустой GameObject.
///
/// Физика: Verlet (position-based dynamics). Никаких Rigidbody и Joint, поэтому
/// толщина провода не влияет на стабильность (в отличие от цепочки Rigidbody,
/// которая трясётся, когда узлы становятся мелкими).
///
/// Рендер: трубка со сглаживанием по Catmull-Rom, радиус задаётся независимо
/// от физики.
///
/// Использование:
///   startAnchor / endAnchor = Transform штекеров (пустышка на кончике штекера).
///   Если anchor пустой, этот конец свободен и висит под действием гравитации.
///   Подключение/отключение в коде: SetStartAnchor / SetEndAnchor.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
[DisallowMultipleComponent]
public class Cable : MonoBehaviour
{
    [Header("Концы")]
    [Tooltip("Если задан, первый узел прибит к этому Transform (штекер, розетка, барабан).")]
    public Transform startAnchor;
    [Tooltip("Если задан, последний узел прибит к этому Transform.")]
    public Transform endAnchor;

    [Header("Длина")]
    [Tooltip("Если оба конца заданы, длина = расстояние между ними * Slack (на старте).")]
    public bool autoLength = true;
    [Min(1f)] public float autoLengthSlack = 1.15f;
    [Tooltip("Длина провода в метрах (если autoLength выключен или нет обоих концов). " +
             "Можно менять в рантайме или через SetLength().")]
    [Min(0.01f)] public float length = 1f;
    [Tooltip("Желаемая длина одного физического сегмента (м). Меньше = плавнее изгиб, дороже.")]
    [Min(0.005f)] public float segmentLength = 0.05f;
    [Range(2, 200)] public int maxNodes = 80;

    [Header("Внешний вид")]
    [Tooltip("Радиус провода (м). 0.004 = 8 мм толщиной. Физику не затрагивает.")]
    [Min(0.0001f)] public float radius = 0.004f;
    [Range(3, 16)] public int radialSegments = 6;
    [Tooltip("Сколько точек рисовать между физическими узлами (сглаживание).")]
    [Range(1, 8)] public int renderSubdivisions = 3;
    [Tooltip("Если пусто, создаётся материал по умолчанию текущего рендер-пайплайна.")]
    public Material material;
    public Color color = new Color(0.08f, 0.08f, 0.08f, 1f);
    [Tooltip("Через сколько метров повторяется текстура по длине.")]
    public float uvMetersPerRepeat = 0.1f;

    [Header("Физика")]
    [Range(-2f, 2f)] public float gravityScale = 1f;
    [Tooltip("Затухание скорости (1/с). Больше = провод быстрее успокаивается.")]
    [Range(0f, 10f)] public float damping = 1.5f;
    [Tooltip("Жёсткость на изгиб. 0 = мягкая нитка, 1 = упругий толстый кабель.")]
    [Range(0f, 1f)] public float bendStiffness = 0.15f;
    [Range(1, 6)] public int substeps = 2;
    [Tooltip("Итераций решателя длины за подшаг. Больше = меньше растягивается.")]
    [Range(1, 40)] public int iterations = 10;
    [Tooltip("Максимальная скорость узла (м/с), защита от взрывов при телепорте штекера.")]
    public float maxNodeSpeed = 30f;

    [Header("Столкновения")]
    public bool collide = true;
    public LayerMask collisionLayers = ~0;
    [Tooltip("Радиус коллизии узла (м). 0 = брать радиус провода.")]
    public float collisionRadius = 0f;
    [Range(0f, 1f)] public float contactFriction = 0.2f;
    [Tooltip("Коллайдеры штекера (объекта, к которому прибит конец) игнорируются только " +
             "рядом с якорем: на этом расстоянии вдоль провода (м). Дальше от якоря " +
             "провод сталкивается и со своим штекером, поэтому не проходит сквозь него.")]
    [Min(0f)] public float anchorIgnoreDistance = 0.06f;
    [Tooltip("Для Plane, Terrain и невыпуклых MeshCollider: с какой высоты над узлом " +
             "пускается луч вниз, чтобы поймать поверхность. Должно быть больше, " +
             "чем глубина, на которую узел может провалиться за один шаг.")]
    [Min(0.02f)] public float floorSupportDistance = 0.15f;

    [Header("Натяжение: тянуть привязанные объекты")]
    [Tooltip("Когда провод натянут до предела, он не рвётся, а тянет к себе объекты " +
             "(с Rigidbody), к которым прибиты его концы.")]
    public bool pullBodies = true;
    [Tooltip("Запас до начала тяги: доля длины. 0.02 = тянуть, когда расстояние " +
             "превысило длину провода на 2%.")]
    [Range(0f, 0.2f)] public float pullSlack = 0.02f;
    [Tooltip("Сила тяги: ускорение (м/с²) на каждый метр растяжения. " +
             "Не зависит от массы объекта.")]
    public float pullStiffness = 60f;
    [Tooltip("Гашение скорости вдоль провода, чтобы объект не раскачивался как на пружине.")]
    public float pullDamping = 8f;
    [Tooltip("Максимальное ускорение тяги (м/с²). Должно быть больше, чем трение " +
             "объекта о пол, иначе тяжёлый объект не сдвинется.")]
    public float maxPullAcceleration = 40f;

    [Header("Хватать мышью (необязательно)")]
    public bool allowMouseGrab = false;
    public float grabRadius = 0.1f;

    // ---------------- публичное API ----------------

    public int NodeCount => _pos != null ? _pos.Length : 0;
    public Vector3 GetNodePosition(int i) => _pos[i];

    public void SetStartAnchor(Transform t) { startAnchor = t; }
    public void SetEndAnchor(Transform t) { endAnchor = t; }

    /// <summary>
    /// Хват рукой/контроллером (VR). Берёт ближайший свободный узел в радиусе
    /// <paramref name="radius"/> от руки, и он следует за рукой. Узлы, прибитые
    /// к якорям, не хватаются. Возвращает true, если что-то схвачено.
    /// </summary>
    public bool BeginGrab(Transform hand, float radius)
    {
        if (hand == null || _pos == null) return false;

        float best = radius;
        int bestIdx = -1;
        for (int i = 0; i < _pos.Length; i++)
        {
            if ((i == 0 && startAnchor != null) ||
                (i == _pos.Length - 1 && endAnchor != null)) continue;

            float d = Vector3.Distance(hand.position, _pos[i]);
            if (d < best) { best = d; bestIdx = i; }
        }

        if (bestIdx < 0) return false;

        _grab = bestIdx;
        _grabHand = hand;
        _grabLocal = hand.InverseTransformPoint(_pos[bestIdx]); // без рывка при хвате
        return true;
    }

    /// <summary>Отпустить провод.</summary>
    public void EndGrab()
    {
        _grabHand = null;
        _grab = -1;
    }

    public bool IsGrabbed => _grabHand != null && _grab >= 0;

    /// <summary>Меняет длину в рантайме (барабан, натяжение). Форма сохраняется.</summary>
    public void SetLength(float newLength)
    {
        length = Mathf.Max(0.01f, newLength);
        _lengthApplied = length;
        if (_pos == null) return;

        int want = DesiredNodeCount(length);
        if (want != _pos.Length) Resample(want);
        _rest = length / (_pos.Length - 1);
    }

    /// <summary>Пересоздаёт провод заново по текущим якорям.</summary>
    public void Rebuild()
    {
        if (autoLength && startAnchor != null && endAnchor != null)
            length = Mathf.Max(0.01f,
                Vector3.Distance(startAnchor.position, endAnchor.position) * autoLengthSlack);

        _lengthApplied = length;
        int n = DesiredNodeCount(length);
        _pos = new Vector3[n];
        _prev = new Vector3[n];
        _rest = length / (n - 1);

        Vector3 a = startAnchor != null ? startAnchor.position : transform.position;
        Vector3 down = Physics.gravity.sqrMagnitude > 1e-6f ? Physics.gravity.normalized : Vector3.down;
        Vector3 b = endAnchor != null ? endAnchor.position : a + down * length;

        // Начальная провисшая форма (парабола), чтобы не стартовать натянутой струной.
        float d = Vector3.Distance(a, b);
        float sag = (endAnchor != null && length > d)
            ? Mathf.Sqrt(3f * Mathf.Max(d, 0.01f) * (length - d) / 8f) : 0f;

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)(n - 1);
            _pos[i] = Vector3.Lerp(a, b, t) + down * (4f * sag * t * (1f - t));
            _prev[i] = _pos[i];
        }

        _grab = -1;
        RefreshAnchorCache();
    }

    // ---------------- внутреннее состояние ----------------

    Vector3[] _pos, _prev;
    float _rest, _lengthApplied;
    bool _pinStart, _pinEnd;
    int _grab = -1;
    float _grabDepth;
    Transform _grabHand;
    Vector3 _grabLocal;

    readonly Collider[] _hits = new Collider[8];
    readonly RaycastHit[] _rayHits = new RaycastHit[8];
    Transform _cStart, _cEnd;
    Rigidbody _rbStart, _rbEnd;
    float _stretchStart, _stretchEnd;      // растяжение у каждого конца (м)
    Vector3 _dirStart, _dirEnd;            // куда тянуть объект на конце
    readonly int[] _pinIdx = new int[3];

    // рендер
    Mesh _mesh;
    MeshFilter _mf;
    MeshRenderer _mr;
    Material _ownMaterial;
    Vector3[] _rp, _verts, _norms;
    Vector2[] _uvs;
    float[] _cum;
    int[] _tris;
    int _bufRc = -1, _bufRs = -1;

    // ---------------- Unity ----------------

    void Awake()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();

        _mesh = new Mesh { name = "CableMesh" };
        _mesh.MarkDynamic();
        _mf.sharedMesh = _mesh;

        if (material != null)
        {
            _mr.sharedMaterial = material;
        }
        else
        {
            Material baseMat = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.defaultMaterial
                : null;
            _ownMaterial = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Standard"));
            if (_ownMaterial.HasProperty("_BaseColor")) _ownMaterial.SetColor("_BaseColor", color);
            if (_ownMaterial.HasProperty("_Color")) _ownMaterial.SetColor("_Color", color);
            _mr.sharedMaterial = _ownMaterial;
        }
    }

    void Start()
    {
        Rebuild();
        BuildMesh();
    }

    void OnDestroy()
    {
        if (_mesh != null) Destroy(_mesh);
        if (_ownMaterial != null) Destroy(_ownMaterial);
    }

    void LateUpdate()
    {
        if (_pos == null) return;

        Step(Mathf.Min(Time.deltaTime, 1f / 30f));
        BuildMesh();
    }

    // ---------------- симуляция ----------------

    int DesiredNodeCount(float len) =>
        Mathf.Clamp(Mathf.CeilToInt(len / Mathf.Max(0.005f, segmentLength)) + 1, 2, Mathf.Max(2, maxNodes));

    void RefreshAnchorCache()
    {
        if (_cStart != startAnchor)
        {
            _cStart = startAnchor;
            _rbStart = startAnchor != null ? startAnchor.GetComponentInParent<Rigidbody>() : null;
        }
        if (_cEnd != endAnchor)
        {
            _cEnd = endAnchor;
            _rbEnd = endAnchor != null ? endAnchor.GetComponentInParent<Rigidbody>() : null;
        }
    }

    bool IsPinned(int i) =>
        (i == 0 && _pinStart) || (i == _pos.Length - 1 && _pinEnd) || i == _grab;

    void Step(float dt)
    {
        RefreshAnchorCache();

        if (!Mathf.Approximately(length, _lengthApplied)) SetLength(length);

        int n = _pos.Length;
        _pinStart = startAnchor != null;
        _pinEnd = endAnchor != null;

        if (_pinStart) { _pos[0] = startAnchor.position; _prev[0] = _pos[0]; }
        if (_pinEnd) { _pos[n - 1] = endAnchor.position; _prev[n - 1] = _pos[n - 1]; }

        HandleGrab();
        ComputePull();

        if (dt <= 0f) return;

        int sub = Mathf.Max(1, substeps);
        float h = dt / sub;
        Vector3 g = Physics.gravity * gravityScale;
        float keep = Mathf.Exp(-damping * h);
        float maxStep = maxNodeSpeed * h;

        for (int s = 0; s < sub; s++)
        {
            // интегрирование
            for (int i = 0; i < n; i++)
            {
                if (IsPinned(i)) continue;

                Vector3 v = (_pos[i] - _prev[i]) * keep;
                if (v.magnitude > maxStep) v = v.normalized * maxStep;

                _prev[i] = _pos[i];
                _pos[i] += v + g * (h * h);
            }

            SolveConstraints();

            if (collide) Collide();
        }
    }

    void SolveConstraints()
    {
        int n = _pos.Length;

        for (int it = 0; it < iterations; it++)
        {
            // сегменты (длина), направление чередуем, чтобы не было перекоса
            bool forward = (it & 1) == 0;
            for (int k = 0; k < n - 1; k++)
            {
                int i = forward ? k : n - 2 - k;
                SolveDistance(i, i + 1, _rest, 1f, false);
            }

            // изгиб: узлы i и i+2 не должны сближаться
            if (bendStiffness > 0f)
            {
                for (int i = 0; i < n - 2; i++)
                    SolveDistance(i, i + 2, _rest * 2f, bendStiffness * 0.5f, true);
            }
        }
    }

    void SolveDistance(int a, int b, float rest, float stiffness, bool onlyPush)
    {
        Vector3 d = _pos[b] - _pos[a];
        float dist = d.magnitude;
        if (dist < 1e-7f) return;
        if (onlyPush && dist >= rest) return;

        float wa = IsPinned(a) ? 0f : 1f;
        float wb = IsPinned(b) ? 0f : 1f;
        float ws = wa + wb;
        if (ws <= 0f) return;

        Vector3 corr = d * ((dist - rest) / dist * stiffness / ws);
        _pos[a] += corr * wa;
        _pos[b] -= corr * wb;
    }

    void Collide()
    {
        float r = collisionRadius > 0f ? collisionRadius : radius;
        int n = _pos.Length;

        for (int i = 0; i < n; i++)
        {
            if (IsPinned(i)) continue;

            int c = Physics.OverlapSphereNonAlloc(_pos[i], r, _hits, collisionLayers,
                                                  QueryTriggerInteraction.Ignore);
            for (int k = 0; k < c; k++)
            {
                Collider col = _hits[k];
                if (col == null || IsIgnored(col, i)) continue;

                // ClosestPoint не работает с невыпуклыми мешами и Terrain:
                // их обрабатывает CollideRay ниже
                if (IsHardCollider(col)) continue;

                Vector3 cp = col.ClosestPoint(_pos[i]);
                Vector3 d = _pos[i] - cp;
                float dist = d.magnitude;
                if (dist >= r) continue;                  // не касается
                if (dist < 1e-6f) { PushOutUp(col, i, r); continue; } // узел внутри коллайдера

                _pos[i] = cp + d / dist * r;
                _prev[i] = Vector3.Lerp(_prev[i], _pos[i], contactFriction);
            }

            CollideRay(i, r);
        }
    }

    /// <summary>
    /// Узел оказался внутри коллайдера (например, провод был заспавнен сквозь
    /// предмет). ClosestPoint в этом случае ничего не даёт, поэтому выводим узел
    /// вверх, на поверхность: провод ложится поверх предмета.
    /// </summary>
    void PushOutUp(Collider col, int i, float r)
    {
        Vector3 up = Physics.gravity.sqrMagnitude > 1e-6f
            ? -Physics.gravity.normalized : Vector3.up;
        float span = col.bounds.size.magnitude + r;

        var ray = new Ray(_pos[i] + up * span, -up);
        if (col.Raycast(ray, out RaycastHit hit, span * 2f))
        {
            _pos[i] = hit.point + hit.normal * r;
            _prev[i] = _pos[i];
        }
    }

    static bool IsHardCollider(Collider col) =>
        (col is MeshCollider mc && !mc.convex) || col is TerrainCollider;

    /// <summary>
    /// Поддержка для поверхностей, к которым нельзя применить ClosestPoint
    /// (Plane, Terrain, невыпуклый MeshCollider): луч сверху вниз от узла.
    /// </summary>
    void CollideRay(int i, float r)
    {
        Vector3 up = Physics.gravity.sqrMagnitude > 1e-6f
            ? -Physics.gravity.normalized : Vector3.up;
        float lift = Mathf.Max(floorSupportDistance, r * 2f);

        int c = Physics.RaycastNonAlloc(_pos[i] + up * lift, -up, _rayHits, lift + r,
                                        collisionLayers, QueryTriggerInteraction.Ignore);

        int best = -1;
        float bestDist = float.MaxValue;
        for (int k = 0; k < c; k++)
        {
            Collider col = _rayHits[k].collider;
            if (col == null || !IsHardCollider(col) || IsIgnored(col, i)) continue;
            if (_rayHits[k].distance < bestDist) { bestDist = _rayHits[k].distance; best = k; }
        }
        if (best < 0) return;

        RaycastHit h = _rayHits[best];
        float depth = r - Vector3.Dot(_pos[i] - h.point, h.normal);
        if (depth <= 0f) return;

        _pos[i] += h.normal * depth;
        _prev[i] = Vector3.Lerp(_prev[i], _pos[i], contactFriction);
    }

    bool IsIgnored(Collider col, int node)
    {
        Transform t = col.transform;
        if (t.IsChildOf(transform)) return true;

        int n = _pos.Length;
        if (_cStart != null && node * _rest < anchorIgnoreDistance &&
            OwnedBy(t, _cStart, _rbStart)) return true;
        if (_cEnd != null && (n - 1 - node) * _rest < anchorIgnoreDistance &&
            OwnedBy(t, _cEnd, _rbEnd)) return true;

        return false;
    }

    static bool OwnedBy(Transform colliderT, Transform anchor, Rigidbody rb)
    {
        Transform owner = rb != null ? rb.transform
                        : (anchor.parent != null ? anchor.parent : anchor);
        return colliderT.IsChildOf(owner);
    }

    // ---------------- натяжение: тяга привязанных объектов ----------------

    /// <summary>
    /// Считает, насколько провод растянут между закреплёнными точками
    /// (якорь, якорь или схваченная рукой точка). Растяжение превращается в тягу
    /// на объекты, к которым прибиты концы. Само применение в FixedUpdate.
    /// </summary>
    void ComputePull()
    {
        _stretchStart = _stretchEnd = 0f;
        if (!pullBodies) return;

        int n = _pos.Length;
        int c = 0;
        if (_pinStart) _pinIdx[c++] = 0;
        if (_grab >= 0 && !(_pinStart && _grab == 0) && !(_pinEnd && _grab == n - 1))
            _pinIdx[c++] = _grab;
        if (_pinEnd) _pinIdx[c++] = n - 1;

        for (int k = 0; k < c - 1; k++)
        {
            int i = _pinIdx[k], j = _pinIdx[k + 1];
            if (i >= j) continue;

            Vector3 d = _pos[j] - _pos[i];
            float dist = d.magnitude;
            if (dist < 1e-5f) continue;

            float restLen = (j - i) * _rest;
            float stretch = dist - restLen * (1f + pullSlack);
            if (stretch <= 0f) continue;

            Vector3 dir = d / dist;
            if (i == 0 && _pinStart) { _stretchStart = stretch; _dirStart = dir; }
            if (j == n - 1 && _pinEnd) { _stretchEnd = stretch; _dirEnd = -dir; }
        }
    }

    void FixedUpdate()
    {
        if (!pullBodies) return;
        ApplyPull(_rbStart, _dirStart, _stretchStart);
        ApplyPull(_rbEnd, _dirEnd, _stretchEnd);
    }

    void ApplyPull(Rigidbody rb, Vector3 dir, float stretch)
    {
        // Кинематические тела (например, штекер в руке) не тянем: рука сильнее.
        if (rb == null || rb.isKinematic || stretch <= 0f) return;

        float a = Mathf.Min(pullStiffness * stretch, maxPullAcceleration);
        float along = Vector3.Dot(LinearVelocity(rb), dir);

        rb.AddForce(dir * (a - pullDamping * along), ForceMode.Acceleration);
    }

    static Vector3 LinearVelocity(Rigidbody rb)
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    // ---------------- захват мышью ----------------

    void HandleGrab()
    {
        // VR / внешний хват: узел следует за рукой
        if (_grabHand != null)
        {
            if (_grab < 0) { _grabHand = null; return; } // провод пересоздан/ресэмплирован
            _pos[_grab] = _grabHand.TransformPoint(_grabLocal);
            _prev[_grab] = _pos[_grab];
            return;
        }

        if (!allowMouseGrab) { _grab = -1; return; }

        Camera cam = Camera.main;
        if (cam == null) return;

        bool down, held;
        Vector2 mp;
#if ENABLE_INPUT_SYSTEM
        var m = Mouse.current;
        if (m == null) return;
        down = m.leftButton.wasPressedThisFrame;
        held = m.leftButton.isPressed;
        mp = m.position.ReadValue();
#else
        down = Input.GetMouseButtonDown(0);
        held = Input.GetMouseButton(0);
        mp = Input.mousePosition;
#endif

        if (!held) { _grab = -1; return; }

        Ray ray = cam.ScreenPointToRay(mp);
        Vector3 fwd = cam.transform.forward;

        if (down)
        {
            float best = grabRadius;
            int bestIdx = -1;
            _grab = -1; // чтобы IsPinned не мешал выбору

            for (int i = 0; i < _pos.Length; i++)
            {
                if ((i == 0 && _pinStart) || (i == _pos.Length - 1 && _pinEnd)) continue;

                float along = Vector3.Dot(_pos[i] - ray.origin, ray.direction);
                if (along < 0f) continue;

                float dist = Vector3.Distance(ray.origin + ray.direction * along, _pos[i]);
                if (dist < best) { best = dist; bestIdx = i; }
            }

            _grab = bestIdx;
            if (_grab >= 0)
                _grabDepth = Vector3.Dot(_pos[_grab] - cam.transform.position, fwd);
        }

        if (_grab >= 0)
        {
            float denom = Vector3.Dot(ray.direction, fwd);
            if (Mathf.Abs(denom) < 1e-4f) return;

            float t = (_grabDepth - Vector3.Dot(ray.origin - cam.transform.position, fwd)) / denom;
            _pos[_grab] = ray.origin + ray.direction * t;
            _prev[_grab] = _pos[_grab];
        }
    }

    // ---------------- ресэмплинг при смене длины ----------------

    void Resample(int want)
    {
        int n = _pos.Length;
        var cum = new float[n];
        for (int i = 1; i < n; i++)
            cum[i] = cum[i - 1] + Vector3.Distance(_pos[i - 1], _pos[i]);
        float total = cum[n - 1];

        var np = new Vector3[want];
        int j = 0;
        for (int i = 0; i < want; i++)
        {
            float d = total * i / (want - 1);
            while (j < n - 2 && cum[j + 1] < d) j++;

            float segL = cum[j + 1] - cum[j];
            float t = segL > 1e-6f ? Mathf.Clamp01((d - cum[j]) / segL) : 0f;
            np[i] = Vector3.Lerp(_pos[j], _pos[j + 1], t);
        }

        _pos = np;
        _prev = (Vector3[])np.Clone();
        _grab = -1;
    }

    // ---------------- меш ----------------

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (2f * p1 + (-p0 + p2) * t +
                       (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                       (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    static Vector3 SafeDir(Vector3 v, Vector3 fallback) =>
        v.sqrMagnitude < 1e-10f ? fallback : v.normalized;

    static Vector3 Perpendicular(Vector3 axis)
    {
        Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.99f
            ? Vector3.right : Vector3.up;
        return Vector3.Cross(axis, reference).normalized;
    }

    void EnsureMeshBuffers(int rc, int rs)
    {
        if (rc == _bufRc && rs == _bufRs) return;

        int vpr = rs + 1;
        _bufRc = rc;
        _bufRs = rs;

        _verts = new Vector3[rc * vpr];
        _norms = new Vector3[rc * vpr];
        _uvs = new Vector2[rc * vpr];
        _cum = new float[rc];
        _tris = new int[(rc - 1) * rs * 6];

        int t = 0;
        for (int r = 0; r < rc - 1; r++)
        {
            for (int s = 0; s < rs; s++)
            {
                int a = r * vpr + s;
                int b = (r + 1) * vpr + s;
                _tris[t++] = a; _tris[t++] = b; _tris[t++] = a + 1;
                _tris[t++] = a + 1; _tris[t++] = b; _tris[t++] = b + 1;
            }
        }

        _mesh.Clear();
        _mesh.indexFormat = _verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
        _topologyDirty = true;
    }

    bool _topologyDirty;

    void BuildMesh()
    {
        int n = _pos.Length;
        int sub = Mathf.Max(1, renderSubdivisions);
        int rc = (n - 1) * sub + 1;
        int rs = Mathf.Max(3, radialSegments);
        int vpr = rs + 1;

        if (_rp == null || _rp.Length != rc) _rp = new Vector3[rc];

        // сглаженная центральная линия
        for (int i = 0; i < n - 1; i++)
        {
            Vector3 p0 = _pos[Mathf.Max(i - 1, 0)];
            Vector3 p1 = _pos[i];
            Vector3 p2 = _pos[i + 1];
            Vector3 p3 = _pos[Mathf.Min(i + 2, n - 1)];

            for (int s = 0; s < sub; s++)
                _rp[i * sub + s] = CatmullRom(p0, p1, p2, p3, s / (float)sub);
        }
        _rp[rc - 1] = _pos[n - 1];

        EnsureMeshBuffers(rc, rs);

        // длина вдоль кривой для UV
        float total = 0f;
        _cum[0] = 0f;
        for (int i = 1; i < rc; i++)
        {
            total += Vector3.Distance(_rp[i - 1], _rp[i]);
            _cum[i] = total;
        }

        Matrix4x4 w2l = transform.worldToLocalMatrix;
        float uvLen = Mathf.Max(1e-4f, uvMetersPerRepeat);

        Vector3 tangent = SafeDir(_rp[1] - _rp[0], Vector3.down);
        Vector3 normal = Perpendicular(tangent);

        for (int r = 0; r < rc; r++)
        {
            Vector3 next = r == 0 ? tangent
                         : r == rc - 1 ? SafeDir(_rp[r] - _rp[r - 1], tangent)
                         : SafeDir(_rp[r + 1] - _rp[r - 1], tangent);

            // parallel transport: кабель не закручивается вокруг своей оси
            normal = Quaternion.FromToRotation(tangent, next) * normal;
            tangent = next;

            normal = Vector3.ProjectOnPlane(normal, tangent);
            if (normal.sqrMagnitude < 1e-8f) normal = Perpendicular(tangent);
            normal.Normalize();

            Vector3 binormal = Vector3.Cross(tangent, normal);
            float v = _cum[r] / uvLen;

            for (int s = 0; s <= rs; s++)
            {
                float a = s / (float)rs * Mathf.PI * 2f;
                Vector3 dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);

                int idx = r * vpr + s;
                _verts[idx] = w2l.MultiplyPoint3x4(_rp[r] + dir * radius);
                _norms[idx] = w2l.MultiplyVector(dir).normalized;
                _uvs[idx] = new Vector2(s / (float)rs, v);
            }
        }

        _mesh.SetVertices(_verts);
        _mesh.SetNormals(_norms);
        _mesh.SetUVs(0, _uvs);

        if (_topologyDirty)
        {
            _mesh.SetTriangles(_tris, 0);
            _topologyDirty = false;
        }

        _mesh.RecalculateBounds();
    }

    // ---------------- гизмо ----------------

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        if (startAnchor != null) Gizmos.DrawWireSphere(startAnchor.position, 0.02f);
        if (endAnchor != null) Gizmos.DrawWireSphere(endAnchor.position, 0.02f);

        if (_pos == null) return;
        Gizmos.color = new Color(0.3f, 1f, 0.3f, 0.6f);
        for (int i = 0; i < _pos.Length; i++)
            Gizmos.DrawWireSphere(_pos[i], Mathf.Max(radius, 0.005f));
    }
}
