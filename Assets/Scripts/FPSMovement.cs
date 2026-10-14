using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FPSMovement : MonoBehaviour
{
    public float moveSpeed = 5.0f;
    public float lookSpeed = 2.0f;
    public float gravity = -9.81f;
    public Transform cameraTransform;

    private CharacterController _characterController;
    private float _verticalVelocity;
    private float _cameraRotationX = 0f;

    void Awake()
    {
        _characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Поворот мыши
        float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
        float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

        transform.Rotate(Vector3.up * mouseX);
        _cameraRotationX -= mouseY;
        _cameraRotationX = Mathf.Clamp(_cameraRotationX, -85f, 85f);
        cameraTransform.localRotation = Quaternion.Euler(_cameraRotationX, 0f, 0f);

        // Движение WASD
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        if (_characterController.isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = -2f;
        }

        Vector3 moveDirection = transform.forward * moveZ + transform.right * moveX;
        _verticalVelocity += gravity * Time.deltaTime;

        Vector3 finalVelocity = (moveDirection * moveSpeed) + (Vector3.up * _verticalVelocity);
        _characterController.Move(finalVelocity * Time.deltaTime);
    }
}