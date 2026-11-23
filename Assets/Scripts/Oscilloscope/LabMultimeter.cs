using TMPro;
using UnityEngine;


public enum MultimeterMode
{
    Current20mA = 0,   // Предел 20 мА (нужен по методичке для амперметра)
    Current200mA = 1,  // Предел 200 мА
    Voltage2V = 2,     // Предел 2 В (нужен по методичке для вольтметра)
    Voltage20V = 3     // Предел 20 В
}

public class LabMultimeter : MonoBehaviour
{
    [Header("Связь со схемой")]
    [SerializeField] private ACCircuitSolver solver;
    [SerializeField] private CircuitGraphManager graphManager;

    [Header("Элементы управления модели")]
    [SerializeField] private VRButtonGrip powerButton;
    [SerializeField] private Transform powerRockerVisual; // Опционально: визуальный поворот красной клавиши
    [SerializeField] private Unity.VRTemplate.XRKnob modeKnob;

    [Header("Щупы (заполнять только для Вольтметра)")]
    [SerializeField] private VoltmeterProbe redProbe;
    [SerializeField] private VoltmeterProbe blackProbe;

    [Header("Экран (World Space Canvas)")]
    [SerializeField] private GameObject displayRoot;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private TextMeshProUGUI unitText;
    [SerializeField] private TextMeshProUGUI rangeHintText;

    public bool IsPowered { get; private set; } = false;
    public MultimeterMode CurrentMode { get; private set; } = MultimeterMode.Current20mA;

    private void Start()
    {
        if (powerButton != null)
            powerButton.onClick.AddListener(TogglePower);

        if (modeKnob != null)
        {
            modeKnob.clampedMotion = true;
            modeKnob.onValueChange.AddListener(OnKnobChanged);
            OnKnobChanged(modeKnob.value);
        }

        RefreshPowerVisual();
    }

    private void TogglePower()
    {
        IsPowered = !IsPowered;
        RefreshPowerVisual();
    }

    private void RefreshPowerVisual()
    {
        if (displayRoot != null)
            displayRoot.SetActive(IsPowered);

        if (powerRockerVisual != null)
            powerRockerVisual.localRotation = Quaternion.Euler(IsPowered ? -15f : 15f, 0f, 0f);
    }

    private void OnKnobChanged(float normalizedValue)
    {
        // Разбиваем ход ручки [0..1] на 4 дискретных положения
        int index = Mathf.Clamp(Mathf.FloorToInt(normalizedValue * 4f), 0, 3);
        CurrentMode = (MultimeterMode)index;
    }

    private void Update()
    {
        if (solver == null) return;

        switch (CurrentMode)
        {
            case MultimeterMode.Current20mA:
                DisplayCurrent(20f, "20 mA");
                break;

            case MultimeterMode.Current200mA:
                DisplayCurrent(200f, "200 mA");
                break;

            case MultimeterMode.Voltage2V:
                DisplayVoltage(2f, "2 V");
                break;

            case MultimeterMode.Voltage20V:
                DisplayVoltage(20f, "20 V");
                break;
        }
    }

    private void DisplayCurrent(float maxLimitMa, string rangeLabel)
    {
        if (unitText != null) unitText.text = "mA ~";
        if (rangeHintText != null) rangeHintText.text = $"RANGE: {rangeLabel}";

        // Если к прибору подключены щупы (это вольтметр, а его переключили на ток) — показываем 0
        if (redProbe != null || blackProbe != null)
        {
            if (valueText != null) valueText.text = "0.00";
            return;
        }

        float currentMa = solver.CurrentRms * 1000f;

        if (valueText != null)
        {
            valueText.text = currentMa > maxLimitMa ? "OL" : currentMa.ToString("F2");
        }
    }

    private void DisplayVoltage(float maxLimitV, string rangeLabel)
    {
        if (unitText != null) unitText.text = "V ~";
        if (rangeHintText != null) rangeHintText.text = $"RANGE: {rangeLabel}";

        float voltageV = 0f;

        if (redProbe != null && blackProbe != null && graphManager != null)
        {
            voltageV = graphManager.GetVoltageBetweenTerminals(redProbe.CurrentTerminal, blackProbe.CurrentTerminal);
        }

        if (valueText != null)
        {
            valueText.text = voltageV > maxLimitV ? "OL" : voltageV.ToString("F3");
        }
    }

    private void OnDestroy()
    {
        if (powerButton != null) powerButton.onClick.RemoveListener(TogglePower);
        if (modeKnob != null) modeKnob.onValueChange.RemoveListener(OnKnobChanged);
    }
}