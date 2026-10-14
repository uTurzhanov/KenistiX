using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Связующий слой между UI (поля ввода, кнопки, текстовые поля вывода)
/// и логикой симуляции (TrifilarPlatformSimulator, MultifunctionalTimer).
///
/// Использует TextMeshPro-компоненты (TMP_InputField, TMP_Text), которые
/// есть в любом проекте после импорта TMP Essentials
/// (Window -> TextMeshPro -> Import TMP Essential Resources).
/// </summary>
public class SimulationUIController : MonoBehaviour
{
    [Header("Ссылки на логику симуляции")]
    public TrifilarPlatformSimulator platform;
    public MultifunctionalTimer timer;

    [Header("Поля ввода параметров (R, l, r, g)")]
    public TMP_InputField inputR;
    public TMP_InputField inputL;
    public TMP_InputField inputRDisk;
    public TMP_InputField inputG;

    [Header("Кнопки")]
    public Button startButton;
    public Button resetButton;

    [Header("Текстовые поля вывода результата")]
    public TMP_Text theoreticalPeriodText;
    public TMP_Text measuredTimeText;
    public TMP_Text measuredPeriodText;
    public TMP_Text statusText;

    private void Start()
    {
        // Заполняем поля ввода стартовыми значениями из инспектора платформы
        inputR.text = platform.R.ToString("0.###");
        inputL.text = platform.l.ToString("0.###");
        inputRDisk.text = platform.r.ToString("0.###");
        inputG.text = platform.g.ToString("0.###");

        startButton.onClick.AddListener(OnStartClicked);
        if (resetButton != null)
        {
            resetButton.onClick.AddListener(OnResetClicked);
        }

        ApplyParametersFromUI();
        UpdateStatus("Готово к запуску");
    }

    private void Update()
    {
        if (theoreticalPeriodText != null)
        {
            theoreticalPeriodText.text = $"T теор = {platform.Period:0.000} с";
        }

        if (timer.IsRunning)
        {
            UpdateStatus($"Идёт измерение... ({timer.CompletedOscillations}/{timer.oscillationsToCount})");
        }
        else if (timer.IsFinished)
        
        {
            measuredTimeText.text = $"t ({timer.oscillationsToCount} колеб.) = {timer.MeasuredTime:0.000} с";
            measuredPeriodText.text = $"T изм = {timer.MeasuredPeriod:0.000} с";
            UpdateStatus("Измерение завершено");
        }
    }

    public void OnStartClicked()
    {
        ApplyParametersFromUI();
        timer.ResetMeasurement();
        measuredTimeText.text = "";
        measuredPeriodText.text = "";

        platform.StartOscillation();
        timer.StartMeasurement();
        UpdateStatus("Колебания запущены...");
    }

    public void OnResetClicked()
    {
        platform.StopOscillation();
        timer.ResetMeasurement();
        measuredTimeText.text = "";
        measuredPeriodText.text = "";
        UpdateStatus("Готово к запуску");
    }

    private void ApplyParametersFromUI()
    {
        if (float.TryParse(inputR.text, out float rVal)) platform.R = rVal;
        if (float.TryParse(inputL.text, out float lVal)) platform.l = lVal;
        if (float.TryParse(inputRDisk.text, out float rDiskVal)) platform.r = rDiskVal;
        if (float.TryParse(inputG.text, out float gVal)) platform.g = gVal;

        platform.Recalculate();
    }

    private void UpdateStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}
