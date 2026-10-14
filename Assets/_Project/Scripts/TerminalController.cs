using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.IO;
using System;

// Класс для хранения эталонных данных (Констант)
[System.Serializable]
public class BlockParameters
{
    public string materialName; // Название в Dropdown
    [Header("Истинные значения (в мм и граммах)")]
    public float trueLength; // L_true (мм)
    public float trueWidth;  // W_true (мм)
    public float trueHeight; // H_true (мм)
    public float trueMass;   // m_true (граммы)
}

public class TerminalController : MonoBehaviour
{
    [Header("Блок А: Идентификация")]
    [SerializeField] private TMP_Dropdown blockDropdown;
    [SerializeField] private BlockParameters[] blocksData;

    [Header("Блок B: Прямые измерения (Штангенциркуль)")]
    [SerializeField] private TMP_InputField lengthMeanInput;
    [SerializeField] private TMP_InputField lengthErrorInput;
    [Space]
    [SerializeField] private TMP_InputField widthMeanInput;
    [SerializeField] private TMP_InputField widthErrorInput;
    [Space]
    [SerializeField] private TMP_InputField heightMeanInput;
    [SerializeField] private TMP_InputField heightErrorInput;

    [Header("Блок C: Косвенные измерения (Плотность)")]
    [SerializeField] private TMP_InputField densityMeanInput;
    [SerializeField] private TMP_InputField densityErrorInput;

    [Header("Управление и Отчет")]
    [SerializeField] private Button submitButton;
    [SerializeField] private TMP_Text reportText;

    [Header("Настройки проверки")]
    [Range(0.01f, 0.15f)]
    [Tooltip("Допуск в долях (0.05 = 5%)")]
    [SerializeField] private float tolerance = 0.05f; 

    private void Start()
    {
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(CheckStudentAnswers);
        }
        if (reportText != null) reportText.text = "Ожидание ввода данных...";
    }

    private void CheckStudentAnswers()
    {
        int selectedIndex = blockDropdown.value;
        if (blocksData == null || blocksData.Length <= selectedIndex)
        {
            reportText.text = "<color=red>Ошибка: Данные брусков не настроены!</color>";
            return;
        }

        BlockParameters currentBlock = blocksData[selectedIndex];
        string errorLog = "";

        // Шаг 1: Расчет истинной плотности (p = m / V)
        // Исходные данные: мм и граммы. 
        float trueVolumeCm3 = (currentBlock.trueLength / 10f) * (currentBlock.trueWidth / 10f) * (currentBlock.trueHeight / 10f);
        float trueDensityGcm3 = currentBlock.trueMass / trueVolumeCm3;
        
        // ПЕРЕВОД В СИСТЕМУ СИ (кг/м3), так как 1 г/см3 = 1000 кг/м3
        float trueDensityKgM3 = trueDensityGcm3 * 1000f;

        // Шаг 2 и 3: Проверка всех полей
        // Прямые измерения проверяем в миллиметрах
        bool lValid = ValidateParameter("Длина (L)", currentBlock.trueLength, lengthMeanInput, lengthErrorInput, out string lErr);
        bool wValid = ValidateParameter("Ширина (W)", currentBlock.trueWidth, widthMeanInput, widthErrorInput, out string wErr);
        bool hValid = ValidateParameter("Высота (H)", currentBlock.trueHeight, heightMeanInput, heightErrorInput, out string hErr);
        
        // Косвенное измерение (Плотность) проверяем в системе СИ (кг/м3)
        bool dValid = ValidateParameter("Плотность", trueDensityKgM3, densityMeanInput, densityErrorInput, out string dErr);

        errorLog = lErr + wErr + hErr + dErr;

        // Шаг 4: Экспорт/Вердикт
        if (lValid && wValid && hValid && dValid)
        {
            reportText.text = $"<color=green>УСПЕХ!</color>\nЛабораторная работа по материалу <b>{currentBlock.materialName}</b> выполнена безупречно.\n<size=80%>Все значения в пределах допуска и доверительных интервалов.</size>";
            ExportReportToQuest(currentBlock.materialName, true, "Все проверки пройдены.");
        }
        else
        {
            reportText.text = $"<color=red>В расчетах есть ошибки:</color>\n<size=80%>{errorLog}</size>";
            ExportReportToQuest(currentBlock.materialName, false, errorLog);
        }
    }

    private void ExportReportToQuest(string materialName, bool isSuccess, string logMsg)
    {
        // Формируем уникальное имя файла с датой и временем
        string fileName = $"Lab1_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        
        // persistentDataPath автоматически указывает на разрешенную папку приложения в памяти Quest 3
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        string reportContent = "====================================\n" +
                               "ОТЧЕТ: ЛАБОРАТОРНАЯ РАБОТА №1\n" +
                               "====================================\n" +
                               $"Дата и время: {DateTime.Now}\n" +
                               $"Выбранный образец: {materialName}\n" +
                               $"Статус: {(isSuccess ? "СДАНО" : "НЕ СДАНО")}\n\n" +
                               $"Детали проверки:\n{logMsg}\n" +
                               "====================================";

        // Запись файла
        File.WriteAllText(filePath, reportContent);
        Debug.Log($"[Экспорт] Отчет успешно сохранен в шлем по пути: {filePath}");
    }

    // Универсальный метод проверки по алгоритму из документации
    private bool ValidateParameter(string paramName, float trueValue, TMP_InputField meanInput, TMP_InputField errorInput, out string logMsg)
    {
        logMsg = "";

        if (!float.TryParse(meanInput.text, out float studentMean) || !float.TryParse(errorInput.text, out float studentError))
        {
            logMsg = $"- {paramName}: Пустые поля или неверный формат чисел.\n";
            return false;
        }

        float diff = Mathf.Abs(studentMean - trueValue);

        // Правило 1: Среднее значение должно быть в пределах допуска
        bool isMeanValid = (diff / trueValue) <= tolerance;
        if (!isMeanValid)
        {
            logMsg = $"- {paramName}: Неверное среднее значение (отклонение > {tolerance * 100}%).\n";
            return false;
        }

        // Правило 2: Истинное значение должно попадать в доверительный интервал
        bool isIntervalValid = diff <= studentError;
        if (!isIntervalValid)
        {
            logMsg = $"- {paramName}: Занижена погрешность (интервал не накрывает истинное значение).\n";
            return false;
        }

        // Правило 3: Защита от гигантской погрешности
        if (studentError > trueValue * tolerance)
        {
            logMsg = $"- {paramName}: Погрешность аномально велика.\n";
            return false;
        }

        return true; 
    }
}