using UnityEngine;

public class DiodComponent : CircuitComponent
{
    [Header("Diode Settings")]
    public float ForwardVoltageDrop = 0.7f; 
    public float MaxForwardCurrent = 1f; 
    public float ForwardResistance = 0.5f; 
    public float ReverseResistance = 9999999f; 

    public bool IsBurnedOut = false;
    public ParticleSystem BurnSparkEffect;

    private void Awake()
    {
        // ВАЖНО: Используем Awake, а не Start, чтобы сопротивление 
        // инициализировалось ДО первого расчета KirchhoffManager
        if (!IsBurnedOut) 
        {
            Resistance = ForwardResistance;
        }
        BurnSparkEffect = GetComponentInChildren<ParticleSystem>();
    }

    public override void UpdateElectricalData(float newCurrent)
    {
        if (IsBurnedOut)
        {
            Resistance = ReverseResistance;
            base.UpdateElectricalData(0f);
            return;
        }

        // Порог, чтобы избежать деления на ноль и дрожания при микротоках
        float threshold = 0.000001f; 
        float currentMagnitude = Mathf.Abs(newCurrent);

        // --- ЛОГИКА ДИНАМИЧЕСКОГО СОПРОТИВЛЕНИЯ (SPICE-модель) ---
        if (newCurrent < -threshold)
        {
            // Ток пытается течь в обратном направлении (от Катода к Аноду). Закрываем диод.
            if (Resistance != ReverseResistance)
            {
                Resistance = ReverseResistance;
                KirchhoffManager.Instance.RequestRecalculate();
                return;
            }
        }
        else if (newCurrent > threshold)
        {
            // Ток течет в прямом направлении. Диод открыт.
            // Нам нужно, чтобы падение потенциала было ~0.7В. 
            // Высчитываем, какое сопротивление для этого нужно прямо сейчас:
            float targetVoltage = ForwardVoltageDrop + (currentMagnitude * ForwardResistance);
            float neededResistance = targetVoltage / currentMagnitude;

            // Защита: если напряжение источника слишком слабое (например 0.5В), 
            // требуемое сопротивление улетит в бесконечность. Ограничиваем его:
            if (neededResistance > ReverseResistance) 
            {
                neededResistance = ReverseResistance;
            }

            // Если текущее сопротивление отличается от идеального больше чем на 2%, 
            // корректируем его и просим решатель пересчитать потенциалы цепи.
            if (Mathf.Abs(Resistance - neededResistance) > (neededResistance * 0.02f))
            {
                Resistance = neededResistance;
                KirchhoffManager.Instance.RequestRecalculate();
                
                // Прерываем логику, ждем пока решатель прогонит цепь с новым сопротивлением
                return; 
            }
        }

        // --- СОХРАНЕНИЕ ДАННЫХ ---
        // Базовый класс сделает Voltage = Current * Resistance. 
        // Так как мы хитро подогнали Resistance, Voltage теперь будет строго ~0.7V!
        // А самое главное — потенциалы (Potential) до и после диода будут иметь разницу 0.7V.
        base.UpdateElectricalData(newCurrent);

        // Проверка на сгорание
        if (currentMagnitude > MaxForwardCurrent)
        {
            BurnOut();
        }
    }

    private void BurnOut()
    {
        IsBurnedOut = true;
        Resistance = ReverseResistance;
        Debug.LogWarning("Диод пробит/сгорел!");
        if (BurnSparkEffect != null)
        {
            // Если партиклы настроены на Loop, они будут играть постоянно.
            // Если они должны выстрелить один раз, просто Play().
            BurnSparkEffect.Play();
        }
        KirchhoffManager.Instance.RequestRecalculate();
    }
}