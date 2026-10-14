using UnityEngine;

public class VRKnob : MonoBehaviour
{
    [SerializeField] private RotationInteractable _associatedRotationInteractable;

    public float Delta =>
        _associatedRotationInteractable == null ?
        0f :
        _associatedRotationInteractable.GetRotationDelta();
}