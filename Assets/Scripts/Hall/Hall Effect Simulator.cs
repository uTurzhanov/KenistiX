using UnityEngine;
using TMPro;
using Unity.VRTemplate;

public class HallSensorSimulator : MonoBehaviour
{

    [Header("UI References")]
    [Tooltip("Дисплей миллиампер (mA)")]
    public TextMeshProUGUI mADisplay;
    [Tooltip("Дисплей милливольт (mV)")]
    public TextMeshProUGUI mVDisplay;

    [Header("Hardware Controllers")]
    [Tooltip("Интерактивный Knob, возвращающий значение от 0 до 1")]
    public XRKnob currentKnob;

    [Header("Внешние данные (из других скриптов)")]
    [Tooltip("Позиция датчика от 0 до 12 см. Обновляйте это значение из вашего скрипта перемещения.")]
    // ЗДЕСЬ НУЖНО ЗНАЧЕНИЕ: Ваш скрипт должен менять эту переменную
    public float sensorPositionCm = 0f;

    [Header("Simulation Parameters")]
    [Tooltip("Максимальный ток соленоида в миллиамперах")]
    public float maxCurrentMA = 500f;
    [Tooltip("Текущая полярность (1 для прямого тока, -1 для обратного, 0 если цепь разорвана)")]
    public int polarity = 1;

    [Tooltip("Статическая ошибка прибора V0 в милливольтах (например, -0.6mV)")]
    public float v0Error = -0.6f;
    [Tooltip("Амплитуда случайного шума показаний (mV)")]
    public float noiseAmplitude = 0.1f;

    private float currentMA = 0f;
    private float displayedMV = 0f;

    // Массив аппроксимированных базовых значений напряжения (mV) при токе 500 mA
    private float[] baseVoltages = new float[]
    {
        18.0f, 30.8f, 43.4f, 51.4f, 54.8f, 56.6f, 57.0f, 57.4f, 57.5f, 57.6f, 57.7f, 57.4f, 57.2f
    };

    void Update()
    {
        CalculateCurrent();
        CalculateHallVoltage();
        UpdateDisplays();
    }

    private void CalculateCurrent()
    {
        // Если цепь разорвана (провода вытащены), polarity должна быть 0
        currentMA = (currentKnob.value * maxCurrentMA) * polarity;
    }

    private void CalculateHallVoltage()
    {
        // 1. Блокируем ошибки загрузки (защита от NaN и Infinity в первые кадры)
        if (float.IsNaN(sensorPositionCm) || float.IsInfinity(sensorPositionCm))
        {
            sensorPositionCm = 0f;
        }

        // 2. Гарантируем, что позиция строго в рамках 0-12
        float clampedPosition = Mathf.Clamp(sensorPositionCm, 0f, 12f);

        int lowerIndex = Mathf.FloorToInt(clampedPosition);
        int upperIndex = Mathf.CeilToInt(clampedPosition);

        // 3. ЖЕСТКАЯ ЗАЩИТА ИНДЕКСОВ: не даем им выйти за реальный размер массива
        int maxIndex = Mathf.Max(0, baseVoltages.Length - 1);
        lowerIndex = Mathf.Clamp(lowerIndex, 0, maxIndex);
        upperIndex = Mathf.Clamp(upperIndex, 0, maxIndex);

        // 4. Безопасная интерполяция
        float t = clampedPosition - lowerIndex;
        float baseVAt500mA = Mathf.Lerp(baseVoltages[lowerIndex], baseVoltages[upperIndex], t);

        // 5. Итоговый расчет напряжения
        float currentMultiplier = currentMA / 500f;
        float theoreticalVoltage = baseVAt500mA * currentMultiplier;

        float randomNoise = Random.Range(-noiseAmplitude, noiseAmplitude);
        displayedMV = theoreticalVoltage + v0Error + randomNoise;
    }

    private void UpdateDisplays()
    {
        if (mADisplay != null)
        {
            mADisplay.text = Mathf.RoundToInt(currentMA).ToString();
        }

        if (mVDisplay != null)
        {
            mVDisplay.text = displayedMV.ToString("F1");
        }
    }

    /// <summary>
    /// Метод для смены полярности из скрипта-контроллера цепи
    /// </summary>
    public void SetPolarity(int newPolarity)
    {
        polarity = newPolarity;
    }

    /// <summary>
    /// ВАШ СКРИПТ ПЕРЕМЕЩЕНИЯ ДОЛЖЕН ВЫЗЫВАТЬ ЭТОТ МЕТОД
    /// Передайте сюда значение от 0 до 12.
    /// </summary>
    public void UpdateSensorPosition(float newPositionCm)
    {
        sensorPositionCm = newPositionCm;
    }
}