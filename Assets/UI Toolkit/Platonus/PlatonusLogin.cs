using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

public class PlatonusAuth : MonoBehaviour
{
    [Header("UI Toolkit")]
    [SerializeField] private UIDocument uiDocument;

    [Header("Настройки по умолчанию")]
    [SerializeField] private bool startInDarkTheme = false;

    private const string LoginUrl = "https://platonus.iitu.edu.kz/rest/api/login";
    private const string FioUrl = "https://platonus.iitu.edu.kz/rest/fio/ru";

    private VisualElement _rootContainer;
    private Toggle _themeToggle;
    private TextField _loginField;
    private TextField _passwordField;
    private Button _submitButton;
    private Label _statusLabel;

    /// <summary>
    /// Полученное ФИО студента после успешной авторизации.
    /// </summary>
    public string UserFullName { get; private set; }

    /// <summary>
    /// Событие вызывается при успешном входе и передаёт ФИО пользователя.
    /// </summary>
    public event Action<string> OnAuthSuccess;

    private void OnEnable()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        var root = uiDocument.rootVisualElement;

        _rootContainer = root.Q<VisualElement>("root-container");
        _themeToggle = root.Q<Toggle>("theme-toggle");
        _loginField = root.Q<TextField>("login-input");
        _passwordField = root.Q<TextField>("password-input");
        _submitButton = root.Q<Button>("login-button");
        _statusLabel = root.Q<Label>("status-label");

        // Инициализация темы
        if (_rootContainer != null)
        {
            _rootContainer.EnableInClassList("theme-dark", startInDarkTheme);
        }

        if (_themeToggle != null)
        {
            _themeToggle.SetValueWithoutNotify(startInDarkTheme);
            _themeToggle.RegisterValueChangedCallback(OnThemeChanged);
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
            _themeToggle.UnregisterValueChangedCallback(OnThemeChanged);
        }

        if (_submitButton != null)
        {
            _submitButton.clicked -= OnLoginClicked;
        }
    }

    private void OnThemeChanged(ChangeEvent<bool> evt)
    {
        if (_rootContainer != null)
        {
            _rootContainer.EnableInClassList("theme-dark", evt.newValue);
        }
    }

    private void OnLoginClicked()
    {
        string login = _loginField != null ? _loginField.value.Trim() : string.Empty;
        string password = _passwordField != null ? _passwordField.value : string.Empty;

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

            yield return GetFioRoutine(authData.auth_token, authData.sid);
        }

        _submitButton.SetEnabled(true);
    }

    private IEnumerator GetFioRoutine(string authToken, string sid)
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

            SetStatus($"Добро пожаловать, {UserFullName}!", isSuccess: true);
            Debug.Log($"[PlatonusAuth] Авторизован: {UserFullName}");

            OnAuthSuccess?.Invoke(UserFullName);
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