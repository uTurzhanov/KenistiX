using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Детектор движения/поворота стиками: завершается, когда пользователь реально подвигал нужный(е)
/// стик(и) суммарно не меньше заданного времени. Использует Input System (Action-based).
///
/// Какой именно стик проверять настраивается per-step через SetRequiredAxes() - так один и тот же
/// детектор переиспользуется и для "подвигайте левым стиком" (только Move), и отдельно для
/// "подвигайте правым стиком" (только Turn), и для обоих сразу.
/// </summary>
public class ThumbstickActivityChecker : StepCompletionChecker
{
    [Header("Input Action References")]
    [Tooltip("Обычно Vector2-экшен движения, например XRI LeftHand Locomotion/Move")]
    [SerializeField] private InputActionReference moveAction;

    [Tooltip("Обычно Vector2-экшен поворота, например XRI RightHand Locomotion/Turn")]
    [SerializeField] private InputActionReference turnAction;

    [Header("Пороги")]
    [SerializeField] private float deadzone = 0.5f;
    [Tooltip("Сколько секунд суммарно нужно подержать каждый стик отклонённым дальше deadzone")]
    [SerializeField] private float requiredActiveSeconds = 1f;

    // Какие стики реально нужно проверять на текущем шаге - настраивается TutorialManager'ом
    // перед ResetChecker() через SetRequiredAxes(). По умолчанию - оба (старое поведение).
    private bool checkMove = true;
    private bool checkTurn = true;

    private float moveActiveTime;
    private float turnActiveTime;

    /// <summary>Настроить, какие стики нужно проверять на этом конкретном шаге. Вызывать ДО ResetChecker().</summary>
    public void SetRequiredAxes(bool requireMove, bool requireTurn)
    {
        checkMove = requireMove;
        checkTurn = requireTurn;
    }

    public override void ResetChecker()
    {
        base.ResetChecker();
        moveActiveTime = 0f;
        turnActiveTime = 0f;
    }

    private void OnEnable()
    {
        moveAction?.action?.Enable();
        turnAction?.action?.Enable();
    }

    private void Update()
    {
        if (IsCompleted)
            return;

        if (moveAction != null && moveAction.action != null)
        {
            Vector2 moveValue = moveAction.action.ReadValue<Vector2>();
            if (moveValue.magnitude > deadzone)
                moveActiveTime += Time.deltaTime;
        }

        if (turnAction != null && turnAction.action != null)
        {
            Vector2 turnValue = turnAction.action.ReadValue<Vector2>();
            if (turnValue.magnitude > deadzone)
                turnActiveTime += Time.deltaTime;
        }

        bool moveSatisfied = !checkMove || moveActiveTime >= requiredActiveSeconds;
        bool turnSatisfied = !checkTurn || turnActiveTime >= requiredActiveSeconds;

        if (moveSatisfied && turnSatisfied)
            IsCompleted = true;
    }
}