using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.UIElements;
using TMPro;
using UIButton = UnityEngine.UIElements.Button;

public class DocxExportManager : MonoBehaviour
{
    [Header("Связь с данными")]
    [SerializeField] private LabDataManager localDataManager;

    [Header("Связь с UI Toolkit")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private UIToolkitGalleryController galleryController;

    [Header("Связь с uGUI (Опционально)")]
    [SerializeField] private TMP_Text statusText;

    [Header("Настройки Google Диска (Apps Script)")]
    [SerializeField] private string googleAppsScriptUrl = "ВСТАВЬТЕ_СЮДА_URL_ИЗ_APPS_SCRIPT";
    [SerializeField] private Texture2D iituLogoTexture;

    [Header("Водяные знаки и инфо-плашка на скриншотах")]
    [Tooltip("Перетащите сюда TMP_FontAsset с поддержкой кириллицы и казахских символов")]
    [SerializeField] private TMP_FontAsset watermarkFont;
    [Tooltip("Свободный слой (0..31) для изолированного рендера водяных знаков")]
    [SerializeField] private int watermarkRenderLayer = 30;

    private UIButton _btnCreateReport;
    private Label _lblServerStatus;

    private void Awake()
    {
        InitUIToolkit();
    }

    private void InitUIToolkit()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            VisualElement root = uiDocument.rootVisualElement;
            _btnCreateReport = root.Q<UIButton>("btn-create-report");
            _lblServerStatus = root.Q<Label>("lbl-server-status");

            if (_btnCreateReport != null)
            {
                _btnCreateReport.clicked += OnExportButtonClicked;
            }
        }
    }

    private void Start()
    {
        if (!string.IsNullOrEmpty(googleAppsScriptUrl) && googleAppsScriptUrl.StartsWith("https://"))
            UpdateStatus("Готов к выгрузке на Google Диск");
        else
            UpdateStatus("Не настроен URL Google Диска");
    }

    private void OnExportButtonClicked()
    {
        if (string.IsNullOrEmpty(googleAppsScriptUrl) || !googleAppsScriptUrl.StartsWith("https://"))
        {
            UpdateStatus("Ошибка: Не задан URL Google Диска!");
            return;
        }

        if (_btnCreateReport != null) _btnCreateReport.SetEnabled(false);
        UpdateStatus("Формирование и отправка отчета...");
        StartCoroutine(GenerateAndSendToGoogleDrive());
    }

    private IEnumerator GenerateAndSendToGoogleDrive()
    {
        string labTitle = localDataManager != null ? localDataManager.FinalLabTitle : "Без названия";
        string studentName = localDataManager != null
            ? localDataManager.FinalStudentName
            : PlayerPrefs.GetString(PlatonusAuth.StudentNamePrefKey, "Неизвестный студент");
        string studentEmail = PlayerPrefs.GetString(PlatonusAuth.StudentLoginPrefKey, string.Empty);

        string sessionTime = localDataManager != null ? localDataManager.GetFormattedSessionTime() : "00:00:00";
        string timestamp = localDataManager != null ? localDataManager.GetFormattedTimestamp() : DateTime.Now.ToString("dd.MM.yyyy · HH:mm");

        List<ScreenshotData> items = galleryController != null ? galleryController.CapturedData : new List<ScreenshotData>();

        var groupOrder = new List<string>();
        var groupMap = new Dictionary<string, GroupPayload>();
        var deviceCounters = new Dictionary<string, int>();
        var screenshotsPayload = new List<ScreenshotFilePayload>();

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            if (item == null) continue;

            string eventTitle = string.IsNullOrEmpty(item.eventTitle) ? "Замер" : item.eventTitle;

            if (!groupMap.ContainsKey(eventTitle))
            {
                groupOrder.Add(eventTitle);
                groupMap[eventTitle] = new GroupPayload
                {
                    eventTitle = eventTitle,
                    instances = new List<InstancePayload>()
                };
            }
            var currentGroup = groupMap[eventTitle];

            var cleanValues = new List<string>();
            if (item.separatedValues != null)
            {
                foreach (var val in item.separatedValues)
                {
                    if (!string.IsNullOrWhiteSpace(val))
                        cleanValues.Add(val.Trim());
                }
            }

            // Проверка на дубликаты
            bool isDuplicate = false;
            if (cleanValues.Count > 0)
            {
                foreach (var existing in currentGroup.instances)
                {
                    if (existing.values.Count == cleanValues.Count)
                    {
                        bool match = true;
                        for (int j = 0; j < cleanValues.Count; j++)
                        {
                            if (existing.values[j] != cleanValues[j])
                            {
                                match = false;
                                break;
                            }
                        }
                        if (match)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }
                }
            }

            if (isDuplicate) continue;

            string devID = string.IsNullOrWhiteSpace(item.deviceID) ? "X" : item.deviceID.Trim();
            if (!deviceCounters.ContainsKey(devID)) deviceCounters[devID] = 0;
            deviceCounters[devID]++;
            string formattedID = $"{devID}-{deviceCounters[devID]}";

            string screenshotFileName = string.Empty;
            if (item.screenshot != null)
            {
                byte[] imgBytes = EncodeTextureToPngSafe(item.screenshot);
                if (imgBytes != null && imgBytes.Length > 0)
                {
                    // Рисуем диагональные водяные знаки с ФИО и нижнюю инфо-плашку
                    imgBytes = AddWatermarkAndFooterGpu(imgBytes, studentName, labTitle, eventTitle, formattedID);

                    // Вшиваем PNG-метаданные KenistiX
                    imgBytes = InjectPngMetadata(imgBytes, "Author", "KenistiX");
                    imgBytes = InjectPngMetadata(imgBytes, "Software", "KenistiX VR Lab");

                    string safeEventTitle = SanitizeFileName(eventTitle.Replace(" ", "_"));
                    screenshotFileName = $"Скриншот_ID{formattedID}_{safeEventTitle}.png";

                    screenshotsPayload.Add(new ScreenshotFilePayload
                    {
                        fileName = screenshotFileName,
                        base64 = Convert.ToBase64String(imgBytes)
                    });
                }

                yield return null;
            }

            currentGroup.instances.Add(new InstancePayload
            {
                id = formattedID,
                screenshotFileName = screenshotFileName,
                values = cleanValues
            });
        }

        var orderedGroups = new List<GroupPayload>();
        foreach (string key in groupOrder)
        {
            orderedGroups.Add(groupMap[key]);
        }

        string logoBase64 = string.Empty;
        if (iituLogoTexture != null)
        {
            byte[] logoBytes = EncodeTextureToPngSafe(iituLogoTexture);
            if (logoBytes != null) logoBase64 = Convert.ToBase64String(logoBytes);
        }

        var payload = new AppsScriptPayload
        {
            studentName = studentName,
            studentEmail = studentEmail,
            labTitle = labTitle,
            sessionTime = sessionTime,
            timestamp = timestamp,
            logoBase64 = logoBase64,
            groups = orderedGroups,
            screenshots = screenshotsPayload
        };

        string jsonBody = JsonUtility.ToJson(payload);

        using (UnityWebRequest www = new UnityWebRequest(googleAppsScriptUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.redirectLimit = 10;
            www.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[DocxExport] Ошибка Google Диска ({www.responseCode}): {www.error} | {www.downloadHandler.text}");
                UpdateStatus("Ошибка отправки на Google Диск!");
            }
            else
            {
                Debug.Log("[DocxExport] Отчет загружен на Google Диск! Ответ: " + www.downloadHandler.text);
                UpdateStatus("Отчет сохранён в .docx!");
            }

            if (_btnCreateReport != null) _btnCreateReport.SetEnabled(true);
        }
    }

    private byte[] EncodeTextureToPngSafe(Texture2D source)
    {
        if (source == null) return null;

        try
        {
            if (source.format == TextureFormat.RGBA32 ||
                source.format == TextureFormat.ARGB32 ||
                source.format == TextureFormat.RGB24)
            {
                return source.EncodeToPNG();
            }
        }
        catch { }

        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(source, rt);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D readableCopy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        readableCopy.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readableCopy.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        byte[] pngBytes = readableCopy.EncodeToPNG();
        Destroy(readableCopy);
        return pngBytes;
    }

    private byte[] AddWatermarkAndFooterGpu(byte[] pngBytes, string studentName, string labTitle, string eventTitle, string measurementID)
    {
        Texture2D srcTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!srcTex.LoadImage(pngBytes))
        {
            Destroy(srcTex);
            return pngBytes;
        }

        int w = srcTex.width;
        int h = srcTex.height;
        int panelH = Mathf.Max(36, h / 10);
        int totalH = h + panelH;

        int layer = Mathf.Clamp(watermarkRenderLayer, 0, 31);
        RenderTexture rt = RenderTexture.GetTemporary(w, totalH, 24, RenderTextureFormat.ARGB32);

        GameObject camObj = new GameObject("TempWatermarkCam");
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = totalH * 0.5f;
        cam.transform.position = new Vector3(0f, 0f, -100f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        cam.cullingMask = 1 << layer;
        cam.targetTexture = rt;

        GameObject canvasObj = new GameObject("TempWatermarkCanvas");
        canvasObj.layer = layer;
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(w, totalH);
        canvasRect.position = Vector3.zero;

        // 1. Исходный скриншот выше нижней плашки
        GameObject imgObj = new GameObject("ScreenshotImg");
        imgObj.layer = layer;
        imgObj.transform.SetParent(canvasRect, false);
        RawImage rawImg = imgObj.AddComponent<RawImage>();
        rawImg.texture = srcTex;
        RectTransform imgRect = imgObj.GetComponent<RectTransform>();
        imgRect.anchorMin = new Vector2(0f, 0f);
        imgRect.anchorMax = new Vector2(1f, 1f);
        imgRect.offsetMin = new Vector2(0f, panelH);
        imgRect.offsetMax = Vector2.zero;

        // 2. Диагональная сетка водяных знаков с ФИО студента
        GameObject wmContainer = new GameObject("Watermarks");
        wmContainer.layer = layer;
        RectTransform wmRect = wmContainer.AddComponent<RectTransform>();
        wmRect.SetParent(imgRect, false);
        wmRect.anchorMin = Vector2.zero;
        wmRect.anchorMax = Vector2.one;
        wmRect.offsetMin = Vector2.zero;
        wmRect.offsetMax = Vector2.zero;
        wmContainer.AddComponent<RectMask2D>();

        int wmFontSize = Mathf.Max(18, w / 28);
        float stepX = Mathf.Max(220f, studentName.Length * wmFontSize * 0.65f);
        float stepY = wmFontSize * 4.5f;

        for (float x = -w * 0.6f; x <= w * 0.6f; x += stepX)
        {
            for (float y = -h * 0.6f; y <= h * 0.6f; y += stepY)
            {
                GameObject wmObj = new GameObject("WM");
                wmObj.layer = layer;
                wmObj.transform.SetParent(wmRect, false);
                TextMeshProUGUI wmText = wmObj.AddComponent<TextMeshProUGUI>();
                if (watermarkFont != null) wmText.font = watermarkFont;
                wmText.text = studentName;
                wmText.fontSize = wmFontSize;
                wmText.color = new Color(0.65f, 0.65f, 0.65f, 0.22f);
                wmText.alignment = TextAlignmentOptions.Center;
                wmText.textWrappingMode = TextWrappingModes.NoWrap;

                RectTransform r = wmText.rectTransform;
                r.sizeDelta = new Vector2(stepX * 1.5f, wmFontSize * 2f);
                r.anchoredPosition = new Vector2(x, y);
                r.localRotation = Quaternion.Euler(0f, 0f, 35f);
            }
        }

        // 3. Нижняя светло-серая инфо-плашка
        GameObject footerObj = new GameObject("FooterPanel");
        footerObj.layer = layer;
        footerObj.transform.SetParent(canvasRect, false);
        UnityEngine.UI.Image footerBg = footerObj.AddComponent<UnityEngine.UI.Image>();
        footerBg.color = new Color(0.94f, 0.94f, 0.94f, 1f);
        RectTransform footerRect = footerObj.GetComponent<RectTransform>();
        footerRect.anchorMin = new Vector2(0f, 0f);
        footerRect.anchorMax = new Vector2(1f, 0f);
        footerRect.pivot = new Vector2(0.5f, 0f);
        footerRect.sizeDelta = new Vector2(0f, panelH);
        footerRect.anchoredPosition = Vector2.zero;

        GameObject footerTextObj = new GameObject("FooterText");
        footerTextObj.layer = layer;
        footerTextObj.transform.SetParent(footerRect, false);
        TextMeshProUGUI footerText = footerTextObj.AddComponent<TextMeshProUGUI>();
        if (watermarkFont != null) footerText.font = watermarkFont;
        footerText.text = $"[ID: {measurementID}] Студент: {studentName}  |  {labTitle}  |  Замер: {eventTitle}";
        footerText.color = Color.black;
        footerText.alignment = TextAlignmentOptions.Center;
        footerText.enableAutoSizing = true;
        footerText.fontSizeMin = 10;
        footerText.fontSizeMax = Mathf.Clamp(panelH * 0.42f, 12, 42);
        footerText.textWrappingMode = TextWrappingModes.NoWrap;

        RectTransform ftRect = footerText.rectTransform;
        ftRect.anchorMin = Vector2.zero;
        ftRect.anchorMax = Vector2.one;
        ftRect.offsetMin = new Vector2(16f, 4f);
        ftRect.offsetMax = new Vector2(-16f, -4f);

        // 4. Рендер в RenderTexture и считывание результата
        Canvas.ForceUpdateCanvases();
        cam.Render();

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D finalTex = new Texture2D(w, totalH, TextureFormat.RGBA32, false);
        finalTex.ReadPixels(new Rect(0, 0, w, totalH), 0, 0);
        finalTex.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        byte[] resultBytes = finalTex.EncodeToPNG();

        Destroy(camObj);
        Destroy(canvasObj);
        Destroy(srcTex);
        Destroy(finalTex);

        return resultBytes;
    }

    private byte[] InjectPngMetadata(byte[] pngBytes, string key, string value)
    {
        if (pngBytes == null || pngBytes.Length < 12) return pngBytes;

        int iendStart = pngBytes.Length - 12;
        if (pngBytes[iendStart + 4] != 'I' || pngBytes[iendStart + 5] != 'E' ||
            pngBytes[iendStart + 6] != 'N' || pngBytes[iendStart + 7] != 'D')
        {
            return pngBytes;
        }

        byte[] keyBytes = Encoding.ASCII.GetBytes(key);
        byte[] valBytes = Encoding.UTF8.GetBytes(value);
        byte[] textData = new byte[keyBytes.Length + 1 + valBytes.Length];
        Buffer.BlockCopy(keyBytes, 0, textData, 0, keyBytes.Length);
        textData[keyBytes.Length] = 0;
        Buffer.BlockCopy(valBytes, 0, textData, keyBytes.Length + 1, valBytes.Length);

        byte[] chunkType = Encoding.ASCII.GetBytes("tEXt");
        byte[] crcInput = new byte[chunkType.Length + textData.Length];
        Buffer.BlockCopy(chunkType, 0, crcInput, 0, chunkType.Length);
        Buffer.BlockCopy(textData, 0, crcInput, chunkType.Length, textData.Length);

        uint crc = ComputeCrc32(crcInput);

        var chunk = new List<byte>();
        chunk.AddRange(ToBigEndian((uint)textData.Length));
        chunk.AddRange(chunkType);
        chunk.AddRange(textData);
        chunk.AddRange(ToBigEndian(crc));

        byte[] result = new byte[iendStart + chunk.Count + 12];
        Buffer.BlockCopy(pngBytes, 0, result, 0, iendStart);
        Buffer.BlockCopy(chunk.ToArray(), 0, result, iendStart, chunk.Count);
        Buffer.BlockCopy(pngBytes, iendStart, result, iendStart + chunk.Count, 12);

        return result;
    }

    private byte[] ToBigEndian(uint val)
    {
        return new byte[]
        {
            (byte)((val >> 24) & 0xFF),
            (byte)((val >> 16) & 0xFF),
            (byte)((val >> 8) & 0xFF),
            (byte)(val & 0xFF)
        };
    }

    private uint ComputeCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        for (int i = 0; i < data.Length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : (crc >> 1);
            }
        }
        return ~crc;
    }

    private string SanitizeFileName(string name)
    {
        foreach (char c in new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' })
        {
            name = name.Replace(c.ToString(), "");
        }
        return name;
    }

    private void UpdateStatus(string message)
    {
        if (_lblServerStatus != null)
            _lblServerStatus.text = $"Состояние: {message}";

        if (statusText != null)
            statusText.text = message;
    }

    [Serializable]
    private class InstancePayload
    {
        public string id;
        public string screenshotFileName;
        public List<string> values;
    }

    [Serializable]
    private class GroupPayload
    {
        public string eventTitle;
        public List<InstancePayload> instances;
    }

    [Serializable]
    private class ScreenshotFilePayload
    {
        public string fileName;
        public string base64;
    }

    [Serializable]
    private class AppsScriptPayload
    {
        public string studentName;
        public string studentEmail;
        public string labTitle;
        public string sessionTime;
        public string timestamp;
        public string logoBase64;
        public List<GroupPayload> groups;
        public List<ScreenshotFilePayload> screenshots;
    }
}