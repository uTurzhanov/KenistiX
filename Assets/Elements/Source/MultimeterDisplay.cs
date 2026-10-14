using TMPro;
using UnityEngine;

public class MultimeterDisplay : MonoBehaviour
{
    public TextMeshProUGUI ScreenText;

    private const string OVERFLOW_TEXT = "1.000";

    [Header("Симуляция АЦП и LCD-экрана")]
    [Tooltip("Скорость, с которой внутренняя математика 'догоняет' реальный ток")]
    public float SmoothSpeed = 6f;

    [Tooltip("Частота обновления экрана в секундах (0.2 = 5 раз в секунду). Не дает цифрам сливаться в кашу.")]
    public float RefreshRate = 0.2f;

    private float _targetValue;
    private float _currentDisplayedValue;
    private bool _isOverflow;
    private bool _isStatic;

    private string _currentFormat = "F2";

    // Раздельные границы для асимметричных скачков (например, от -0.002 до +0.006)
    private float _noiseMin = 0f;
    private float _noiseMax = 0f;

    private float _timer = 0f;

    private void Update()
    {
        if (_isStatic) return;

        if (_isOverflow)
        {
            ScreenText.text = (_targetValue < 0 ? "-" : "") + OVERFLOW_TEXT;
            return;
        }

        // 1. Внутреннее значение плавно стремится к цели каждый кадр
        _currentDisplayedValue = Mathf.Lerp(_currentDisplayedValue, _targetValue, Time.deltaTime * SmoothSpeed);

        // 2. Вывод на экран срабатывает только по таймеру (симуляция герцовки дисплея)
        _timer += Time.deltaTime;
        if (_timer >= RefreshRate)
        {
            _timer = 0f; // Сброс таймера

            // Стабилизация нуля (чтобы разорванная цепь показывала 000.00, а не шум)
            if (Mathf.Abs(_targetValue) < 0.0001f && Mathf.Abs(_currentDisplayedValue) < 0.005f)
            {
                _currentDisplayedValue = 0f;
                ScreenText.text = (0f).ToString(_currentFormat);
                return;
            }

            // 3. Генерируем скачок в заданных границах
            float noise = Random.Range(_noiseMin, _noiseMax);
            float finalValue = _currentDisplayedValue + noise;

            ScreenText.text = finalValue.ToString(_currentFormat);
        }
    }

    // Вывод для блока питания
    public void DisplayStaticVoltage(float voltage)
    {
        _isStatic = true;
        ScreenText.text = voltage.ToString("F2") + " V";
    }

    // Настройка Вольтметра
    public void DisplayVoltage(float voltage, float limit)
    {
        _isStatic = false;
        _targetValue = voltage;

        if (Mathf.Abs(voltage) > limit)
        {
            _isOverflow = true;
        }
        else
        {
            _isOverflow = false;
            _currentFormat = "0.000";

            _noiseMin = -0.003f;
            _noiseMax = 0.006f;
        }
    }

    // Настройка Амперметра
    public void DisplayCurrent(float currentInAmperes, float limitInAmperes, string unit)
    {
        _isStatic = false;

        if (Mathf.Abs(currentInAmperes) > limitInAmperes)
        {
            _isOverflow = true;
            _targetValue = currentInAmperes;
        }
        else
        {
            _isOverflow = false;

            float value = currentInAmperes;
            if (unit == "mA")
            {
                value *= 1000f;
                _currentFormat = "000.00";
                _noiseMin = -0.02f;
                _noiseMax = 0.05f;
            }
            else if (unit == "uA")
            {
                value *= 1000000f;
                _currentFormat = "0000.0";
                _noiseMin = -0.2f;
                _noiseMax = 0.5f;
            }

            _targetValue = value;
        }
    }

    // Вывод пустого экрана или нулей, когда цепь не собрана (решает вашу ошибку)
    public void DisplayZero()
    {
        _isStatic = true; // Останавливаем математику плавающих цифр и шума
        _targetValue = 0f;
        _currentDisplayedValue = 0f;
        ScreenText.text = "0.00"; // Моментально выводим дефолтные нули
    }
}