using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class NearFarUICursor : MonoBehaviour
{
    [Header("Интеракторы контроллера (XRI 3.x)")]
    [SerializeField] private NearFarInteractor nearFarInteractor;
    [SerializeField] private XRPokeInteractor pokeInteractor;

    [Header("Настройки 3D-точки курсора")]
    [SerializeField] private float dotRadius = 0.008f;      // 8 мм
    [SerializeField] private float surfaceOffset = 0.003f;  // 3 мм над поверхностью панели
    [SerializeField] private float maxRayDistance = 10f;
    [SerializeField] private LayerMask uiRaycastMask = ~0;  // Все слои по умолчанию
    [SerializeField] private Color normalColor = new Color(0.18f, 0.65f, 1.0f, 1f);
    [SerializeField] private Color pressedColor = new Color(0.16f, 0.85f, 0.45f, 1f);

    private Transform _cursorDot;
    private Material _dotMaterial;
    private XRUIInputModule _uiInputModule;

    private void Awake()
    {
        if (nearFarInteractor == null)
            nearFarInteractor = GetComponentInChildren<NearFarInteractor>();

        if (pokeInteractor == null)
            pokeInteractor = GetComponentInChildren<XRPokeInteractor>();

        CreateCursorDot();
    }

    private void Start()
    {
        if (EventSystem.current != null)
        {
            _uiInputModule = EventSystem.current.currentInputModule as XRUIInputModule;
        }
    }

    private void CreateCursorDot()
    {
        GameObject dotObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dotObj.name = "NearFar_UI_CursorDot";
        dotObj.transform.localScale = Vector3.one * (dotRadius * 2f);

        Destroy(dotObj.GetComponent<Collider>());
        dotObj.layer = LayerMask.NameToLayer("Ignore Raycast");

        var renderer = dotObj.GetComponent<MeshRenderer>();
        Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");

        _dotMaterial = new Material(unlitShader);
        _dotMaterial.color = normalColor;
        renderer.material = _dotMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        _cursorDot = dotObj.transform;
        _cursorDot.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_cursorDot == null || nearFarInteractor == null) return;

        if (pokeInteractor != null && pokeInteractor.hasHover)
        {
            _cursorDot.gameObject.SetActive(false);
            return;
        }

        if (_uiInputModule == null && EventSystem.current != null)
        {
            _uiInputModule = EventSystem.current.currentInputModule as XRUIInputModule;
        }

        bool isPressed = nearFarInteractor.isSelectActive || nearFarInteractor.hasSelection;

        // 1. Проверяем uGUI Canvas через XRUIInputModule
        Vector3 rayOrigin = Vector3.zero;
        Vector3 rayDir = Vector3.forward;
        bool hasTrackedRay = false;

        if (_uiInputModule != null && _uiInputModule.GetTrackedDeviceModel(nearFarInteractor, out TrackedDeviceModel model))
        {
            rayOrigin = model.position;
            rayDir = model.orientation * Vector3.forward;
            hasTrackedRay = true;

            RaycastResult hit = model.currentRaycast;
            if (hit.isValid && hit.gameObject != null && hit.worldPosition != Vector3.zero)
            {
                ShowCursorAt(hit.worldPosition, hit.worldNormal, isPressed);
                return;
            }
        }

        // 2. Прямой 3D-рейкаст по всем World Space UIDocument в сцене
        if (!hasTrackedRay || rayOrigin == Vector3.zero)
        {
            Transform originTransform = nearFarInteractor.attachTransform != null
                ? nearFarInteractor.attachTransform
                : nearFarInteractor.transform;
            rayOrigin = originTransform.position;
            rayDir = originTransform.forward;
        }

        Ray ray = new Ray(rayOrigin, rayDir);

        // Проверяем все попадания вдоль луча (включая Trigger-коллайдеры, которые использует UI Toolkit!)
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, uiRaycastMask, QueryTriggerInteraction.Collide);
        if (hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var h in hits)
            {
                if (h.collider == null) continue;

                // Проверяем, относится ли коллайдер к UIDocument (на нём самом, на родителе или в детях)
                bool isUIToolkitPanel =
                    h.collider.GetComponent<UIDocument>() != null ||
                    h.collider.GetComponentInParent<UIDocument>() != null ||
                    h.collider.GetComponentInChildren<UIDocument>() != null;

                if (isUIToolkitPanel)
                {
                    ShowCursorAt(h.point, h.normal, isPressed);
                    return;
                }
            }
        }

        _cursorDot.gameObject.SetActive(false);
    }

    private void ShowCursorAt(Vector3 point, Vector3 normal, bool isPressed)
    {
        if (normal == Vector3.zero) normal = Vector3.back;
        _cursorDot.gameObject.SetActive(true);
        _cursorDot.position = point + normal * surfaceOffset;
        _dotMaterial.color = isPressed ? pressedColor : normalColor;
    }

    private void OnDestroy()
    {
        if (_cursorDot != null) Destroy(_cursorDot.gameObject);
        if (_dotMaterial != null) Destroy(_dotMaterial);
    }
}