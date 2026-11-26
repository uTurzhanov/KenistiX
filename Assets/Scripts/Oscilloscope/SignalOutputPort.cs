using UnityEngine;

public class SignalOutputPort : MonoBehaviour
{
    public enum PortSourceType
    {
        DirectComponent, // Для прямого подключения к SignalGenerator
        CircuitNode      // Для подключения к узлам решателя ACCircuitSolver
    }

    [SerializeField] private PortSourceType sourceType = PortSourceType.CircuitNode;

    [Header("Если выбран CircuitNode")]
    [SerializeField] private ACCircuitSolver solver;
    [SerializeField] private NodeType nodeType = NodeType.Total;

    [Header("Если выбран DirectComponent")]
    [SerializeField] private MonoBehaviour signalSourceComponent;

    public ISignalSource Source
    {
        get
        {
            if (sourceType == PortSourceType.CircuitNode)
            {
                if (solver == null) return null;
                return nodeType switch
                {
                    NodeType.Total => solver.TotalNode,
                    NodeType.Resistor => solver.ResistorNode,
                    NodeType.Capacitor => solver.CapacitorNode,
                    NodeType.Inductor => solver.InductorNode,
                    _ => null
                };
            }

            return signalSourceComponent as ISignalSource;
        }
    }
}