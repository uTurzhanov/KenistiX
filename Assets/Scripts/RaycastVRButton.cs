using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRSimpleInteractable))]
public class VRButtonGrip : MonoBehaviour
{
    [Header("Визуальные настройки (по оси Z)")]
    [Tooltip("На сколько кнопка уходит вдаль/внутрь (отрицательное значение для -Z)")]
    public float pressDepth = -0.05f;
    [Tooltip("Скорость анимации")]
    public float animationSpeed = 15f;

    [Header("События")]
    public UnityEvent onClick;

    private XRSimpleInteractable interactable;
    private Vector3 initialLocalPosition;
    private Vector3 targetLocalPosition;
    
    // Флаг, который показывает, что кнопка сейчас движется "вниз" (внутрь)
    private bool isClickingDown = false;

    private void Awake()
    {
        interactable = GetComponent<XRSimpleInteractable>();
        initialLocalPosition = transform.localPosition;
        targetLocalPosition = initialLocalPosition;

        // Подписываемся на события
        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.selectExited.AddListener(OnSelectExited);
    }

    private void OnDestroy()
    {
        interactable.selectEntered.RemoveListener(OnSelectEntered);
        interactable.selectExited.RemoveListener(OnSelectExited);
    }

    private void Update()
    {
        // Плавная анимация движения кнопки
        if (transform.localPosition != targetLocalPosition)
        {
            transform.localPosition = Vector3.Lerp(
                transform.localPosition, 
                targetLocalPosition, 
                Time.deltaTime * animationSpeed
            );
        }

        // АВТОВОЗВРАТ: Если кнопка двигалась внутрь и почти достигла дна...
        if (isClickingDown)
        {
            // Проверяем дистанцию между текущей позицией и целевой (дном)
            if (Vector3.Distance(transform.localPosition, targetLocalPosition) < 0.002f)
            {
                // Отключаем режим "нажатия" и отправляем кнопку обратно
                isClickingDown = false;
                targetLocalPosition = initialLocalPosition;
            }
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        // Задаем цель - двигаться по оси Z 
        // (так как pressDepth отрицательный, кнопка поедет по -Z)
        targetLocalPosition = initialLocalPosition + new Vector3(0, 0, pressDepth);
        isClickingDown = true; // Включаем отслеживание достижения дна
        
        onClick?.Invoke();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        // Если игрок отпустил Grip раньше, чем кнопка дошла до дна - всё равно возвращаем обратно
        isClickingDown = false;
        targetLocalPosition = initialLocalPosition;
    }
}