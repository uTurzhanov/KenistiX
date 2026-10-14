using UnityEngine;

// Наследуемся от нашего базового класса
public class Terminal : CircuitComponent
{
    // Здесь пока может быть пусто.
    // Класс автоматически получает все переменные (Voltage, Current) 
    // и методы (ConnectTo, PropagateMinusConnection) из базового класса.
    // Но так как он больше не abstract, Unity разрешит повесить его на объект.
}