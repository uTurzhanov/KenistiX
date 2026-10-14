using UnityEngine;

/// <summary>
/// ƒетектор поворота головы: завершаетс€, когда пользователь реально повернул голову
/// на нужные оси относительно точки, где сто€л в начале шага. Ќе таймер - именно факт поворота.
///
/// ќси проверки настраиваютс€ per-step через SetRequiredAxes() - так один и тот же детектор
/// можно переиспользовать и дл€ "поверните влево-вправо" (только Yaw), и отдельно дл€
/// "посмотрите вверх-вниз" (только Pitch), и дл€ обоих сразу, в разных шагах инструктажа.
/// </summary>
public class HeadRotationChecker : StepCompletionChecker
{
    [Header("„то отслеживаем")]
    [Tooltip("ќбычно Main Camera из XR Origin")]
    [SerializeField] private Transform headTransform;

    [Header("ѕороги (градусы от начальной точки)")]
    [SerializeField] private float requiredYawDegrees = 20f;   // влево/вправо
    [SerializeField] private float requiredPitchDegrees = 10f; // вверх/вниз

    //  акие оси реально нужно провер€ть на текущем шаге - настраиваетс€ TutorialManager'ом
    // перед ResetChecker() через SetRequiredAxes(). ѕо умолчанию - обе (старое поведение).
    private bool checkYaw = true;
    private bool checkPitch = true;

    private Quaternion startRotation;
    private bool lookedLeft, lookedRight, lookedUp, lookedDown;

    /// <summary>Ќастроить, какие оси нужно провер€ть на этом конкретном шаге. ¬ызывать ƒќ ResetChecker().</summary>
    public void SetRequiredAxes(bool requireYaw, bool requirePitch)
    {
        checkYaw = requireYaw;
        checkPitch = requirePitch;
    }

    public override void ResetChecker()
    {
        base.ResetChecker();
        lookedLeft = lookedRight = lookedUp = lookedDown = false;

        if (headTransform != null)
            startRotation = headTransform.rotation;
    }

    private void Update()
    {
        if (IsCompleted || headTransform == null)
            return;

        Vector3 startEuler = startRotation.eulerAngles;
        Vector3 currentEuler = headTransform.rotation.eulerAngles;

        float yawDelta = Mathf.DeltaAngle(startEuler.y, currentEuler.y);
        float pitchDelta = Mathf.DeltaAngle(startEuler.x, currentEuler.x);

        if (yawDelta <= -requiredYawDegrees) lookedLeft = true;
        if (yawDelta >= requiredYawDegrees) lookedRight = true;

        // «нак дл€ "вверх"/"вниз" зависит от того, как физически считает углы конкретный шлем -
        // если в тесте окажетс€ перепутано местами, просто помен€йте местами lookedUp/lookedDown ниже.
        if (pitchDelta <= -requiredPitchDegrees) lookedUp = true;
        if (pitchDelta >= requiredPitchDegrees) lookedDown = true;

        bool yawSatisfied = !checkYaw || (lookedLeft && lookedRight);
        bool pitchSatisfied = !checkPitch || (lookedUp && lookedDown);

        if (yawSatisfied && pitchSatisfied)
            IsCompleted = true;
    }
}