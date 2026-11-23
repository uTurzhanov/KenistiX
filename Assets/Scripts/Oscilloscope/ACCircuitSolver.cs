using System;
using UnityEngine;

public enum CircuitTopology
{
    Open,       // Цепь не собрана или собрана с ошибкой
    Series,     // Последовательный контур (Упр. 1 и Упр. 2.1)
    Parallel    // Параллельный контур (Упр. 2.2)
}

public class ACCircuitSolver : MonoBehaviour
{
    [Header("Источник питания")]
    [SerializeField] private SignalGenerator generator;

    [Header("Параметры элементов цепи")]
    [Tooltip("Активное сопротивление магазина R (Ом)")]
    public float resistanceR = 89f;

    [Tooltip("Собственное сопротивление катушки RL из методички (Ом)")]
    public float coilResistanceRL = 11f;

    [Tooltip("Индуктивность катушки L (Гн). 0.08 Гн = 80 мГн")]
    public float inductanceL = 0.08f;

    [Tooltip("Емкость конденсатора C (Ф). 2.5e-6 Ф = 2.5 мкФ; 0.33e-6 Ф = 0.33 мкФ")]
    public float capacitanceC = 2.5e-6f;

    [Header("Текущая топология (будет задаваться менеджером проводов)")]
    public CircuitTopology currentTopology = CircuitTopology.Series;

    // Действующие (RMS) значения для мультиметров
    public float CurrentRms { get; private set; }         // А (для амперметра умножаем на 1000 -> мА)
    public float TotalVoltageRms { get; private set; }    // В (U_изм)
    public float ResistorVoltageRms { get; private set; } // В (U_R)
    public float CapacitorVoltageRms { get; private set; }// В (U_C)
    public float InductorVoltageRms { get; private set; } // В (U_L)
    public float PhaseShiftRad { get; private set; }      // Сдвиг фаз φ (рад)

    // Узлы для подключения щупов осциллографа (ISignalSource)
    public CircuitNodeSource TotalNode { get; private set; }
    public CircuitNodeSource ResistorNode { get; private set; }
    public CircuitNodeSource CapacitorNode { get; private set; }
    public CircuitNodeSource InductorNode { get; private set; }

    private void Awake()
    {
        TotalNode = new CircuitNodeSource(this, NodeType.Total);
        ResistorNode = new CircuitNodeSource(this, NodeType.Resistor);
        CapacitorNode = new CircuitNodeSource(this, NodeType.Capacitor);
        InductorNode = new CircuitNodeSource(this, NodeType.Inductor);
    }

    private void Update()
    {
        RecalculateCircuit();
    }

    public void RecalculateCircuit()
    {
        // Если генератор выключен, цепь разомкнута или выбран не синус — тока нет
        if (generator == null || !generator.IsPowered || !generator.IsOutputEnabled ||
            currentTopology == CircuitTopology.Open || generator.CurrentWaveform != WaveformType.Sine)
        {
            ResetValues();
            return;
        }

        float f = generator.Frequency;
        float uRms = generator.RmsVoltage;
        float omega = 2f * Mathf.PI * f;

        float xL = omega * inductanceL;
        float xC = 1f / (omega * capacitanceC);

        TotalVoltageRms = uRms;

        if (currentTopology == CircuitTopology.Series)
        {
            float rTotal = resistanceR + coilResistanceRL;
            float xTotal = xL - xC;
            float impedanceZ = Mathf.Sqrt(rTotal * rTotal + xTotal * xTotal);

            CurrentRms = uRms / impedanceZ;
            PhaseShiftRad = Mathf.Atan2(xTotal, rTotal);

            ResistorVoltageRms = CurrentRms * resistanceR;
            CapacitorVoltageRms = CurrentRms * xC;
            InductorVoltageRms = CurrentRms * Mathf.Sqrt(coilResistanceRL * coilResistanceRL + xL * xL);
        }
        else if (currentTopology == CircuitTopology.Parallel)
        {
            // Ветвь C параллельна ветви (RL + L), резистор R исключен
            float denom = coilResistanceRL * coilResistanceRL + xL * xL;
            float gTotal = coilResistanceRL / denom;
            float bTotal = (1f / xC) - (xL / denom);
            float admittanceY = Mathf.Sqrt(gTotal * gTotal + bTotal * bTotal);

            CurrentRms = uRms * admittanceY;
            PhaseShiftRad = Mathf.Atan2(-bTotal, gTotal);

            ResistorVoltageRms = 0f;
            CapacitorVoltageRms = uRms;
            InductorVoltageRms = uRms;
        }
    }

    private void ResetValues()
    {
        CurrentRms = 0f;
        TotalVoltageRms = 0f;
        ResistorVoltageRms = 0f;
        CapacitorVoltageRms = 0f;
        InductorVoltageRms = 0f;
        PhaseShiftRad = 0f;
    }

    /// <summary>
    /// Мгновенное напряжение на выбранном участке для осциллографа (с учетом фазы)
    /// </summary>
    public float GetNodeInstantVoltage(NodeType node, float time)
    {
        if (CurrentRms <= 0f && TotalVoltageRms <= 0f) return 0f;

        float omegaT = 2f * Mathf.PI * generator.Frequency * time;
        float sqrt2 = Mathf.Sqrt(2f);

        switch (node)
        {
            case NodeType.Total:
                return (TotalVoltageRms * sqrt2) * Mathf.Sin(omegaT);

            case NodeType.Resistor:
                if (currentTopology != CircuitTopology.Series) return 0f;
                // Ток отстает от входного напряжения на PhaseShiftRad; напряжение на R в фазе с током
                return (ResistorVoltageRms * sqrt2) * Mathf.Sin(omegaT - PhaseShiftRad);

            case NodeType.Capacitor:
                if (currentTopology == CircuitTopology.Parallel)
                    return (CapacitorVoltageRms * sqrt2) * Mathf.Sin(omegaT);
                // Напряжение на C отстает от тока на π/2
                return (CapacitorVoltageRms * sqrt2) * Mathf.Sin(omegaT - PhaseShiftRad - Mathf.PI / 2f);

            case NodeType.Inductor:
                if (currentTopology == CircuitTopology.Parallel)
                    return (InductorVoltageRms * sqrt2) * Mathf.Sin(omegaT);
                // Напряжение на катушке (с учетом RL) опережает ток на угол atan2(XL, RL)
                float omega = 2f * Mathf.PI * generator.Frequency;
                float coilPhase = Mathf.Atan2(omega * inductanceL, coilResistanceRL);
                return (InductorVoltageRms * sqrt2) * Mathf.Sin(omegaT - PhaseShiftRad + coilPhase);

            default:
                return 0f;
        }
    }
}

public enum NodeType { Total, Resistor, Capacitor, Inductor }

public class CircuitNodeSource : ISignalSource
{
    private readonly ACCircuitSolver _solver;
    private readonly NodeType _nodeType;

    public CircuitNodeSource(ACCircuitSolver solver, NodeType nodeType)
    {
        _solver = solver;
        _nodeType = nodeType;
    }

    public float GetVoltage(float time) => _solver.GetNodeInstantVoltage(_nodeType, time);
}