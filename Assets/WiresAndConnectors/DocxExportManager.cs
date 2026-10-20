using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

public class DocxExportManager : MonoBehaviour
{
    [Header("Связь с данными")]
    [SerializeField] private LabDataManager localDataManager; // Перетащи сюда LabDataManager с этого же канваса
    [Header("Связь с UI")]
    [SerializeField] private ScreenshotGalleryUI galleryUI;   // Ваша галерея со скриншотами
    [SerializeField] private Button exportButton;             // Кнопка "Отправить/Создать отчет"
    [SerializeField] private TMP_Text statusText;             // Текст для показа статуса пользователю (опционально)

    [Header("Сетевые настройки")]
    [SerializeField] private int udpDiscoveryPort = 8888;
    [SerializeField] private string discoveryMessage = "QUEST3_SEARCHING_DOCX_SERVER";

    private string foundServerUrl = "";

    private void Start()
    {
        if (exportButton != null)
            exportButton.onClick.AddListener(OnExportButtonClicked);

        UpdateStatus("Поиск сервера в локальной сети...");

        // Запускаем асинхронный поиск Go-сервера при старте сцены
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

                // Отправляем бродкаст-запрос
                await udpClient.SendAsync(requestData, requestData.Length, endPoint);

                // Ждем ответ от сервера (можно добавить таймаут, но для упрощения ждем бесконечно)
                UdpReceiveResult result = await udpClient.ReceiveAsync();
                string response = Encoding.UTF8.GetString(result.Buffer);

                if (response.StartsWith("DOCX_SERVER_IP:"))
                {
                    string ip = response.Split(':')[1];
                    foundServerUrl = $"http://{ip}:8080/generate";
                    UpdateStatus($"Сервер найден ({ip}). Готов к отправке.");
                    Debug.Log("Сервер найден: " + foundServerUrl);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Ошибка UDP поиска: " + e.Message);
            UpdateStatus("Ошибка поиска сервера.");
        }
    }

    private void OnExportButtonClicked()
    {
        if (string.IsNullOrEmpty(foundServerUrl))
        {
            UpdateStatus("Ошибка: Сервер не найден! Убедитесь, что Go-сервер запущен.");
            return;
        }

        exportButton.interactable = false; // Блокируем кнопку от двойных нажатий
        UpdateStatus("Сбор данных и отправка...");
        StartCoroutine(SendReportToServer());
    }

    private IEnumerator SendReportToServer()
    {
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
        // 1. Берем название лабы и имя из локального менеджера этого канваса
        if (localDataManager != null)
        {
            formData.Add(new MultipartFormDataSection("lab_title", localDataManager.FinalLabTitle));
            formData.Add(new MultipartFormDataSection("student_name", localDataManager.FinalStudentName));
        }
        else
        {
            formData.Add(new MultipartFormDataSection("lab_title", "Без названия"));
            formData.Add(new MultipartFormDataSection("student_name", "Неизвестный студент"));
            Debug.LogWarning("Local Data Manager не назначен в инспекторе!");
        }

        // 2. Метаданные скриншотов (переводим в JSON)
        List<ScreenshotData> items = galleryUI.capturedData;
        string jsonMetadata = JsonUtility.ToJson(new GalleryMetadataWrapper { array = items.ToArray() });
        formData.Add(new MultipartFormDataSection("gallery_metadata", jsonMetadata));

        // 3. Сами картинки скриншотов
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].screenshot != null)
            {
                byte[] imgData = items[i].screenshot.EncodeToPNG();
                formData.Add(new MultipartFormFileSection($"image_{i}", imgData, $"screenshot_{i}.png", "image/png"));
            }
        }

        // 4. Отправляем POST-запрос
        using (UnityWebRequest www = UnityWebRequest.Post(foundServerUrl, formData))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Ошибка сервера: " + www.error);
                UpdateStatus("Ошибка отправки: " + www.error);
                exportButton.interactable = true;
            }
            else
            {
                Debug.Log("Документ успешно создан сервером! Ответ: " + www.downloadHandler.text);
                UpdateStatus("Успех! Документ создан на сервере.");
                // Кнопку можно не разблокировать, если работа окончена
                exportButton.interactable = true;
            }
        }
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }

    // Класс-обертка для правильной сериализации массива в JSON встроенным JsonUtility
    [System.Serializable]
    private class GalleryMetadataWrapper
    {
        public ScreenshotData[] array;
    }
}