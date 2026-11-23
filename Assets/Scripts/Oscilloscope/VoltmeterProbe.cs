using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class VoltmeterProbe : MonoBehaviour
{
    public CircuitTerminal CurrentTerminal { get; private set; }

    private XRGrabInteractable _grabInteractable;
    private CircuitTerminal _socketTerminal;
    private CircuitTerminal _triggerTerminal;

    private void Awake()
    {
        _grabInteractable = GetComponent<XRGrabInteractable>();
        _grabInteractable.selectEntered.AddListener(OnSocketEntered);
        _grabInteractable.selectExited.AddListener(OnSocketExited);
    }

    private void OnSocketEntered(SelectEnterEventArgs args)
    {
        if (args.interactorObject is MonoBehaviour socket)
        {
            _socketTerminal = socket.GetComponentInParent<CircuitTerminal>();
            UpdateActiveTerminal();
        }
    }

    private void OnSocketExited(SelectExitEventArgs args)
    {
        if (args.interactorObject is MonoBehaviour)
        {
            _socketTerminal = null;
            UpdateActiveTerminal();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var term = other.GetComponentInParent<CircuitTerminal>();
        if (term != null)
        {
            _triggerTerminal = term;
            UpdateActiveTerminal();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var term = other.GetComponentInParent<CircuitTerminal>();
        if (term != null && term == _triggerTerminal)
        {
            _triggerTerminal = null;
            UpdateActiveTerminal();
        }
    }

    private void UpdateActiveTerminal()
    {
        CurrentTerminal = _socketTerminal != null ? _socketTerminal : _triggerTerminal;
    }

    private void OnDestroy()
    {
        if (_grabInteractable != null)
        {
            _grabInteractable.selectEntered.RemoveListener(OnSocketEntered);
            _grabInteractable.selectExited.RemoveListener(OnSocketExited);
        }
    }
}