namespace VRIntro
{
    /// <summary>
    /// Реализуют все триггеры шагов, у которых есть внутреннее состояние "уже сработал"
    /// (_consumed/_tracking и т.п.). TutorialManager вызывает ResetForReplay() на всех
    /// таких компонентах при перезапуске обучения (StartTutorial()), иначе повторное
    /// прохождение зависает на первом же шаге с таким состоянием.
    /// </summary>
    public interface IResettableTutorialTrigger
    {
        void ResetForReplay();
    }
}