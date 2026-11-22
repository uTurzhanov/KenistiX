using Mono.Cecil;
using System;
using UnityEngine;

[Serializable]
public class OscilloscopeChannel
{
    [Header("Железо")]
    //[SerializeField] private DummySignalGenerator source;

    [Header("Настройки")]
    public CouplingMode coupling = CouplingMode.DC;

    // Внутренние значения канала (управляются из Core через дельту)
    public ReactableValue<float> VoltsPerDiv = new(2f);
    public ReactableValue<float> YOffset = new(0f);
    private ISignalSource _source;

    public bool HasSource => _source != null;

    public float GetRawVoltage(float time)
    {
        if (_source == null) return 0f;
        return _source.GetVoltage(time);
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

    public void SetSource(ISignalSource source)
    {
        _source = source;
    }

    public void ClearSource()
    {
        _source = null;
    }
}