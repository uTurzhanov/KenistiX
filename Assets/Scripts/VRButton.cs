using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody))]
public class PhysicalVRButton : MonoBehaviour
{
    [Header("Настройки хода кнопки (теперь числа ПЛЮСОВЫЕ)")]
    [Tooltip("Дистанция вдавливания для клика (например 0.05)")]
    public float pressDistance = 0.05f;
    
    [Tooltip("Максимальная глубина утапливания (например 0.07)")]
    public float maxDistance = 0.07f; 
    
    [Tooltip("Скорость возврата кнопки")]
    public float returnSpeed = 5f;

    [Header("События")]
    public UnityEvent onPhysicalPress;
    public UnityEvent onPhysicalRelease;

    private Vector3 initialLocalPosition;
    private bool isPressed = false;
    private Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = false; 
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous; 
        
        // Вращение блокируем, двигаем скриптом
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        initialLocalPosition = transform.localPosition;
    }

    private void FixedUpdate() 
    {
        // Не даем физическому движку "усыпить" кнопку
        rb.WakeUp();
        
        // Гасим левые инерционные силы
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        LimitMovement();
        CheckPress();
        AutoReturn();
    }

    private void LimitMovement()
    {
        Vector3 currentPos = transform.localPosition;
        
        // Жестко держим X и Y на изначальном месте
        currentPos.x = initialLocalPosition.x;
        currentPos.y = initialLocalPosition.y;

        // Ограничиваем ход по Z (и в плюс, и в минус, чтобы не зависеть от направления осей)
        float minZ = initialLocalPosition.z - maxDistance;
        float maxZ = initialLocalPosition.z + maxDistance;
        currentPos.z = Mathf.Clamp(currentPos.z, minZ, maxZ);

        transform.localPosition = currentPos;
    }

    private void CheckPress()
    {
        // Считаем пройденное расстояние от стартовой точки (абсолютное значение без минусов!)
        float currentDistance = Mathf.Abs(transform.localPosition.z - initialLocalPosition.z);
        
        // Дистанция отпускания (зазор в 1 миллиметр, чтобы кнопка не дребезжала)
        float releaseDistance = pressDistance - 0.01f; 

        if (!isPressed && currentDistance >= pressDistance)
        {
            isPressed = true;
            Debug.Log("<color=green>Кнопка НАЖАТА!</color>");
            onPhysicalPress?.Invoke();
        }
        else if (isPressed && currentDistance <= releaseDistance)
        {
            isPressed = false;
            Debug.Log("<color=yellow>Кнопка ОТПУЩЕНА (сброс)!</color>");
            onPhysicalRelease?.Invoke();
        }
    }

    private void AutoReturn()
    {
        // Плавно возвращаем кнопку в стартовую точку
        if (transform.localPosition != initialLocalPosition)
        {
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition,
                initialLocalPosition,
                returnSpeed * Time.fixedDeltaTime
            );
        }
    }
}