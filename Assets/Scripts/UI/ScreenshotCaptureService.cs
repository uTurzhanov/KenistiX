using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

[Serializable]
public class TMPDataMapping
{
    public string prefix;
    public TMP_Text sourceTMP;

    [Tooltip("Целевой TextMeshPro для ПРЕВЬЮ скриншота (в интерфейсе камеры, если используется)")]
    public TMP_Text targetPreviewTMP;
}

[Serializable]
public class CapturePointConfig
{
    [Tooltip("Название прибора или замера (например: 'Мультиметр', 'Штангенциркуль')")]
    public string pointName = "Замер";

    [Tooltip("Позиция и поворот камеры")]
    public Transform point;

    [Tooltip("Угол обзора (Field of View) для этой точки")]
    [Range(1f, 179f)]
    public float fieldOfView = 60f;

    [Tooltip("Список показателей, которые нужно собирать для этой точки (Макс 10)")]
    public List<TMPDataMapping> dataMappings = new List<TMPDataMapping>();
}

public class ScreenshotCaptureService : MonoBehaviour
{
    [Header("Настройки съемки")]
    [SerializeField] private Camera captureCamera;
    [SerializeField] private List<CapturePointConfig> capturePoints = new List<CapturePointConfig>();

    [Header("UI Toolkit Контроллер")]
    [SerializeField] private UIToolkitGalleryController uiToolkitGallery;

    [Header("Разрешение скриншота")]
    [SerializeField] private int screenshotWidth = 1280;
    [SerializeField] private int screenshotHeight = 720;

    [Header("Данные сессии")]
    [SerializeField] private string sessionId = "TEST_SESSION";
    [SerializeField] private string defaultEventTitle = "Снятие контрольного показания";

    [Header("Динамический суффикс")]
    [Tooltip("Текст, добавляемый к названию замера (например: ' height')")]
    [SerializeField] private string currentTitleSuffix = "";

    // C# событие для подписки сторонних контроллеров
    public event Action<Texture2D, string, string, List<string>, string, string, string> OnScreenshotCaptured;

    private float _sessionStartTime;
    private int _screenshotNumber;
    private bool _isCapturing;

    public bool IsCapturing => _isCapturing;

    private void Awake()
    {
        _sessionStartTime = Time.time;
        if (captureCamera != null)
        {
            captureCamera.enabled = false;
        }
    }

    #region Публичные методы управления

    /// <summary>
    /// Установить суффикс для названия замера
    /// </summary>
    public void SetEventTitleSuffix(string suffix)
    {
        currentTitleSuffix = suffix;
    }

    /// <summary>
    /// Сбросить суффикс
    /// </summary>
    public void ClearEventTitleSuffix()
    {
        currentTitleSuffix = "";
    }

    /// <summary>
    /// Снять скриншот с текущей или первой доступной точки
    /// </summary>
    public void TakeScreenshot()
    {
        TakeScreenshot(0);
    }

    /// <summary>
    /// Снять скриншот с конкретной точки по её индексу
    /// </summary>
    /// <summary>
    /// Снять скриншот с конкретной точки по её индексу
    /// </summary>
    public void TakeScreenshot(int pointIndex)
    {
        if (_isCapturing) return;

        CapturePointConfig activeConfig = null;
        string eventTitle = defaultEventTitle;
        int safeIndex = 0; // Переменная объявлена снаружи блока if

        if (capturePoints != null && capturePoints.Count > 0)
        {
            safeIndex = Mathf.Clamp(pointIndex, 0, capturePoints.Count - 1);
            activeConfig = capturePoints[safeIndex];

            if (!string.IsNullOrEmpty(activeConfig.pointName))
            {
                eventTitle = activeConfig.pointName;
            }
        }

        if (!string.IsNullOrEmpty(currentTitleSuffix))
        {
            eventTitle += currentTitleSuffix;
        }

        StartCoroutine(CaptureRoutine(eventTitle, activeConfig, safeIndex.ToString()));
    }

    #endregion

    #region Основная логика захвата

    private IEnumerator CaptureRoutine(string eventTitle, CapturePointConfig activeConfig, string deviceID)
    {
        _isCapturing = true;

        // 1. Собираем текстовые данные с 3D-дисплеев
        var (combinedValues, separatedValues) = ProcessDynamicTMPData(activeConfig);

        // 2. Настраиваем камеру
        SynchronizeCaptureCamera(activeConfig);

        // 3. Создаем RenderTexture для кадра
        RenderTexture renderTexture = RenderTexture.GetTemporary(screenshotWidth, screenshotHeight, 24, RenderTextureFormat.ARGB32);
        captureCamera.targetTexture = renderTexture;
        captureCamera.enabled = true;

        // Ждем завершения рендеринга текущего кадра
        yield return new WaitForEndOfFrame();

        captureCamera.enabled = false;

        // 4. Считываем пиксели из RenderTexture в Texture2D
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture.active = renderTexture;

        Texture2D screenshot = new Texture2D(screenshotWidth, screenshotHeight, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, screenshotWidth, screenshotHeight), 0, 0);
        screenshot.Apply();

        // 5. Освобождаем ресурсы рендеринга
        RenderTexture.active = previousActive;
        captureCamera.targetTexture = null;
        RenderTexture.ReleaseTemporary(renderTexture);

        // 6. Формируем метаданные
        _screenshotNumber++;
        string sessionTime = GetSessionTime();
        string timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");

        // 7. Сохраняем PNG на диск
        SaveScreenshotToFile(screenshot, _screenshotNumber);

        // 8. Передаем данные в UI Toolkit Галерею
        if (uiToolkitGallery != null)
        {
            uiToolkitGallery.AddScreenshotEntry(
                screenshot,
                eventTitle,
                combinedValues,
                separatedValues,
                sessionTime,
                timestamp
            );
        }

        // 9. Оповещаем внешних подписчиков
        OnScreenshotCaptured?.Invoke(screenshot, eventTitle, combinedValues, separatedValues, sessionTime, timestamp, deviceID);

        _isCapturing = false;
    }

    /// <summary>
    /// Возвращает список названий приборов, настроенных в Инспекторе Unity
    /// </summary>
    public List<string> GetCapturePointNames()
    {
        List<string> names = new List<string>();
        if (capturePoints != null)
        {
            foreach (var pointConfig in capturePoints)
            {
                if (!string.IsNullOrEmpty(pointConfig.pointName))
                    names.Add(pointConfig.pointName);
                else
                    names.Add($"Точка {names.Count + 1}");
            }
        }
        return names;
    }

    private (string combined, List<string> separated) ProcessDynamicTMPData(CapturePointConfig activeConfig)
    {
        string combinedValues = "";
        List<string> separatedList = new List<string>();

        // Скрываем все превью на сцене перед сбором
        foreach (var config in capturePoints)
        {
            if (config?.dataMappings == null) continue;
            foreach (var mapping in config.dataMappings)
            {
                if (mapping.targetPreviewTMP != null)
                {
                    mapping.targetPreviewTMP.gameObject.SetActive(false);
                }
            }
        }

        // Собираем актуальные показания с активного прибора
        if (activeConfig?.dataMappings != null)
        {
            foreach (var mapping in activeConfig.dataMappings)
            {
                if (separatedList.Count >= 10) break;

                if (mapping.sourceTMP != null && !string.IsNullOrEmpty(mapping.sourceTMP.text))
                {
                    string formattedText = $"{mapping.prefix}{mapping.sourceTMP.text}";

                    if (mapping.targetPreviewTMP != null)
                    {
                        mapping.targetPreviewTMP.gameObject.SetActive(true);
                        mapping.targetPreviewTMP.text = formattedText;
                    }

                    separatedList.Add(formattedText);
                    combinedValues += formattedText + " · ";
                }
            }
        }

        if (combinedValues.EndsWith(" · "))
        {
            combinedValues = combinedValues.Substring(0, combinedValues.Length - 3);
        }

        return (combinedValues, separatedList);
    }

    private void SynchronizeCaptureCamera(CapturePointConfig config)
    {
        if (captureCamera == null) return;

        if (config != null && config.point != null)
        {
            captureCamera.transform.SetPositionAndRotation(config.point.position, config.point.rotation);
            captureCamera.fieldOfView = config.fieldOfView;
        }

        captureCamera.aspect = (float)screenshotWidth / screenshotHeight;
    }

    private void SaveScreenshotToFile(Texture2D screenshot, int number)
    {
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "LabJournal", sessionId, "Screenshots");
            Directory.CreateDirectory(dir);
            string filePath = Path.Combine(dir, $"shot_{number:000}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            File.WriteAllBytes(filePath, screenshot.EncodeToPNG());
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ScreenshotCaptureService] Ошибка сохранения снимка: {ex.Message}");
        }
    }

    private string GetSessionTime()
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(Time.time - _sessionStartTime);
        return $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
    }

    #endregion
}