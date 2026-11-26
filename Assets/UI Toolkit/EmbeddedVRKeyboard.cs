using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class EmbeddedVRKeyboard
{
    public struct Config
    {
        public float bottomOffset;
        public bool showNextFieldButton;
        public string submitButtonText;
        public Action onSubmit;
        public Action onHide;
        public Action onNextField;
        public Action onClear;
        public Action<string> onCharacterTyped;
        public Action onBackspace;
    }

    private static readonly string[][] KeyboardRows = new string[][]
    {
        new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "_" },
        new[] { "q", "w", "e", "r", "t", "y", "u", "i", "o", "p", "@", "." },
        new[] { "a", "s", "d", "f", "g", "h", "j", "k", "l", "!", "#", "$" },
        new[] { "SHIFT", "z", "x", "c", "v", "b", "n", "m", "%", "&", "*", "⌫" }
    };

    private readonly VisualElement _container;
    private readonly VisualElement _previewBar;
    private readonly Label _previewFieldLabel;
    private readonly Label _previewValueLabel;
    private readonly Config _config;

    private Button _hoveredKeyButton;
    private bool _isShiftActive;
    private bool _isDarkTheme;

    public bool IsVisible => _container != null && _container.style.display == DisplayStyle.Flex;

    public EmbeddedVRKeyboard(VisualElement parent, Config config)
    {
        _config = config;

        var existing = parent.Q<VisualElement>("embedded-vr-keyboard");
        existing?.RemoveFromHierarchy();

        _container = new VisualElement { name = "embedded-vr-keyboard" };
        _container.style.position = Position.Absolute;
        _container.style.bottom = config.bottomOffset;
        _container.style.left = Length.Percent(50);
        _container.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
        _container.style.paddingTop = 10;
        _container.style.paddingBottom = 10;
        _container.style.paddingLeft = 10;
        _container.style.paddingRight = 10;
        _container.style.borderTopLeftRadius = 14;
        _container.style.borderTopRightRadius = 14;
        _container.style.borderBottomLeftRadius = 14;
        _container.style.borderBottomRightRadius = 14;
        _container.style.borderTopWidth = 1.5f;
        _container.style.borderBottomWidth = 1.5f;
        _container.style.borderLeftWidth = 1.5f;
        _container.style.borderRightWidth = 1.5f;

        // --- ВЕРХНЯЯ ПАНЕЛЬ ПРЕДПРОСМОТРА ВВОДА (чтобы видеть ввод даже если поле перекрыто) ---
        _previewBar = new VisualElement { name = "keyboard-preview-bar" };
        _previewBar.style.flexDirection = FlexDirection.Row;
        _previewBar.style.alignItems = Align.Center;
        _previewBar.style.justifyContent = Justify.SpaceBetween;
        _previewBar.style.height = 38;
        _previewBar.style.marginBottom = 8;
        _previewBar.style.paddingLeft = 12;
        _previewBar.style.paddingRight = 12;
        _previewBar.style.borderTopLeftRadius = 9;
        _previewBar.style.borderTopRightRadius = 9;
        _previewBar.style.borderBottomLeftRadius = 9;
        _previewBar.style.borderBottomRightRadius = 9;
        _previewBar.style.borderTopWidth = 1f;
        _previewBar.style.borderBottomWidth = 1f;
        _previewBar.style.borderLeftWidth = 1f;
        _previewBar.style.borderRightWidth = 1f;

        _previewFieldLabel = new Label("ПОЛЕ:") { name = "kbd-preview-field-name" };
        _previewFieldLabel.style.fontSize = 13;
        _previewFieldLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _previewFieldLabel.style.marginRight = 10;
        _previewFieldLabel.style.flexShrink = 0;

        _previewValueLabel = new Label("|") { name = "kbd-preview-value" };
        _previewValueLabel.style.fontSize = 16;
        _previewValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _previewValueLabel.style.flexGrow = 1;
        _previewValueLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        _previewValueLabel.style.overflow = Overflow.Hidden;

        _previewBar.Add(_previewFieldLabel);
        _previewBar.Add(_previewValueLabel);
        _container.Add(_previewBar);

        // --- РЯДЫ КЛАВИШ ---
        foreach (var rowKeys in KeyboardRows)
        {
            var rowEl = new VisualElement();
            rowEl.style.flexDirection = FlexDirection.Row;
            rowEl.style.justifyContent = Justify.Center;
            rowEl.style.marginBottom = 6;

            foreach (var key in rowKeys)
            {
                var btn = new Button();
                bool isSpecial = key == "SHIFT" || key == "⌫";
                btn.name = key == "SHIFT" ? "key-SHIFT" : (isSpecial ? "key-special" : "key-char");
                btn.text = key;

                btn.style.minWidth = isSpecial ? 62 : 42;
                btn.style.height = 42;
                btn.style.marginLeft = 3;
                btn.style.marginRight = 3;
                btn.style.fontSize = 18;
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                btn.style.borderTopLeftRadius = 8;
                btn.style.borderTopRightRadius = 8;
                btn.style.borderBottomLeftRadius = 8;
                btn.style.borderBottomRightRadius = 8;

                RegisterKeyHoverCallbacks(btn);

                string capturedKey = key;
                btn.clicked += () => OnVirtualKeyPressed(capturedKey);
                rowEl.Add(btn);
            }

            _container.Add(rowEl);
        }

        // --- НИЖНИЙ РЯД УПРАВЛЯЮЩИХ КНОПОК ---
        var bottomRow = new VisualElement();
        bottomRow.style.flexDirection = FlexDirection.Row;
        bottomRow.style.justifyContent = Justify.Center;

        var clearBtn = CreateActionButton("Очистить", 105, "key-clear", () => _config.onClear?.Invoke());
        bottomRow.Add(clearBtn);

        if (_config.showNextFieldButton)
        {
            var nextFieldBtn = CreateActionButton("След. поле ⇅", 135, "key-special", () => _config.onNextField?.Invoke());
            bottomRow.Add(nextFieldBtn);
        }

        var hideBtn = CreateActionButton("Скрыть ⌄", 105, "key-special", () =>
        {
            SetVisible(false);
            _config.onHide?.Invoke();
        });
        bottomRow.Add(hideBtn);

        if (!string.IsNullOrEmpty(_config.submitButtonText))
        {
            var enterBtn = CreateActionButton(_config.submitButtonText, 115, "key-enter", () => _config.onSubmit?.Invoke());
            bottomRow.Add(enterBtn);
        }

        _container.Add(bottomRow);
        parent.Add(_container);
    }

    public void SetVisible(bool visible)
    {
        if (_container != null)
            _container.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (!visible && _hoveredKeyButton != null)
        {
            var prev = _hoveredKeyButton;
            _hoveredKeyButton = null;
            ApplyKeyButtonVisualState(prev, isHovered: false);
        }
    }

    /// <summary>
    /// Обновляет верхнюю строку предпросмотра над клавиатурой.
    /// </summary>
    public void UpdatePreview(string fieldCaption, string displayValueWithCursor)
    {
        if (_previewFieldLabel != null)
            _previewFieldLabel.text = fieldCaption;

        if (_previewValueLabel != null)
            _previewValueLabel.text = displayValueWithCursor;
    }

    public void ApplyTheme(bool isDark)
    {
        _isDarkTheme = isDark;
        if (_container == null) return;

        Color panelBg = isDark
            ? new Color(0.11f, 0.12f, 0.15f, 0.97f)
            : new Color(0.95f, 0.96f, 0.98f, 0.98f);

        Color panelBorder = isDark
            ? new Color(0.30f, 0.35f, 0.45f, 0.85f)
            : new Color(0.75f, 0.78f, 0.84f, 0.95f);

        Color keyBorder = isDark
            ? new Color(0.28f, 0.31f, 0.38f, 1f)
            : new Color(0.78f, 0.81f, 0.86f, 1f);

        _container.style.backgroundColor = panelBg;
        SetBorder(_container, 1.5f, panelBorder);

        if (_previewBar != null)
        {
            _previewBar.style.backgroundColor = isDark
                ? new Color(0.16f, 0.19f, 0.25f, 1f)
                : new Color(1.00f, 1.00f, 1.00f, 1f);
            SetBorder(_previewBar, 1f, new Color(0.18f, 0.55f, 0.95f, 0.75f));
        }

        if (_previewFieldLabel != null)
            _previewFieldLabel.style.color = new Color(0.18f, 0.55f, 0.95f, 1f);

        if (_previewValueLabel != null)
            _previewValueLabel.style.color = isDark ? Color.white : new Color(0.10f, 0.12f, 0.16f, 1f);

        foreach (var btn in _container.Query<Button>().ToList())
        {
            SetBorder(btn, 1f, keyBorder);
            ApplyKeyButtonVisualState(btn, isHovered: btn == _hoveredKeyButton);
        }
    }

    private Button CreateActionButton(string label, float width, string btnName, Action onClick)
    {
        var btn = new Button(onClick) { text = label, name = btnName };
        btn.style.width = width;
        btn.style.height = 42;
        btn.style.marginLeft = 4;
        btn.style.marginRight = 4;
        btn.style.fontSize = 15;
        btn.style.unityFontStyleAndWeight = FontStyle.Bold;
        btn.style.borderTopLeftRadius = 8;
        btn.style.borderTopRightRadius = 8;
        btn.style.borderBottomLeftRadius = 8;
        btn.style.borderBottomRightRadius = 8;

        RegisterKeyHoverCallbacks(btn);
        return btn;
    }

    private void OnVirtualKeyPressed(string key)
    {
        if (key == "SHIFT")
        {
            _isShiftActive = !_isShiftActive;
            UpdateShiftVisuals();
            return;
        }

        if (key == "⌫")
        {
            _config.onBackspace?.Invoke();
            return;
        }

        string charToInsert = _isShiftActive ? key.ToUpperInvariant() : key.ToLowerInvariant();
        _config.onCharacterTyped?.Invoke(charToInsert);

        if (_isShiftActive)
        {
            _isShiftActive = false;
            UpdateShiftVisuals();
        }
    }

    private void UpdateShiftVisuals()
    {
        if (_container == null) return;

        var shiftBtn = _container.Q<Button>("key-SHIFT");
        if (shiftBtn != null)
            ApplyKeyButtonVisualState(shiftBtn, isHovered: shiftBtn == _hoveredKeyButton);

        foreach (var btn in _container.Query<Button>("key-char").ToList())
        {
            if (btn.text.Length == 1 && char.IsLetter(btn.text[0]))
                btn.text = _isShiftActive ? btn.text.ToUpperInvariant() : btn.text.ToLowerInvariant();
        }
    }

    private void ApplyKeyButtonVisualState(Button btn, bool isHovered)
    {
        if (btn == null) return;

        if (btn.name == "key-clear")
        {
            btn.style.backgroundColor = isHovered
                ? new Color(0.86f, 0.28f, 0.28f, 1f)
                : new Color(0.75f, 0.24f, 0.24f, 1f);
            btn.style.color = Color.white;
            return;
        }

        if (btn.name == "key-enter")
        {
            btn.style.backgroundColor = isHovered
                ? new Color(0.20f, 0.66f, 0.40f, 1f)
                : new Color(0.16f, 0.56f, 0.34f, 1f);
            btn.style.color = Color.white;
            return;
        }

        if (btn.name == "key-SHIFT" && _isShiftActive)
        {
            btn.style.backgroundColor = isHovered
                ? new Color(0.24f, 0.62f, 1.00f, 1f)
                : new Color(0.18f, 0.55f, 0.95f, 1f);
            btn.style.color = Color.white;
            return;
        }

        if (isHovered)
        {
            if (_isDarkTheme)
            {
                btn.style.backgroundColor = new Color(0.15f, 0.40f, 0.78f, 1f);
                btn.style.color = Color.white;
            }
            else
            {
                btn.style.backgroundColor = new Color(0.82f, 0.85f, 0.90f, 1f);
                btn.style.color = new Color(0.08f, 0.10f, 0.14f, 1f);
            }
            return;
        }

        bool isSpecial = btn.name == "key-special" || btn.name == "key-SHIFT";
        if (_isDarkTheme)
        {
            btn.style.backgroundColor = isSpecial
                ? new Color(0.25f, 0.30f, 0.38f, 1f)
                : new Color(0.20f, 0.22f, 0.27f, 1f);
            btn.style.color = Color.white;
        }
        else
        {
            btn.style.backgroundColor = isSpecial
                ? new Color(0.88f, 0.90f, 0.94f, 1f)
                : new Color(1.00f, 1.00f, 1.00f, 1f);
            btn.style.color = new Color(0.12f, 0.14f, 0.18f, 1f);
        }
    }

    private void RegisterKeyHoverCallbacks(Button btn)
    {
        btn.RegisterCallback<PointerEnterEvent>(_ =>
        {
            _hoveredKeyButton = btn;
            ApplyKeyButtonVisualState(btn, isHovered: true);
        });

        btn.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            if (_hoveredKeyButton == btn) _hoveredKeyButton = null;
            ApplyKeyButtonVisualState(btn, isHovered: false);
        });
    }

    private void SetBorder(VisualElement el, float width, Color color)
    {
        el.style.borderTopWidth = width;
        el.style.borderBottomWidth = width;
        el.style.borderLeftWidth = width;
        el.style.borderRightWidth = width;
        el.style.borderTopColor = color;
        el.style.borderBottomColor = color;
        el.style.borderLeftColor = color;
        el.style.borderRightColor = color;
    }
}