using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// 1. Создаем специальный класс для хранения индивидуальных настроек каждого объекта
[System.Serializable]
public class RotatableObject
{
    [Tooltip("Объект, который нужно вращать")]
    public Transform targetTransform;
    
    [Tooltip("Начальный угол поворота (когда кнопка отжата)")]
    public Vector3 startRotationAngles;
    
    [Tooltip("Конечный угол поворота (когда кнопка нажата)")]
    public Vector3 endRotationAngles;
}




[RequireComponent(typeof(XRSimpleInteractable))]
public class OnOffBtnScript : MonoBehaviour
{

    [Header("Настройки кнопки (по оси Z)")]
    [Tooltip("На сколько кнопка уходит вдаль/внутрь (отрицательное значение для -Z)")]
    public float pressDepth = -0.05f;
    [Tooltip("Скорость анимации кнопки и вращения")]
    public float animationSpeed = 10f;

    [Header("Настройки индивидуального вращения")]
    [Tooltip("Добавьте сюда объекты и настройте для каждого свои углы")]
    public RotatableObject[] objectsToRotate;

    [Header("События (Вызов ваших функций)")]
    public UnityEvent onToggleOn;  // Срабатывает при первом нажатии (вдавливании)
    public UnityEvent onToggleOff; // Срабатывает при втором нажатии (отжатии)

    private XRSimpleInteractable interactable;
    private Vector3 initialLocalPosition;
    
    // Целевая позиция самой кнопки
    private Vector3 targetLocalPosition;
    
    // Текущее состояние кнопки (нажата/отжата)
    private bool isToggled = false;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        
        // Запоминаем стартовую позицию кнопки
        initialLocalPosition = transform.localPosition;
        targetLocalPosition = initialLocalPosition;

        // Подписываемся на событие нажатия (Select)
        interactable.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDestroy()
    {
        interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

    private void Update()
    {
        // 1. Плавная анимация движения самой кнопки
        if (transform.localPosition != targetLocalPosition)
        {
            transform.localPosition = Vector3.Lerp(
                transform.localPosition, 
                targetLocalPosition, 
                Time.deltaTime * animationSpeed
            );
        }

        // 2. Плавная анимация вращения КАЖДОГО объекта к его индивидуальной цели
        if (objectsToRotate != null && objectsToRotate.Length > 0)
        {
            foreach (RotatableObject rotObj in objectsToRotate)
            {
                // Проверяем, что объект вообще назначен
                if (rotObj.targetTransform != null)
                {
                    // Определяем, куда этот конкретный объект должен быть повернут сейчас
                    Quaternion targetRotation = isToggled 
                        ? Quaternion.Euler(rotObj.endRotationAngles) 
                        : Quaternion.Euler(rotObj.startRotationAngles);

                    // Если он еще не достиг нужного угла — вращаем
                    if (rotObj.targetTransform.localRotation != targetRotation)
                    {
                        rotObj.targetTransform.localRotation = Quaternion.Slerp(
                            rotObj.targetTransform.localRotation, 
                            targetRotation, 
                            Time.deltaTime * animationSpeed
                        );
                    }
                }
            }
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        // Меняем состояние на противоположное при клике
        isToggled = !isToggled;

        if (isToggled)
        {
            // Кнопка НАЖАТА
            targetLocalPosition = initialLocalPosition + new Vector3(0, 0, pressDepth);
            onToggleOn?.Invoke(); 
        }
        else
        {
            // Кнопка ОТЖАТА (второе нажатие)
            targetLocalPosition = initialLocalPosition;
            onToggleOff?.Invoke(); 
        }
    }
}