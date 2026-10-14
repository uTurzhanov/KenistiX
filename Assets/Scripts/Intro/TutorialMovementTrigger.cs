using UnityEngine;

namespace VRIntro
{
    /// <summary>
    /// Засчитывает шаг обучения движению, когда игрок реально прошёл нужное расстояние
    /// стиком (Continuous Move Provider) — без физического коллайдера/триггер-зоны.
    ///
    /// Работает по тому же принципу, что и TutorialRotationTrigger: следит за
    /// горизонтальным смещением rigTransform (XR Origin) от точки, где игрок
    /// находился в момент начала шага.
    /// </summary>
    public class TutorialMovementTrigger : MonoBehaviour, IResettableTutorialTrigger
    {
        [Tooltip("Индекс шага в TutorialManager, который завершает этот компонент")]
        [SerializeField] private int stepIndexToComplete;

        [Tooltip("Трансформ, который реально двигают Move-провайдеры (обычно корень XR Origin)")]
        [SerializeField] private Transform rigTransform;

        [Tooltip("Сколько метров по горизонтали нужно пройти, чтобы шаг засчитался")]
        [SerializeField] private float requiredDistanceMeters = 1.5f;

        private bool _tracking;
        private Vector3 _baselinePosition;
        private bool _consumed;

        private void Update()
        {
            if (_consumed || rigTransform == null || TutorialManager.Instance == null)
                return;

            bool isCurrentStep = TutorialManager.Instance.CurrentStepIndex == stepIndexToComplete;

            if (!isCurrentStep)
            {
                // Шаг ещё не начался (или уже прошёл) — сбрасываем слежение,
                // чтобы отсчёт расстояния начинался заново, когда шаг реально станет активным
                _tracking = false;
                return;
            }

            Vector3 currentFlat = rigTransform.position;
            currentFlat.y = 0f; // считаем только горизонтальное перемещение, высота не важна

            if (!_tracking)
            {
                _baselinePosition = currentFlat;
                _tracking = true;
                return;
            }

            float distance = Vector3.Distance(_baselinePosition, currentFlat);
            if (distance >= requiredDistanceMeters)
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