using UnityEngine;

/// <summary>
/// Ѕазовый класс дл€ любого "детектора" завершени€ шага - вместо фиксированного таймера
/// шаг завершаетс€, когда реально произошло нужное действие (осмотрелись, покрутили контроллер,
/// подвигали стиком, нажали кнопку).
///
/// TutorialManager вызывает ResetChecker() в момент показа шага, а затем каждый кадр читает IsCompleted.
/// </summary>
public abstract class StepCompletionChecker : MonoBehaviour
{
    /// <summary>—тало true в тот момент, когда пользователь выполнил нужное действие.</summary>
    public bool IsCompleted { get; protected set; }

    /// <summary>¬ызываетс€ TutorialManager'ом в начале шага - сбросить прогресс и начать отслеживание заново.</summary>
    public virtual void ResetChecker()
    {
        IsCompleted = false;
    }
}