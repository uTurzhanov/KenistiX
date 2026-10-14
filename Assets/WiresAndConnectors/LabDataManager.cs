using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class LabDataManager : MonoBehaviour
{
    [Header("Связь с UI (Локальные объекты этого канваса)")]
    [SerializeField] private TMP_Text labTitleTMP;
    [SerializeField] private TMP_Text studentNameInput; // или TMP_Text, если используешь его

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

    private void Start() // Изменено с Awake на Start для безопасной инициализации
    {
        InitializeLabData();
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
}