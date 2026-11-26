using TMPro;
using Unity.VRTemplate;
using UnityEngine;

public class DecadeBoxModule : MonoBehaviour
{
    public enum BoxType { Resistor, Capacitor }

    [Header("Тип прибора и связь с решателем")]
    [SerializeField] private BoxType boxType = BoxType.Resistor;
    [SerializeField] private ACCircuitSolver solver;

    [Header("Органы управления (можно назначить кнопку, крутилку или оба)")]
    [SerializeField] private VRButtonGrip cycleButton;
    [SerializeField] private XRKnob selectorKnob;

    [Header("Экран прибора")]
    [SerializeField] private TextMeshProUGUI valueDisplayText;

    [Header("Номиналы (для R в Омах; для C в микрофарадах, мкФ)")]
    [SerializeField] private float[] presets = { 89f, 50f, 150f, 200f };

    private int _currentIndex = 0;


    private void Start()
    {
        if (cycleButton != null)
            cycleButton.onClick.AddListener(CycleNextPreset);

        if (selectorKnob != null)
        {
            selectorKnob.clampedMotion = true;
            selectorKnob.onValueChange.AddListener(OnKnobValueChanged);
            OnKnobValueChanged(selectorKnob.value);
        }
        else
        {
            ApplyCurrentPreset();
        }
    }

    public void CycleNextPreset()
    {
        if (presets == null || presets.Length == 0) return;
        _currentIndex = (_currentIndex + 1) % presets.Length;
        ApplyCurrentPreset();
    }

    private void OnKnobValueChanged(float normalizedValue)
    {
        if (presets == null || presets.Length == 0) return;
        int newIndex = Mathf.Clamp(Mathf.FloorToInt(normalizedValue * presets.Length), 0, presets.Length - 1);
        if (newIndex != _currentIndex)
        {
            _currentIndex = newIndex;
            ApplyCurrentPreset();
        }
        else
        {
            ApplyCurrentPreset();

        }
    }

    private void ApplyCurrentPreset()
    {
        if (presets == null || presets.Length == 0) return;
        float selectedValue = presets[_currentIndex];

        if (boxType == BoxType.Resistor)
        {
            if (solver != null)
                solver.resistanceR = selectedValue;

            if (valueDisplayText != null)
                valueDisplayText.text = $"R = {selectedValue:F0} Ом";
        }
        else
        {
            // В решатель передаем емкость в Фарадах (мкФ * 10^-6)
            if (solver != null)
                solver.capacitanceC = selectedValue * 1e-6f;

            if (valueDisplayText != null)
                valueDisplayText.text = $"C = {selectedValue:F2} мкФ";
        }
    }

    private void OnDestroy()
    {
        if (cycleButton != null)
            cycleButton.onClick.RemoveListener(CycleNextPreset);

        if (selectorKnob != null)
            selectorKnob.onValueChange.RemoveListener(OnKnobValueChanged);
    }
}