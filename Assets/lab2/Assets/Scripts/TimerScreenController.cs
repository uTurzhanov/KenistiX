using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Управляет маленьким экраном таймера прямо на 3D-модели устройства:
///
///  1) Режим ввода - пользователь вводит количество колебаний N
///     в TMP_InputField, расположенный на этом же экране.
///  2) После нажатия "Старт" поле ввода прячется, и экран показывает
///     обратный отсчёт: N, N-1, N-2, ... 0 - по одному полному
///     колебанию платформы за раз.
///  3) После завершения измерения экран показывает итоговое время
///     в секундах с 3 знаками после запятой (например, 18.440).
///
/// Использует уже существующие TrifilarPlatformSimulator и
/// MultifunctionalTimer - этот скрипт только управляет тем, что
/// показывается на маленьком экране, и запускает измерение.
///
/// ВАЖНО: если в сцене уже есть другая кнопка "Старт" (например,
/// в SimulationUIController на отдельной Screen Space панели),
/// отключите там вызов platform.StartOscillation()/timer.StartMeasurement(),
/// чтобы не запускать измерение дважды одновременно. Запуск должен
/// происходить только из одного места.
/// </summary>
public class TimerScreenController : MonoBehaviour
{
    private enum ScreenState { Input, Counting, Finished }

    [Header("Ссылки на логику симуляции")]
    public TrifilarPlatformSimulator platform;
    public MultifunctionalTimer timer;

    [Header("UI на экране устройства (World Space Canvas)")]
    public TMP_InputField oscillationsInput;

    public TMP_Text displayText;

    [Header("Настройки по умолчанию")]
    public int defaultOscillations = 10;

    [Tooltip("Максимальный случайный разброс итогового времени в секундах (±). " +
             "Например, 0.05 даёт разброс от -0.050 до +0.050. " +
             "Имитирует погрешность ручной реакции при работе с реальным таймером.")]
    public float maxJitterSeconds = 0.05f;

    private int _targetOscillations;
    private ScreenState _state;

    private void Start()
    {
        _targetOscillations = defaultOscillations;
        oscillationsInput.text = _targetOscillations.ToString();

        // Ограничиваем ввод целыми числами, чтобы нельзя было ввести буквы
        oscillationsInput.contentType = TMP_InputField.ContentType.IntegerNumber;



        SetState(ScreenState.Input);
    }

    private void Update()
    {
        if (_state != ScreenState.Counting) return;

        int remaining = Mathf.Max(0, _targetOscillations - timer.CompletedOscillations);
        displayText.text = remaining.ToString();

        if (timer.IsFinished)
        {
            SetState(ScreenState.Finished);
        }
    }

    /// <summary>
    /// Сбросить экран обратно в режим ввода (показать поле ввода,
    /// скрыть displayText, вернуть N к значению по умолчанию).
    /// Повесьте этот метод вторым слушателем на уже существующую
    /// Reset-кнопку (Inspector -> Button -> OnClick -> "+" -> перетащить
    /// объект с этим скриптом -> выбрать TimerScreenController.ResetScreen).
    /// Также на всякий случай останавливает платформу и таймер -
    /// не повредит, даже если это уже делает другой Reset-обработчик.
    /// </summary>
    public void ResetScreen()
    {
        platform.StopOscillation();
        timer.ResetMeasurement();

        _targetOscillations = defaultOscillations;
        oscillationsInput.text = _targetOscillations.ToString();
        oscillationsInput.interactable = true;

        SetState(ScreenState.Input);
    }

    public void OnStartClicked()
    {
        // Повторное нажатие после показа результата - начать новое измерение
        if (_state == ScreenState.Finished)
        {
            SetState(ScreenState.Input);
            return;
        }

        if (!int.TryParse(oscillationsInput.text, out int n) || n <= 0)
        {
            displayText.gameObject.SetActive(true);
            displayText.text = "Err";
            return;
        }

        _targetOscillations = n;
        timer.oscillationsToCount = _targetOscillations;
        timer.ResetMeasurement();

        platform.StartOscillation();
        timer.StartMeasurement();

        SetState(ScreenState.Counting);
    }

    private void SetState(ScreenState newState)
    {
        _state = newState;

        switch (newState)
        {
            case ScreenState.Input:
                oscillationsInput.gameObject.SetActive(true);
                displayText.gameObject.SetActive(false);
                break;

            case ScreenState.Counting:
                oscillationsInput.gameObject.SetActive(false);
                displayText.gameObject.SetActive(true);
                displayText.text = _targetOscillations.ToString();
                break;

            case ScreenState.Finished:
                oscillationsInput.gameObject.SetActive(false);
                displayText.gameObject.SetActive(true);
                float jitter = UnityEngine.Random.Range(-maxJitterSeconds, maxJitterSeconds);
                float displayedTime = Mathf.Max(0f, timer.MeasuredTime + jitter);
                displayText.text = displayedTime.ToString("0.000");
                break;
        }
    }
}