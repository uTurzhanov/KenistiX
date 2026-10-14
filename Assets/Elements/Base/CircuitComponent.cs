using System.Collections.Generic;
using UnityEngine;

public abstract class CircuitComponent : MonoBehaviour
{
    public float Resistance = 0f;
    public float Voltage = 0f;
    public float Current = 0f;
    public bool IsMinusConnected = false;
    public float Potential = 0f;
    // Список физически соединенных компонентов
    public List<CircuitComponent> ConnectedComponents = new List<CircuitComponent>();

    // Добавление связи (вызывается из SocketEventListener)
    public void ConnectTo(CircuitComponent other)
    {
        if (other == null || other == this) return;

        // Проверяем ОБЕ стороны связи, а не только свою собственную.
        // Это защищает от дублирования связи независимо от того, кто первый
        // вызвал ConnectTo (this->other или other->this), что важно, так как
        // у каждого провода 2 разных пина/сокета, и события могут приходить
        // в любом порядке.
        bool alreadyConnected = ConnectedComponents.Contains(other) || other.ConnectedComponents.Contains(this);
        if (alreadyConnected) return;

        ConnectedComponents.Add(other);
        other.ConnectedComponents.Add(this); // Двусторонняя связь
    }

    // Удаление связи
    public void DisconnectFrom(CircuitComponent other)
    {
        if (ConnectedComponents.Contains(other))
        {
            ConnectedComponents.Remove(other);
            other.ConnectedComponents.Remove(this);
        }
    }

    // Волновая передача состояния IsMinusConnected (рекурсивный обход графа)
    public void PropagateMinusConnection(bool state, HashSet<CircuitComponent> visited)
    {
        // Защита от зацикливания (если цепь замкнута)
        if (visited.Contains(this)) return;
        visited.Add(this);

        IsMinusConnected = state;

        foreach (var component in ConnectedComponents)
        {
            component.PropagateMinusConnection(state, visited);
        }
    }

    // Метод для обновления физических величин
    public virtual void UpdateElectricalData(float newCurrent)
    {
        Current = newCurrent;
        Voltage = Current * Resistance; // U = I * R
    }
}