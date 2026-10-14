using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace VRIntro
{
    /// <summary>
    /// Описание одного шага обучения: какая панель показывается,
    /// какие объекты активны, и что происходит при завершении шага.
    /// </summary>
    [Serializable]
    public class TutorialStep
    {
        [Tooltip("Название шага для удобства в инспекторе")]
        public string stepName;

        [Tooltip("Панель с текстом инструкции, которая должна появиться на этом шаге")]
        public GameObject instructionPanel;

        [Tooltip("Объекты сцены, которые нужно включить при старте шага (зоны, платформы, подсветка)")]
        public GameObject[] objectsToEnable;

        [Tooltip("Объекты сцены, которые нужно выключить при старте шага")]
        public GameObject[] objectsToDisable;

        [Tooltip("Событие, вызываемое при успешном завершении шага (например, звук/партиклы)")]
        public UnityEvent onStepCompleted;
    }

    /// <summary>
    /// Управляет последовательностью шагов обучающей комнаты (интро).
    /// TutorialTriggerZone на сцене вызывает CompleteStep(index), когда пользователь
    /// выполнил нужное действие (прошёл зону, телепортировался, взял предмет и т.д.).
    /// </summary>
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        [Header("Шаги обучения (по порядку)")]
        [SerializeField] private List<TutorialStep> steps = new List<TutorialStep>();

        [Header("События")]
        public UnityEvent onTutorialCompleted;

        private int _currentStepIndex = -1;

        private void Awake()
        {
            // Простой синглтон в пределах сцены, чтобы триггер-зонам было легко достучаться
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            StartTutorial();
        }

        public void StartTutorial()
        {
            HideAllPanels();
            ResetAllStatefulTriggers();

            // Сбрасываем "выбранный" UI-элемент — иначе кнопка, по которой только что
            // кликнули (например, кнопка перезапуска), при повторном показе панели
            // продолжит выглядеть как Selected Color вместо Normal Color
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            _currentStepIndex = -1;
            GoToStep(0);
        }

        /// <summary>
        /// Сбрасывает состояние ("уже сработал") у всех триггеров шагов на сцене —
        /// TutorialRotationTrigger, TutorialMovementTrigger, TutorialTriggerZone и т.п.
        /// Без этого при повторном прохождении интро эти компоненты помнят,
        /// что уже отработали в первый раз, и просто не реагируют повторно.
        /// </summary>
        private void ResetAllStatefulTriggers()
        {
            var resettables = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IResettableTutorialTrigger>();

            foreach (var trigger in resettables)
            {
                trigger.ResetForReplay();
            }
        }

        /// <summary>
        /// Принудительно выключает панели ВСЕХ шагов. Нужно на случай, если в редакторе
        /// панели забыли выключить вручную — без этого при старте могли бы быть видны
        /// сразу несколько инструкций одновременно, а не по одной поочерёдно.
        /// </summary>
        private void HideAllPanels()
        {
            foreach (var step in steps)
            {
                if (step.instructionPanel != null)
                    step.instructionPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Вызывается из TutorialTriggerZone, когда пользователь выполнил действие текущего шага.
        /// stepIndex — индекс шага, который был завершён (защита от повторных срабатываний триггера).
        /// </summary>
        public void CompleteStep(int stepIndex)
        {
            if (stepIndex != _currentStepIndex)
                return; // триггер сработал не для текущего шага — игнорируем

            if (_currentStepIndex >= 0 && _currentStepIndex < steps.Count)
            {
                steps[_currentStepIndex].onStepCompleted?.Invoke();
            }

            GoToStep(_currentStepIndex + 1);
        }

        private void GoToStep(int index)
        {
            // Выключаем панель предыдущего шага
            if (_currentStepIndex >= 0 && _currentStepIndex < steps.Count)
            {
                var prev = steps[_currentStepIndex];
                if (prev.instructionPanel != null)
                    prev.instructionPanel.SetActive(false);
            }

            _currentStepIndex = index;

            if (_currentStepIndex >= steps.Count)
            {
                onTutorialCompleted?.Invoke();
                Debug.Log("[TutorialManager] Интро завершено.");
                return;
            }

            var step = steps[_currentStepIndex];

            if (step.instructionPanel != null)
                step.instructionPanel.SetActive(true);

            foreach (var obj in step.objectsToEnable)
                if (obj != null) obj.SetActive(true);

            foreach (var obj in step.objectsToDisable)
                if (obj != null) obj.SetActive(false);

            Debug.Log($"[TutorialManager] Начат шаг {_currentStepIndex}: {step.stepName}");
        }

        public int CurrentStepIndex => _currentStepIndex;

        /// <summary>
        /// Завершает ТЕКУЩИЙ активный шаг, без необходимости передавать индекс вручную.
        /// Удобно вешать напрямую на UnityEvent (Select Entered, Activated, OnClick и т.д.) —
        /// метод без параметров всегда виден в выпадающем списке функций сразу,
        /// без переключения между Static Parameters / Dynamic.
        /// </summary>
        public void CompleteCurrentStep()
        {
            if (_currentStepIndex >= 0 && _currentStepIndex < steps.Count)
            {
                CompleteStep(_currentStepIndex);
            }
        }
    }
}