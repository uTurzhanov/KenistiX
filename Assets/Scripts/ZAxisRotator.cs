using UnityEngine;

public class LocalPendulumTracker : MonoBehaviour
{
    [Header("Target References")]
    public Transform originObject; // Точка крепления маятника на штативе
    public Transform ball;         // Мячик

    [Header("Object to Rotate")]
    public Transform objectToRotate; // Транспортир

    [Header("Settings")]
    public float angleOffset = 0f;

    [Tooltip("Поставьте галочку, если транспортир крутится в противоположную от мяча сторону")]
    public bool invertAngle = false;

    void Update()
    {
        if (originObject == null || ball == null || objectToRotate == null) return;

        // 1. Переводим мировую позицию мяча в локальные координаты относительно точки крепления.
        // Теперь скрипту неважно, как повернут весь стол или штатив в мире.
        Vector3 localPos = originObject.InverseTransformPoint(ball.position);

        // 2. Считаем точный угол. 
        // localPos.x - это смещение маятника влево/вправо в локальных осях.
        // -localPos.y - это расстояние вниз (ставим минус, чтобы ровное положение давало 0 градусов).
        float angle = Mathf.Atan2(localPos.x, -localPos.y) * Mathf.Rad2Deg;

        if (invertAngle)
        {
            angle = -angle;
        }

        // 3. Жестко применяем угол к локальной оси Z транспортира.
        Vector3 currentEuler = objectToRotate.localEulerAngles;
        objectToRotate.localEulerAngles = new Vector3(currentEuler.x, currentEuler.y, angle + angleOffset);
    }
}