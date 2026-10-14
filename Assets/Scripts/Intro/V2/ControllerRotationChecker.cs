using UnityEngine;

/// <summary>
/// Шаг 2 "Знакомство с контроллерами": завершается, когда пользователь реально покрутил
/// хотя бы один контроллер в руке на достаточный суммарный угол (не одномоментный поворот,
/// а накопленное вращение - так не защитывается случайное дрожание руки).
/// </summary>
public class ControllerRotationChecker : StepCompletionChecker
{
    [Header("Что отслеживаем")]
    [Tooltip("Transform'ы контроллеров (обычно Left Controller и Right Controller из XR Origin)")]
    [SerializeField] private Transform[] controllersToWatch;

    [Header("Порог")]
    [Tooltip("Суммарный угол поворота в градусах, который нужно накопить хотя бы на одном из контроллеров")]
    [SerializeField] private float requiredCumulativeDegrees = 270f;

    private float[] accumulatedDegrees;
    private Quaternion[] lastRotation;

    public override void ResetChecker()
    {
        base.ResetChecker();

        int count = controllersToWatch != null ? controllersToWatch.Length : 0;
        accumulatedDegrees = new float[count];
        lastRotation = new Quaternion[count];

        for (int i = 0; i < count; i++)
            lastRotation[i] = controllersToWatch[i] != null ? controllersToWatch[i].rotation : Quaternion.identity;
    }

    private void Update()
    {
        // lastRotation/accumulatedDegrees заполняются только в ResetChecker(), который вызывает
        // TutorialManager при старте ИМЕННО этого шага. Update() при этом всё равно выполняется
        // каждый кадр, пока идут другие шаги (компонент включён в сцене постоянно) - до первого
        // ResetChecker() массивы ещё null, поэтому обязательно проверяем это здесь.
        if (IsCompleted || controllersToWatch == null || lastRotation == null)
            return;

        for (int i = 0; i < controllersToWatch.Length; i++)
        {
            Transform controller = controllersToWatch[i];
            if (controller == null) continue;

            float frameDelta = Quaternion.Angle(lastRotation[i], controller.rotation);
            accumulatedDegrees[i] += frameDelta;
            lastRotation[i] = controller.rotation;

            if (accumulatedDegrees[i] >= requiredCumulativeDegrees)
            {
                IsCompleted = true;
                return;
            }
        }
    }
}