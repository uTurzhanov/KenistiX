using System;
using UnityEngine;
using TMPro;

namespace VRIntro
{
    /// <summary>
    /// Показывает список ВСЕХ шагов обучения одновременно (чек-лист), в отличие от
    /// InstructionPanel, который показывает только текущий шаг. Подсвечивает активный
    /// шаг, помечает пройденные, приглушает будущие.
    ///
    /// Настройка: создай в Canvas по одной строке (Text/TMP) на каждый шаг обучения,
    /// в том же порядке, что и Steps в TutorialManager, и перетащи их сюда по порядку.
    /// </summary>
    public class TutorialStepListUI : MonoBehaviour
    {
        [Serializable]
        public class StepRow
        {
            [Tooltip("Текстовый элемент строки (название шага)")]
            public TMP_Text label;

            [Tooltip("Необязательно: иконка/галочка, которая появляется, когда шаг пройден")]
            public GameObject completedIcon;
        }

        [Tooltip("Строки списка — по одной на каждый шаг, СТРОГО в том же порядке, что и Steps в TutorialManager")]
        [SerializeField] private StepRow[] rows;

        [Header("Цвета состояний")]
        [SerializeField] private Color upcomingColor = new Color(1f, 1f, 1f, 0.35f); // будущий шаг — приглушённый
        [SerializeField] private Color activeColor = new Color(1f, 0.85f, 0.2f, 1f);  // текущий шаг — акцентный
        [SerializeField] private Color completedColor = new Color(0.4f, 1f, 0.5f, 0.8f); // пройденный шаг — зелёный

        private int _lastKnownStepIndex = int.MinValue;

        private void Update()
        {
            if (TutorialManager.Instance == null) return;

            int current = TutorialManager.Instance.CurrentStepIndex;

            // Перерисовываем список, только если индекс шага реально изменился —
            // не тратим ресурсы на перекраску одного и того же состояния каждый кадр
            if (current == _lastKnownStepIndex) return;

            _lastKnownStepIndex = current;
            Refresh(current);
        }

        private void Refresh(int currentStepIndex)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row.label == null) continue;

                if (i < currentStepIndex)
                {
                    // Шаг уже пройден
                    row.label.color = completedColor;
                    if (row.completedIcon != null) row.completedIcon.SetActive(true);
                }
                else if (i == currentStepIndex)
                {
                    // Текущий активный шаг
                    row.label.color = activeColor;
                    if (row.completedIcon != null) row.completedIcon.SetActive(false);
                }
                else
                {
                    // Шаг ещё впереди
                    row.label.color = upcomingColor;
                    if (row.completedIcon != null) row.completedIcon.SetActive(false);
                }
            }
        }
    }
}