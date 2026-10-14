using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSocketInteractor))]
public class SocketEventListener : MonoBehaviour
{
    private XRSocketInteractor _socketInteractor;
    private CircuitComponent _myCircuitComponent;

    // ДОБАВЛЕНО: Ссылка на компонент, который сейчас физически вставлен в этот сокет
    public CircuitComponent ConnectedComponent { get; private set; }

    private void Awake()
    {
        _socketInteractor = GetComponent<XRSocketInteractor>();
        _myCircuitComponent = ResolveCircuitComponent(gameObject);

        if (_myCircuitComponent == null)
        {
            Debug.LogWarning($"SocketEventListener на объекте {gameObject.name} не смог найти CircuitComponent у себя или родителя!");
        }
    }

    private void OnEnable()
    {
        _socketInteractor.selectEntered.AddListener(OnObjectSnapped);
        _socketInteractor.selectExited.AddListener(OnObjectRemoved);
    }

    private void OnDisable()
    {
        _socketInteractor.selectEntered.RemoveListener(OnObjectSnapped);
        _socketInteractor.selectExited.RemoveListener(OnObjectRemoved);
    }

    private CircuitComponent ResolveCircuitComponent(GameObject obj)
    {
        WirePinIdentity identity = obj.GetComponent<WirePinIdentity>();
        if (identity != null && identity.Owner != null) return identity.Owner;

        identity = obj.GetComponentInParent<WirePinIdentity>();
        if (identity != null && identity.Owner != null) return identity.Owner;

        return obj.GetComponentInParent<CircuitComponent>();
    }

    private void OnObjectSnapped(SelectEnterEventArgs args)
    {
        GameObject snappedObject = args.interactableObject.transform.gameObject;
        CircuitComponent snappedCircuitComp = ResolveCircuitComponent(snappedObject);

        if (_myCircuitComponent != null && snappedCircuitComp != null)
        {
            // ЗАПОМИНАЕМ, кто зашел в сокет
            ConnectedComponent = snappedCircuitComp;

            _myCircuitComponent.ConnectTo(snappedCircuitComp);
            KirchhoffManager.Instance.RecalculateCircuit();
        }
        else
        {
            Debug.LogWarning($"[Socket] Не удалось соединить '{gameObject.name}'...");
        }
    }

    private void OnObjectRemoved(SelectExitEventArgs args)
    {
        GameObject removedObject = args.interactableObject.transform.gameObject;
        CircuitComponent removedCircuitComp = ResolveCircuitComponent(removedObject);

        if (_myCircuitComponent != null && removedCircuitComp != null)
        {
            _myCircuitComponent.DisconnectFrom(removedCircuitComp);

            // ОЧИЩАЕМ ссылку при отключении
            ConnectedComponent = null;

            KirchhoffManager.Instance.RecalculateCircuit();
        }
    }
}