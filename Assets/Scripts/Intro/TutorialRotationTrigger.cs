using UnityEngine;

namespace VRIntro
{
    /// <summary>
    /// Засчитывает шаг обучения, когда пользователь реально повернулся на нужный угол
    /// (правым стиком — Snap Turn или Continuous Turn, без разницы какой провайдер используется).
    ///
    /// Не требует триггер-коллайдера: просто следит за yaw (поворотом вокруг Y)
    /// объекта rigTransform, который вращают Turn-провайдеры XRI (обычно это XR Origin).
    /// </summary>
    public class TutorialRotationTrigger : MonoBehaviour, IResettableTutorialTrigger
    {
        [Tooltip("Индекс шага в TutorialManager, который завершает этот компонент")]
        [SerializeField] private int stepIndexToComplete;

        [Tooltip("Трансформ, который реально вращают Turn-провайдеры (обычно корень XR Origin)")]
        [SerializeField] private Transform rigTransform;

        [Tooltip("На сколько градусов суммарно нужно повернуться, чтобы шаг засчитался")]
        [SerializeField] private float requiredAngleDegrees = 60f;

        private bool _tracking;
        private float _baselineYaw;
        private bool _consumed;

        private void Update()
        {
            if (_consumed || rigTransform == null || TutorialManager.Instance == null)
                return;

            bool isCurrentStep = TutorialManager.Instance.CurrentStepIndex == stepIndexToComplete;

            if (!isCurrentStep)
            {
                // Шаг ещё не начался (или уже закончился) — сбрасываем слежение,
                // чтобы отсчёт стартовал заново, когда шаг реально станет активным
                _tracking = false;
                return;
            }

            if (!_tracking)
            {
                _baselineYaw = rigTransform.eulerAngles.y;
                _tracking = true;
                return;
            }

            float delta = Mathf.DeltaAngle(_baselineYaw, rigTransform.eulerAngles.y);
            if (Mathf.Abs(delta) >= requiredAngleDegrees)
            {
                _consumed = true;
                TutorialManager.Instance.CompleteStep(stepIndexToComplete);
            }
        }

        public void ResetForReplay()
        {
            _consumed = false;
            _tracking = false;
        }
    }
}