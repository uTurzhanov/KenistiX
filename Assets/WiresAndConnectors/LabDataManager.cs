using System.Collections.Generic;
using TMPro;
using UnityEngine;
using System;

public class LabDataManager : MonoBehaviour
{
    [Header("Связь с UI (Локальные объекты этого канваса)")]
    [SerializeField] private TMP_Text labTitleTMP;
    [SerializeField] private TMP_Text studentNameInput; // или TMP_Text, если используешь его

    [Header("UI Времени")]
    [SerializeField] private TMP_Text sessionTimeTMP; // TMP для отображения времени сессии (00:00:00)
    [SerializeField] private TMP_Text timestampTMP;   // TMP для отображения глобального времени (28.08.2026 · 14:22)

    public string FinalLabTitle { get; private set; } = "";

    public string FinalStudentName
    {
        get
        {
            return studentNameInput != null && !string.IsNullOrEmpty(studentNameInput.text)
                ? studentNameInput.text
                : "Имя не указано";
        }
    }

    private readonly Dictionary<string, string> labTitlesDatabase = new Dictionary<string, string>
    {
        { "0", "№0. Измерение плотности тела" },
        { "1", "№1. Ускорение свободного падения" },
        { "2", "№2. Момент инерции" },
        { "3", "№3. Определение адиабаты методом Клемана-Дезорма" },
        { "4", "№4. ВАХ для разных элементов цепи." },
        { "5", "№5. Эффект Холла" },
        { "6", "№6. Переменный ток, сложение сигнала, осциллограф, частоты" },
        { "7", "№7. Дифракция, коллимация" }
    };

    private float sessionTimer = 0f; // Таймер сессии в секундами

    private void Start()
    {
        InitializeLabData();

        // Сбрасываем таймер сессии при запуске сцены
        sessionTimer = 0f;

        // Сразу обновляем UI актуального времени
        UpdateTimestampUI();
    }

    private void Update()
    {
        // Увеличиваем таймер
        sessionTimer += Time.deltaTime;

        // Обновляем UI сессии
        if (sessionTimeTMP != null)
        {
            sessionTimeTMP.text = GetFormattedSessionTime();
        }

        // Обновляем актуальное время (можно не каждый кадр, но для простоты здесь)
        UpdateTimestampUI();
    }

    private void InitializeLabData()
    {
        string labNumber = PlayerPrefs.GetString("LabNum", "0");

        if (labTitlesDatabase.TryGetValue(labNumber, out string title))
        {
            FinalLabTitle = title;
        }
        else
        {
            FinalLabTitle = $"№{labNumber}. Неизвестная лабораторная работа";
        }

        if (labTitleTMP != null)
        {
            labTitleTMP.text = FinalLabTitle;
        }
    }

    private void UpdateTimestampUI()
    {
        if (timestampTMP != null)
        {
            timestampTMP.text = GetFormattedTimestamp();
        }
    }

    // Возвращает отформатированное время сессии (ЧЧ:ММ:СС)
    public string GetFormattedSessionTime()
    {
        TimeSpan t = TimeSpan.FromSeconds(sessionTimer);
        return string.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
    }

    // Возвращает отформатированное глобальное время (ДД.ММ.ГГГГ · ЧЧ:ММ)
    public string GetFormattedTimestamp()
    {
        return DateTime.Now.ToString("dd.MM.yyyy · HH:mm");
    }
}