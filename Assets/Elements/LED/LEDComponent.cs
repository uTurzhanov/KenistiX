using UnityEngine;

public class LedComponent : CircuitComponent
{
    public enum LedColor
    {
        Blue,
        White
    }

    [Header("Настройки Светодиода")]
    [Tooltip("Выберите цвет. Параметры ВАХ изменятся автоматически.")]
    public LedColor CurrentColor = LedColor.White;

    [Header("Визуализация (Emission, без Light-компонента)")]
    [Tooltip("Renderer светодиода. Яркость свечения управляется через Emission материала.")]
    public Renderer LedRenderer;

    [Tooltip("Базовый цвет свечения при максимальной яркости")]
    public Color EmissionColor = Color.white;

    [Tooltip("Сила свечения (HDR intensity), при которой ток = MaxForwardCurrent")]
    public float MaxEmissionIntensity = 4f;

    [Header("Параметры модели Шокли (Авто-настройка по цвету)")]
    [Tooltip("Тепловое напряжение, ~0.026В при комнатной температуре")]
    public float ThermalVoltage = 0.026f;

    [Tooltip("Коэффициент неидеальности перехода. ПОДОБРАН ПО ИЗМЕРЕННЫМ ДАННЫМ ЛАБЫ " +
             "(scipy.curve_fit, погрешность <0.1-0.2В на всем диапазоне) для White и Blue.")]
    public float IdealityFactor = 6.08f;

    [Tooltip("Ток насыщения — сдвигает кривую по оси U, подбирается по цвету")]
    public float SaturationCurrent = 9.85e-11f;

    [Tooltip("Объемное сопротивление кристалла — небольшой линейный довесок на больших токах")]
    public float BulkResistance = 10.07f;

    [Tooltip("Максимальный прямой ток (обычно 20-30 мА = 0.02-0.03 А)")]
    public float MaxForwardCurrent = 0.03f;

    [Tooltip("Сопротивление закрытого светодиода")]
    public float ReverseResistance = 9999999f;

    [Tooltip("Погрешность для сходимости решателя")]
    public float ConvergenceTolerance = 0.02f;

    [Tooltip("Стартовое сопротивление 'открытого' состояния — используется только для того, " +
             "чтобы решатель мог получить НЕНУЛЕВОЙ ток на первом проходе и попасть в зону, " +
             "где модель Шокли уже способна сама себя скорректировать.")]
    public float StartupForwardResistance = 50f;

    public bool IsBurnedOut = false;
    public ParticleSystem BurnSparkEffect;

    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    private MaterialPropertyBlock _propBlock;

    // Текущее предположение решателя об открытом/закрытом состоянии светодиода.
    // Это состояние НЕ зависит от величины Current (см. UpdateElectricalData) —
    // иначе получается заколдованный круг: при Resistance = ReverseResistance ток
    // получается микроскопическим (~1e-6 А), что меньше эпсилон-порога, из-за чего
    // светодиод никогда не переключится в открытое состояние, даже если по схеме
    // он физически смещен в прямом направлении.
    private bool _assumedForward = false;

    private void OnValidate()
    {
        ApplyColorSettings();
    }

    private void Awake()
    {
        ApplyColorSettings();
        if (Resistance <= 0f) Resistance = ReverseResistance;
        _propBlock = new MaterialPropertyBlock();
        SetEmission(0f);
        BurnSparkEffect = GetComponentInChildren<ParticleSystem>();
    }

    // --- ПАРАМЕТРЫ ПОДОБРАНЫ МЕТОДОМ НАИМЕНЬШИХ КВАДРАТОВ (scipy.curve_fit) ПО РЕАЛЬНЫМ
    // ДАННЫМ ЛАБ. ИЗМЕРЕНИЙ для White и Blue — это единственные два светодиода в данной
    // лабораторной работе ("Диод белый" и "Диод синий" в тетради). Максимальная погрешность
    // фита <0.1-0.2В на всём измеренном диапазоне (E=2..12В).
    // Скрипт для переподбора при появлении новых данных: /mnt/user-data/outputs/fit_leds2.py
    private void ApplyColorSettings()
    {
        switch (CurrentColor)
        {
            case LedColor.Blue:
                // Подобрано по факт. данным "Диод синий": n=6.08, Is=9.85e-11, Rbulk=10.07
                EmissionColor = new Color(0.2f, 0.4f, 1f);
                IdealityFactor = 6.08f;
                SaturationCurrent = 9.85e-11f;
                BulkResistance = 10.07f;
                break;
            case LedColor.White:
                // Подобрано по факт. данным "Диод белый": n=5.72, Is=9.73e-11, Rbulk=9.52
                EmissionColor = Color.white;
                IdealityFactor = 5.72f;
                SaturationCurrent = 9.73e-11f;
                BulkResistance = 9.52f;
                break;
        }
    }

    public override void UpdateElectricalData(float newCurrent)
    {
        if (IsBurnedOut)
        {
            base.UpdateElectricalData(0f);
            SetEmission(0f);
            return;
        }

        float currentMagnitude = Mathf.Abs(newCurrent);
        float previousResistance = Resistance;

        // --- ОПРЕДЕЛЕНИЕ НАПРАВЛЕНИЯ ТОКА (С ПЕРЕКЛЮЧЕНИЕМ СОСТОЯНИЯ) ---
        // ВАЖНО: реальный ток утечки в этой сцене (Resistance=ReverseResistance=9999999,
        // 12В источник) равен ~1.2e-6 А — это МЕНЬШЕ старого порога 0.00001f (1e-5), поэтому
        // переключение никогда не срабатывало (Current годами оставался 1.19994e-06).
        // Порог должен быть заведомо МЕНЬШЕ, чем 1/ReverseResistance при разумном напряжении
        // источника — иначе для любых высоких значений ReverseResistance ток утечки сам
        // никогда не пересечёт порог. Берём 1e-7 (на порядок меньше типичной утечки).
        const float forwardEpsilon = 1e-7f;

        if (newCurrent > forwardEpsilon && !_assumedForward)
        {
            _assumedForward = true;
            Resistance = StartupForwardResistance;
            KirchhoffManager.Instance.RequestRecalculate();
        }
        else if (newCurrent < -forwardEpsilon && _assumedForward)
        {
            _assumedForward = false;
            Resistance = ReverseResistance;
            KirchhoffManager.Instance.RequestRecalculate();
        }

        bool conductingForward = _assumedForward;

        base.UpdateElectricalData(newCurrent);

        // --- НАПРЯЖЕНИЕ ПО МОДЕЛИ ШОКЛИ ---
        // U = n*Vt * ln(I/Is + 1) + I*Rbulk вместо линейной U = V0 + I*R.
        // Линейная модель не может воспроизвести "плоский участок + резкий излом",
        // видимый и на эталонном графике (Image 1), и в измеренных данных лабы.
        //
        // ВАЖНО (исправление самоотмены переключения): раньше здесь стояла проверка
        // "currentMagnitude > 0.000001f" — но именно в кадре, когда _assumedForward
        // только что стал true (см. блок выше), currentMagnitude — это ВСЁ ЕЩЁ тот же
        // микроскопический ток утечки, который и вызвал переключение. Он почти всегда
        // меньше 1e-6, поэтому код проваливался в "else" и СРАЗУ возвращал
        // Resistance = ReverseResistance, отменяя переключение в том же кадре —
        // светодиод никогда не успевал "увидеть" следующий, уже нормальный проход решателя.
        // Теперь проверяем только conductingForward (направление), а не величину тока:
        // если currentMagnitude слишком мал для формулы Шокли (был бы log(0)),
        // просто используем StartupForwardResistance как временное приближение —
        // решатель пересчитает на следующем кадре уже с реальным током.
        if (conductingForward)
        {
            if (currentMagnitude > 1e-9f)
            {
                float nVt = IdealityFactor * ThermalVoltage;
                float shockleyVoltage = nVt * Mathf.Log(currentMagnitude / SaturationCurrent + 1f);
                Voltage = shockleyVoltage + currentMagnitude * BulkResistance;

                // --- ВАЖНО: ПОЧЕМУ ИМЕННО СЕКУЩАЯ (Voltage/I), А НЕ КАСАТЕЛЬНАЯ (dV/dI) ---
                // Решатель (KirchhoffManager) использует Resistance ИСКЛЮЧИТЕЛЬНО через
                // закон Ома U=I*R при расчете потенциалов узлов (SolveOnePass, ApplyDirectShort).
                // Это значит, что Resistance ДОЛЖНО давать ТОЧНО ТО ЖЕ Voltage при умножении
                // на текущий ток — то есть Resistance = Voltage / I (секущая), а НЕ
                // производная dV/dI (касательная).
                //
                // Касательная (динамическое сопротивление) корректно описывает только НАКЛОН
                // кривой в точке, но для кривой со смещением (любой диод/светодиод не проходит
                // через ноль линейно — есть "колено") касательная*I НЕ равна реальному U в этой
                // точке. Численная проверка: при I=22.86мА касательная давала R=16.98Ом =>
                // U=I*R=0.39В, тогда как настоящее (по формуле Шокли) Voltage=3.28В — решатель
                // считал потенциалы узлов по ЗАНИЖЕННОМУ напряжению, и вольтметр (который читает
                // именно Potential узлов, а не LED.Voltage напрямую) показывал неверные 0.39-0.57В,
                // хотя LED.Voltage внутри компонента был правильным (см. диагностику с реальными
                // показаниями ВАХ). Секущая гарантированно самосогласована: I*(Voltage/I)=Voltage.
                Resistance = Mathf.Min(Voltage / currentMagnitude, ReverseResistance);

                // Светимость пропорциональна прямому току (базовая физика LED:
                // световой поток ~ линейно с током выше порога), не напряжению.
                float targetIntensity = currentMagnitude / MaxForwardCurrent;
                SetEmission(Mathf.Clamp01(targetIntensity) * MaxEmissionIntensity);
            }
            else
            {
                // Ток пока слишком мал для формулы (этот самый кадр переключения) —
                // оставляем StartupForwardResistance, НЕ откатываемся в ReverseResistance.
                Resistance = StartupForwardResistance;
                SetEmission(0f);
            }
        }
        else
        {
            // Обратное смещение / нет тока: светодиод не проводит и не светится
            Resistance = ReverseResistance;
            Voltage = currentMagnitude * ReverseResistance;
            SetEmission(0f);
        }

        // --- ЗАПРОС ПЕРЕСЧЁТА ДЛЯ СХОДИМОСТИ (та же логика, что у DiodComponent) ---
        bool resistanceChangedSignificantly =
            Mathf.Abs(Resistance - previousResistance) > ConvergenceTolerance * Mathf.Max(previousResistance, 0.0001f);

        if (resistanceChangedSignificantly)
        {
            KirchhoffManager.Instance.RequestRecalculate();
        }

        // --- СГОРАНИЕ ОТ ПРЕВЫШЕНИЯ ТОКА ---
        if (conductingForward && currentMagnitude > MaxForwardCurrent)
        {
            BurnOut();
        }
    }

    // Управляет яркостью через Emission материала вместо Light-компонента.
    // Использует MaterialPropertyBlock, чтобы не создавать множество уникальных
    // материалов в проекте при наличии нескольких LED одного материала.
    private void SetEmission(float intensity)
    {
        if (LedRenderer == null) return;

        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

        LedRenderer.GetPropertyBlock(_propBlock);
        Color finalColor = EmissionColor * intensity;
        _propBlock.SetColor(EmissionColorID, finalColor);
        LedRenderer.SetPropertyBlock(_propBlock);
    }

    private void BurnOut()
    {
        IsBurnedOut = true;
        Resistance = ReverseResistance;
        SetEmission(0f);

        Debug.LogWarning($"Светодиод ({CurrentColor}) сгорел! Ток превысил {MaxForwardCurrent} А");
        if (BurnSparkEffect != null)
        {
            // Есллы настроены на Loop, они будут играть постоянно.
            // Если они должны выстрелить один раз, просто Play().
            BurnSparkEffect.Play();
        }
        KirchhoffManager.Instance.RequestRecalculate();
    }
}