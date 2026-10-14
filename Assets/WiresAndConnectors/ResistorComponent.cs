using UnityEngine;

public class ResistorComponent : CircuitComponent
{
    [Header("Resistor Settings")]
    public float MaxPowerDissipation = 10f; // Максимальная мощность в Ваттах (например, четвертьваттный резистор)
    public bool IsBurnedOut = false;
    public ParticleSystem BurnSparkEffect;

    private void Awake()
    {
        // Устанавливаем дефолтное сопротивление, если забыли указать в инспекторе
        if (Resistance <= 0f) Resistance = 100f;
        BurnSparkEffect = GetComponentInChildren<ParticleSystem>();
    }

    public override void UpdateElectricalData(float newCurrent)
    {
        if (IsBurnedOut)
        {
            // Если сгорел, ток через него не идет (обрыв цепи)
            // KirchhoffManager не должен был бы его включить, но для подстраховки:
            base.UpdateElectricalData(0f);
            return;
        }

        base.UpdateElectricalData(newCurrent);

        // Физика: P = I^2 * R (Мощность = Ток в квадрате * Сопротивление)
        float currentPower = (Current * Current) * Resistance;

        if (currentPower > MaxPowerDissipation)
        {
            BurnOut();
        }
    }

    private void BurnOut()
    {
        IsBurnedOut = true;
        Resistance = 9999999f; // Делаем сопротивление бесконечным (разрыв цепи)
        Debug.LogWarning($"Резистор сгорел! Мощность превысила {MaxPowerDissipation} Вт");

        // Здесь можно добавить смену материала на черный/обугленный
        // GetComponent<MeshRenderer>().material.color = Color.black;
        if (BurnSparkEffect != null)
        {
            // Если партиклы настроены на Loop, они будут играть постоянно.
            // Если они должны выстрелить один раз, просто Play().
            BurnSparkEffect.Play();
        }
        // ВАЖНО: мы можем находиться ВНУТРИ стека вызовов KirchhoffManager.RecalculateCircuit()
        // прямо сейчас (BurnOut -> UpdateElectricalData -> вызывается из решателя цепи).
        // Прямой вызов RecalculateCircuit() здесь привёл бы к реентерабельному вызову:
        // внешний расчёт продолжил бы работать с уже сброшенными/пересчитанными данными
        // (классическая ошибка "modify collection while iterating" + испорченный результат).
        // Поэтому откладываем пересчёт до LateUpdate текущего кадра.
        KirchhoffManager.Instance.RequestRecalculate();
    }
}