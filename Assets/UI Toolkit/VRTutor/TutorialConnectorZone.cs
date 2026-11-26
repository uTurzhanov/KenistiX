using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class TutorialConnectorZone : MonoBehaviour
{
    private VRTutorialController _controller;
    private bool _isOccupied = false;

    public void Init(VRTutorialController controller)
    {
        _controller = controller;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isOccupied || _controller == null) return;

        // Проверяем, что в коннектор попал конец провода (по компоненту XRGrabInteractable или имени/тегу)
        bool isWireEnd = other.GetComponentInParent<XRGrabInteractable>() != null
                         || other.name.ToLower().Contains("wire")
                         || other.name.ToLower().Contains("plug")
                         || other.name.ToLower().Contains("end");

        if (isWireEnd)
        {
            _isOccupied = true;
            _controller.NotifyWireEndConnected();
        }
    }
}