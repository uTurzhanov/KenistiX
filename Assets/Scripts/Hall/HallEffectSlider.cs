using UnityEngine;

public class HallEffectSlider : MonoBehaviour
{
    public Transform railStart; // Start point of the rail
    public Transform railEnd;   // End point of the rail
    public HallSensorSimulator HES;

    private Vector3 startPosition;
    private Vector3 endPosition;

    public float maxInsertage = 12.0f; // Maximum resistance value

    private void Start()
    {
        startPosition = railStart.position;
        endPosition = railEnd.position;
    }

    private void Update()
    {
        // Находим минимальное и максимальное значения по оси Z для корректного Clamp
        float minZ = Mathf.Min(startPosition.z, endPosition.z);
        float maxZ = Mathf.Max(startPosition.z, endPosition.z);

        // Limit the movement of the slider along the Z axis between the start and end points
        transform.position = new Vector3(
            transform.position.x,
            transform.position.y,
            Mathf.Clamp(transform.position.z, minZ, maxZ)
        );

        // Calculate the normalized position of the slider along the Z axis (от 0 до 1)
        float normalizedPosition = (transform.position.z - startPosition.z) / (endPosition.z - startPosition.z);

        // Инвертируем значение: (1 - 0 = 1), (1 - 1 = 0)
        float newValue = (1f - normalizedPosition) * maxInsertage;
        HES.sensorPositionCm = newValue;
    }

    // Utility function to map resistance to light intensity
    private float MapResistanceToIntensity(float resistance, float minRes, float maxRes, float minIntensity, float maxIntensity)
    {
        return (resistance - minRes) / (maxRes - minRes) * (maxIntensity - minIntensity) + minIntensity;
    }
}