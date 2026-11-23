using TMPro;
using UnityEngine;

public class CircuitComponentsSwitcher : MonoBehaviour
{
    public enum ElementKind { Resistor, Capacitor }

    [Header("Настройки элемента")]
    [SerializeField] private ElementKind elementKind = ElementKind.Resistor;
    [SerializeField] private ACCircuitSolver solver;
    [SerializeField] private VRButtonGrip switchPresetButton;
    [SerializeField] private TextMeshPro valueLabel;

    [Header("Пресеты (для R в Омах; для C в микрофарадах, мкФ)")]
    [SerializeField] private float[] presets = { 89f, 150f, 200f };
    private int _currentIndex = 0;

    private void Start()
    {
        if (switchPresetButton != null)
            switchPresetButton.onClick.AddListener(NextPreset);

        ApplyPreset();
    }

    public void NextPreset()
    {
        if (presets == null || presets.Length == 0) return;
        _currentIndex = (_currentIndex + 1) % presets.Length;
        ApplyPreset();
    }

    private void ApplyPreset()
    {
        if (presets == null || presets.Length == 0) return;
        float val = presets[_currentIndex];

        if (elementKind == ElementKind.Resistor)
        {
            if (solver != null) solver.resistanceR = val;
            if (valueLabel != null) valueLabel.text = $"R = {val:F0} Ом";
        }
        else
        {
            // Переводим мкФ в Фарады для формул решателя
            if (solver != null) solver.capacitanceC = val * 1e-6f;
            if (valueLabel != null) valueLabel.text = $"C = {val:F2} мкФ";
        }
    }

    private void OnDestroy()
    {
        if (switchPresetButton != null)
            switchPresetButton.onClick.RemoveListener(NextPreset);
    }
}