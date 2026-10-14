using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Добавляем класс для хранения всех данных скриншота
[System.Serializable]
public class ScreenshotData
{
    public Texture2D screenshot;
    public string eventTitle;
    public string values;
    public List<string> separatedValues;
    public string sessionTime;
    public string timestamp;
}

public class ScreenshotGalleryUI : MonoBehaviour
{
    [Header("Галерея")]
    [SerializeField] private Transform content;
    [SerializeField] private ScreenshotCardUI cardTemplate;
    [SerializeField] private TMP_Text emptyScreenshotsText;
    [SerializeField] private ScrollRect scrollRect;

    // Заменяем List<Texture2D> на List<ScreenshotData>
    public List<ScreenshotData> capturedData { get; private set; } = new();

    private void Awake()
    {
        cardTemplate.gameObject.SetActive(false);
        emptyScreenshotsText.gameObject.SetActive(true);
    }

    public void AddScreenshot(
        Texture2D screenshot,
        string eventTitle,
        string values,
        List<string> separatedValues,
        string sessionTime,
        string timestamp)
    {
        if (screenshot == null)
        {
            Debug.LogError("ScreenshotGalleryUI: получена пустая текстура.");
            return;
        }

        // Сохраняем все данные для парсера
        capturedData.Add(new ScreenshotData
        {
            screenshot = screenshot,
            eventTitle = eventTitle,
            values = values,
            separatedValues = separatedValues,
            sessionTime = sessionTime,
            timestamp = timestamp
        });

        ScreenshotCardUI newCard = Instantiate(cardTemplate, content, false);
        newCard.gameObject.SetActive(true);

        newCard.Setup(screenshot, eventTitle, values, separatedValues, sessionTime, timestamp);

        emptyScreenshotsText.gameObject.SetActive(false);
        StartCoroutine(ScrollToLastScreenshot());
    }

    private IEnumerator ScrollToLastScreenshot()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
    }

    private void OnDestroy()
    {
        foreach (var data in capturedData)
        {
            if (data.screenshot != null) Destroy(data.screenshot);
        }
        capturedData.Clear();
    }
}