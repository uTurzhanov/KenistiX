using System;
using UnityEngine;

[Serializable]
public class OscilloscopeChannel
{
    [Header("Железо")]
    [SerializeField] private DummySignalGenerator source;

    [Header("Настройки")]
    public CouplingMode coupling = CouplingMode.DC;

    // Внутренние значения канала (управляются из Core через дельту)
    public ReactableValue<float> VoltsPerDiv = new(2f);
    public ReactableValue<float> YOffset = new(0f);

    public bool HasSource => source != null;

    public float GetRawVoltage(float time)
    {
        if (!HasSource || coupling == CouplingMode.GND) return 0f;
        return source.GetVoltage(time);
    }

    public float GetScreenYPosition(float rawVoltage, float frameAverageVoltage)
    {
        float finalVolts = rawVoltage;

        if (coupling == CouplingMode.AC)
        {
            finalVolts -= frameAverageVoltage;
        }

        return (finalVolts / VoltsPerDiv.CurrentValue) + YOffset.CurrentValue;
    }
}