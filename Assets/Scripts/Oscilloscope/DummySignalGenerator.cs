using UnityEngine;

public class DummySignalGenerator : MonoBehaviour
{
    [Header("Настройки сигнала")]
    public float amplitude = 5f;
    public float frequency = 50f;
    public float dcOffset = 0f;

    public float GetVoltage(float time)
    {
        return dcOffset + amplitude * (Mathf.Sin(2f * Mathf.PI * frequency * time));
    }
}