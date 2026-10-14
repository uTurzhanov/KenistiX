using System.Collections;
using UnityEngine;

namespace VRIntro
{
    /// <summary>
    /// Вешается на GameObject с Box/Sphere Collider (IsTrigger = true).
    /// Когда в зону заходит XR Origin игрока (по тегу "Player"),
    /// засчитывает завершение указанного шага обучения.
    ///
    /// ВАЖНО: тег "Player" нужно повесить на корневой объект XR Origin
    /// (или на CharacterController внутри него — тогда Collider там же).
    ///
    /// Помимо OnTriggerEnter (быстрый путь для обычной ходьбы), компонент
    /// периодически проверяет пересечение bounds напрямую — это подстраховка
    /// на случай мгновенного перемещения (телепорт), при котором CharacterController
    /// иногда не генерирует стандартные события триггера, если позиция
    /// была изменена не через CharacterController.Move().
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class TutorialTriggerZone : MonoBehaviour, IResettableTutorialTrigger
    {
        [Tooltip("Индекс шага в списке TutorialManager, который завершает эта зона")]
        [SerializeField] private int stepIndexToComplete;

        [Tooltip("Тег объекта игрока, который должен войти в зону (обычно XR Origin)")]
        [SerializeField] private string playerTag = "Player";

        [Tooltip("Если true — зона срабатывает один раз и потом отключается")]
        [SerializeField] private bool triggerOnce = true;

        [Tooltip("Как часто проверять пересечение bounds вручную (сек). Нужно как страховка для телепорта")]
        [SerializeField] private float manualCheckInterval = 0.1f;

        private bool _consumed;
        private Collider _zoneCollider;
        private Collider _cachedPlayerCollider;

        private void Awake()
        {
            _zoneCollider = GetComponent<Collider>();
        }

        private void Reset()
        {
            // Удобная подсказка при добавлении компонента: сразу делаем коллайдер триггером
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnEnable()
        {
            StartCoroutine(ManualOverlapCheckRoutine());
        }

        private IEnumerator ManualOverlapCheckRoutine()
        {
            var wait = new WaitForSeconds(manualCheckInterval);
            while (true)
            {
                yield return wait;

                if (_consumed && triggerOnce)
                    continue;

                TryCompleteViaBoundsOverlap();
            }
        }

        private void TryCompleteViaBoundsOverlap()
        {
            if (TutorialManager.Instance == null) return;
            if (TutorialManager.Instance.CurrentStepIndex != stepIndexToComplete) return;

            if (_cachedPlayerCollider == null)
            {
                var playerObj = GameObject.FindGameObjectWithTag(playerTag);
                if (playerObj == null) return;
                _cachedPlayerCollider = playerObj.GetComponentInChildren<Collider>();
                if (_cachedPlayerCollider == null) return;
            }

            if (_zoneCollider.bounds.Intersects(_cachedPlayerCollider.bounds))
            {
                CompleteNow();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_consumed && triggerOnce) return;
            if (!other.CompareTag(playerTag)) return;

            if (TutorialManager.Instance == null)
            {
                Debug.LogWarning("[TutorialTriggerZone] TutorialManager не найден на сцене.");
                return;
            }

            // Засчитываем шаг, только если он действительно текущий —
            // защита от случайных повторных срабатываний между шагами
            if (TutorialManager.Instance.CurrentStepIndex == stepIndexToComplete)
            {
                CompleteNow();
            }
        }

        private void CompleteNow()
        {
            if (_consumed && triggerOnce) return;

            _consumed = true;
            TutorialManager.Instance.CompleteStep(stepIndexToComplete);

            if (triggerOnce)
                _zoneCollider.enabled = false;
        }

        public void ResetForReplay()
        {
            _consumed = false;
            if (_zoneCollider != null)
                _zoneCollider.enabled = true;
        }
    }
}