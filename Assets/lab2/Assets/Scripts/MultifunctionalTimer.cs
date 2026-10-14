using UnityEngine;

/// <summary>
/// Симулирует многофункциональный таймер из реального опыта.
///
/// ВАЖНО: этот скрипт НЕ обращается к теоретической формуле периода.
/// Он только наблюдает за текущим углом платформы (TrifilarPlatformSimulator.CurrentAngleDeg)
/// и считает время N полных колебаний так же, как это делает реальный прибор:
/// фиксирует моменты прохождения платформой положения равновесия (угол = 0)
/// в одном и том же направлении (снизу вверх).
///
/// Между первым и (N+1)-м таким пересечением проходит ровно N полных периодов.
/// Измеренный период T_изм = t / N должен совпасть с теоретическим T_теор,
/// рассчитанным в TrifilarPlatformSimulator - это и есть проверка формулы.
/// </summary>
public class MultifunctionalTimer : MonoBehaviour
{
    [Tooltip("Симулятор платформы, период которой измеряется")]
    public TrifilarPlatformSimulator platform;

    [Tooltip("Сколько полных колебаний отмерять (в методичке - 10)")]
    public int oscillationsToCount = 10;

    /// <summary>Измеренное суммарное время N колебаний, с.</summary>
    public float MeasuredTime { get; private set; }

    /// <summary>Измеренный период T = t / N, с.</summary>
    public float MeasuredPeriod { get; private set; }

    /// <summary>Сколько полных периодов уже отмерено на данный момент.</summary>
    public int CompletedOscillations { get; private set; }

    public bool IsRunning { get; private set; }
    public bool IsFinished { get; private set; }

    private float _previousAngle;
    private float _startTime;
    private int _crossingCount;

    /// <summary>Начать измерение. Платформа должна уже колебаться.</summary>
    public void StartMeasurement()
    {
        if (platform == null)
        {
            Debug.LogWarning("MultifunctionalTimer: не назначен platform.");
            return;
        }

        _crossingCount = 0;
        CompletedOscillations = 0;
        IsRunning = true;
        IsFinished = false;
        MeasuredTime = 0f;
        MeasuredPeriod = 0f;
        _previousAngle = platform.CurrentAngleDeg;
    }

    public void ResetMeasurement()
    {
        IsRunning = false;
        IsFinished = false;
        MeasuredTime = 0f;
        MeasuredPeriod = 0f;
        CompletedOscillations = 0;
        _crossingCount = 0;
    }

    private void Update()
    {
        if (!IsRunning || platform == null) return;

        float angle = platform.CurrentAngleDeg;

        // Пересечение нуля снизу вверх = начало нового полного периода
        bool crossedUp = _previousAngle < 0f && angle >= 0f;

        if (crossedUp)
        {
            _crossingCount++;

            if (_crossingCount == 1)
            {
                // первое пересечение - точка старта отсчёта времени
                _startTime = Time.time;
            }
            else
            {
                CompletedOscillations = _crossingCount - 1;

                if (_crossingCount == oscillationsToCount + 1)
                {
                    MeasuredTime = Time.time - _startTime;
                    MeasuredPeriod = MeasuredTime / oscillationsToCount;
                    IsRunning = false;
                    IsFinished = true;
                }
            }
        }

        _previousAngle = angle;
    }
}
