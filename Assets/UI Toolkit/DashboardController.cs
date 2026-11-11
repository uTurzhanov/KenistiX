using UnityEngine;
using UnityEngine.UIElements;

public class DashboardController : MonoBehaviour
{
    private VisualElement _root;
    private Toggle _themeToggle;
    private Button _sessionBtn;
    private VisualElement _sessionDropdown;

    private void OnEnable()
    {
        UIDocument uiDoc = GetComponent<UIDocument>();
        if (uiDoc == null) return;

        _root = uiDoc.rootVisualElement.Q<VisualElement>("root");
        _themeToggle = _root.Q<Toggle>("theme-toggle");
        _sessionBtn = _root.Q<Button>("session-btn");
        _sessionDropdown = _root.Q<VisualElement>("session-dropdown");

        // 1. Обработка переключения темы
        if (_themeToggle != null)
        {
            _themeToggle.RegisterValueChangedCallback(OnThemeToggled);
        }

        // 2. Отображение / Скрытие Dropdown Меню
        if (_sessionBtn != null && _sessionDropdown != null)
        {
            _sessionBtn.clicked += ToggleDropdown;
        }

        // 3. Закрытие Dropdown при клике вне его области
        _root.RegisterCallback<PointerDownEvent>(OnRootClicked, TrickleDown.TrickleDown);
    }

    private void OnDisable()
    {
        if (_themeToggle != null)
            _themeToggle.UnregisterValueChangedCallback(OnThemeToggled);

        if (_sessionBtn != null)
            _sessionBtn.clicked -= ToggleDropdown;

        if (_root != null)
            _root.UnregisterCallback<PointerDownEvent>(OnRootClicked);
    }

    private void OnThemeToggled(ChangeEvent<bool> evt)
    {
        if (evt.newValue)
        {
            _root.AddToClassList("theme-dark");
        }
        else
        {
            _root.RemoveFromClassList("theme-dark");
        }
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