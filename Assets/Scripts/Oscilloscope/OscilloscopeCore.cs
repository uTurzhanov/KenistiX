using System;
using UnityEngine;

public enum DisplayMode { YT, XY }
public enum CouplingMode { DC, AC, GND }
public enum TriggerEdge { Rising, Falling }
public enum CursorMode { Off, TimeAxis, VoltageAxis }
public enum EditChannel { None, CH1, CH2 }

public enum MathOperation { Add, Subtract, Multiply, Divide }

public class OscilloscopeCore : MonoBehaviour
{
    private float lastSweepTime = 0f;

    [Header("Каналы")]
    [SerializeField] private OscilloscopeChannel ch1 = new OscilloscopeChannel();
    [SerializeField] private OscilloscopeChannel ch2 = new OscilloscopeChannel();

    [Header("Общие крутилки вертикали (Shared)")]
    [SerializeField] private VRKnob sharedVoltsDivKnob;
    [SerializeField] private VRKnob sharedYPosKnob;

    [Header("Горизонтальная развертка")]
    [SerializeField] private VRKnob timeDivKnob;
    [SerializeField] private VRKnob xPosKnob;

    [Header("Кнопки выбора канала")]
    [SerializeField] private VRButton _ch1SelectButton;
    [SerializeField] private VRButton _ch2SelectButton;

    [Header("Блок Триггера (Синхронизация)")]
    [SerializeField] private VRKnob triggerLevelKnob;
    [SerializeField] public TriggerEdge triggerEdge = TriggerEdge.Rising;

    [Header("Режим дисплея")]
    [field: SerializeField] public DisplayMode CurrentMode { get; private set; } = DisplayMode.YT;
    [SerializeField] private VRButton _modeSelectButton;

    [Header("Курсоры")]
    public CursorMode cursorMode = CursorMode.Off;
    [SerializeField] private VRKnob cursor1Knob;
    [SerializeField] private VRKnob cursor2Knob;

    [Header("Настройки экрана")]
    [SerializeField] private OscilloscopeValuesPresentor _valuesPresentor;
    [SerializeField] private int resolution = 512;
    [SerializeField] private float screenGridWidth = 10f;

    [Header("Блок MATH (Математика)")]
    [SerializeField] private MathOperation mathOperation = MathOperation.Add;
    [SerializeField] private VRButton _mathButton;
    public bool IsMathEnabled { get; private set; } = false;
    
    [Header("Состояние прибора")]
    [SerializeField] private VRButton _stopButton;

    private bool _isRunning = true;
    public event Action<Vector3[], Vector3[], Vector3[], CursorMode, float, float> OnGraphReady;

    [field: SerializeField] public ReactableValue<float> CurrentTimeDiv { get; private set; } = new(0.01f);

    private float _currentXPos = 0f;
    private float _currentTriggerLevel = 0f;
    private float _currentCursor1 = -2f;
    private float _currentCursor2 = 2f;

    private Vector3[] bufferCH1;
    private Vector3[] bufferCH2;
    private Vector3[] bufferMath;
    private float[] rawBufferCH1;
    private float[] rawBufferCH2;

    private EditChannel _editChannel = EditChannel.None;

    public void Initialize()
    {
        bufferCH1 = new Vector3[resolution];
        bufferCH2 = new Vector3[resolution];
        bufferMath = new Vector3[resolution];

        rawBufferCH1 = new float[resolution];
        rawBufferCH2 = new float[resolution];

        if (_ch1SelectButton != null) _ch1SelectButton.OnClicked += SelectChannel;
        if (_ch2SelectButton != null) _ch2SelectButton.OnClicked += SelectChannel;

        CheckTheSelection();

        _valuesPresentor.Initailize(ch1.VoltsPerDiv, ch2.VoltsPerDiv, CurrentTimeDiv);

        _stopButton.OnClicked += ToggleRunStop;
        CheckIsStopped();

        _modeSelectButton.OnClicked += ToggleMode;
        CheckIsModeSelect();

        _mathButton.OnClicked += ToggleMath;
        CheckMath();
    }
    private void CheckTheSelection()
    {
        if (_ch1SelectButton == null || _ch2SelectButton == null) return;
        _editChannel = _ch1SelectButton.IsPressed ? EditChannel.CH1 : (_ch2SelectButton.IsPressed ? EditChannel.CH2 : EditChannel.None);
    }

    private void ToggleMath(VRButton vRButton)
    {
        CheckMath();
    }
    private void CheckMath()
    {
        IsMathEnabled = _mathButton.IsPressed;
    }

    private void ToggleMode(VRButton vRButton)
    {
        CheckIsModeSelect();
    }
    private void CheckIsModeSelect()
    {
        CurrentMode = _modeSelectButton.IsPressed ? DisplayMode.XY : DisplayMode.YT;
    }

    private void ToggleRunStop(VRButton vRButton)
    {
        CheckIsStopped();
    }
    private void CheckIsStopped()
    {
        _isRunning = _stopButton.IsPressed == false;
    }

    private void SelectChannel(VRButton vRButton)
    {
        if (!vRButton.IsPressed)
        {
            _editChannel = EditChannel.None;
            return;
        }

        if (vRButton == _ch1SelectButton)
        {
            _editChannel = EditChannel.CH1;
            _ch2SelectButton.SetIsPressed(false);
        }
        else if (vRButton == _ch2SelectButton)
        {
            _editChannel = EditChannel.CH2;
            _ch1SelectButton.SetIsPressed(false);
        }
    }

    void Update()
    {
        if (!ch1.HasSource) return;

        // Чувствительность: сколько единиц прибавляется за 1 градус поворота ручки
        float voltsSens = 0.01f;
        float yPosSens = 0.05f;
        float timeSens = 0.0001f;
        float xPosSens = 0.05f;
        float trigSens = 0.05f;
        float cursorSens = 0.02f;

        // 1. ЛОГИКА ЭНКОДЕРОВ КАНАЛОВ (Volts, Y-Pos)
        if (sharedVoltsDivKnob != null && sharedYPosKnob != null)
        {
            float deltaVolts = sharedVoltsDivKnob.Delta * voltsSens;
            float deltaYPos = sharedYPosKnob.Delta * yPosSens;

            if (_editChannel == EditChannel.CH1)
            {
                ch1.VoltsPerDiv.SetValue(ch1.VoltsPerDiv.CurrentValue + deltaVolts);
                ch1.YOffset.SetValue(ch1.YOffset.CurrentValue + deltaYPos);
                if (ch1.VoltsPerDiv.CurrentValue < 0.01f) 
                    ch1.VoltsPerDiv.SetValue(0.01f); 
            }
            else if (_editChannel == EditChannel.CH2)
            {
                ch2.VoltsPerDiv.SetValue(ch2.VoltsPerDiv.CurrentValue + deltaVolts);
                ch2.YOffset.SetValue(ch2.YOffset.CurrentValue + deltaYPos);
                if (ch2.VoltsPerDiv.CurrentValue < 0.01f)
                    ch2.VoltsPerDiv.SetValue(0.01f);
            }
        }

        // 2. ЛОГИКА БЕСКОНЕЧНЫХ КРУТИЛОК РАЗВЕРТКИ И ТРИГГЕРА
        if (timeDivKnob != null)
        {
            CurrentTimeDiv.SetValue(CurrentTimeDiv.CurrentValue + timeDivKnob.Delta * timeSens);
            CurrentTimeDiv.SetValue(Mathf.Clamp(CurrentTimeDiv.CurrentValue, 0.0005f, 1f));
        }

        if (xPosKnob != null)
        {
            _currentXPos += xPosKnob.Delta * xPosSens;
            _currentXPos = Mathf.Clamp(_currentXPos, -10f, 10f); // Сдвиг +- 10 клеток
        }

        if (triggerLevelKnob != null)
        {
            _currentTriggerLevel += triggerLevelKnob.Delta * trigSens;
            _currentTriggerLevel = Mathf.Clamp(_currentTriggerLevel, -20f, 20f); // Лимит триггера +- 20 Вольт
        }

        // 3. КУРСОРЫ
        if (cursor1Knob != null)
        {
            _currentCursor1 += cursor1Knob.Delta * cursorSens;
            _currentCursor1 = Mathf.Clamp(_currentCursor1, -5f, 5f);
        }
        if (cursor2Knob != null)
        {
            _currentCursor2 += cursor2Knob.Delta * cursorSens;
            _currentCursor2 = Mathf.Clamp(_currentCursor2, -5f, 5f);
        }

        if (_isRunning)
        {
            if (CurrentMode == DisplayMode.YT)
                GenerateYTBuffer();
            else if (CurrentMode == DisplayMode.XY)
                GenerateXYBuffer();
        }

        OnGraphReady?.Invoke(bufferCH1, bufferCH2, bufferMath, cursorMode, _currentCursor1, _currentCursor2);
    }

    private void GenerateYTBuffer()
    {
        float tDiv = CurrentTimeDiv.CurrentValue;
        float xTimeOffset = _currentXPos * tDiv;
        float trigLevel = _currentTriggerLevel;

        float totalTimeOnScreen = screenGridWidth * tDiv;
        float timeStep = totalTimeOnScreen / (resolution - 1);
        float startX = -screenGridWidth / 2f;
        float xStep = screenGridWidth / (resolution - 1);

        float startTime = lastSweepTime;
        float searchStep = totalTimeOnScreen / 200f;
        bool triggerFound = false;

        float currentRealTime = Time.time - xTimeOffset;

        for (int i = 0; i < 300; i++)
        {
            float t1 = currentRealTime - (i * searchStep);
            float t2 = currentRealTime - ((i + 1) * searchStep);

            float v1 = ch1.GetRawVoltage(t1);
            float v2 = ch1.GetRawVoltage(t2);

            if (triggerEdge == TriggerEdge.Rising)
            {
                if (v2 < trigLevel && v1 >= trigLevel)
                {
                    float fraction = (trigLevel - v2) / (v1 - v2);
                    startTime = t2 + (t1 - t2) * fraction;
                    triggerFound = true;
                    break;
                }
            }
            else
            {
                if (v2 > trigLevel && v1 <= trigLevel)
                {
                    float fraction = (trigLevel - v2) / (v1 - v2);
                    startTime = t2 + (t1 - t2) * fraction;
                    triggerFound = true;
                    break;
                }
            }
        }

        if (!triggerFound)
        {
            if (Time.time >= lastSweepTime + totalTimeOnScreen)
            {
                lastSweepTime = Time.time;
            }
            startTime = lastSweepTime - xTimeOffset;
        }
        else
        {
            lastSweepTime = startTime;
        }

        float sumCH1 = 0f, sumCH2 = 0f;

        for (int i = 0; i < resolution; i++)
        {
            float t = startTime + (i * timeStep);

            rawBufferCH1[i] = ch1.GetRawVoltage(t);
            sumCH1 += rawBufferCH1[i];

            if (ch2.HasSource)
            {
                rawBufferCH2[i] = ch2.GetRawVoltage(t);
                sumCH2 += rawBufferCH2[i];
            }
        }

        float avgCH1 = sumCH1 / resolution;
        float avgCH2 = sumCH2 / resolution;

        // --- В ЭТОМ ЦИКЛЕ СЧИТАЮТСЯ И CH1, И CH2, И MATH ---
        for (int i = 0; i < resolution; i++)
        {
            float xPos = startX + (i * xStep);

            // 1. Отрисовка CH1
            bufferCH1[i] = new Vector3(xPos, ch1.GetScreenYPosition(rawBufferCH1[i], avgCH1), 0f);

            // 2. Отрисовка CH2
            bufferCH2[i] = ch2.HasSource ? new Vector3(xPos, ch2.GetScreenYPosition(rawBufferCH2[i], avgCH2), 0f) : Vector3.zero;

            // 3. Отрисовка MATH (только если включена кнопка и подключен второй генератор)
            if (IsMathEnabled && ch2.HasSource)
            {
                // Берем чистые вольты без сдвигов (YOffset), чтобы математика была честной
                float v1 = rawBufferCH1[i] - (ch1.coupling == CouplingMode.AC ? avgCH1 : 0f);
                float v2 = rawBufferCH2[i] - (ch2.coupling == CouplingMode.AC ? avgCH2 : 0f);

                float mathResult = 0f;

                switch (mathOperation)
                {
                    case MathOperation.Add: mathResult = v1 + v2; break;
                    case MathOperation.Subtract: mathResult = v1 - v2; break;
                    case MathOperation.Multiply: mathResult = v1 * v2; break;
                    case MathOperation.Divide: mathResult = (v2 != 0) ? (v1 / v2) : 0f; break; // Защита от деления на ноль
                }

                // Временно привязываем масштаб фиолетовой линии к масштабу CH1
                float yPosMath = mathResult / ch1.VoltsPerDiv.CurrentValue;

                bufferMath[i] = new Vector3(xPos, yPosMath, 0f);
            }
            else
            {
                bufferMath[i] = Vector3.zero; // Скрываем математику
            }
        }
    }

    private void GenerateXYBuffer()
    {
        if (!ch2.HasSource) return;

        // БЕРЕМ ГОТОВОЕ ЗНАЧЕНИЕ РАЗВЕРТКИ
        float tDiv = CurrentTimeDiv.CurrentValue;
        float totalTimeToDraw = screenGridWidth * tDiv;

        float timeStep = totalTimeToDraw / (resolution - 1);
        float startTime = Time.time;

        for (int i = 0; i < resolution; i++)
        {
            float t = startTime + (i * timeStep);

            float rawCH1 = ch1.GetRawVoltage(t);
            float rawCH2 = ch2.GetRawVoltage(t);

            float yPos = ch1.GetScreenYPosition(rawCH1, 0f);
            float xPos = ch2.GetScreenYPosition(rawCH2, 0f);

            bufferCH1[i] = new Vector3(xPos, yPos, 0f);
            bufferCH2[i] = Vector3.zero;
        }
    }

    private void OnDestroy()
    {
        if (_ch1SelectButton != null) 
            _ch1SelectButton.OnClicked -= SelectChannel;

        if (_ch2SelectButton != null) 
            _ch2SelectButton.OnClicked -= SelectChannel;

        if(_stopButton != null) 
            _stopButton.OnClicked -= ToggleRunStop;

        if(_modeSelectButton != null)
            _modeSelectButton.OnClicked -= ToggleMode;

        if(_mathButton != null)
            _mathButton.OnClicked -= ToggleMath;
    }
}