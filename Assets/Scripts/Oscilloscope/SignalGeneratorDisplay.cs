using TMPro;
using UnityEngine;

public class SignalGeneratorDisplay : MonoBehaviour
{
    [SerializeField] private SignalGenerator generator;
    [SerializeField] private GameObject screenContentRoot; // Контейнер со всем UI экрана

    [Header("Текстовые поля экрана")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI waveformText;
    [SerializeField] private TextMeshProUGUI frequencyText;
    [SerializeField] private TextMeshProUGUI voltageText;
    [SerializeField] private TextMeshProUGUI stepHintText;

    [Header("Цвета выделения активного параметра")]
    [SerializeField] private Color activeColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color normalColor = Color.white;

    private void OnEnable()
    {
        if (generator != null)
            generator.OnGeneratorStateChanged += RefreshDisplay;
    }

    private void Start()
    {
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (generator == null) return;

        // Если питание выключено — гасим экран
        if (screenContentRoot != null)
            screenContentRoot.SetActive(generator.IsPowered);

        if (!generator.IsPowered) return;

        // Статус выхода
        if (statusText != null)
        {
            statusText.text = generator.IsOutputEnabled ? "OUT: ON" : "OUT: OFF";
            statusText.color = generator.IsOutputEnabled ? Color.green : Color.red;
        }

        // Форма сигнала
        if (waveformText != null)
        {
            waveformText.text = generator.CurrentWaveform switch
            {
                WaveformType.Sine => "WAVE: ~ SINE",
                WaveformType.Square => "WAVE: П SQUARE",
                WaveformType.Triangle => "WAVE: /\\ TRI",
                _ => "WAVE: ~"
            };
        }

        // Частота и напряжение
        if (frequencyText != null)
        {
            frequencyText.text = $"FREQ: {generator.Frequency:F0} Hz";
            frequencyText.color = generator.ActiveParam == GeneratorEditParam.Frequency ? activeColor : normalColor;
        }

        if (voltageText != null)
        {
            voltageText.text = $"U (RMS): {generator.RmsVoltage:F2} V";
            voltageText.color = generator.ActiveParam == GeneratorEditParam.Voltage ? activeColor : normalColor;
        }

        // Подсказка текущего шага энкодера
        if (stepHintText != null)
        {
            string unit = generator.ActiveParam == GeneratorEditParam.Frequency ? "Hz" : "V";
            stepHintText.text = $"STEP: {generator.CurrentStep} {unit}";
        }
    }

    private void OnDisable()
    {
        if (generator != null)
            generator.OnGeneratorStateChanged -= RefreshDisplay;
    }
}