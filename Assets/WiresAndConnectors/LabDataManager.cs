using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using TMPro;

public class LabDataManager : MonoBehaviour
{
    [Header("Связь с UI Toolkit")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Связь с uGUI (Опционально)")]
    [SerializeField] private TMP_Text labTitleTMP;
    [SerializeField] private TMP_Text studentNameInput;
    [SerializeField] private TMP_Text sessionTimeTMP;
    [SerializeField] private TMP_Text timestampTMP;

    private Label _studentNameLabel;
    private Label _experimentTitleLabel;
    private Label _sessionTimeLabel;

    public string FinalLabTitle { get; private set; } = "";

    public string FinalStudentName
    {
        get
        {
            if (studentNameInput != null && !string.IsNullOrEmpty(studentNameInput.text))
                return studentNameInput.text;

            if (_studentNameLabel != null && !string.IsNullOrEmpty(_studentNameLabel.text))
                return _studentNameLabel.text;

            return "КОНСТАНТИНОПОЛЬСКИЙ-СЕВЕРСКИЙ АЛЕКСАНДР АЛЕКСАНДРОВИЧ";
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

    private float sessionTimer = 0f;

    private void Start()
    {
        // Вызываем поиск элементов в Start, когда rootVisualElement гарантированно инициализирован
        InitUIToolkitElements();
        InitializeLabData();
        sessionTimer = 0f;
        UpdateTimestampUI();
    }

    private void InitUIToolkitElements()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();

        if (uiDocument != null && uiDocument.rootVisualElement != null)
        {
            VisualElement root = uiDocument.rootVisualElement;
            _studentNameLabel = root.Q<Label>("student-name");
            _experimentTitleLabel = root.Q<Label>("experiment-title");
            _sessionTimeLabel = root.Q<Label>("lbl-session-time");
        }
    }

    private void Update()
    {
        sessionTimer += Time.deltaTime;
        string formattedTime = GetFormattedSessionTime();

        if (sessionTimeTMP != null) sessionTimeTMP.text = formattedTime;

        if (_sessionTimeLabel != null)
        {
            _sessionTimeLabel.text = $"Время сессии: {formattedTime}";
        }

        UpdateTimestampUI();
    }

    private void InitializeLabData()
    {
        // Защитная повторная проверка на случай, если ссылка оказалась null
        if (_experimentTitleLabel == null)
        {
            InitUIToolkitElements();
        }

        // 1. Считываем номер лабы из PlayerPrefs
        string labNumber = PlayerPrefs.GetString("LabNum", "0");
        if (string.IsNullOrEmpty(labNumber)) labNumber = "0";

        // 2. Ищем название в словаре
        if (labTitlesDatabase.TryGetValue(labNumber, out string title))
        {
            FinalLabTitle = title;
        }
        else
        {
            FinalLabTitle = $"№{labNumber}. Неизвестная лабораторная работа";
        }

        // 3. Устанавливаем текст в UI Toolkit
        if (_experimentTitleLabel != null)
        {
            _experimentTitleLabel.text = FinalLabTitle;
        }

        // Устанавливаем текст в uGUI (если используется)
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

    public string GetFormattedSessionTime()
    {
        TimeSpan t = TimeSpan.FromSeconds(sessionTimer);
        return string.Format("{0:D2}:{1:D2}:{2:D2}", t.Hours, t.Minutes, t.Seconds);
    }

    public string GetFormattedTimestamp()
    {
        return DateTime.Now.ToString("dd.MM.yyyy · HH:mm");
    }
}