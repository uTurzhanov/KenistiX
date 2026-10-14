using UnityEngine;
using TMPro;

public class SecondaryCameraMaskController : MonoBehaviour
{
    [Header("Настройки камеры")]
    [Tooltip("Другая камера, у которой нужно менять Culling Mask")]
    [SerializeField] private Camera secondaryCamera;

    [Header("Связь с основным сервисом")]
    [Tooltip("Тот же Dropdown, который использует ScreenshotCaptureService для выбора точки")]
    [SerializeField] private TMP_Dropdown objectSelectionDropdown;

    [Header("Настройки слоев")]
    [Tooltip("Маски слоев для каждой точки. Порядок и количество должны совпадать с точками в ScreenshotCaptureService")]
    [SerializeField] private LayerMask[] pointCullingMasks;

    private void Start()
    {
        if (objectSelectionDropdown != null)
        {
            // Подписываемся на изменение выбранного элемента в Dropdown
            objectSelectionDropdown.onValueChanged.AddListener(OnPointSelected);

            // Применяем маску сразу при старте для нулевого элемента
            OnPointSelected(objectSelectionDropdown.value);
        }
        else
        {
            Debug.LogWarning("SecondaryCameraMaskController: Не назначен Dropdown!");
        }
    }

    private void OnPointSelected(int index)
    {
        if (secondaryCamera == null) return;

        // Проверяем, что для выбранного индекса настроена маска
        if (pointCullingMasks != null && index >= 0 && index < pointCullingMasks.Length)
        {
            // Передаем информацию о culling mask в другую камеру
            secondaryCamera.cullingMask = pointCullingMasks[index];
        }
        else
        {
            Debug.LogWarning($"SecondaryCameraMaskController: Нет настроек маски для индекса {index}.");
        }
    }

    private void OnDestroy()
    {
        // Отписываемся от события при удалении объекта во избежание ошибок
        if (objectSelectionDropdown != null)
        {
            objectSelectionDropdown.onValueChanged.RemoveListener(OnPointSelected);
        }
    }
}