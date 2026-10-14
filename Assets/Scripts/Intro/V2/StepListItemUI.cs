using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Один пункт в списке шагов слева снизу ("Шаг 1", "Шаг 2" и т.д.).
/// Три состояния: Upcoming (ещё не дошли), Active (текущий), Completed (пройден).
/// Завершённый шаг помечается сменой иконки статуса на галочку (а не зачёркиванием текста).
/// </summary>
public class StepListItemUI : MonoBehaviour
{
    public enum StepState { Upcoming, Active, Completed }

    [Header("Ссылки")]
    [SerializeField] private TMP_Text label;

    [Tooltip("Круглая иконка слева от текста - меняет спрайт/цвет в зависимости от состояния шага.")]
    [SerializeField] private Image statusIcon;

    [Header("Спрайты иконки")]
    [Tooltip("Спрайт для Upcoming/Active - обычно просто пустой круг/контур.")]
    [SerializeField] private Sprite defaultIconSprite;

    [Tooltip("Спрайт для Completed - галочка. Можно взять встроенный спрайт Unity 'Checkmark' " +
             "(используется по умолчанию в компоненте Toggle) через выбор спрайта в инспекторе.")]
    [SerializeField] private Sprite completedIconSprite;

    [Header("Цвета текста")]
    [SerializeField] private Color upcomingColor = new Color(1f, 1f, 1f, 0.5f);
    [SerializeField] private Color activeColor = Color.white;
    [SerializeField] private Color completedColor = new Color(1f, 1f, 1f, 0.5f);

    [Header("Цвета иконки")]
    [SerializeField] private Color upcomingIconColor = new Color(0.42f, 0.42f, 0.44f); // #6B6B70
    [SerializeField] private Color activeIconColor = new Color(0.56f, 0.66f, 1f);      // #8FA8FF
    [SerializeField] private Color completedIconColor = new Color(0.18f, 0.49f, 0.36f); // зелёный

    [Header("Масштаб активного шага (необязательно)")]
    [SerializeField] private bool scaleActiveStep = true;
    [SerializeField] private float activeScale = 1.1f;

    private StepState currentState;

    public void SetText(string text)
    {
        if (label == null) return;

        label.text = text;

        // Известная особенность Unity: у TMP-текста, созданного через Instantiate() и сразу
        // заполненного текстом, Canvas Renderer иногда не успевает пересчитать меш до первого
        // рендера и считает его "пустым" - из-за этого текст остаётся невидимым, пока что-то
        // ещё раз не изменится. ForceMeshUpdate() принудительно пересчитывает меш сразу.
        label.ForceMeshUpdate();
    }

    public void SetState(StepState state)
    {
        currentState = state;

        switch (state)
        {
            case StepState.Upcoming:
                if (label != null) label.color = upcomingColor;
                SetIcon(defaultIconSprite, upcomingIconColor);
                transform.localScale = Vector3.one;
                break;

            case StepState.Active:
                if (label != null) label.color = activeColor;
                SetIcon(defaultIconSprite, activeIconColor);
                transform.localScale = scaleActiveStep ? Vector3.one * activeScale : Vector3.one;
                break;

            case StepState.Completed:
                if (label != null) label.color = completedColor;
                SetIcon(completedIconSprite, completedIconColor);
                transform.localScale = Vector3.one;
                break;
        }
    }

    private void SetIcon(Sprite sprite, Color color)
    {
        if (statusIcon == null) return;

        if (sprite != null)
            statusIcon.sprite = sprite;

        statusIcon.color = color;
    }
}