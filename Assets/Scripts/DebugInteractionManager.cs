using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR.Interaction.Toolkit; // Используем XRIT
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;   // Для XRSocketInteractor в XRIT 3.x
using UnityEngine.XR.Interaction.Toolkit.Transformers; 
public class DebugInteractionController : MonoBehaviour
{
    [Header("Общие настройки хвата")]
    public float maxGrabDistance = 25.0f; 
    public float holdDistance = 3.0f;    

    [Header("RTS Настройки высоты и вращения")]
    [Tooltip("Фиксированная высота Y, на которую поднимается предмет в режиме RTS")]
    public float rtsFixedHoldHeight = 0.5f; 
    public float rotationSnapAngle = 90f; 

    private Camera _activeCamera;
    private XRGrabInteractable _currentlyGrabbedItem = null;
    private Rigidbody _grabbedRigidbody = null;
    private float _rtsCurrentYRotation = 0f; 

    // Храним состояние, чтобы вернуть физику в исходный вид
    private bool _originalKinematicState = false;
    private bool _originalUseGravityState = true;

    private List<XRGrabInteractable> _disabledXRGrabInteractables = new List<XRGrabInteractable>();

    public void SetActiveCamera(Camera cam)
    {
        _activeCamera = cam;
    }

    void Update()
    {
        if (_activeCamera == null) return;

        // ЛКМ — Взять / Поставить объект
        if (Input.GetMouseButtonDown(0))
        {
            if (_currentlyGrabbedItem == null) TryGrab();
            else ReleaseObject(throwObject: false);
        }

        // ПКМ — Бросить объект физикой
        if (Input.GetMouseButtonDown(1) && _currentlyGrabbedItem != null)
        {
            ReleaseObject(throwObject: true);
        }

        // Если объект удерживается — плавно ведем его физикой Rigidbody
        if (_currentlyGrabbedItem != null && _grabbedRigidbody != null)
        {
            UpdateObjectPhysicsPosition();
        }
    }

    void TryGrab()
    {
        Ray ray;
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            ray = _activeCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        }
        else
        {
            ray = _activeCamera.ScreenPointToRay(Input.mousePosition);
        }

        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxGrabDistance))
        {
            XRGrabInteractable xrGrab = hit.collider.GetComponentInParent<XRGrabInteractable>();
            if (xrGrab != null)
            {
                Rigidbody rb = xrGrab.GetComponent<Rigidbody>();
                if (rb == null) return; // Без Rigidbody физическое перемещение невозможно

                _currentlyGrabbedItem = xrGrab;
                _grabbedRigidbody = rb;
                _rtsCurrentYRotation = _grabbedRigidbody.transform.rotation.eulerAngles.y;

                // --- ЛОГИКА XR SOCKET INTERACTOR ---
                // Если объект сейчас находится в сокете, принудительно заставляем сокет отпустить его
                if (xrGrab.isSelected)
                {
                    // Получаем текущий интерактор (например, сокет)
                    var interactor = xrGrab.firstInteractorSelecting;
                    if (interactor is XRSocketInteractor socket)
                    {
                        // Явное приведение типов (Casting) для старого InteractionManager, как просит ошибка:
                        xrGrab.interactionManager.SelectExit((IXRSelectInteractor)socket, (IXRSelectInteractable)xrGrab);
                    }
                }

                // Запоминаем исходное состояние физики перед дебажным хватом
                _originalKinematicState = _grabbedRigidbody.isKinematic;
                _originalUseGravityState = _grabbedRigidbody.useGravity;

                // Для физического перемещения (без прохождения сквозь стены) делаем объект НЕ кинематичным,
                // но выключаем гравитацию, чтобы он не падал во время переноса.
                _grabbedRigidbody.isKinematic = false;
                _grabbedRigidbody.useGravity = false;

                // Ограничиваем лишнее вращение от столкновений при переносе мыском
                _grabbedRigidbody.angularVelocity = Vector3.zero;

                // Временно отключаем сам интерактор, чтобы VR-контроллеры не хватали объект «из-под мышки»
                if (xrGrab.enabled)
                {
                    xrGrab.enabled = false;
                    _disabledXRGrabInteractables.Add(xrGrab);
                }

                // Гасим базовые трансформеры XRIT, чтобы они не спорили с Rigidbody
                var transformers = xrGrab.GetComponentsInChildren<XRBaseGrabTransformer>();
                foreach (var t in transformers) if (t.enabled) t.enabled = false;
            }
        }
    }

    void UpdateObjectPhysicsPosition()
    {
        Vector3 targetPosition;
        Quaternion targetRotation;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
            // === РЕЖИМ FPS ===
            targetPosition = _activeCamera.transform.position + _activeCamera.transform.forward * holdDistance;
            targetRotation = _activeCamera.transform.rotation;
        }
        else
        {
            // === РЕЖИМ RTS ===
            Ray ray = _activeCamera.ScreenPointToRay(Input.mousePosition);
            Plane rtsMovementPlane = new Plane(Vector3.up, new Vector3(0f, rtsFixedHoldHeight, 0f));
            
            if (rtsMovementPlane.Raycast(ray, out float enterDistance))
            {
                targetPosition = ray.GetPoint(enterDistance);
            }
            else
            {
                targetPosition = _activeCamera.transform.position + ray.direction * holdDistance;
                targetPosition.y = rtsFixedHoldHeight;
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                _rtsCurrentYRotation = Mathf.Round((_rtsCurrentYRotation + rotationSnapAngle) / rotationSnapAngle) * rotationSnapAngle;
            }

            targetRotation = Quaternion.Euler(0f, _rtsCurrentYRotation, 0f);
        }

        // ВМЕСТО ПРЯМОГО ИЗМЕНЕНИЯ TRANSFORMA:
        // Двигаем объект через физический velocity, чтобы он не пролетал сквозь коллайдеры стола/стен
        Vector3 direction = targetPosition - _grabbedRigidbody.position;
        _grabbedRigidbody.linearVelocity = direction * 15f; // Коэффициент скорости доводки

        // Плавно доворачиваем физическое тело к целевому углу
        Quaternion deltaRotation = targetRotation * Quaternion.Inverse(_grabbedRigidbody.rotation);
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
        
        if (angle > 180f) angle -= 360f;
        if (!float.IsNaN(axis.x))
        {
            _grabbedRigidbody.angularVelocity = axis * (angle * Mathf.Deg2Rad * 15f);
        }
    }

    void ReleaseObject(bool throwObject)
    {
        if (_currentlyGrabbedItem == null || _grabbedRigidbody == null) return;

        // Включаем обратно XR-компоненты хвата
        foreach (var i in _disabledXRGrabInteractables) if (i != null) i.enabled = true;
        _disabledXRGrabInteractables.Clear();

        var transformers = _currentlyGrabbedItem.GetComponentsInChildren<XRBaseGrabTransformer>(true);
        foreach (var t in transformers) if (t != null) t.enabled = true;

        // Возвращаем физике исходное состояние
        _grabbedRigidbody.isKinematic = _originalKinematicState;
        _grabbedRigidbody.useGravity = _originalUseGravityState;

        if (throwObject)
        {
            // Бросок импульсом силы
            Vector3 throwForce = (_activeCamera.transform.forward * 12f) + (_activeCamera.transform.up * 2f);
            _grabbedRigidbody.AddForce(throwForce, ForceMode.Impulse);
        }
        else
        {
            // Просто роняем/отпускаем. 
            // Когда скорость зануляется, триггер XRSocketInteractor мгновенно обнаружит 
            // коллайдер Rigidbody и всосёт объект в гнездо (если он находится в зоне сокета).
            _grabbedRigidbody.linearVelocity = Vector3.zero;
            _grabbedRigidbody.angularVelocity = Vector3.zero;
        }

        _currentlyGrabbedItem = null;
        _grabbedRigidbody = null;
    }
}