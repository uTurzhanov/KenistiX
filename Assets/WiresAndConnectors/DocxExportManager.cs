using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using TMPro;

public class DocxExportManager : MonoBehaviour
{
    [Header("Связь с данными")]
    [SerializeField] private LabDataManager localDataManager;

    [Header("Связь с UI Toolkit")]
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private UIToolkitGalleryController galleryController;

    [Header("Связь с uGUI (Опционально)")]
    [SerializeField] private TMP_Text statusText;

    [Header("Сетевые настройки")]
    [SerializeField] private int udpDiscoveryPort = 8888;
    [SerializeField] private string discoveryMessage = "QUEST3_SEARCHING_DOCX_SERVER";

    private string foundServerUrl = "";
    private Button _btnCreateReport;
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
            _btnCreateReport = root.Q<Button>("btn-create-report");
            _lblServerStatus = root.Q<Label>("lbl-server-status");

            if (_btnCreateReport != null)
            {
                _btnCreateReport.clicked += OnExportButtonClicked;
            }
        }
    }

    private void Start()
    {
        UpdateStatus("Поиск сервера...");
        _ = SearchForServerAsync();
    }

    private async Task SearchForServerAsync()
    {
        try
        {
            using (UdpClient udpClient = new UdpClient())
            {
                udpClient.EnableBroadcast = true;
                byte[] requestData = Encoding.UTF8.GetBytes(discoveryMessage);
                IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, udpDiscoveryPort);

                await udpClient.SendAsync(requestData, requestData.Length, endPoint);

                UdpReceiveResult result = await udpClient.ReceiveAsync();
                string response = Encoding.UTF8.GetString(result.Buffer);

                if (response.StartsWith("DOCX_SERVER_IP:"))
                {
                    string ip = response.Split(':')[1];
                    foundServerUrl = $"http://{ip}:8080/generate";
                    UpdateStatus($"Сервер найден ({ip})");
                    Debug.Log("Сервер найден: " + foundServerUrl);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Ошибка UDP поиска: " + e.Message);
            UpdateStatus("Ошибка поиска сервера");
        }
    }

    private void OnExportButtonClicked()
    {
        if (string.IsNullOrEmpty(foundServerUrl))
        {
            UpdateStatus("Ошибка: Сервер не найден!");
            return;
        }

        if (_btnCreateReport != null) _btnCreateReport.SetEnabled(false);
        UpdateStatus("Отправка отчета...");
        StartCoroutine(SendReportToServer());
    }

    private IEnumerator SendReportToServer()
    {
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();

        // 1. Метаданные лабораторной
        if (localDataManager != null)
        {
            formData.Add(new MultipartFormDataSection("lab_title", localDataManager.FinalLabTitle));
            formData.Add(new MultipartFormDataSection("student_name", localDataManager.FinalStudentName));
            formData.Add(new MultipartFormDataSection("session_time", localDataManager.GetFormattedSessionTime()));
            formData.Add(new MultipartFormDataSection("timestamp", localDataManager.GetFormattedTimestamp()));
        }
        else
        {
            formData.Add(new MultipartFormDataSection("lab_title", "Без названия"));
            formData.Add(new MultipartFormDataSection("student_name", "Неизвестный студент"));
            formData.Add(new MultipartFormDataSection("session_time", "00:00:00"));
            formData.Add(new MultipartFormDataSection("timestamp", System.DateTime.Now.ToString("dd.MM.yyyy · HH:mm")));
        }

        // 2. Метаданные снимков из UIToolkitGalleryController
        List<ScreenshotData> items = galleryController != null ? galleryController.CapturedData : new List<ScreenshotData>();
        string jsonMetadata = JsonUtility.ToJson(new GalleryMetadataWrapper { array = items.ToArray() });
        formData.Add(new MultipartFormDataSection("gallery_metadata", jsonMetadata));

        // 3. Бинарные данные PNG карт
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].screenshot != null)
            {
                byte[] imgData = items[i].screenshot.EncodeToPNG();
                formData.Add(new MultipartFormFileSection($"image_{i}", imgData, $"screenshot_{i}.png", "image/png"));
            }
        }

        // 4. Отправка POST запроса
        using (UnityWebRequest www = UnityWebRequest.Post(foundServerUrl, formData))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Ошибка сервера: " + www.error);
                UpdateStatus("Ошибка отправки!");
                if (_btnCreateReport != null) _btnCreateReport.SetEnabled(true);
            }
            else
            {
                Debug.Log("Документ создан! Ответ: " + www.downloadHandler.text);
                UpdateStatus("Отчет создан!");
                if (_btnCreateReport != null) _btnCreateReport.SetEnabled(true);
            }
        }
    }

    private void UpdateStatus(string message)
    {
        if (_lblServerStatus != null)
        {
            _lblServerStatus.text = $"Состояние сервера: {message}";
        }

        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    [System.Serializable]
    private class GalleryMetadataWrapper
    {
        public ScreenshotData[] array;
    }
}