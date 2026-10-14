using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScreenshotGalleryUI : MonoBehaviour
{
    [Header("Галерея")]
    [SerializeField] private Transform content;
    [SerializeField] private ScreenshotCardUI cardTemplate;
    [SerializeField] private TMP_Text emptyScreenshotsText;
    [SerializeField] private ScrollRect scrollRect;

    private readonly List<Texture2D> capturedTextures = new();

    private void Awake()
    {
        cardTemplate.gameObject.SetActive(false);
        emptyScreenshotsText.gameObject.SetActive(true);
    }

    // Добавлен новый параметр: List<string> separatedValues
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
            Debug.LogError(
                "ScreenshotGalleryUI: получена пустая текстура."
            );

            return;
        }

        capturedTextures.Add(screenshot);

        ScreenshotCardUI newCard = Instantiate(
            cardTemplate,
            content,
            false
        );

        newCard.gameObject.SetActive(true);

        // Передаем список отдельных значений в карточку
        newCard.Setup(
            screenshot,
            eventTitle,
            values,
            separatedValues,
            sessionTime,
            timestamp
        );

        emptyScreenshotsText.gameObject.SetActive(false);

        StartCoroutine(ScrollToLastScreenshot());
    }

    private IEnumerator ScrollToLastScreenshot()
    {
        yield return null;

        Canvas.ForceUpdateCanvases();

        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private void OnDestroy()
    {
        foreach (Texture2D texture in capturedTextures)
        {
            if (texture != null)
            {
                Destroy(texture);
            }
        }

        capturedTextures.Clear();
    }
}