using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // Ссылка на трансформ игрока
    public Vector3 offset = new Vector3(0, 5, -10); // Смещение камеры относительно игрока
    public float smoothSpeed = 0.125f; // Скорость сглаживания

    void LateUpdate()
    {
        if (target != null)
        {
            Vector3 desiredPosition = target.position + offset;
            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
            transform.position = smoothedPosition;

            transform.LookAt(target); // Камера всегда смотрит на игрока
        }
    }
}