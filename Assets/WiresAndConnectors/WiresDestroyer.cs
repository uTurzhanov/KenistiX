using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class WiresDestroyer : MonoBehaviour
{
    [ContextMenu("Delete All Wires")]
    public void DeleteAllWires()
    {
        WirePinIdentity[] allPins = FindObjectsOfType<WirePinIdentity>();

        // Шаг 1: Принудительно отключаем все пины от сокетов и рук
        foreach (WirePinIdentity pin in allPins)
        {
            if (pin != null)
            {
                XRBaseInteractable interactable = pin.GetComponent<XRBaseInteractable>();

                if (interactable != null && interactable.isSelected)
                {
                    // Явно приводим объект к интерфейсу IXRSelectInteractable (исправление ошибки CS0619)
                    interactable.interactionManager.CancelInteractableSelection((IXRSelectInteractable)interactable);
                }
            }
        }

        // Шаг 2: Теперь безопасно удаляем пины
        foreach (WirePinIdentity pin in allPins)
        {
            if (pin != null) Destroy(pin.gameObject);
        }

        // Шаг 3: Удаляем базовые провода
        WireComponent[] allWires = FindObjectsOfType<WireComponent>();
        foreach (WireComponent wire in allWires)
        {
            if (wire != null) Destroy(wire.gameObject);
        }
    }
}