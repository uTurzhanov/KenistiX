using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class CircuitPatchWire : MonoBehaviour
{
    [SerializeField] private XRGrabInteractable plugA;
    [SerializeField] private XRGrabInteractable plugB;

    public CircuitTerminal TerminalA { get; private set; }
    public CircuitTerminal TerminalB { get; private set; }
    public bool IsConnectedBothEnds => TerminalA != null && TerminalB != null;

    private void Awake()
    {
        if (plugA != null)
        {
            plugA.selectEntered.AddListener(OnPlugAEntered);
            plugA.selectExited.AddListener(OnPlugAExited);
        }
        if (plugB != null)
        {
            plugB.selectEntered.AddListener(OnPlugBEntered);
            plugB.selectExited.AddListener(OnPlugBExited);
        }
    }

    private void Start()
    {
        if (CircuitGraphManager.Instance != null)
            CircuitGraphManager.Instance.RegisterWire(this);
    }

    private void OnPlugAEntered(SelectEnterEventArgs args)
    {
        TerminalA = FindTerminal(args.interactorObject as MonoBehaviour);
        NotifyGraphChanged();
    }

    private void OnPlugAExited(SelectExitEventArgs args)
    {
        TerminalA = null;
        NotifyGraphChanged();
    }

    private void OnPlugBEntered(SelectEnterEventArgs args)
    {
        TerminalB = FindTerminal(args.interactorObject as MonoBehaviour);
        NotifyGraphChanged();
    }

    private void OnPlugBExited(SelectExitEventArgs args)
    {
        TerminalB = null;
        NotifyGraphChanged();
    }

    private CircuitTerminal FindTerminal(MonoBehaviour socket)
    {
        if (socket == null) return null;
        // Ищем CircuitTerminal на самом сокете или на его родительском объекте-клемме
        return socket.GetComponentInParent<CircuitTerminal>();
    }

    private void NotifyGraphChanged()
    {
        if (CircuitGraphManager.Instance != null)
            CircuitGraphManager.Instance.EvaluateTopology();
    }

    private void OnDestroy()
    {
        if (CircuitGraphManager.Instance != null)
            CircuitGraphManager.Instance.UnregisterWire(this);

        if (plugA != null)
        {
            plugA.selectEntered.RemoveListener(OnPlugAEntered);
            plugA.selectExited.RemoveListener(OnPlugAExited);
        }
        if (plugB != null)
        {
            plugB.selectEntered.RemoveListener(OnPlugBEntered);
            plugB.selectExited.RemoveListener(OnPlugBExited);
        }
    }
}