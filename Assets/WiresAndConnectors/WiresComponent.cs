using UnityEngine;

public class WireComponent : CircuitComponent
{
    private void Awake()
    {
        // Делаем сопротивление провода стремящимся к нулю. 
        // Не ставьте ровно 0, чтобы избежать ошибки деления на ноль 
        // при коротком замыкании (когда игрок соединит плюс и минус напрямую).
        Resistance = 0.001f;
    }

    // Вы можете переопределить UpdateElectricalData, если хотите, 
    // например, менять цвет провода в зависимости от силы тока
    public override void UpdateElectricalData(float newCurrent)
    {
        base.UpdateElectricalData(newCurrent);

        // Пример: если ток слишком большой - провод "сгорает" или краснеет
        if (Mathf.Abs(Current) > 5f)
        {
            Debug.LogWarning("Провод перегревается! Короткое замыкание!");
        }
    }
}