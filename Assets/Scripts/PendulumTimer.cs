using NUnit.Framework.Constraints;
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;

public class PendulumTimer : MonoBehaviour
{
    [Header("Настройки измерения")]
    public int targetOscillations = 10; // Количество полных колебаний
    public float addedError = 0.300f;

    [Header("UI элементы (опционально)")]
    public TextMeshProUGUI infoText;
    public TextMeshProUGUI timerHistory;
    public TextMeshProUGUI calculations;

    [Header("Для расчета теоретического значения")]
    public float pendulumLength = 5f; // Длина маятника в метрах
    private const float STANDARD_GRAVITY = 9.805f; // Фиксированное значение g

    private int halfOscillations = 0;
    private float startTime;
    private float totalTime;
    private bool isMeasuring = false;
    private bool hasStarted = false;

    private int doneMeasures = 0;
    private float[] measuresForCalc;

    void Start()
    {

        measuresForCalc = new float[10];
        // Рассчитываем теоретический период с фиксированным g = 9.81
        float theoreticalPeriod = 2 * Mathf.PI * Mathf.Sqrt(pendulumLength / STANDARD_GRAVITY);

        Debug.Log("=== ТЕОРЕТИЧЕСКИЕ РАСЧЕТЫ ===");
        Debug.Log($"Длина маятника: {pendulumLength} м");
        Debug.Log($"Ускорение свободного падения: {STANDARD_GRAVITY} м/с²");
        Debug.Log($"Теоретический период: T = 2π√(L/g) = {theoreticalPeriod:F4} сек");
        Debug.Log($"Ожидаемое время {targetOscillations} колебаний: {theoreticalPeriod * targetOscillations:F4} сек");
        Debug.Log("================================\n");
        /*

        if (infoText != null)
        {
            infoText.text = $"Теория: T = {theoreticalPeriod:F3} сек\n" +
                           $"Длина: {pendulumLength} м, g = {STANDARD_GRAVITY} м/с²\n" +
                           $"Ожидается {targetOscillations}T = {theoreticalPeriod * targetOscillations:F3} сек";
        }
        */
    }

    // Вызывается когда маятник входит в триггер
    void OnTriggerEnter(Collider other)
    {
        if (other.tag != "Pendulum")
        {
            return;
        }

        halfOscillations++;
        if (isMeasuring)
            infoText.text = (targetOscillations - GetCurrentOscillations()).ToString();

        // Первое прохождение - начинаем отсчет
        if (halfOscillations == 1 && !hasStarted)
        {
            startTime = Time.time;
            isMeasuring = true;
            hasStarted = true;
            Debug.Log($"▶ Начало измерения (прохождение #{halfOscillations})...");
        }
        // Каждое второе прохождение = одно полное колебание
        else if (halfOscillations % 2 == 0 && isMeasuring)
        {
            int completedOscillations = halfOscillations / 2;
            Debug.Log($"  ✓ Прохождение #{halfOscillations} (полных колебаний: {completedOscillations})");

            // Закончили измерение нужного количества колебаний
            if (completedOscillations == targetOscillations)
            {
                totalTime = Time.time - startTime;
                totalTime = AddError(totalTime);
                isMeasuring = false;

                float averagePeriod = totalTime / targetOscillations;

                // Рассчитываем теоретическое значение
                float theoreticalPeriod = 2 * Mathf.PI * Mathf.Sqrt(pendulumLength / STANDARD_GRAVITY);
                float error = Mathf.Abs(averagePeriod - theoreticalPeriod) / theoreticalPeriod * 100;

                Debug.Log("\n═══════════════════════════════════════");
                Debug.Log($"✓ ИЗМЕРЕНИЕ ЗАВЕРШЕНО!");
                Debug.Log($"Время {targetOscillations} колебаний: {totalTime:F4} сек");
                Debug.Log($"Среднее время одного колебания: {averagePeriod:F4} сек");

                if (doneMeasures < 9)
                {
                    if (doneMeasures == 0)
                        timerHistory.text = "";
                    measuresForCalc[doneMeasures++] = averagePeriod;
                    timerHistory.text += $"t {doneMeasures} = {averagePeriod:F3} сек\n";
                }
                else
                {
                    measuresForCalc[doneMeasures++] = averagePeriod;
                    timerHistory.text += $"t {doneMeasures} = {averagePeriod:F3} сек\n";



                    StatisticsResult stats = CalculateStatistics(measuresForCalc);

                    calculations.text = ($"Среднее: {stats.Mean:F4}\n Дисперсия: {stats.Variance:F6}\n Сигма: {stats.Sigma:F4}\n Абсолютная ошибка: {stats.AbsoluteError:F4}\n Медиана: {stats.Median:F4}\n"  );

                }


                Debug.Log($"Теоретическое значение: {theoreticalPeriod:F4} сек");
                Debug.Log($"Погрешность: {error:F2}%");
                Debug.Log("═══════════════════════════════════════\n");

                if (infoText != null)
                {
                    infoText.text = $"{averagePeriod:F3}";
                }
            }
        }
    }

    // Метод для сброса измерений
    public void ResetMeasurement()
    {
        halfOscillations = 0;
        isMeasuring = false;
        hasStarted = false;
        totalTime = 0;
        Debug.Log("⟲ Измерение сброшено");


    }

    public int GetCurrentOscillations()
    {
        return halfOscillations / 2;
    }


    public void ContinueMeasure()
    {
        ResetMeasurement();
        isMeasuring = true;


    }

    private float AddError(float inp)
    {



        float error = UnityEngine.Random.Range(-addedError, addedError);

        Debug.Log($"Добавленная ошибка {error}");

        return inp + error;

    }

    public struct StatisticsResult
    {
        public float Mean;              // Среднее значение
        public float Variance;          // Дисперсия
        public float Sigma;             // Стандартное отклонение (сигма)
        public float AbsoluteError;     // Средняя абсолютная ошибка
        public float Median;            // Медиана
        public float Min;               // Минимальное значение
        public float Max;               // Максимальное значение
        public float Range;             // Размах (Max - Min)
    }


    public static StatisticsResult CalculateStatistics(float[] values)
    {
        if (values == null || values.Length == 0)
            throw new ArgumentException("Массив не может быть пустым");

        int n = values.Length;
        StatisticsResult result = new StatisticsResult();

        // 1. Среднее значение (Mean)
        result.Mean = values.Average();

        // 2. Дисперсия (Variance) - выборочная дисперсия (делим на n-1)
        float sumSquaredDiffs = 0;
        foreach (float value in values)
        {
            float diff = value - result.Mean;
            sumSquaredDiffs += diff * diff;
        }
        result.Variance = sumSquaredDiffs / (n - 1);

        // 3. Сигма / Стандартное отклонение (Sigma)
        result.Sigma = (float)Math.Sqrt(result.Variance);

        // 4. Средняя абсолютная ошибка (Mean Absolute Error)
        float sumAbsoluteDiffs = 0;
        foreach (float value in values)
        {
            sumAbsoluteDiffs += Math.Abs(value - result.Mean);
        }
        result.AbsoluteError = sumAbsoluteDiffs / n;

        // 5. Минимум и максимум
        result.Min = values.Min();
        result.Max = values.Max();

        // 6. Размах (Range)
        result.Range = result.Max - result.Min;

        // 7. Медиана (Median)
        float[] sorted = values.OrderBy(v => v).ToArray();
        if (n % 2 == 0)
        {
            result.Median = (sorted[n / 2 - 1] + sorted[n / 2]) / 2f;
        }
        else
        {
            result.Median = sorted[n / 2];
        }

        return result;
    }


}