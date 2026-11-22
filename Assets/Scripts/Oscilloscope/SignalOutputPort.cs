using UnityEngine;

public class SignalOutputPort : MonoBehaviour
{
    [Tooltip("Перетащи сюда объект с SignalGenerator (или узел цепи)")]
    [SerializeField] private MonoBehaviour signalSourceComponent;

    public ISignalSource Source => signalSourceComponent as ISignalSource;
}