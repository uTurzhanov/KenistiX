using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class TMPDataMapping
{
    public string prefix;
    public TMP_Text sourceTMP;

    [Tooltip("Целевой TextMeshPro для ПРЕВЬЮ скриншота (в интерфейсе камеры)")]
    public TMP_Text targetPreviewTMP;
}

[Serializable]
public class CapturePointConfig
{
    [Tooltip("Позиция и поворот камеры")]
    public Transform point;

    [Tooltip("Угол обзора (Field of View) для этой точки")]
    [Range(1f, 179f)]
    public float fieldOfView = 60f;
    [Tooltip("Список показателей, которые нужно собирать именно для этой точки (Макс 10)")]
    public List<TMPDataMapping> dataMappings = new List<TMPDataMapping>();
}

public class ScreenshotCaptureService : MonoBehaviour
{
    [Header("Точки съемки и настройки")]
    [Tooltip("Список точек. Порядок должен совпадать с опциями Dropdown.")]
    [SerializeField] private List<CapturePointConfig> capturePoints = new List<CapturePointConfig>();
    [SerializeField] private Camera captureCamera;

    [Header("Интерфейс управления и галереи")]
    [SerializeField] private ScreenshotGalleryUI galleryUI;
    [SerializeField] private Button captureButton;
    [SerializeField] private TMP_Dropdown objectSelectionDropdown;

    [Header("Вывод последнего скриншота")]
    [SerializeField] private ScreenshotCardUI latestScreenshotCardUI;

    [Header("Размер изображения")]
    [SerializeField] private int screenshotWidth = 1280;
    [SerializeField] private int screenshotHeight = 720;

    [Header("Тестовые данные")]
    [SerializeField] private string sessionId = "TEST_SESSION";
    [SerializeField] private string testEventTitle = "Снятие контрольного показания";

    private float sessionStartTime;
    private int screenshotNumber;
    private bool isCapturing;

    private void Awake()
    {
        sessionStartTime = Time.time;
        if (captureCamera != null) captureCamera.enabled = false;
    }

    public void TakeScreenshot()
    {
        string currentTitle = testEventTitle;
        CapturePointConfig activeConfig = null;

        if (objectSelectionDropdown != null && objectSelectionDropdown.options.Count > 0)
        {
            int selectedIndex = objectSelectionDropdown.value;
            currentTitle = objectSelectionDropdown.options[selectedIndex].text;

            if (selectedIndex < capturePoints.Count)
            {
                activeConfig = capturePoints[selectedIndex];
            }
            else
            {
                Debug.LogWarning($"ScreenshotCaptureService: Нет настроек съемки для индекса {selectedIndex}.");
            }
        }
        else if (capturePoints.Count > 0)
        {
            activeConfig = capturePoints[0];
        }

        // ИЗМЕНЕНИЕ: Убрана блокировка. Если точки нет, просто предупреждаем в консоли и делаем снимок с текущего места.
        if (activeConfig == null || activeConfig.point == null)
        {
            Debug.Log("ScreenshotCaptureService: Точка съемки не задана. Камера останется на текущей позиции.");
        }

        CaptureScreenshot(currentTitle, activeConfig, "");
    }

    public void CaptureScreenshot(string eventTitle, CapturePointConfig activeConfig, string fallbackValues = "")
    {
        if (isCapturing) return;
        StartCoroutine(CaptureRoutine(eventTitle, activeConfig, fallbackValues));
    }

    private IEnumerator CaptureRoutine(string eventTitle, CapturePointConfig activeConfig, string fallbackValues)
    {
        isCapturing = true;
        if (captureButton != null) captureButton.interactable = false;

        var (combinedValues, separatedValues) = ProcessDynamicTMPData(activeConfig);
        string finalValues = !string.IsNullOrEmpty(combinedValues) ? combinedValues : fallbackValues;

        SynchronizeCaptureCamera(activeConfig);

        RenderTexture renderTexture = new RenderTexture(screenshotWidth, screenshotHeight, 24, RenderTextureFormat.ARGB32);
        captureCamera.targetTexture = renderTexture;
        captureCamera.enabled = true;

        yield return new WaitForEndOfFrame();

        captureCamera.enabled = false;

        RenderTexture previousTexture = RenderTexture.active;
        RenderTexture.active = renderTexture;
        Texture2D screenshot = new Texture2D(screenshotWidth, screenshotHeight, TextureFormat.RGB24, false);
        screenshot.ReadPixels(new Rect(0, 0, screenshotWidth, screenshotHeight), 0, 0);
        screenshot.Apply();

        RenderTexture.active = previousTexture;
        captureCamera.targetTexture = null;
        renderTexture.Release();
        Destroy(renderTexture);

        screenshotNumber++;
        string sessionTime = GetSessionTime();
        string timestamp = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        SaveScreenshotToFile(screenshot, screenshotNumber);

        if (galleryUI != null)
        {
            galleryUI.AddScreenshot(screenshot, eventTitle, finalValues, separatedValues, sessionTime, timestamp);
        }

        if (latestScreenshotCardUI != null)
        {
            latestScreenshotCardUI.Setup(screenshot, eventTitle, finalValues, separatedValues, sessionTime, timestamp);
        }

        if (captureButton != null) captureButton.interactable = true;
        isCapturing = false;
    }

    private (string combined, List<string> separated) ProcessDynamicTMPData(CapturePointConfig activeConfig)
    {
        string combinedValues = "";
        List<string> separatedList = new List<string>();

        foreach (var config in capturePoints)
        {
            foreach (var mapping in config.dataMappings)
            {
                if (mapping.targetPreviewTMP != null)
                {
                    mapping.targetPreviewTMP.gameObject.SetActive(false);
                }
            }
        }

        // ИЗМЕНЕНИЕ: Добавлена проверка activeConfig на null
        if (activeConfig != null && activeConfig.dataMappings != null)
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
        // ИЗМЕНЕНИЕ: Если конфига или точки нет, просто подгоняем aspect ratio, не трогая Transform и FOV
        if (config == null || config.point == null)
        {
            captureCamera.aspect = (float)screenshotWidth / screenshotHeight;
            return;
        }

        captureCamera.transform.SetPositionAndRotation(config.point.position, config.point.rotation);
        captureCamera.fieldOfView = config.fieldOfView;
        captureCamera.aspect = (float)screenshotWidth / screenshotHeight;
    }

    private void SaveScreenshotToFile(Texture2D screenshot, int number)
    {
        string dir = Path.Combine(Application.persistentDataPath, "LabJournal", sessionId, "Screenshots");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, $"shot_{number:000}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.png");
        File.WriteAllBytes(file, screenshot.EncodeToPNG());
    }

    private string GetSessionTime()
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(Time.time - sessionStartTime);
        return $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00}";
    }
}