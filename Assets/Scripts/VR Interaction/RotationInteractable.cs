using UnityEngine;

[RequireComponent(typeof(HingeJoint))]
public class RotationInteractable : MonoBehaviour
{
    private Transform _referenceParent;
    private Vector3 _initialLocalUp;
    private Vector3 _initialLocalForward;

    private float _lastAngle = 0f;

    private void Awake()
    {
        _referenceParent = transform.parent;

        if (_referenceParent != null)
        {
            _initialLocalUp = _referenceParent.InverseTransformDirection(transform.up);
            _initialLocalForward = _referenceParent.InverseTransformDirection(transform.forward);
        }
        else
        {
            _initialLocalUp = transform.up;
            _initialLocalForward = transform.forward;
        }

        // »нициализируем стартовый угол
        _lastAngle = GetCurrentAngle();
    }

    // ¬озвращает, на сколько градусов ручка повернулась с прошлого вызова
    public float GetRotationDelta()
    {
        float currentAngle = GetCurrentAngle();
        float delta = currentAngle - _lastAngle;

        // «ащита от резкого скачка при переходе через 360/180 градусов
        if (delta > 180f) delta -= 360f;
        if (delta < -180f) delta += 360f;

        _lastAngle = currentAngle;
        return delta;
    }

    private float GetCurrentAngle()
    {
        Vector3 refUp;
        Vector3 refForward;

        if (_referenceParent != null)
        {
            refUp = _referenceParent.TransformDirection(_initialLocalUp);
            refForward = _referenceParent.TransformDirection(_initialLocalForward);
        }
        else
        {
            refUp = _initialLocalUp;
            refForward = _initialLocalForward;
        }

        // ≈сли ось вращени€ в Hinge Joint друга€ (не Z), 
        // возможно придетс€ помен€ть transform.up на transform.forward или transform.right
        return Vector3.SignedAngle(refUp, transform.up, refForward);
    }
}