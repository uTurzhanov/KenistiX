using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Шаг "Нажатие кнопки": завершается сразу по факту первого нажатия нужной кнопки
/// (например, курка захвата - Grip, или Trigger) - на ЛЮБОМ из двух контроллеров, левом или правом.
/// </summary>
public class ButtonPressChecker : StepCompletionChecker
{
    [Tooltip("Экшен нажатия кнопки на правом контроллере, например XRI RightHand Interaction/Activate")]
    [SerializeField] private InputActionReference rightButtonAction;

    [Tooltip("Тот же экшен, но на левом контроллере, например XRI LeftHand Interaction/Activate")]
    [SerializeField] private InputActionReference leftButtonAction;

    private void OnEnable()
    {
        if (rightButtonAction != null && rightButtonAction.action != null)
        {
            rightButtonAction.action.Enable();
            rightButtonAction.action.performed += OnButtonPerformed;
        }

        if (leftButtonAction != null && leftButtonAction.action != null)
        {
            leftButtonAction.action.Enable();
            leftButtonAction.action.performed += OnButtonPerformed;
        }
    }

    private void OnDisable()
    {
        if (rightButtonAction != null && rightButtonAction.action != null)
            rightButtonAction.action.performed -= OnButtonPerformed;

        if (leftButtonAction != null && leftButtonAction.action != null)
            leftButtonAction.action.performed -= OnButtonPerformed;
    }

    private void OnButtonPerformed(InputAction.CallbackContext context)
    {
        IsCompleted = true;
    }
}