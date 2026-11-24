using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using TMPro;

public class LabDataManager : MonoBehaviour
{
    private const string DefaultStudentName = "КОНСТАНТИНОПОЛЬСКИЙ-СЕВЕРСКИЙ АЛЕКСАНДР АЛЕКСАНДРОВИЧ";

    [Header("Связь с UI Toolkit")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Связь с uGUI (Опционально)")]
    [SerializeField] private TMP_Text labTitleTMP;
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
            string savedName = PlayerPrefs.GetString(PlatonusAuth.StudentNamePrefKey, string.Empty);
            if (!string.IsNullOrEmpty(savedName))
                return savedName;

            if (_studentNameLabel != null && !string.IsNullOrEmpty(_studentNameLabel.text))
                return _studentNameLabel.text;

            return DefaultStudentName;
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
    private PlatonusAuth _platonusAuth;

    private void OnEnable()
    {
        // Если PlatonusAuth присутствует на этой же сцене, подписываемся на момент завершения входа
        _platonusAuth = FindFirstObjectByType<PlatonusAuth>();
        if (_platonusAuth != null)
        {
            _platonusAuth.OnAuthSuccess += OnPlatonusAuthSuccess;
        }
    }

    private void OnDisable()
    {
        if (_platonusAuth != null)
        {
            _platonusAuth.OnAuthSuccess -= OnPlatonusAuthSuccess;
        }
    }

    private void Start()
    {
        InitUIToolkitElements();
        InitializeLabData();
        RefreshStudentNameFromPrefs();
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

    private void OnPlatonusAuthSuccess(string _)
    {
        RefreshStudentNameFromPrefs();
    }

    /// <summary>
    /// Подтягивает ФИО студента из PlayerPrefs и обновляет UI Toolkit.
    /// </summary>
    public void RefreshStudentNameFromPrefs()
    {
        if (_studentNameLabel == null)
        {
            InitUIToolkitElements();
        }

        string studentName = PlayerPrefs.GetString(PlatonusAuth.StudentNamePrefKey, string.Empty);
        if (string.IsNullOrEmpty(studentName))
        {
            studentName = DefaultStudentName;
        }

        if (_studentNameLabel != null)
        {
            _studentNameLabel.text = studentName;
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