using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class ScreenshotInputTrigger : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Ссылка на скрипт ScreenshotCaptureService на сцене")]
    [SerializeField] private ScreenshotCaptureService captureService;

    [Header("Настройки ввода (XR Bindings)")]
    [Tooltip("Список действий для создания скриншота (например, кнопки для левого и правого контроллеров)")]
    [SerializeField] private List<InputActionReference> captureActions = new List<InputActionReference>();

    [Header("Capture Services")]
    [SerializeField] private List<ScreenshotCaptureService> CaptureServices = new List<ScreenshotCaptureService>();

    private void OnEnable()
    {
        // Проходим по всему списку и подписываемся на каждое действие
        foreach (var actionRef in captureActions)
        {
            if (actionRef != null && actionRef.action != null)
            {
                actionRef.action.Enable();
                actionRef.action.performed += OnCapturePerformed;
            }
        }

        if (captureActions.Count == 0)
        {
            Debug.LogWarning("ScreenshotInputTrigger: Не назначено ни одного действия для скриншота (Capture Actions пуст).");
        }
    }

    private void OnDisable()
    {
        // Отписываемся от всех действий при отключении
        foreach (var actionRef in captureActions)
        {
            if (actionRef != null && actionRef.action != null)
            {
                actionRef.action.performed -= OnCapturePerformed;
                actionRef.action.Disable();
            }
        }
    }

    private void OnCapturePerformed(InputAction.CallbackContext context)
    {


        if (captureService != null)
        {
            captureService.TakeScreenshot();
        }
        else
        {
            if(PlayerPrefs.GetString("LabNum") != null && CaptureServices[Convert.ToInt16(PlayerPrefs.GetString("LabNum"))] !=null)
            {
                captureService = CaptureServices[Convert.ToInt16(PlayerPrefs.GetString("LabNum"))];
            }
            else{


            Debug.LogError("ScreenshotInputTrigger: Ссылка на ScreenshotCaptureService пуста!");
            }
        }
    }
}