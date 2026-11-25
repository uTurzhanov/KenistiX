using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

public class PlatonusAuth : MonoBehaviour
{
    public const string StudentNamePrefKey = "StudentFullName";
    public const string StudentLoginPrefKey = "StudentLogin";

    public static bool IsAuthenticatedThisSession { get; private set; } = false;

    [Header("UI Toolkit")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Настройки по умолчанию")]
    [SerializeField] private bool startInDarkTheme = false;
    [SerializeField] private float hideDelaySeconds = 0.6f;

    [Header("Позиция VR-клавиатуры (px от низа панели)")]
    [SerializeField] private float keyboardBottomOffset = 20f;

    private const string LoginUrl = "https://platonus.iitu.edu.kz/rest/api/login";
    private const string FioUrl = "https://platonus.iitu.edu.kz/rest/fio/ru";

    private VisualElement _rootContainer;
    private Toggle _themeToggle;
    private TextField _loginField;
    private TextField _passwordField;
    private Button _submitButton;
    private Label _statusLabel;

    private VisualElement _vrKeyboardContainer;
    private Button _hoveredKeyButton;
    private bool _editingPassword = false;
    private bool _isShiftActive = false;
    private bool _isDarkTheme = false;

    private string _actualLogin = string.Empty;
    private string _actualPassword = string.Empty;

    public string UserFullName { get; private set; }
    public event Action<string> OnAuthSuccess;

    private VisualElement _overlayWrapper;

    private static readonly string[][] KeyboardRows = new string[][]
    {
        new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "_" },
        new[] { "q", "w", "e", "r", "t", "y", "u", "i", "o", "p", "@", "." },
        new[] { "a", "s", "d", "f", "g", "h", "j", "k", "l", "!", "#", "$" },
        new[] { "SHIFT", "z", "x", "c", "v", "b", "n", "m", "%", "&", "*", "⌫" }
    };

    private void Awake()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (IsAuthenticatedThisSession)
        {
            HideAuthUIImmediate();
        }
    }

    private void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        var root = uiDocument.rootVisualElement;
        if (root == null) return;

        _overlayWrapper = root.Q<VisualElement>("platonus-login-overlay");

        // Если в этой сессии уже авторизовались — скрываем только оверлей
        if (IsAuthenticatedThisSession)
        {
            HideAuthUIImmediate();
            return;
        }

        if (_overlayWrapper != null)
            _overlayWrapper.style.display = DisplayStyle.Flex;

        // _rootContainer теперь берём из корневого app-root, чтобы тема менялась сразу у всего экрана
        _rootContainer = root.Q<VisualElement>("app-root") ?? root.Q<VisualElement>("login-root-container");
        _themeToggle = root.Q<Toggle>("theme-toggle");
        _loginField = root.Q<TextField>("login-input");
        _passwordField = root.Q<TextField>("password-input");
        _submitButton = root.Q<Button>("login-button");
        _statusLabel = root.Q<Label>("status-label");

        _isDarkTheme = GetGlobalThemeState();

        SetupFieldForInGameKeyboard(_loginField, isPassword: false);
        SetupFieldForInGameKeyboard(_passwordField, isPassword: true);

        BuildEmbeddedVRKeyboard(root);
        ApplyTheme(_isDarkTheme, syncGlobalManager: true);
        SetKeyboardVisible(false);

        if (_themeToggle != null)
        {
            _themeToggle.SetValueWithoutNotify(_isDarkTheme);
            _themeToggle.RegisterValueChangedCallback(OnThemeToggled);
        }

        if (_submitButton != null)
        {
            _submitButton.clicked += OnLoginClicked;
        }

        SetStatus(string.Empty);
    }

    private void OnDisable()
    {
        if (_themeToggle != null)
        {
            _themeToggle.UnregisterValueChangedCallback(OnThemeToggled);
        }

        if (_loginField != null)
        {
            _loginField.UnregisterCallback<PointerDownEvent>(OnLoginFieldClicked, TrickleDown.TrickleDown);
        }

        if (_passwordField != null)
        {
            _passwordField.UnregisterCallback<PointerDownEvent>(OnPasswordFieldClicked, TrickleDown.TrickleDown);
        }

        if (_submitButton != null)
        {
            _submitButton.clicked -= OnLoginClicked;
        }
    }

    private bool GetGlobalThemeState()
    {
        try
        {
            var managerType = typeof(UIThemeManager);
            var prop = managerType.GetProperty("IsDarkTheme") ?? managerType.GetProperty("isDarkTheme") ?? managerType.GetProperty("CurrentTheme");
            if (prop != null && prop.PropertyType == typeof(bool))
            {
                return (bool)prop.GetValue(null);
            }

            var field = managerType.GetField("IsDarkTheme") ?? managerType.GetField("isDarkTheme");
            if (field != null && field.FieldType == typeof(bool))
            {
                return (bool)field.GetValue(null);
            }
        }
        catch { }

        return startInDarkTheme;
    }

    private void OnThemeToggled(ChangeEvent<bool> evt)
    {
        ApplyTheme(evt.newValue, syncGlobalManager: true);
    }

    private void ApplyTheme(bool isDark, bool syncGlobalManager)
    {
        _isDarkTheme = isDark;

        if (syncGlobalManager)
        {
            UIThemeManager.SetTheme(isDark);
        }

        if (_rootContainer != null)
        {
            _rootContainer.EnableInClassList("theme-dark", isDark);
        }

        ApplyKeyboardTheme(isDark);
    }

    private void ApplyKeyboardTheme(bool isDark)
    {
        if (_vrKeyboardContainer == null) return;

        Color panelBg = isDark
            ? new Color(0.11f, 0.12f, 0.15f, 0.97f)
            : new Color(0.95f, 0.96f, 0.98f, 0.98f);

        Color panelBorder = isDark
            ? new Color(0.30f, 0.35f, 0.45f, 0.85f)
            : new Color(0.75f, 0.78f, 0.84f, 0.95f);

        Color keyBorder = isDark
            ? new Color(0.28f, 0.31f, 0.38f, 1f)
            : new Color(0.78f, 0.81f, 0.86f, 1f);

        _vrKeyboardContainer.style.backgroundColor = panelBg;
        _vrKeyboardContainer.style.borderTopColor = panelBorder;
        _vrKeyboardContainer.style.borderBottomColor = panelBorder;
        _vrKeyboardContainer.style.borderLeftColor = panelBorder;
        _vrKeyboardContainer.style.borderRightColor = panelBorder;

        foreach (var btn in _vrKeyboardContainer.Query<Button>().ToList())
        {
            SetButtonBorder(btn, keyBorder);
            ApplyKeyButtonVisualState(btn, isHovered: btn == _hoveredKeyButton);
        }
    }

    /// <summary>
    /// Единый расчёт цвета клавиши в зависимости от темы, состояния hover и активности SHIFT.
    /// </summary>
    private void ApplyKeyButtonVisualState(Button btn, bool isHovered)
    {
        if (btn == null) return;

        // Акцентные кнопки ("Очистить" и "Войти")
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

        // Активный SHIFT
        if (btn.name == "key-SHIFT" && _isShiftActive)
        {
            btn.style.backgroundColor = isHovered
                ? new Color(0.24f, 0.62f, 1.00f, 1f)
                : new Color(0.18f, 0.55f, 0.95f, 1f);
            btn.style.color = Color.white;
            return;
        }

        // Обычные и служебные клавиши:
        // На светлой теме hover = сероватый, на тёмной теме hover = синий
        if (isHovered)
        {
            if (_isDarkTheme)
            {
                btn.style.backgroundColor = new Color(0.15f, 0.40f, 0.78f, 1f); // Синий hover на тёмной теме
                btn.style.color = Color.white;
            }
            else
            {
                btn.style.backgroundColor = new Color(0.82f, 0.85f, 0.90f, 1f); // Сероватый hover на светлой теме
                btn.style.color = new Color(0.08f, 0.10f, 0.14f, 1f);
            }
            return;
        }

        // Обычное (не наведённое) состояние
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

    private void SetButtonBorder(VisualElement el, Color borderColor)
    {
        el.style.borderTopWidth = 1f;
        el.style.borderBottomWidth = 1f;
        el.style.borderLeftWidth = 1f;
        el.style.borderRightWidth = 1f;
        el.style.borderTopColor = borderColor;
        el.style.borderBottomColor = borderColor;
        el.style.borderLeftColor = borderColor;
        el.style.borderRightColor = borderColor;
    }

    private void SetupFieldForInGameKeyboard(TextField field, bool isPassword)
    {
        if (field == null) return;

        field.isReadOnly = true;
        field.focusable = false;
        field.isPasswordField = false;

        if (isPassword)
        {
            field.RegisterCallback<PointerDownEvent>(OnPasswordFieldClicked, TrickleDown.TrickleDown);
        }
        else
        {
            field.RegisterCallback<PointerDownEvent>(OnLoginFieldClicked, TrickleDown.TrickleDown);
        }
    }

    private void OnLoginFieldClicked(PointerDownEvent evt)
    {
        evt.StopImmediatePropagation();
        SetActiveInputField(isPassword: false);
        SetKeyboardVisible(true);
    }

    private void OnPasswordFieldClicked(PointerDownEvent evt)
    {
        evt.StopImmediatePropagation();
        SetActiveInputField(isPassword: true);
        SetKeyboardVisible(true);
    }

    private void SetKeyboardVisible(bool visible)
    {
        if (_vrKeyboardContainer != null)
        {
            _vrKeyboardContainer.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        if (!visible)
        {
            if (_hoveredKeyButton != null)
            {
                var prevHovered = _hoveredKeyButton;
                _hoveredKeyButton = null;
                ApplyKeyButtonVisualState(prevHovered, isHovered: false);
            }

            UpdateFieldVisualHighlight(_loginField, false);
            UpdateFieldVisualHighlight(_passwordField, false);
            RefreshFieldsDisplay(showCursor: false);
        }
    }

    private void SetActiveInputField(bool isPassword)
    {
        _editingPassword = isPassword;
        UpdateFieldVisualHighlight(_loginField, !isPassword);
        UpdateFieldVisualHighlight(_passwordField, isPassword);
        RefreshFieldsDisplay(showCursor: true);
    }

    private void UpdateFieldVisualHighlight(TextField field, bool isActive)
    {
        if (field == null) return;
        var inputBox = field.Q("unity-text-input") ?? field;
        Color borderColor = isActive ? new Color(0.18f, 0.55f, 0.95f, 1f) : new Color(0.5f, 0.5f, 0.5f, 0.4f);
        float borderWidth = isActive ? 3f : 1.5f;

        inputBox.style.borderTopColor = borderColor;
        inputBox.style.borderBottomColor = borderColor;
        inputBox.style.borderLeftColor = borderColor;
        inputBox.style.borderRightColor = borderColor;
        inputBox.style.borderTopWidth = borderWidth;
        inputBox.style.borderBottomWidth = borderWidth;
        inputBox.style.borderLeftWidth = borderWidth;
        inputBox.style.borderRightWidth = borderWidth;
    }

    private void BuildEmbeddedVRKeyboard(VisualElement parent)
    {
        if (parent == null) return;

        var existing = parent.Q<VisualElement>("embedded-vr-keyboard");
        existing?.RemoveFromHierarchy();

        _vrKeyboardContainer = new VisualElement { name = "embedded-vr-keyboard" };

        _vrKeyboardContainer.style.position = Position.Absolute;
        _vrKeyboardContainer.style.bottom = keyboardBottomOffset;
        _vrKeyboardContainer.style.left = Length.Percent(50);
        _vrKeyboardContainer.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);

        _vrKeyboardContainer.style.paddingTop = 10;
        _vrKeyboardContainer.style.paddingBottom = 10;
        _vrKeyboardContainer.style.paddingLeft = 10;
        _vrKeyboardContainer.style.paddingRight = 10;
        _vrKeyboardContainer.style.borderTopLeftRadius = 14;
        _vrKeyboardContainer.style.borderTopRightRadius = 14;
        _vrKeyboardContainer.style.borderBottomLeftRadius = 14;
        _vrKeyboardContainer.style.borderBottomRightRadius = 14;
        _vrKeyboardContainer.style.borderTopWidth = 1.5f;
        _vrKeyboardContainer.style.borderBottomWidth = 1.5f;
        _vrKeyboardContainer.style.borderLeftWidth = 1.5f;
        _vrKeyboardContainer.style.borderRightWidth = 1.5f;

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

            _vrKeyboardContainer.Add(rowEl);
        }

        // Нижний ряд управляющих кнопок
        var bottomRow = new VisualElement();
        bottomRow.style.flexDirection = FlexDirection.Row;
        bottomRow.style.justifyContent = Justify.Center;

        var clearBtn = CreateActionButton("Очистить", 105, "key-clear", () =>
        {
            if (_editingPassword) _actualPassword = string.Empty;
            else _actualLogin = string.Empty;
            RefreshFieldsDisplay(showCursor: true);
        });

        var nextFieldBtn = CreateActionButton("След. поле ⇅", 135, "key-special", () =>
        {
            SetActiveInputField(!_editingPassword);
        });

        var hideBtn = CreateActionButton("Скрыть ⌄", 105, "key-special", () =>
        {
            SetKeyboardVisible(false);
        });

        var enterBtn = CreateActionButton("Войти ↵", 115, "key-enter", OnLoginClicked);

        bottomRow.Add(clearBtn);
        bottomRow.Add(nextFieldBtn);
        bottomRow.Add(hideBtn);
        bottomRow.Add(enterBtn);
        _vrKeyboardContainer.Add(bottomRow);

        parent.Add(_vrKeyboardContainer);
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
            if (_editingPassword)
            {
                if (_actualPassword.Length > 0)
                    _actualPassword = _actualPassword.Substring(0, _actualPassword.Length - 1);
            }
            else
            {
                if (_actualLogin.Length > 0)
                    _actualLogin = _actualLogin.Substring(0, _actualLogin.Length - 1);
            }
            RefreshFieldsDisplay(showCursor: true);
            return;
        }

        string charToInsert = _isShiftActive ? key.ToUpperInvariant() : key.ToLowerInvariant();

        if (_editingPassword)
            _actualPassword += charToInsert;
        else
            _actualLogin += charToInsert;

        if (_isShiftActive)
        {
            _isShiftActive = false;
            UpdateShiftVisuals();
        }

        RefreshFieldsDisplay(showCursor: true);
    }

    private void UpdateShiftVisuals()
    {
        if (_vrKeyboardContainer == null) return;

        var shiftBtn = _vrKeyboardContainer.Q<Button>("key-SHIFT");
        if (shiftBtn != null)
        {
            ApplyKeyButtonVisualState(shiftBtn, isHovered: shiftBtn == _hoveredKeyButton);
        }

        foreach (var btn in _vrKeyboardContainer.Query<Button>("key-char").ToList())
        {
            if (btn.text.Length == 1 && char.IsLetter(btn.text[0]))
            {
                btn.text = _isShiftActive ? btn.text.ToUpperInvariant() : btn.text.ToLowerInvariant();
            }
        }
    }

    private void RefreshFieldsDisplay(bool showCursor = true)
    {
        if (_loginField != null)
        {
            string cursor = (showCursor && !_editingPassword) ? "|" : string.Empty;
            _loginField.SetValueWithoutNotify(_actualLogin + cursor);
        }

        if (_passwordField != null)
        {
            string cursor = (showCursor && _editingPassword) ? "|" : string.Empty;
            _passwordField.SetValueWithoutNotify(new string('•', _actualPassword.Length) + cursor);
        }
    }

    private void HideAuthUIImmediate()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        var overlay = _overlayWrapper ?? uiDocument.rootVisualElement.Q<VisualElement>("platonus-login-overlay");
        if (overlay != null)
        {
            overlay.style.display = DisplayStyle.None;
        }
    }

    private void OnLoginClicked()
    {
        SetKeyboardVisible(false);

        string login = _actualLogin.Trim();
        string password = _actualPassword;

        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
        {
            SetStatus("Введите логин и пароль", isError: true);
            return;
        }

        StartCoroutine(AuthAndGetFioRoutine(login, password));
    }

    private IEnumerator AuthAndGetFioRoutine(string login, string password)
    {
        _submitButton.SetEnabled(false);
        SetStatus("Авторизация в Platonus...");

        string safeLogin = EscapeJson(login);
        string safePassword = EscapeJson(password);
        string jsonBody = $"{{\"login\":\"{safeLogin}\",\"iin\":null,\"icNumber\":null,\"password\":\"{safePassword}\",\"authForDeductedStudentsAndGraduates\":\"false\"}}";

        using (var loginRequest = new UnityWebRequest(LoginUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            loginRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            loginRequest.downloadHandler = new DownloadHandlerBuffer();
            loginRequest.certificateHandler = new BypassCertificateHandler();
            loginRequest.disposeCertificateHandlerOnDispose = true;
            loginRequest.SetRequestHeader("Content-Type", "application/json;charset=UTF-8");
            loginRequest.SetRequestHeader("Accept", "application/json, text/plain, */*");

            yield return loginRequest.SendWebRequest();

            if (loginRequest.result != UnityWebRequest.Result.Success)
            {
                SetStatus($"Ошибка сети ({loginRequest.responseCode}): {loginRequest.error}", isError: true);
                Debug.LogError($"[PlatonusAuth] Login network error: {loginRequest.error} | {loginRequest.downloadHandler.text}");
                _submitButton.SetEnabled(true);
                yield break;
            }

            LoginResponse authData = null;
            try
            {
                authData = JsonUtility.FromJson<LoginResponse>(loginRequest.downloadHandler.text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlatonusAuth] JSON parse error: {ex.Message}");
            }

            if (authData == null || authData.login_status != "success" || string.IsNullOrEmpty(authData.auth_token))
            {
                SetStatus("Неверный логин или пароль", isError: true);
                _submitButton.SetEnabled(true);
                yield break;
            }

            yield return GetFioRoutine(authData.auth_token, authData.sid, login);
        }

        _submitButton.SetEnabled(true);
    }

    private IEnumerator GetFioRoutine(string authToken, string sid, string login)
    {
        SetStatus("Загрузка профиля...");

        using (var fioRequest = UnityWebRequest.Get(FioUrl))
        {
            fioRequest.certificateHandler = new BypassCertificateHandler();
            fioRequest.disposeCertificateHandlerOnDispose = true;
            fioRequest.SetRequestHeader("token", authToken);
            if (!string.IsNullOrEmpty(sid))
            {
                fioRequest.SetRequestHeader("sid", sid);
            }
            fioRequest.SetRequestHeader("Accept", "application/json, text/plain, */*");

            yield return fioRequest.SendWebRequest();

            if (fioRequest.result != UnityWebRequest.Result.Success)
            {
                SetStatus($"Ошибка профиля ({fioRequest.responseCode}): {fioRequest.error}", isError: true);
                Debug.LogError($"[PlatonusAuth] FIO error: {fioRequest.error} | {fioRequest.downloadHandler.text}");
                yield break;
            }

            string rawResponse = fioRequest.downloadHandler.text.Trim();

            if (rawResponse.StartsWith("{"))
            {
                var fioObj = JsonUtility.FromJson<FioResponse>(rawResponse);
                UserFullName = !string.IsNullOrEmpty(fioObj?.fio) ? fioObj.fio : fioObj?.fullName;
            }
            else
            {
                UserFullName = rawResponse.Trim('"');
            }

            if (string.IsNullOrEmpty(UserFullName))
            {
                SetStatus("Не удалось распознать ФИО пользователя", isError: true);
                yield break;
            }

            PlayerPrefs.SetString(StudentNamePrefKey, UserFullName);
            PlayerPrefs.SetString(StudentLoginPrefKey, login);
            PlayerPrefs.Save();

            IsAuthenticatedThisSession = true;

            SetStatus($"Добро пожаловать, {UserFullName}!", isSuccess: true);
            Debug.Log($"[PlatonusAuth] Авторизован и сохранено в PlayerPrefs: {UserFullName} ({login})");

            OnAuthSuccess?.Invoke(UserFullName);

            if (hideDelaySeconds > 0f)
            {
                yield return new WaitForSeconds(hideDelaySeconds);
            }

            HideAuthUIImmediate();
        }
    }

    private void SetStatus(string message, bool isError = false, bool isSuccess = false)
    {
        if (_statusLabel == null) return;

        _statusLabel.text = message;
        _statusLabel.EnableInClassList("error", isError);
        _statusLabel.EnableInClassList("success", isSuccess);
    }

    private string EscapeJson(string str)
    {
        if (string.IsNullOrEmpty(str)) return string.Empty;
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    [Serializable]
    private class LoginResponse
    {
        public string login_status;
        public string sid;
        public string auth_token;
        public int blockMinutesRemaining;
    }

    [Serializable]
    private class FioResponse
    {
        public string fio;
        public string fullName;
    }

    private class BypassCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData) => true;
    }
}