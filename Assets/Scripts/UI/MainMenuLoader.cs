using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenuLoader : MonoBehaviour
{
    [Serializable]
    public class LabWorkInfo
    {
        public string labNum = "0";
        public string badgeTitle = "ЛАБОРАТОРНАЯ РАБОТА №0";
        public string title = "Вводный инструктаж";
        [TextArea(2, 4)]
        public string description = "Описание лабораторной работы...";
        public string duration = "~25 МИНУТ";
        public string devicesCount = "3 ПРИБОРА";
        public bool isAvailable = true;
        public bool isCompleted = false;

        [Header("Скриншот стенда (перетащите Texture2D или Sprite)")]
        public Texture2D previewScreenshot;
    }

    [Header("Настройки сцен и UI")]
    public string LoadSceneName = "210";
    [SerializeField] private UIDocument uiDocument;
    [SerializeField] private PlatonusAuth platonusAuth;

    [Header("Список лабораторных работ (настраивается в Инспекторе)")]
    [SerializeField]
    private List<LabWorkInfo> labs = new List<LabWorkInfo>()
    {
        new LabWorkInfo { labNum = "0", badgeTitle = "ЛАБОРАТОРНАЯ РАБОТА №0", title = "Вводный инструктаж и калибровка приборов", description = "Ознакомление с интерфейсом виртуальной лаборатории, правилами техники безопасности и калибровкой измерительных приборов.", duration = "~15 МИНУТ", devicesCount = "2 ПРИБОРА", isAvailable = true, isCompleted = true },
        new LabWorkInfo { labNum = "1", badgeTitle = "ЛАБОРАТОРНАЯ РАБОТА №1", title = "Ускорение свободного падения", description = "Измерьте длину нити маятника с помощью линейки, проведите серию колебаний с секундомером и рассчитайте значение ускорения свободного падения g.", duration = "~25 МИНУТ", devicesCount = "3 ПРИБОРА", isAvailable = true, isCompleted = false },
        new LabWorkInfo { labNum = "2", badgeTitle = "ЛАБОРАТОРНАЯ РАБОТА №2", title = "Законы электрических цепей постоянного тока", description = "Соберите электрическую цепь, снимите показания амперметра и вольтметра и проверьте закон Ома для участка цепи.", duration = "~30 МИНУТ", devicesCount = "4 ПРИБОРА", isAvailable = true, isCompleted = true },
        new LabWorkInfo { labNum = "3", badgeTitle = "ЛАБОРАТОРНАЯ РАБОТА №3", title = "Магнитное поле соленоида (недоступно)", description = "Работа временно недоступна.", duration = "~25 МИНУТ", devicesCount = "3 ПРИБОРА", isAvailable = false, isCompleted = false },
        new LabWorkInfo { labNum = "4", badgeTitle = "ЛАБОРАТОРНАЯ РАБОТА №4", title = "Определение показателя преломления стекла", description = "Измерьте углы падения и преломления светового пучка на границе воздух-стекло и вычислите показатель преломления.", duration = "~20 МИНУТ", devicesCount = "2 ПРИБОРА", isAvailable = true, isCompleted = false },
        new LabWorkInfo { labNum = "5", badgeTitle = "ЛАБОРАТОРНАЯ РАБОТА №5", title = "Изучение внешнего фотоэффекта (недоступно)", description = "Работа временно недоступна.", duration = "~30 МИНУТ", devicesCount = "3 ПРИБОРА", isAvailable = false, isCompleted = false },
        new LabWorkInfo { labNum = "6", badgeTitle = "ЛАБОРАТОРНАЯ РАБОТА №6", title = "Колебания математического и физического маятников", description = "Исследуйте зависимость периода колебаний от длины подвеса и момента инерции физического маятника.", duration = "~25 МИНУТ", devicesCount = "3 ПРИБОРА", isAvailable = true, isCompleted = false },
        new LabWorkInfo { labNum = "7", badgeTitle = "ЛАБОРАТОРНАЯ РАБОТА №7", title = "Спектральный анализ излучения (недоступно)", description = "Работа временно недоступна.", duration = "~25 МИНУТ", devicesCount = "2 ПРИБОРА", isAvailable = false, isCompleted = false }
    };

    private VisualElement _appRoot;
    private Toggle _themeToggle;
    private Label _studentFioLabel;
    private ScrollView _labsScrollView;

    // Элементы правого превью
    private VisualElement _previewScreenshot;
    private Label _previewBadge;
    private Label _previewTitle;
    private Label _previewDesc;
    private Label _previewDuration;
    private Label _previewDevices;
    private Button _startLabBtn;

    // Кнопки фильтров
    private Button _filterAllBtn;
    private Button _filterAvailableBtn;
    private Button _filterCompletedBtn;

    private int _selectedLabIndex = 1;
    private string _currentFilter = "ALL"; // ALL, AVAILABLE, COMPLETED

    private void OnEnable()
    {
        if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
        if (platonusAuth == null) platonusAuth = GetComponent<PlatonusAuth>();

        var root = uiDocument.rootVisualElement;
        if (root == null) return;

        _appRoot = root.Q<VisualElement>("app-root");
        _themeToggle = root.Q<Toggle>("dashboard-theme-toggle");
        _studentFioLabel = root.Q<Label>("student-fio-label");
        _labsScrollView = root.Q<ScrollView>("labs-list-scroll");

        _previewScreenshot = root.Q<VisualElement>("preview-screenshot");
        _previewBadge = root.Q<Label>("preview-lab-badge");
        _previewTitle = root.Q<Label>("preview-lab-title");
        _previewDesc = root.Q<Label>("preview-lab-desc");
        _previewDuration = root.Q<Label>("preview-duration");
        _previewDevices = root.Q<Label>("preview-devices");
        _startLabBtn = root.Q<Button>("start-lab-btn");

        _filterAllBtn = root.Q<Button>("filter-all-btn");
        _filterAvailableBtn = root.Q<Button>("filter-available-btn");
        _filterCompletedBtn = root.Q<Button>("filter-completed-btn");

        // Подписка на успешную авторизацию из PlatonusAuth
        if (platonusAuth != null)
        {
            platonusAuth.OnAuthSuccess += UpdateStudentFio;
        }

        // Подгружаем ФИО, если студент уже входил ранее в этой сессии
        string savedFio = PlayerPrefs.GetString(PlatonusAuth.StudentNamePrefKey, string.Empty);
        if (!string.IsNullOrEmpty(savedFio))
        {
            UpdateStudentFio(savedFio);
        }

        // Переключатель темы главного меню
        if (_themeToggle != null)
        {
            bool isDark = _appRoot != null && _appRoot.ClassListContains("theme-dark");
            _themeToggle.SetValueWithoutNotify(isDark);
            _themeToggle.RegisterValueChangedCallback(evt =>
            {
                _appRoot?.EnableInClassList("theme-dark", evt.newValue);
                UIThemeManager.SetTheme(evt.newValue);
            });
        }

        // Фильтры сайдбара
        _filterAllBtn?.RegisterCallback<ClickEvent>(_ => SetFilter("ALL"));
        _filterAvailableBtn?.RegisterCallback<ClickEvent>(_ => SetFilter("AVAILABLE"));
        _filterCompletedBtn?.RegisterCallback<ClickEvent>(_ => SetFilter("COMPLETED"));

        // Кнопки запуска и выхода
        if (_startLabBtn != null) _startLabBtn.clicked += OnStartSelectedLab;
        var exitBtn = root.Q<Button>("exit-app-btn");
        if (exitBtn != null) exitBtn.clicked += ClseApplication;

        RebuildLabsList();
        SelectLab(_selectedLabIndex);
    }

    private void OnDisable()
    {
        if (platonusAuth != null)
            platonusAuth.OnAuthSuccess -= UpdateStudentFio;

        if (_startLabBtn != null)
            _startLabBtn.clicked -= OnStartSelectedLab;
    }

    private void UpdateStudentFio(string fullName)
    {
        if (_studentFioLabel != null && !string.IsNullOrEmpty(fullName))
        {
            _studentFioLabel.text = fullName.ToUpperInvariant();
        }
    }

    private void SetFilter(string filter)
    {
        _currentFilter = filter;
        _filterAllBtn?.EnableInClassList("active", filter == "ALL");
        _filterAvailableBtn?.EnableInClassList("active", filter == "AVAILABLE");
        _filterCompletedBtn?.EnableInClassList("active", filter == "COMPLETED");
        RebuildLabsList();
    }

    private void RebuildLabsList()
    {
        if (_labsScrollView == null) return;
        _labsScrollView.Clear();

        for (int i = 0; i < labs.Count; i++)
        {
            var lab = labs[i];

            if (_currentFilter == "AVAILABLE" && !lab.isAvailable) continue;
            if (_currentFilter == "COMPLETED" && !lab.isCompleted) continue;

            int capturedIndex = i;
            var itemBtn = new VisualElement();
            itemBtn.AddToClassList("lab-list-item");
            if (i == _selectedLabIndex) itemBtn.AddToClassList("selected");
            if (!lab.isAvailable) itemBtn.AddToClassList("locked");

            var leftBox = new VisualElement();
            leftBox.AddToClassList("lab-item-left");

            var dot = new VisualElement();
            dot.AddToClassList("lab-status-dot");
            if (!lab.isAvailable) dot.AddToClassList("locked");
            else if (lab.isCompleted) dot.AddToClassList("completed");

            var textCol = new VisualElement();
            textCol.AddToClassList("lab-item-texts");

            var numLabel = new Label(lab.badgeTitle);
            numLabel.AddToClassList("lab-item-number");

            var subLabel = new Label(lab.title);
            subLabel.AddToClassList("lab-item-subtitle");

            textCol.Add(numLabel);
            textCol.Add(subLabel);
            leftBox.Add(dot);
            leftBox.Add(textCol);

            var rightIcon = new Label(lab.isAvailable ? "›" : "🔒");
            rightIcon.AddToClassList("lab-item-arrow");

            itemBtn.Add(leftBox);
            itemBtn.Add(rightIcon);

            itemBtn.RegisterCallback<ClickEvent>(_ => SelectLab(capturedIndex));
            _labsScrollView.Add(itemBtn);
        }
    }

    private void SelectLab(int index)
    {
        if (index < 0 || index >= labs.Count) return;
        _selectedLabIndex = index;
        var lab = labs[index];

        // Сохраняем номер выбранной лабы так же, как делали ваши методы SetLabNumX()
        PlayerPrefs.SetString("LabNum", lab.labNum);

        // Обновляем выделение в списке
        RebuildLabsList();

        // Заполняем правую карточку превью
        if (_previewBadge != null) _previewBadge.text = lab.badgeTitle;
        if (_previewTitle != null) _previewTitle.text = lab.title;
        if (_previewDesc != null) _previewDesc.text = lab.description;
        if (_previewDuration != null) _previewDuration.text = $"◷ {lab.duration}";
        if (_previewDevices != null) _previewDevices.text = $"🔧 {lab.devicesCount}";

        // Подстановка скриншота стенда из инспектора
        if (_previewScreenshot != null)
        {
            if (lab.previewScreenshot != null)
                _previewScreenshot.style.backgroundImage = new StyleBackground(lab.previewScreenshot);
            else
                _previewScreenshot.style.backgroundImage = StyleKeyword.None;
        }

        if (_startLabBtn != null)
        {
            _startLabBtn.SetEnabled(lab.isAvailable);
            _startLabBtn.text = lab.isAvailable
                ? "НАЖМИТЕ НА КНОПКУ, ЧТОБЫ НАЧАТЬ →"
                : "РАБОТА ВРЕМЕННО НЕДОСТУПНА";
        }
    }

    private void OnStartSelectedLab()
    {
        if (_selectedLabIndex >= 0 && _selectedLabIndex < labs.Count && !labs[_selectedLabIndex].isAvailable)
            return;

        LoadScene();
    }

    public void LoadScene()
    {
        SceneManager.LoadScene(LoadSceneName);
    }

    public void ClseApplication()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}