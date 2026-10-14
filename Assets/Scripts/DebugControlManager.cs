using UnityEngine;

public enum DebugControlMode { FPS, RTS }

public class DebugControlManager : MonoBehaviour
{
    public DebugControlMode currentMode = DebugControlMode.FPS;
    public KeyCode switchKey = KeyCode.Tab;

    [Header("Movement Components")]
    public MonoBehaviour fpsMovement; // Ссылка на компонент FPSMovement
    public MonoBehaviour rtsMovement; // Ссылка на компонент RTSMovement

    [Header("Cameras")]
    public Camera fpsCamera;
    public Camera rtsCamera;

    [Header("Interaction")]
    public DebugInteractionController interactionController;

    void Start()
    {
        ApplyMode(currentMode);
    }

    void Update()
    {
        if (Input.GetKeyDown(switchKey))
        {
            currentMode = (currentMode == DebugControlMode.FPS) ? DebugControlMode.RTS : DebugControlMode.FPS;
            ApplyMode(currentMode);
        }
    }

    private void ApplyMode(DebugControlMode mode)
    {
        if (mode == DebugControlMode.FPS)
        {
            // Настройки для FPS
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            fpsMovement.enabled = true;
            rtsMovement.enabled = false;

            fpsCamera.gameObject.SetActive(true);
            rtsCamera.gameObject.SetActive(false);

            // Передаем актуальную камеру в контроллер взаимодействия
            interactionController.SetActiveCamera(fpsCamera);
        }
        else
        {   
            // Настройки для RTS
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            fpsMovement.enabled = false;
            rtsMovement.enabled = true;

            fpsCamera.gameObject.SetActive(false);
            rtsCamera.gameObject.SetActive(true);

            interactionController.SetActiveCamera(rtsCamera);
        }
    }
}