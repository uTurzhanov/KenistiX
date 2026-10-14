using UnityEngine;
using System;

[Serializable]
public class ReactableValue<T>
{
    public event Action<T> OnValueChanged;

    [field: SerializeField] public T CurrentValue { get; private set; }

    public ReactableValue(T value)
    {
        CurrentValue = value;
    }

    public void SetValue(T value)
    {
        CurrentValue = value;
        OnValueChanged?.Invoke(value);
    }
}
