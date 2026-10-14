using UnityEngine;

public class DiodeComponent : CircuitComponent
{
    [Header("Diode Settings (Shockley model)")]
    [Tooltip("Тепловое напряжение при комнатной температуре, обычно ~0.026В (kT/q)")]
    public float ThermalVoltage = 0.026f;

    [Tooltip("Коэффициент неидеальности диода. ПОДОБРАН ПО ИЗМЕРЕННЫМ ДАННЫМ ЛАБЫ " +
             "(выпрямительный диод: n≈1.82, погрешность фита <11мВ на всём диапазоне)")]
    public float IdealityFactor = 1.818f;

    [Tooltip("Обратный ток насыщения, Амперы. ПОДОБРАН ПО ИЗМЕРЕННЫМ ДАННЫМ ЛАБЫ (метод наименьших квадратов scipy.curve_fit)")]
    public float SaturationCurrent = 4.655e-9f;

    [Tooltip("Максимальный прямой ток в Амперах")]
    public float MaxForwardCurrent = 1f;

    [Tooltip("Сопротивление закрытого диода (Ом)")]
    public float ReverseResistance = 9999999f;

    [Tooltip("Объемное сопротивление кристалла. ПОДОБРАНО ПО ИЗМЕРЕННЫМ ДАННЫМ ЛАБЫ")]
    public float BulkResistance = 0.0012f;

    [Tooltip("Относительное изменение Resistance за один проход, при котором запрашиваем еще один пересчёт (сходимость)")]
    public float ConvergenceTolerance = 0.02f;

    public bool IsBurnedOut = false;
    public ParticleSystem BurnSparkEffect;
    private void Awake()
    {
        // Изначально предполагаем, что диод закрыт (сопротивление бесконечно)
        if (Resistance <= 0f) Resistance = ReverseResistance;
    }

    public override void UpdateElectricalData(float newCurrent)
    {
        if (IsBurnedOut)
        {
            base.UpdateElectricalData(0f);
            return;
        }

        float currentMagnitude = Mathf.Abs(newCurrent);

        // Эпсилон должен быть заведомо меньше тока утечки через ReverseResistance
        // при любом разумном напряжении источника (см. аналогичное исправление в LedComponent) —
        // иначе диод может застрять в обратном состоянии и никогда не переключиться в прямое.
        const float forwardEpsilon = 1e-7f;
        bool conductingForward = newCurrent > forwardEpsilon;
        float previousResistance = Resistance;

        // Вызываем базовый метод (запишет Current)
        base.UpdateElectricalData(newCurrent);

        // --- НАПРЯЖЕНИЕ ПО МОДЕЛИ ШОКЛИ ---
        // Реальная ВАХ диода нелинейна на всем диапазоне: U растет логарифмически
        // с ростом I, а не остается на фиксированном уровне (0.7В) с линейной добавкой.
        // Подтверждено данными лабораторных измерений: при росте E от 2В до 15В
        // напряжение на диоде растет лишь от ~0.67В до ~0.83В, при этом R = U/I падает
        // с ~142 Ом до ~14 Ом — диод явно не ведет себя как (фикс. напряжение + резистор),
        // у которого R был бы постоянным.
        //
        // Уравнение Шокли: I = Is * (exp(U / (n*Vt)) - 1)  =>  U = n*Vt * ln(I/Is + 1)
        if (conductingForward && !Mathf.Approximately(currentMagnitude, 0f))
        {
            float nVt = IdealityFactor * ThermalVoltage;
            Voltage = nVt * Mathf.Log(currentMagnitude / SaturationCurrent + 1f);
            Voltage += currentMagnitude * BulkResistance; // легкий доп. наклон на больших токах

            // Линеаризация для решателя: следующий проход KirchhoffManager использует
            // именно это Resistance как сопротивление ребра (солвер не делает Newton-Raphson
            // сам — итерация происходит за счет повторных RequestRecalculate() и кадров).
            Resistance = Voltage / currentMagnitude;
        }
        else
        {
            Voltage = currentMagnitude * ReverseResistance;
            Resistance = ReverseResistance;
        }

        // --- ЗАПРОС ПЕРЕСЧЁТА ДЛЯ СХОДИМОСТИ ---
        // В отличие от старой реализации (которая пересчитывала только на бинарном
        // переключении открыт/закрыт), здесь Resistance меняется НА КАЖДОМ проходе,
        // потому что модель Шокли нелинейна. Чтобы решение сошлось к самосогласованной
        // рабочей точке, запрашиваем еще один пересчёт, пока Resistance не стабилизируется.
        bool resistanceChangedSignificantly =
            Mathf.Abs(Resistance - previousResistance) > ConvergenceTolerance * Mathf.Max(previousResistance, 0.0001f);

        if (resistanceChangedSignificantly)
        {
            KirchhoffManager.Instance.RequestRecalculate();
        }

        // --- ПРОВЕРКА НА СГОРАНИЕ ---
        if (currentMagnitude > MaxForwardCurrent)
        {
            BurnOut();
        }
    }

    private void BurnOut()
    {
        IsBurnedOut = true;
        Resistance = ReverseResistance; // Разрыв цепи
        Debug.LogWarning($"Диод пробит/сгорел! Ток превысил {MaxForwardCurrent} А");
        // Активируем визуальный эффект
        if (BurnSparkEffect != null)
        {
            // Если партиклы настроены на Loop, они будут играть постоянно.
            // Если они должны выстрелить один раз, просто Play().
            BurnSparkEffect.Play();
        }
        // Можно добавить смену материала
        // GetComponent<MeshRenderer>().material.color = Color.black;

        KirchhoffManager.Instance.RequestRecalculate();
    }
}