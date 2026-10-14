using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public float rotationSpeed = 10f; // Скорость вращения солнца

    void Update()
    {
        // Вращаем солнце вокруг оси X
        transform.Rotate(Vector3.right * rotationSpeed * Time.deltaTime);
    }
}