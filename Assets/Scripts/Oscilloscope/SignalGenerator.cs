using System;
using Unity.VRTemplate;
using UnityEngine;


public enum WaveformType { Sine, Square, Triangle }
public enum GeneratorEditParam { Frequency, Voltage }

public class SignalGenerator : MonoBehaviour, ISignalSource
{
    [Header("Кнопки питания и выхода")]
    [SerializeField] private VRButtonGrip powerButton;
    [SerializeField] private VRButtonGrip outputButton;

    [Header("Кнопки выбора формы сигнала")]
    [SerializeField] private VRButtonGrip sineButton;
    [SerializeField] private VRButtonGrip squareButton;
    [SerializeField] private VRButtonGrip triangleButton;

    [Header("Кнопки выбора параметра (слева от экрана)")]
    [SerializeField] private VRButtonGrip freqSelectButton;
    [SerializeField] private VRButtonGrip amplSelectButton;

    [Header("Энкодер (XRI XRKnob) и стрелки навигации")]
    [SerializeField] private XRKnob mainEncoderKnob;
    [SerializeField] private VRButtonGrip stepLeftButton;
    [SerializeField] private VRButtonGrip stepRightButton;
    [SerializeField] private VRButtonGrip valueUpButton;
    [SerializeField] private VRButtonGrip valueDownButton;

    [Header("Ограничения параметров (под пределы 2 В и 20 мА)")]
    [SerializeField] private float minFrequency = 50f;
    [SerializeField] private float maxFrequency = 2000f;
    [SerializeField] private float maxRmsVoltage = 1.8f;

    [Header("Чувствительность XRKnob")]
    [Tooltip("Сколько единиц value нужно повернуть для 1 шага (0.08 ≈ 15° при стандартном диапазоне 180°)")]
    [SerializeField] private float knobValuePerStep = 0.08f;

    // Текущее состояние прибора
    public bool IsPowered { get; private set; } = false;
    public bool IsOutputEnabled { get; private set; } = false;
    public WaveformType CurrentWaveform { get; private set; } = WaveformType.Sine;
    public GeneratorEditParam ActiveParam { get; private set; } = GeneratorEditParam.Frequency;

    public float Frequency { get; private set; } = 200f;     // Гц
    public float RmsVoltage { get; private set; } = 0.8f;    // В (действующее значение)
    public float AmplitudeVoltage => RmsVoltage * Mathf.Sqrt(2f); // В (амплитудное для осциллографа)

    // Шаги изменения: для частоты {1, 10, 100} Гц; для напряжения {0.01, 0.1} В
    private readonly float[] _freqSteps = { 1f, 10f, 100f };
    private readonly float[] _voltSteps = { 0.01f, 0.1f };
    private int _freqStepIndex = 1; // По умолчанию шаг 10 Гц
    private int _voltStepIndex = 1; // По умолчанию шаг 0.1 В

    private float _lastKnobValue = 0f;
    private float _accumulatedDelta = 0f;

    public event Action OnGeneratorStateChanged;

    public float CurrentStep => ActiveParam == GeneratorEditParam.Frequency
        ? _freqSteps[_freqStepIndex]
        : _voltSteps[_voltStepIndex];

    private void Start()
    {
        SetupEncoder();
        SubscribeButtons();
        OnGeneratorStateChanged?.Invoke();
    }

    public float GetVoltage(float time)
    {
        return GetInstantVoltage(time);
    }

    private void SetupEncoder()
    {
        if (mainEncoderKnob == null) return;

        // Отключаем ограничение угла, чтобы ручка крутилась бесконечно как энкодер
        mainEncoderKnob.clampedMotion = false;
        _lastKnobValue = mainEncoderKnob.value;
        mainEncoderKnob.onValueChange.AddListener(OnEncoderValueChanged);
    }

    private void SubscribeButtons()
    {
        if (powerButton != null) powerButton.onClick.AddListener(TogglePower);
        if (outputButton != null) outputButton.onClick.AddListener(ToggleOutput);

        if (sineButton != null) sineButton.onClick.AddListener(SelectSine);
        if (squareButton != null) squareButton.onClick.AddListener(SelectSquare);
        if (triangleButton != null) triangleButton.onClick.AddListener(SelectTriangle);

        if (freqSelectButton != null) freqSelectButton.onClick.AddListener(SelectFrequencyParam);
        if (amplSelectButton != null) amplSelectButton.onClick.AddListener(SelectVoltageParam);

        if (stepLeftButton != null) stepLeftButton.onClick.AddListener(IncreaseStep);
        if (stepRightButton != null) stepRightButton.onClick.AddListener(DecreaseStep);
        if (valueUpButton != null) valueUpButton.onClick.AddListener(IncrementValue);
        if (valueDownButton != null) valueDownButton.onClick.AddListener(DecrementValue);
    }

    private void OnEncoderValueChanged(float newValue)
    {
        float delta = newValue - _lastKnobValue;
        _lastKnobValue = newValue;

        if (!IsPowered) return;

        _accumulatedDelta += delta;

        if (Mathf.Abs(_accumulatedDelta) >= knobValuePerStep)
        {
            int steps = _accumulatedDelta > 0
                ? Mathf.FloorToInt(_accumulatedDelta / knobValuePerStep)
                : Mathf.CeilToInt(_accumulatedDelta / knobValuePerStep);

            _accumulatedDelta -= steps * knobValuePerStep;
            ChangeActiveValue(steps * CurrentStep);
        }
    }

    private void TogglePower()
    {
        IsPowered = !IsPowered;
        if (!IsPowered)
        {
            IsOutputEnabled = false;
        }
        OnGeneratorStateChanged?.Invoke();
    }

    private void ToggleOutput()
    {
        if (!IsPowered) return;
        IsOutputEnabled = !IsOutputEnabled;
        OnGeneratorStateChanged?.Invoke();
    }

    private void SelectSine() => SetWaveform(WaveformType.Sine);
    private void SelectSquare() => SetWaveform(WaveformType.Square);
    private void SelectTriangle() => SetWaveform(WaveformType.Triangle);

    private void SetWaveform(WaveformType type)
    {
        if (!IsPowered) return;
        CurrentWaveform = type;
        OnGeneratorStateChanged?.Invoke();
    }

    private void SelectFrequencyParam() => SetActiveParam(GeneratorEditParam.Frequency);
    private void SelectVoltageParam() => SetActiveParam(GeneratorEditParam.Voltage);

    private void SetActiveParam(GeneratorEditParam param)
    {
        if (!IsPowered) return;
        ActiveParam = param;
        _accumulatedDelta = 0f;
        OnGeneratorStateChanged?.Invoke();
    }

    private void IncreaseStep()
    {
        if (!IsPowered) return;
        if (ActiveParam == GeneratorEditParam.Frequency)
            _freqStepIndex = Mathf.Clamp(_freqStepIndex + 1, 0, _freqSteps.Length - 1);
        else
            _voltStepIndex = Mathf.Clamp(_voltStepIndex + 1, 0, _voltSteps.Length - 1);

        OnGeneratorStateChanged?.Invoke();
    }

    private void DecreaseStep()
    {
        if (!IsPowered) return;
        if (ActiveParam == GeneratorEditParam.Frequency)
            _freqStepIndex = Mathf.Clamp(_freqStepIndex - 1, 0, _freqSteps.Length - 1);
        else
            _voltStepIndex = Mathf.Clamp(_voltStepIndex - 1, 0, _voltSteps.Length - 1);

        OnGeneratorStateChanged?.Invoke();
    }

    private void IncrementValue()
    {
        if (!IsPowered) return;
        ChangeActiveValue(CurrentStep);
    }

    private void DecrementValue()
    {
        if (!IsPowered) return;
        ChangeActiveValue(-CurrentStep);
    }

    private void ChangeActiveValue(float delta)
    {
        if (ActiveParam == GeneratorEditParam.Frequency)
        {
            Frequency = Mathf.Clamp(Mathf.Round(Frequency + delta), minFrequency, maxFrequency);
        }
        else
        {
            float rawVolt = RmsVoltage + delta;
            RmsVoltage = Mathf.Clamp((float)Math.Round(rawVolt, 2), 0f, maxRmsVoltage);
        }

        OnGeneratorStateChanged?.Invoke();
    }

    /// <summary>
    /// Мгновенное напряжение на выходе генератора для осциллографа
    /// </summary>
    public float GetInstantVoltage(float time)
    {
        if (!IsPowered || !IsOutputEnabled || RmsVoltage <= 0f) return 0f;

        float phase = 2f * Mathf.PI * Frequency * time;
        float amp = AmplitudeVoltage;

        switch (CurrentWaveform)
        {
            case WaveformType.Sine:
                return amp * Mathf.Sin(phase);
            case WaveformType.Square:
                return amp * Mathf.Sign(Mathf.Sin(phase));
            case WaveformType.Triangle:
                return amp * (2f / Mathf.PI) * Mathf.Asin(Mathf.Sin(phase));
            default:
                return 0f;
        }
    }

    private void OnDestroy()
    {
        if (mainEncoderKnob != null)
            mainEncoderKnob.onValueChange.RemoveListener(OnEncoderValueChanged);

        if (powerButton != null) powerButton.onClick.RemoveListener(TogglePower);
        if (outputButton != null) outputButton.onClick.RemoveListener(ToggleOutput);

        if (sineButton != null) sineButton.onClick.RemoveListener(SelectSine);
        if (squareButton != null) squareButton.onClick.RemoveListener(SelectSquare);
        if (triangleButton != null) triangleButton.onClick.RemoveListener(SelectTriangle);

        if (freqSelectButton != null) freqSelectButton.onClick.RemoveListener(SelectFrequencyParam);
        if (amplSelectButton != null) amplSelectButton.onClick.RemoveListener(SelectVoltageParam);

        if (stepLeftButton != null) stepLeftButton.onClick.RemoveListener(IncreaseStep);
        if (stepRightButton != null) stepRightButton.onClick.RemoveListener(DecreaseStep);
        if (valueUpButton != null) valueUpButton.onClick.RemoveListener(IncrementValue);
        if (valueDownButton != null) valueDownButton.onClick.RemoveListener(DecrementValue);
    }
}