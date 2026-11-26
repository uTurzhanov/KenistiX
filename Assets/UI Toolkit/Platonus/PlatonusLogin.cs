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
    private VisualElement _overlayWrapper;
    private Toggle _themeToggle;
    private TextField _loginField;
    private TextField _passwordField;
    private Button _submitButton;
    private Label _statusLabel;

    private EmbeddedVRKeyboard _vrKeyboard;
    private bool _editingPassword = false;
    private bool _isDarkTheme = false;

    private string _actualLogin = string.Empty;
    private string _actualPassword = string.Empty;

    public string UserFullName { get; private set; }
    public event Action<string> OnAuthSuccess;

    private void Awake()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        if (IsAuthenticatedThisSession)
            HideAuthUIImmediate();
    }

    private void OnEnable()
    {
        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>();

        var root = uiDocument.rootVisualElement;
        if (root == null) return;

        _overlayWrapper = root.Q<VisualElement>("platonus-login-overlay");

        if (IsAuthenticatedThisSession)
        {
            HideAuthUIImmediate();
            return;
        }

        if (_overlayWrapper != null)
            _overlayWrapper.style.display = DisplayStyle.Flex;

        _rootContainer = root.Q<VisualElement>("app-root") ?? root.Q<VisualElement>("login-root-container");
        _themeToggle = root.Q<Toggle>("theme-toggle");
        _loginField = root.Q<TextField>("login-input");
        _passwordField = root.Q<TextField>("password-input");
        _submitButton = root.Q<Button>("login-button");
        _statusLabel = root.Q<Label>("status-label");

        _isDarkTheme = GetGlobalThemeState();

        SetupFieldForInGameKeyboard(_loginField, isPassword: false);
        SetupFieldForInGameKeyboard(_passwordField, isPassword: true);

        _vrKeyboard = new EmbeddedVRKeyboard(root, new EmbeddedVRKeyboard.Config
        {
            bottomOffset = keyboardBottomOffset,
            showNextFieldButton = true,
            submitButtonText = "Войти ↵",
            onCharacterTyped = ch =>
            {
                if (_editingPassword) _actualPassword += ch;
                else _actualLogin += ch;
                RefreshFieldsDisplay(showCursor: true);
            },
            onBackspace = () =>
            {
                if (_editingPassword && _actualPassword.Length > 0)
                    _actualPassword = _actualPassword.Substring(0, _actualPassword.Length - 1);
                else if (!_editingPassword && _actualLogin.Length > 0)
                    _actualLogin = _actualLogin.Substring(0, _actualLogin.Length - 1);
                RefreshFieldsDisplay(showCursor: true);
            },
            onClear = () =>
            {
                if (_editingPassword) _actualPassword = string.Empty;
                else _actualLogin = string.Empty;
                RefreshFieldsDisplay(showCursor: true);
            },
            onNextField = () => SetActiveInputField(!_editingPassword),
            onHide = () => SetKeyboardVisible(false),
            onSubmit = OnLoginClicked
        });

        ApplyTheme(_isDarkTheme, syncGlobalManager: true);
        SetKeyboardVisible(false);

        if (_themeToggle != null)
        {
            _themeToggle.SetValueWithoutNotify(_isDarkTheme);
            _themeToggle.RegisterValueChangedCallback(OnThemeToggled);
        }

        if (_submitButton != null)
            _submitButton.clicked += OnLoginClicked;

        SetStatus(string.Empty);
    }

    private void OnDisable()
    {
        if (_themeToggle != null)
            _themeToggle.UnregisterValueChangedCallback(OnThemeToggled);

        if (_loginField != null)
            _loginField.UnregisterCallback<PointerDownEvent>(OnLoginFieldClicked, TrickleDown.TrickleDown);

        if (_passwordField != null)
            _passwordField.UnregisterCallback<PointerDownEvent>(OnPasswordFieldClicked, TrickleDown.TrickleDown);

        if (_submitButton != null)
            _submitButton.clicked -= OnLoginClicked;
    }

    private bool GetGlobalThemeState()
    {
        try
        {
            var managerType = typeof(UIThemeManager);
            var prop = managerType.GetProperty("IsDarkTheme") ?? managerType.GetProperty("isDarkTheme") ?? managerType.GetProperty("CurrentTheme");
            if (prop != null && prop.PropertyType == typeof(bool))
                return (bool)prop.GetValue(null);

            var field = managerType.GetField("IsDarkTheme") ?? managerType.GetField("isDarkTheme");
            if (field != null && field.FieldType == typeof(bool))
                return (bool)field.GetValue(null);
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
            UIThemeManager.SetTheme(isDark);

        if (_rootContainer != null)
            _rootContainer.EnableInClassList("theme-dark", isDark);

        _vrKeyboard?.ApplyTheme(isDark);
    }

    private void SetupFieldForInGameKeyboard(TextField field, bool isPassword)
    {
        if (field == null) return;

        field.isReadOnly = true;
        field.focusable = false;
        field.isPasswordField = false;

        if (isPassword)
            field.RegisterCallback<PointerDownEvent>(OnPasswordFieldClicked, TrickleDown.TrickleDown);
        else
            field.RegisterCallback<PointerDownEvent>(OnLoginFieldClicked, TrickleDown.TrickleDown);
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
        _vrKeyboard?.SetVisible(visible);

        if (!visible)
        {
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

    private void RefreshFieldsDisplay(bool showCursor = true)
    {
        string loginText = _actualLogin + ((showCursor && !_editingPassword) ? "|" : string.Empty);
        string passText = new string('•', _actualPassword.Length) + ((showCursor && _editingPassword) ? "|" : string.Empty);

        if (_loginField != null)
            _loginField.SetValueWithoutNotify(loginText);

        if (_passwordField != null)
            _passwordField.SetValueWithoutNotify(passText);

        if (_vrKeyboard != null)
        {
            if (_editingPassword)
                _vrKeyboard.UpdatePreview("ПАРОЛЬ:", passText);
            else
                _vrKeyboard.UpdatePreview("ЛОГИН:", loginText);
        }
    }

    private void HideAuthUIImmediate()
    {
        if (uiDocument == null || uiDocument.rootVisualElement == null) return;

        var overlay = _overlayWrapper ?? uiDocument.rootVisualElement.Q<VisualElement>("platonus-login-overlay");
        if (overlay != null)
            overlay.style.display = DisplayStyle.None;
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
                fioRequest.SetRequestHeader("sid", sid);
            fioRequest.SetRequestHeader("Accept", "application/json, text/plain, */*");

            yield return fioRequest.SendWebRequest();

            if (fioRequest.result != UnityWebRequest.Result.Success)
            {
                SetStatus($"Ошибка профиля ({fioRequest.responseCode}): {fioRequest.error}", isError: true);
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
            OnAuthSuccess?.Invoke(UserFullName);

            if (hideDelaySeconds > 0f)
                yield return new WaitForSeconds(hideDelaySeconds);

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