using System.Collections.Generic;
using UnityEngine;

public class CurrentSource : CircuitComponent
{
    [Header("Power Source Settings")]
    public float SourceVoltage = 12f;

    // Ссылки на конкретные контакты (если они разделены на разные сокеты)
    public CircuitComponent PlusTerminal;
    public CircuitComponent MinusTerminal;

    public void InitiateMinusWave()
    {
        HashSet<CircuitComponent> visited = new HashSet<CircuitComponent>();

        // Начинаем волну от минусовой клеммы (или от самого источника, если он единый объект)
        if (MinusTerminal != null)
        {
            MinusTerminal.PropagateMinusConnection(true, visited);
        }
    }

    // Сброс состояния всей цепи
    public void ResetMinusWave()
    {
        HashSet<CircuitComponent> visited = new HashSet<CircuitComponent>();
        if (MinusTerminal != null)
        {
            MinusTerminal.PropagateMinusConnection(false, visited);
        }
    }
}