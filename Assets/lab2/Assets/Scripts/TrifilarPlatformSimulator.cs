using UnityEngine;

/// <summary>
/// Симулирует крутильные колебания платформы трифилярного подвеса,
/// в том числе с телом (диском/кольцом/цилиндром(ами)), положенным на неё.
///
/// Общая рабочая формула (период системы платформа+тело):
///
///     T = 2*pi * sqrt( J_total * l / (m_total * g * R * r) )
///
/// где:
///   J_total = J_platform + J_body   (моменты инерции относительно
///             вертикальной оси платформы, J_platform = 0.5*platformMass*R^2)
///   m_total = platformMass + bodyMass
///   R - радиус платформы, м
///   l - длина нитей подвеса, м
///   r - радиус верхнего (неподвижного) диска, м
///   g - ускорение свободного падения, м/с^2
///
/// Вклад тела (bodyMass, bodyMomentOfInertia) передаётся снаружи через
/// SetBodyContribution(...) - этим занимается BodyConfigController.
/// Когда тела нет (bodyMass = bodyMomentOfInertia = 0), формула сводится
/// к T = pi*sqrt(2*R*l/(g*r)) - масса платформы там действительно
/// сокращается, как и раньше.
///
/// Скрипт пересчитывает T при изменении параметров и поворачивает
/// объект вокруг локальной оси Y по закону гармонических колебаний
/// phi(t) = amplitude * sin(omega0 * t), что соответствует решению (4)
/// дифференциального уравнения крутильных колебаний.
/// </summary>
public class TrifilarPlatformSimulator : MonoBehaviour
{
    [Header("Параметры подвеса (задаются перед запуском)")]
    [Tooltip("Радиус платформы, м")]
    public float R = 0.15f;

    [Tooltip("Длина нитей подвеса, м")]
    public float l = 0.8f;

    [Tooltip("Радиус верхнего (неподвижного) диска, м")]
    public float r = 0.05f;

    [Tooltip("Ускорение свободного падения, м/с^2")]
    public float g = 9.81f;

    [Tooltip("Масса самой платформы, кг. ВАЖНО: раньше этого поля не было, " +
             "т.к. масса сокращалась для пустой платформы. Теперь, когда на " +
             "платформу можно положить тело, масса платформы снова нужна - " +
             "обязательно задайте реальное значение, иначе расчёт будет неверным.")]
    public float platformMass = 0.5f;

    [Header("Параметры колебаний")]
    [Tooltip("Максимальный угол отклонения платформы от положения равновесия, градусы")]
    public float amplitudeDeg = 15f;

    [Tooltip("Коэффициент затухания амплитуды, 1/с. 0 = идеальная теория без потерь на трение")]
    public float dampingPerSecond = 0f;

    /// <summary>Теоретический период колебаний системы платформа+тело, с.</summary>
    public float Period { get; private set; }

    /// <summary>Угловая частота колебаний, рад/с.</summary>
    public float Omega0 { get; private set; }

    /// <summary>Текущий угол поворота платформы относительно положения равновесия, градусы.</summary>
    public float CurrentAngleDeg { get; private set; }

    /// <summary>Истинно, если колебания в данный момент происходят.</summary>
    public bool IsOscillating { get; private set; }

    /// <summary>Момент инерции самой платформы относительно её вертикальной оси, кг*м^2.</summary>
    public float PlatformMomentOfInertia => 0.5f * platformMass * R * R;

    private float _bodyMass;
    private float _bodyMomentOfInertia;

    private float _startTime;
    private float _initialAmplitudeDeg;

    private void Awake()
    {
        Recalculate();
    }

    /// <summary>
    /// Задать вклад тела, лежащего на платформе (вызывается из BodyConfigController
    /// каждый раз, когда меняется выбранное тело или расстояние от оси).
    /// </summary>
    /// <param name="bodyMass">Суммарная масса тела(тел), кг</param>
    /// <param name="bodyMomentOfInertia">Суммарный момент инерции тела(тел) относительно
    /// вертикальной оси платформы (с учётом теоремы Штейнера, если тело смещено), кг*м^2</param>
    public void SetBodyContribution(float bodyMass, float bodyMomentOfInertia)
    {
        _bodyMass = bodyMass;
        _bodyMomentOfInertia = bodyMomentOfInertia;
        Recalculate();
    }

    /// <summary>
    /// Пересчитать теоретический период по текущим значениям R, l, r, g,
    /// массе платформы и текущему вкладу тела.
    /// </summary>
    public void Recalculate()
    {
        if (r <= 0f || g <= 0f || l <= 0f || R <= 0f || platformMass <= 0f)
        {
            Debug.LogWarning("TrifilarPlatformSimulator: R, l, r, g, platformMass должны быть положительными числами.");
            return;
        }

        float jTotal = PlatformMomentOfInertia + _bodyMomentOfInertia;
        float mTotal = platformMass + _bodyMass;

        Period = 2f * Mathf.PI * Mathf.Sqrt(jTotal * l / (mTotal * g * R * r));
        Omega0 = 2f * Mathf.PI / Period;
    }

    /// <summary>
    /// Запустить колебания - аналог поворота верхнего диска в реальном опыте,
    /// которым сообщается крутильный импульс платформе.
    /// </summary>
    public void StartOscillation()
    {
        Recalculate();
        _startTime = Time.time;
        _initialAmplitudeDeg = amplitudeDeg;
        IsOscillating = true;
    }

    /// <summary>Остановить колебания и вернуть платформу в положение равновесия.</summary>
    public void StopOscillation()
    {
        IsOscillating = false;
        CurrentAngleDeg = 0f;
        ApplyRotation();
    }

    private void Update()
    {
        if (!IsOscillating) return;

        float t = Time.time - _startTime;

        // Затухающая амплитуда. При dampingPerSecond = 0 совпадает с идеальной
        // теорией (E = const), используемой при выводе рабочей формулы (7).
        float amplitude = _initialAmplitudeDeg * Mathf.Exp(-dampingPerSecond * t);

        CurrentAngleDeg = amplitude * Mathf.Sin(Omega0 * t);
        ApplyRotation();
    }

    private void ApplyRotation()
    {
        transform.localRotation = Quaternion.Euler(0f, CurrentAngleDeg, 0f);
    }
}
