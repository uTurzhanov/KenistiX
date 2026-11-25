using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class DashboardController : MonoBehaviour
{
    [Header("Навигация")]
    [Tooltip("Название сцены главного меню для выхода")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private VisualElement _root;
    private Toggle _themeToggle;
    private Button _sessionBtn;
    private Button _exitBtn;
    private VisualElement _sessionDropdown;

    private void OnEnable()
    {
        UIDocument uiDoc = GetComponent<UIDocument>();
        if (uiDoc == null) return;

        _root = uiDoc.rootVisualElement.Q<VisualElement>("root");
        _themeToggle = _root.Q<Toggle>("theme-toggle");
        _sessionBtn = _root.Q<Button>("session-btn");
        _exitBtn = _root.Q<Button>("exit-app-btn");
        _sessionDropdown = _root.Q<VisualElement>("session-dropdown");

        // 1. Обработка переключения темы и синхронизация
        if (_themeToggle != null)
        {
            _themeToggle.RegisterValueChangedCallback(OnThemeToggled);
        }
        UIThemeManager.OnThemeChanged += ApplyTheme;
        ApplyTheme(UIThemeManager.IsDarkTheme);

        // 2. Отображение / Скрытие Dropdown Меню
        if (_sessionBtn != null && _sessionDropdown != null)
        {
            _sessionBtn.clicked += ToggleDropdown;
        }

        // 3. Кнопка выхода в главное меню
        if (_exitBtn != null)
        {
            _exitBtn.clicked += ExitToMainMenu;
        }

        // 4. Закрытие Dropdown при клике вне его области
        _root.RegisterCallback<PointerDownEvent>(OnRootClicked, TrickleDown.TrickleDown);
    }

    private void OnDisable()
    {
        UIThemeManager.OnThemeChanged -= ApplyTheme;

        if (_themeToggle != null)
            _themeToggle.UnregisterValueChangedCallback(OnThemeToggled);

        if (_sessionBtn != null)
            _sessionBtn.clicked -= ToggleDropdown;

        if (_exitBtn != null)
            _exitBtn.clicked -= ExitToMainMenu;

        if (_root != null)
            _root.UnregisterCallback<PointerDownEvent>(OnRootClicked);
    }

    private void ExitToMainMenu()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogWarning("[DashboardController] Не указано имя сцены главного меню (mainMenuSceneName)!");
            return;
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void OnThemeToggled(ChangeEvent<bool> evt)
    {
        UIThemeManager.SetTheme(evt.newValue);
    }

    private void ApplyTheme(bool isDark)
    {
        if (_root == null) return;

        if (isDark)
            _root.AddToClassList("theme-dark");
        else
            _root.RemoveFromClassList("theme-dark");

        if (_themeToggle != null)
            _themeToggle.SetValueWithoutNotify(isDark);
    }

    private void ToggleDropdown()
    {
        _sessionDropdown.ToggleInClassList("hidden");
    }

    private void OnRootClicked(PointerDownEvent evt)
    {
        // Если выпадающее меню открыто и клик произошел за его пределами
        if (!_sessionDropdown.ClassListContains("hidden"))
        {
            VisualElement target = evt.target as VisualElement;
            if (target != null && !_sessionDropdown.Contains(target) && !_sessionBtn.Contains(target))
            {
                _sessionDropdown.AddToClassList("hidden");
            }
        }
    }
}