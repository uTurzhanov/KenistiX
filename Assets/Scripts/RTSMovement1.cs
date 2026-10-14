using UnityEngine;

public class RTSMovement : MonoBehaviour
{
    public float panSpeed = 10f;
    public float zoomSpeed = 5f;
    public Vector2 zoomLimits = new Vector2(5f, 20f);

    void Update()
    {
        // Считываем ввод (WASD / Стрелочки)
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // 1. Берем вектор "вперед" самой камеры и обнуляем Y, 
        // чтобы камера двигалась строго по горизонтали, а не пикировала в землю
        Vector3 camForward = transform.forward;
        camForward.y = 0;
        camForward.Normalize(); // Выравниваем длину вектора до 1

        // 2. Берем вектор "вправо" самой камеры и тоже прижимаем к земле
        Vector3 camRight = transform.right;
        camRight.y = 0;
        camRight.Normalize();

        // 3. Складываем их. Теперь "Вперед" — это всегда туда, куда направлен объектив
        Vector3 move = (camForward * moveZ + camRight * moveX) * panSpeed * Time.deltaTime;
        
        // Двигаем камеру в мировом пространстве, но уже по «умному» вектору
        transform.Translate(move, Space.World);

        // Зум через ScrollWheel (колёсико мыши)
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            Vector3 pos = transform.position;
            pos.y -= scroll * zoomSpeed;
            pos.y = Mathf.Clamp(pos.y, zoomLimits.x, zoomLimits.y);
            transform.position = pos;
        }
    }
}