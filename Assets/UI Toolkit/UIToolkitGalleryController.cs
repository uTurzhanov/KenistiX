using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class UIToolkitGalleryController : MonoBehaviour
{
    [Header("Ссылка на Capture Service")]
    [SerializeField] private ScreenshotCaptureService captureService;

    [Header("Ручные варианты приборов (Опционально)")]
    [SerializeField] private List<string> inspectorDeviceOptions = new List<string>();

    // Список сохраненных данных для генератора DOCX-отчетов
    public List<ScreenshotData> CapturedData { get; private set; } = new List<ScreenshotData>();

    private VisualElement _root;
    private Button _btnZamery;
    private Button _btnFiksaciya;
    private Button _btnCapture;

    private ScrollView _measurementsScroll;
    private ScrollView _fixationsScroll;
    private DropdownField _deviceDropdown;

    private int _selectedDeviceIndex = 0;

    private void OnEnable()
    {
        UIDocument uiDoc = GetComponent<UIDocument>();
        if (uiDoc == null || uiDoc.rootVisualElement == null) return;

        _root = uiDoc.rootVisualElement;

        _btnZamery = _root.Q<Button>("btn-tab-zamery");
        _btnFiksaciya = _root.Q<Button>("btn-tab-fiksaciya");
        _btnCapture = _root.Q<Button>("btn-capture");

        _measurementsScroll = _root.Q<ScrollView>("measurements-scroll");
        _fixationsScroll = _root.Q<ScrollView>("fixations-scroll");
        _deviceDropdown = _root.Q<DropdownField>("device-dropdown");

        if (_btnZamery != null) _btnZamery.clicked += () => SwitchTab(true);
        if (_btnFiksaciya != null) _btnFiksaciya.clicked += () => SwitchTab(false);

        SetupDeviceDropdown();

        if (_btnCapture != null && captureService != null)
        {
            _btnCapture.clicked += () => captureService.TakeScreenshot(_selectedDeviceIndex);
        }

        SwitchTab(true);
    }

    private void SwitchTab(bool isZamery)
    {
        if (isZamery)
        {
            _btnZamery?.AddToClassList("active");
            _btnFiksaciya?.RemoveFromClassList("active");

            if (_measurementsScroll != null) _measurementsScroll.style.display = DisplayStyle.Flex;
            if (_fixationsScroll != null) _fixationsScroll.style.display = DisplayStyle.None;
        }
        else
        {
            _btnFiksaciya?.AddToClassList("active");
            _btnZamery?.RemoveFromClassList("active");

            if (_fixationsScroll != null) _fixationsScroll.style.display = DisplayStyle.Flex;
            if (_measurementsScroll != null) _measurementsScroll.style.display = DisplayStyle.None;
        }
    }

    private void SetupDeviceDropdown()
    {
        if (_deviceDropdown == null) return;

        List<string> choices = new List<string>();

        if (inspectorDeviceOptions != null && inspectorDeviceOptions.Count > 0)
        {
            choices = new List<string>(inspectorDeviceOptions);
        }
        else if (captureService != null)
        {
            choices = captureService.GetCapturePointNames();
        }

        if (choices.Count > 0)
        {
            _deviceDropdown.choices = choices;
            _deviceDropdown.index = 0;
            _selectedDeviceIndex = 0;

            _deviceDropdown.RegisterValueChangedCallback(evt =>
            {
                _selectedDeviceIndex = _deviceDropdown.choices.IndexOf(evt.newValue);
            });
        }
    }

    public void AddScreenshotEntry(
        Texture2D screenshot,
        string eventTitle,
        string values,
        List<string> separatedValues,
        string sessionTime,
        string timestamp)
    {
        // 1. Сохраняем метаданные в список для экспорта
        CapturedData.Add(new ScreenshotData
        {
            screenshot = screenshot,
            eventTitle = eventTitle,
            values = values,
            separatedValues = separatedValues,
            sessionTime = sessionTime,
            timestamp = timestamp,
            deviceID = _selectedDeviceIndex.ToString()
        });

        // 2. Отображаем во вкладке "ФИКСАЦИЯ"
        if (_fixationsScroll != null)
        {
            VisualElement fixationCard = CreateFixationCard(screenshot, eventTitle, values, sessionTime, timestamp);
            _fixationsScroll.Add(fixationCard);
            _fixationsScroll.ScrollTo(fixationCard);
        }

        // 3. Отображаем во вкладке "ЗАМЕРЫ"
        if (_measurementsScroll != null)
        {
            VisualElement measurementCard = CreateMeasurementCard(eventTitle, values, timestamp);
            _measurementsScroll.Add(measurementCard);
            _measurementsScroll.ScrollTo(measurementCard);
        }
    }

    private VisualElement CreateFixationCard(Texture2D texture, string title, string values, string sessionTime, string timestamp)
    {
        VisualElement card = new VisualElement();
        card.AddToClassList("screenshot-card");

        VisualElement imgPreview = new VisualElement();
        imgPreview.AddToClassList("screenshot-preview");
        imgPreview.style.backgroundImage = new StyleBackground(texture);
        card.Add(imgPreview);

        Label lblTitle = new Label(title);
        lblTitle.AddToClassList("card-title-text");
        card.Add(lblTitle);

        Label lblTime = new Label($"Сессия: {sessionTime} · {timestamp}");
        lblTime.AddToClassList("card-time-text");
        card.Add(lblTime);

        return card;
    }

    private VisualElement CreateMeasurementCard(string title, string values, string timestamp)
    {
        VisualElement card = new VisualElement();
        card.AddToClassList("measurement-card");

        VisualElement header = new VisualElement();
        header.AddToClassList("measurement-header");

        Label lblTitle = new Label(title);
        lblTitle.AddToClassList("measurement-title");
        header.Add(lblTitle);

        Label lblTime = new Label(timestamp);
        lblTime.AddToClassList("measurement-time");
        header.Add(lblTime);

        card.Add(header);

        Label lblValues = new Label(string.IsNullOrEmpty(values) ? "—" : values);
        lblValues.AddToClassList("measurement-values");
        card.Add(lblValues);

        return card;
    }
}