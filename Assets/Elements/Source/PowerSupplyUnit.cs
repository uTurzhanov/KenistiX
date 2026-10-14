using UnityEngine;
using UnityEngine.UI;
public class PowerSupplyUnit : MonoBehaviour
{
    // Числовые значения старых режимов сохранены, чтобы не сломать уже
    // сериализованные настройки компонентов в сценах и префабах Unity.
    public enum AmmeterRange
    {
        Micro20 = 0,
        Milli20 = 1,
        Milli200 = 2,
        Milli2000 = 3,
        Micro200 = 4,
        Milli2 = 5
    }
    public enum VoltmeterRange { Volt2, Volt20 }

    private const float MinTargetVoltage = 0f;
    private const float MaxTargetVoltage = 15f;

    [Header("Настройки источника")]
    [Range(MinTargetVoltage, MaxTargetVoltage)]
    public float TargetVoltage = 12f;

    [Header("Переключатель питания")]
    public bool IsPowerOn = true;

    [Tooltip("Включите, если физическое положение рычага работает наоборот: 0 = ON, 1 = OFF")]
    public bool InvertPowerSwitch = false;

    // Установленное напряжение сохраняется при выключении прибора,
    // но электрическая цепь получает 0 В.
    public float OutputVoltage => IsPowerOn ? TargetVoltage : 0f;

    [Header("Клеммы (CircuitComponents)")]
    public CircuitComponent PowerPlus;
    public CircuitComponent PowerMinus;

    public CircuitComponent AmmeterPlus;
    public CircuitComponent AmmeterMinus;

    public CircuitComponent VoltmeterPlus;
    public CircuitComponent VoltmeterMinus;

    [Header("Экраны")]
    public MultimeterDisplay PowerScreen;
    public MultimeterDisplay AmmeterScreen;
    public MultimeterDisplay VoltmeterScreen;

    [Tooltip("Canvas с текстовыми экранами, которые нужно скрывать при выключении прибора")]
    public Canvas[] PowerControlledCanvases;

    [Header("VR Управление (UI)")]
    [Tooltip("Ссылка на UI Slider (ползунок) для изменения напряжения")]
    public Slider VoltageSlider;

    [Header("Текущие режимы")]
    public AmmeterRange CurrentAmmeterRange = AmmeterRange.Milli200;
    public VoltmeterRange CurrentVoltmeterRange = VoltmeterRange.Volt20;

    [Header("Подсветка короткого замыкания")]
    [Tooltip("Renderer объекта, на котором должен подсвечиваться выбранный материал")]
    public Renderer ShortCircuitRenderer;

    [Tooltip("Материал, который нужно подсвечивать. Если Renderer содержит несколько материалов, будет изменен только этот слот")]
    public Material ShortCircuitMaterial;

    [Tooltip("Ток источника в амперах, начиная с которого состояние считается коротким замыканием")]
    [Min(0.0001f)]
    public float ShortCircuitCurrentThreshold = 1f;

    [ColorUsage(false, true)]
    public Color ShortCircuitEmissionColor = Color.red;

    [Min(0f)]
    public float ShortCircuitEmissionIntensity = 5f;

    public bool PulseShortCircuitHighlight = true;

    [Min(0.01f)]
    public float ShortCircuitPulseSpeed = 2f;

    public bool IsShortCircuitActive { get; private set; }

    private float _lastTargetVoltage = -1f;
    private bool _lastPowerState;
    private Material _shortCircuitRuntimeMaterial;
    private Material _originalShortCircuitMaterial;
    private int _shortCircuitMaterialIndex = -1;
    private Color _originalEmissionColor = Color.black;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        if (AmmeterPlus != null && AmmeterMinus != null)
        {
            AmmeterPlus.ConnectTo(AmmeterMinus);
            AmmeterPlus.Resistance = 0.001f;
            AmmeterMinus.Resistance = 0.001f;
        }

        InitializeShortCircuitHighlight();
    }

    private void Start()
    {
        _lastTargetVoltage = TargetVoltage;
        _lastPowerState = IsPowerOn;
        PowerScreen.DisplayStaticVoltage(OutputVoltage);
        SetPowerCanvasesVisible(IsPowerOn);

        // Настройка ползунка (если он назначен в инспекторе)
        if (VoltageSlider != null)
        {
            VoltageSlider.minValue = MinTargetVoltage;
            VoltageSlider.maxValue = MaxTargetVoltage;

            // Синхронизируем начальное значение
            VoltageSlider.value = TargetVoltage;

            // Подписываемся на событие "когда игрок тянет ползунок"
            VoltageSlider.onValueChanged.AddListener(OnVoltageSliderChanged);
        }
    }

    // Этот метод вызывается автоматически каждый раз, когда меняется значение ползунка
    private void OnVoltageSliderChanged(float newValue)
    {
        TargetVoltage = Mathf.Clamp(newValue, MinTargetVoltage, MaxTargetVoltage);
        // Нам не нужно вызывать пересчет здесь, так как Update() 
        // сам заметит изменение TargetVoltage и всё обновит.
    }

    // Вызывается событием XR Knob -> On Value Change.
    // XR Knob передает нормализованное значение от 0 до 1.
    public void SetTargetVoltageFromKnob(float normalizedValue)
    {
        TargetVoltage = Mathf.Lerp(
            MinTargetVoltage,
            MaxTargetVoltage,
            Mathf.Clamp01(normalizedValue)
        );
    }

    // Для XR Toggle или другого события, которое передает bool.
    public void SetPowerState(bool isOn)
    {
        IsPowerOn = isOn;
    }

    public void TurnPowerOn() => SetPowerState(true);
    public void TurnPowerOff() => SetPowerState(false);
    public void TogglePower() => SetPowerState(!IsPowerOn);

    // Пять фиксированных положений крутилки амперметра в обратном порядке:
    // 0 = 200 мА, 0.25 = 20 мА, 0.5 = 2 мА,
    // 0.75 = 200 мкА, 1 = 20 мкА.
    // Диапазон 2000 мА остается доступен через отдельный метод для XR-кнопки.
    public void SetAmmeterRangeFromKnob(float normalizedValue)
    {
        int position = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Clamp01(normalizedValue) * 4f),
            0,
            4
        );

        switch (position)
        {
            case 0:
                CurrentAmmeterRange = AmmeterRange.Milli200;
                break;
            case 1:
                CurrentAmmeterRange = AmmeterRange.Milli20;
                break;
            case 2:
                CurrentAmmeterRange = AmmeterRange.Milli2;
                break;
            case 3:
                CurrentAmmeterRange = AmmeterRange.Micro200;
                break;
            case 4:
                CurrentAmmeterRange = AmmeterRange.Micro20;
                break;
        }
    }

    // Два фиксированных положения крутилки вольтметра в обратном порядке:
    // 0 = 20 В, 1 = 2 В.
    public void SetVoltmeterRangeFromKnob(float normalizedValue)
    {
        int position = Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Clamp01(normalizedValue)),
            0,
            1
        );

        switch (position)
        {
            case 0:
                CurrentVoltmeterRange = VoltmeterRange.Volt20;
                break;
            case 1:
                CurrentVoltmeterRange = VoltmeterRange.Volt2;
                break;
        }
    }

    private void Update()
    {
        // 1. Проверяем, изменилось ли напряжение
        bool targetVoltageChanged = !Mathf.Approximately(TargetVoltage, _lastTargetVoltage);
        bool powerStateChanged = IsPowerOn != _lastPowerState;

        if (targetVoltageChanged || powerStateChanged)
        {
            _lastTargetVoltage = TargetVoltage;
            _lastPowerState = IsPowerOn;

            if (powerStateChanged)
            {
                SetPowerCanvasesVisible(IsPowerOn);
            }

            // При OFF установленное положение крутилки сохраняется,
            // но экран и электрическая цепь получают 0 В.
            PowerScreen.DisplayStaticVoltage(OutputVoltage);

            // Просим цепь пересчитаться
            if (KirchhoffManager.Instance != null)
            {
                KirchhoffManager.Instance.RequestRecalculate();
            }

            // Защита: Если вы поменяли напряжение не ползунком (а кодом или в инспекторе),
            // ползунок должен подтянуться за новым значением.
            if (VoltageSlider != null && !Mathf.Approximately(VoltageSlider.value, TargetVoltage))
            {
                // Временно отписываемся от события, чтобы не было бесконечного цикла (Slider -> TargetVoltage -> Slider)
                VoltageSlider.onValueChanged.RemoveListener(OnVoltageSliderChanged);
                VoltageSlider.value = TargetVoltage;
                VoltageSlider.onValueChanged.AddListener(OnVoltageSliderChanged);
            }
        }

        // 2. Обновление амперметра и вольтметра
        bool circuitClosed = KirchhoffManager.Instance != null && KirchhoffManager.Instance.IsCircuitClosed;

        UpdateShortCircuitHighlight(circuitClosed);

        if (!circuitClosed)
        {
            AmmeterScreen.DisplayZero();
            VoltmeterScreen.DisplayZero();
            return;
        }

        UpdateAmmeterDisplay();
        UpdateVoltmeterDisplay();
    }

    private void SetPowerCanvasesVisible(bool visible)
    {
        if (PowerControlledCanvases == null)
        {
            return;
        }

        foreach (Canvas targetCanvas in PowerControlledCanvases)
        {
            if (targetCanvas != null)
            {
                // Отключаем только отрисовку Canvas, а не его GameObject.
                // Поэтому скрипты и сам переключатель продолжают работать.
                targetCanvas.enabled = visible;
            }
        }
    }

    private void OnDestroy()
    {
        // Хорошая практика: отписываться от событий при удалении объекта
        if (VoltageSlider != null)
        {
            VoltageSlider.onValueChanged.RemoveListener(OnVoltageSliderChanged);
        }

        RestoreShortCircuitMaterial();
    }

    private void InitializeShortCircuitHighlight()
    {
        if (ShortCircuitRenderer == null || ShortCircuitMaterial == null)
        {
            return;
        }

        Material[] materials = ShortCircuitRenderer.sharedMaterials;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == ShortCircuitMaterial)
            {
                _shortCircuitMaterialIndex = i;
                break;
            }
        }

        if (_shortCircuitMaterialIndex < 0)
        {
            Debug.LogWarning(
                "[PowerSupplyUnit] Материал подсветки КЗ не найден в Renderer.",
                this
            );
            return;
        }

        _originalShortCircuitMaterial = materials[_shortCircuitMaterialIndex];
        _shortCircuitRuntimeMaterial = new Material(_originalShortCircuitMaterial);
        _shortCircuitRuntimeMaterial.name = _originalShortCircuitMaterial.name + " (Short Circuit Instance)";

        if (!_shortCircuitRuntimeMaterial.HasProperty(EmissionColorId))
        {
            Debug.LogWarning(
                "[PowerSupplyUnit] Выбранный материал не поддерживает свойство _EmissionColor.",
                this
            );

            Destroy(_shortCircuitRuntimeMaterial);
            _shortCircuitRuntimeMaterial = null;
            _shortCircuitMaterialIndex = -1;
            return;
        }

        _originalEmissionColor = _shortCircuitRuntimeMaterial.GetColor(EmissionColorId);
        _shortCircuitRuntimeMaterial.EnableKeyword("_EMISSION");

        // Подменяем материал только на данном Renderer. Исходный material asset
        // и остальные объекты, использующие его, не изменяются.
        materials[_shortCircuitMaterialIndex] = _shortCircuitRuntimeMaterial;
        ShortCircuitRenderer.sharedMaterials = materials;

        SetShortCircuitEmission(false);
    }

    private void UpdateShortCircuitHighlight(bool circuitClosed)
    {
        IsShortCircuitActive =
            circuitClosed &&
            OutputVoltage > 0.001f &&
            PowerPlus != null &&
            Mathf.Abs(PowerPlus.Current) >= ShortCircuitCurrentThreshold;

        SetShortCircuitEmission(IsShortCircuitActive);
    }

    private void SetShortCircuitEmission(bool active)
    {
        if (_shortCircuitRuntimeMaterial == null)
        {
            return;
        }

        if (!active)
        {
            _shortCircuitRuntimeMaterial.SetColor(EmissionColorId, _originalEmissionColor);
            return;
        }

        float pulseMultiplier = 1f;

        if (PulseShortCircuitHighlight)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(
                Time.time * ShortCircuitPulseSpeed * Mathf.PI * 2f
            );
            pulseMultiplier = Mathf.Lerp(0.35f, 1f, pulse);
        }

        Color emission =
            ShortCircuitEmissionColor *
            ShortCircuitEmissionIntensity *
            pulseMultiplier;

        _shortCircuitRuntimeMaterial.SetColor(EmissionColorId, emission);
    }

    private void RestoreShortCircuitMaterial()
    {
        if (
            ShortCircuitRenderer != null &&
            _originalShortCircuitMaterial != null &&
            _shortCircuitMaterialIndex >= 0
        )
        {
            Material[] materials = ShortCircuitRenderer.sharedMaterials;

            if (_shortCircuitMaterialIndex < materials.Length)
            {
                materials[_shortCircuitMaterialIndex] = _originalShortCircuitMaterial;
                ShortCircuitRenderer.sharedMaterials = materials;
            }
        }

        if (_shortCircuitRuntimeMaterial != null)
        {
            Destroy(_shortCircuitRuntimeMaterial);
            _shortCircuitRuntimeMaterial = null;
        }
    }




    private void UpdateAmmeterDisplay()
    {
        if (AmmeterPlus == null || AmmeterMinus == null)
        {
            AmmeterScreen.DisplayZero();
            return;
        }

        // Амперметр измеряет ток, проходящий через него. 
        // Если он в цепи, его ток > 0.
        float current = AmmeterPlus.Current;

        // Определение полярности амперметра
        // Если потенциал на плюсовой клемме меньше, чем на минусовой - ток течет в обратную сторону
        if (AmmeterPlus.Potential < AmmeterMinus.Potential)
        {
            current = -current;
        }

        // Если ток нулевой (цепь разорвана), показываем нули
        if (Mathf.Approximately(current, 0f))
        {
            AmmeterScreen.DisplayZero();
            return;
        }

        switch (CurrentAmmeterRange)
        {
            case AmmeterRange.Micro20:
                AmmeterScreen.DisplayCurrent(current, 0.00002f, "uA"); // 20 мкА
                break;
            case AmmeterRange.Micro200:
                AmmeterScreen.DisplayCurrent(current, 0.0002f, "uA");  // 200 мкА
                break;
            case AmmeterRange.Milli2:
                AmmeterScreen.DisplayCurrent(current, 0.002f, "mA");   // 2 мА
                break;
            case AmmeterRange.Milli20:
                AmmeterScreen.DisplayCurrent(current, 0.02f, "mA");    // 20 мА
                break;
            case AmmeterRange.Milli200:
                AmmeterScreen.DisplayCurrent(current, 0.2f, "mA");     // 200 мА
                break;
            case AmmeterRange.Milli2000:
                AmmeterScreen.DisplayCurrent(current, 2f, "mA");     // 2000 мА
                break;
        }
    }

    private void UpdateVoltmeterDisplay()
    {
        if (VoltmeterPlus == null || VoltmeterMinus == null)
        {
            VoltmeterScreen.DisplayZero();
            return;
        }

        // Вольтметр измеряет разность потенциалов: U = Потенциал(+) - Потенциал(-)
        // Если цепь не замкнута на вольтметр, потенциалы будут 0
        if (!VoltmeterPlus.IsMinusConnected && !VoltmeterMinus.IsMinusConnected)
        {
            VoltmeterScreen.DisplayZero();
            return;
        }

        float measuredVoltage = VoltmeterPlus.Potential - VoltmeterMinus.Potential;

        switch (CurrentVoltmeterRange)
        {
            case VoltmeterRange.Volt2:
                VoltmeterScreen.DisplayVoltage(measuredVoltage, 2f);
                break;
            case VoltmeterRange.Volt20:
                VoltmeterScreen.DisplayVoltage(measuredVoltage, 20f);
                break;
        }
    }

    // Методы для XR кнопок (вызывайте через XR Simple Interactor на кнопках прибора)
    public void SetAmmeterRangeMicro20() => CurrentAmmeterRange = AmmeterRange.Micro20;
    public void SetAmmeterRangeMicro200() => CurrentAmmeterRange = AmmeterRange.Micro200;
    public void SetAmmeterRangeMilli2() => CurrentAmmeterRange = AmmeterRange.Milli2;
    public void SetAmmeterRangeMilli20() => CurrentAmmeterRange = AmmeterRange.Milli20;
    public void SetAmmeterRangeMilli200() => CurrentAmmeterRange = AmmeterRange.Milli200;
    public void SetAmmeterRangeMilli2000() => CurrentAmmeterRange = AmmeterRange.Milli2000;

    public void SetVoltmeterRange2V() => CurrentVoltmeterRange = VoltmeterRange.Volt2;
    public void SetVoltmeterRange20V() => CurrentVoltmeterRange = VoltmeterRange.Volt20;
}
