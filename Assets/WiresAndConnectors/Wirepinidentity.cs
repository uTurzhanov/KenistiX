using UnityEngine;

/// <summary>
/// Решает проблему, при которой XR Interaction Toolkit перепривязывает (reparent)
/// объект "пина"/штекера (например, Pin1) к другому родителю во время захвата
/// или вставки в сокет — из-за чего GetComponentInParent&lt;CircuitComponent&gt;()
/// перестаёт находить владеющий CircuitComponent (провод, резистор и т.п.),
/// так как связь по Transform-иерархии на момент события уже разорвана.
///
/// Повесьте этот компонент на КАЖДЫЙ пин/штекер (Pin1, Pin2 и т.п.), который
/// вставляется в сокет, и вручную укажите в инспекторе owner —
/// CircuitComponent, к которому физически принадлежит этот пин
/// (например, WireComponent на корневом объекте провода).
///
/// SocketEventListener сначала проверяет наличие этого компонента,
/// и только если его нет — откатывается на старый поиск по иерархии
/// (для случаев, где реальной реструктуризации не требуется).
/// </summary>
public class WirePinIdentity : MonoBehaviour
{
    [Tooltip("CircuitComponent (например, WireComponent), к которому принадлежит этот пин. " +
             "Указывается ВРУЧНУЮ в инспекторе — не ищется автоматически по иерархии, " +
             "так как XR Toolkit может временно/постоянно менять родителя пина во время взаимодействия.")]
    public CircuitComponent Owner;

    private void Awake()
    {
        if (Owner == null)
        {
            Debug.LogWarning($"WirePinIdentity на объекте {gameObject.name}: поле Owner не назначено в инспекторе!");
        }
    }
}