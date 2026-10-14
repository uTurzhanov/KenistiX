using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScreenshotCardUI : MonoBehaviour
{
    [SerializeField] private RawImage previewImage;
    [SerializeField] private TMP_Text eventText;
    [SerializeField] private TMP_Text timeText;

    [Header("Общая строка (если используется)")]
    [SerializeField] private TMP_Text valuesText;

    [Header("Отдельные динамические поля (до 10 шт)")]
    [SerializeField] private TMP_Text[] dynamicValueTexts; // Массив полей внутри префаба

    // Метод Setup теперь принимает 6 аргументов, включая List<string> separatedValues
    public void Setup(
        Texture2D screenshot,
        string eventTitle,
        string combinedValues,
        List<string> separatedValues, // Новый аргумент
        string sessionTime,
        string timestamp)
    {
        previewImage.texture = screenshot;
        eventText.text = eventTitle;
        timeText.text = $"Сессия: {sessionTime} · {timestamp}";

        if (valuesText != null)
        {
            valuesText.text = combinedValues;
        }

        if (dynamicValueTexts != null)
        {
            for (int i = 0; i < dynamicValueTexts.Length; i++)
            {
                if (separatedValues != null && i < separatedValues.Count)
                {
                    dynamicValueTexts[i].gameObject.SetActive(true);
                    dynamicValueTexts[i].text = separatedValues[i];
                }
                else
                {
                    dynamicValueTexts[i].gameObject.SetActive(false);
                }
            }
        }
    }
}