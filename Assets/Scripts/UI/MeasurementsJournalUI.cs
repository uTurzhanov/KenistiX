using System;
using UnityEngine;

public class MeasurementsJournalUI : MonoBehaviour
{
    [Header("Интерфейс")]
    [SerializeField] private Transform content;
    [SerializeField] private MeasurementRowUI rowTemplate;

    [Header("Тестовые показания")]
    [SerializeField] private float testVoltage = 12f;
    [SerializeField] private float testCurrent = 1.002f;

    private int measurementCount;
    private float sessionStartTime;

    private void Awake()
    {
        sessionStartTime = Time.time;

        if (rowTemplate == null)
        {
            Debug.LogError(
                "MeasurementsJournalUI: Row Template не назначен."
            );

            return;
        }

        rowTemplate.gameObject.SetActive(false);

        Debug.Log("MeasurementsJournalUI запущен.");
    }

    public void RecordTestMeasurement()
    {
        Debug.Log("Нажата кнопка фиксации показания.");

        AddMeasurement(testVoltage, testCurrent);
    }

    public void AddMeasurement(float voltage, float current)
    {
        if (content == null)
        {
            Debug.LogError(
                "MeasurementsJournalUI: Content не назначен."
            );

            return;
        }

        if (rowTemplate == null)
        {
            Debug.LogError(
                "MeasurementsJournalUI: Row Template не назначен."
            );

            return;
        }

        if (Mathf.Abs(current) < 0.0001f)
        {
            Debug.LogWarning(
                "Невозможно рассчитать сопротивление: ток равен нулю."
            );

            return;
        }

        measurementCount++;

        float resistance = voltage / current;
        string sessionTime = GetSessionTime();

        MeasurementRowUI newRow = Instantiate(
            rowTemplate,
            content,
            false
        );

        newRow.gameObject.SetActive(true);

        newRow.Setup(
            measurementCount,
            voltage,
            current,
            resistance,
            sessionTime
        );

        Debug.Log(
            $"Добавлен замер №{measurementCount}: " +
            $"U={voltage:F2} V, I={current:F3} A"
        );
    }

    private string GetSessionTime()
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(
            Time.time - sessionStartTime
        );

        int totalMinutes = (int)elapsed.TotalMinutes;

        return $"{totalMinutes:00}:{elapsed.Seconds:00}";
    }
}