using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ResettableObject : MonoBehaviour
{
    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private Rigidbody _rb;

    private void Start()
    {
        _rb = GetComponent<Rigidbody>();
        
        // Кэшируем начальные координаты и поворот при загрузке сцены
        _startPosition = transform.position;
        _startRotation = transform.rotation;
    }

    public void ResetToStart()
    {
        // Обязательно обнуляем векторы скорости, иначе после телепортации 
        // объект продолжит падать по инерции сквозь стол
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        // Возвращаем объект на исходную точку
        transform.position = _startPosition;
        transform.rotation = _startRotation;
    }
}