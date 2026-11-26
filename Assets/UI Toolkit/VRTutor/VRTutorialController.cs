using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class VRTutorialController : MonoBehaviour
{
    public const string TutorialCompletedPrefKey = "VRTutorialCompleted";

    [Header("Навигация")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Главный UI обучения")]
    [SerializeField] private UIDocument mainTutorialUIDocument;

    [Header("Шаг 1 и 2: Тестовый предмет для хвата и перемещения")]
    [SerializeField] private XRGrabInteractable grabTestObject;
    [SerializeField] private float requiredMoveDistance = 0.25f;

    [Header("Шаг 3: Провод и коннекторы (префабы из Инспектора)")]
    [SerializeField] private GameObject wirePrefab;
    [SerializeField] private Transform wireSpawnPoint;
    [SerializeField] private GameObject connectorPrefab;
    [SerializeField] private Transform connectorASpawnPoint;
    [SerializeField] private Transform connectorBSpawnPoint;

    [Header("Шаг 4: Отдельный UIToolkit с кнопкой «Нажать»")]
    [SerializeField] private UIDocument buttonStepUIDocument;

    [Header("Шаг 5: Кнопка скриншота (кнопка A)")]
    [Tooltip("Опционально: InputAction для кнопки A. Если не задан — проверяется правый XR-контроллер (primaryButton) и клавиша P в редакторе.")]
    [SerializeField] private InputActionProperty screenshotButtonAction;

    [Header("Шаг 6: Передвижение на стик")]
    [Tooltip("Опционально: InputAction стика движения. Если не задан — читается primary2DAxis с левого/правого XR-контроллера или WASD.")]
    [SerializeField] private InputActionProperty moveStickAction;
    [SerializeField] private float requiredStickHoldTime = 0.6f;

    [Header("Шаг 7: Отдельный UIToolkit с VR-клавиатурой (написать Hello)")]
    [SerializeField] private UIDocument keyboardStepUIDocument;
    [SerializeField] private string targetWord = "Hello";
    [SerializeField] private float step7KeyboardBottomOffset = 16f;

    // Главный UI
    private VisualElement _root;
    private Toggle _themeToggle;
    private Label _stepBadgeLabel;
    private Label _titleLabel;
    private Label _descLabel;
    private Label _counterLabel;
    private VisualElement _progressFill;
    private Button _finishBtn;

    // Шаг 4 UI
    private VisualElement _btnStepRoot;
    private Button _testPressBtn;

    // Шаг 7 UI + единая EmbeddedVRKeyboard
    private VisualElement _kbdStepRoot;
    private TextField _kbdInputField;
    private Label _kbdHintLabel;
    private EmbeddedVRKeyboard _step7Keyboard;
    private string _typedText = string.Empty;
    private bool _kbdFieldSelected = false;

    // Состояние шагов (1..8)
    private int _currentStep = 1;
    private const int TotalSteps = 8;

    private Vector3 _step2StartPos;
    private bool _step2PosCaptured = false;

    private GameObject _spawnedWire;
    private GameObject _spawnedConnA;
    private GameObject _spawnedConnB;
    private int _connectedEndsCount = 0;

    private float _stickMovedTimer = 0f;
    private bool _isDarkTheme = true;
    private bool _isLoadingMenu = false;

    private void OnEnable()
    {
        if (mainTutorialUIDocument == null)
            mainTutorialUIDocument = GetComponent<UIDocument>();

        if (mainTutorialUIDocument != null && mainTutorialUIDocument.rootVisualElement != null)
        {
            var docRoot = mainTutorialUIDocument.rootVisualElement;
            _root = docRoot.Q<VisualElement>("tutorial-root");
            _themeToggle = docRoot.Q<Toggle>("theme-toggle");
            _stepBadgeLabel = docRoot.Q<Label>("step-badge-label");
            _titleLabel = docRoot.Q<Label>("instruction-title-label");
            _descLabel = docRoot.Q<Label>("instruction-desc-label");
            _counterLabel = docRoot.Q<Label>("progress-counter-label");
            _progressFill = docRoot.Q<VisualElement>("progress-bar-fill");
            _finishBtn = docRoot.Q<Button>("btn-to-main-menu");

            if (_themeToggle != null)
                _themeToggle.RegisterValueChangedCallback(OnThemeToggled);

            if (_finishBtn != null)
            {
                _finishBtn.pickingMode = PickingMode.Position;
                _finishBtn.clicked += OnFinishTutorialClicked;
                _finishBtn.RegisterCallback<ClickEvent>(OnFinishButtonClickEvent);
            }
        }

        UIThemeManager.OnThemeChanged += ApplyTheme;
        ApplyTheme(UIThemeManager.IsDarkTheme);

        if (grabTestObject != null)
            grabTestObject.selectEntered.AddListener(OnTestObjectGrabbed);

        SetupStep4UI();
        SetupStep7UI();

        SetStep(1);
    }

    private void OnDisable()
    {
        UIThemeManager.OnThemeChanged -= ApplyTheme;

        if (_themeToggle != null)
            _themeToggle.UnregisterValueChangedCallback(OnThemeToggled);

        if (_finishBtn != null)
        {
            _finishBtn.clicked -= OnFinishTutorialClicked;
            _finishBtn.UnregisterCallback<ClickEvent>(OnFinishButtonClickEvent);
        }

        if (grabTestObject != null)
            grabTestObject.selectEntered.RemoveListener(OnTestObjectGrabbed);

        if (_testPressBtn != null)
            _testPressBtn.clicked -= OnStep4ButtonPressed;
    }

    private void Update()
    {
        switch (_currentStep)
        {
            case 2:
                CheckStep2Movement();
                break;
            case 5:
                CheckStep5ScreenshotButton();
                break;
            case 6:
                CheckStep6StickMovement();
                break;
        }
    }

    // =========================================================
    // ПЕРЕКЛЮЧЕНИЕ ШАГОВ И ОБНОВЛЕНИЕ UI
    // =========================================================
    public void SetStep(int step)
    {
        _currentStep = Mathf.Clamp(step, 1, TotalSteps);

        // Полностью скрываем вспомогательные UI-панели на чужих шагах, чтобы их коллайдеры не перекрывали луч
        SetExtraDocVisible(buttonStepUIDocument, _currentStep == 4);
        SetExtraDocVisible(keyboardStepUIDocument, _currentStep == 7);

        if (_stepBadgeLabel != null)
            _stepBadgeLabel.text = $"ЗАДАНИЕ {_currentStep}";

        if (_counterLabel != null)
            _counterLabel.text = $"{_currentStep} из {TotalSteps} шагов";

        if (_progressFill != null)
        {
            float pct = (_currentStep / (float)TotalSteps) * 100f;
            _progressFill.style.width = Length.Percent(pct);
            _progressFill.EnableInClassList("completed", _currentStep == TotalSteps);
        }

        if (_finishBtn != null)
        {
            bool isLastStep = _currentStep >= TotalSteps;
            _finishBtn.EnableInClassList("hidden", !isLastStep);
            _finishBtn.style.display = isLastStep ? DisplayStyle.Flex : DisplayStyle.None;
            _finishBtn.SetEnabled(isLastStep);
        }

        switch (_currentStep)
        {
            case 1:
                SetInstruction(
                    "1. Взятие предмета в руки",
                    "Поднесите контроллер к тестовому предмету на столе и зажмите боковую кнопку хвата (Grip)."
                );
                break;

            case 2:
                SetInstruction(
                    "2. Перемещение предмета в стороны",
                    "Удерживая предмет в руке, передвиньте его в сторону от начальной точки."
                );
                if (grabTestObject != null)
                {
                    _step2StartPos = grabTestObject.transform.position;
                    _step2PosCaptured = true;
                }
                break;

            case 3:
                SetInstruction(
                    "3. Соединение проводов с коннекторами",
                    "Возьмите в руки появившийся перед вами провод одним концом в левую руку, вторым концом в правую руку и вставьте их в коннекторы перед вами."
                );
                SpawnWireAndConnectors();
                break;

            case 4:
                SetupStep4UI();
                SetInstruction(
                    "4. Взаимодействие с кнопками интерфейса",
                    "Наведите луч контроллера на соседнюю панель и нажмите кнопку «НАЖАТЬ»."
                );
                break;

            case 5:
                SetInstruction(
                    "5. Снимок экрана (скриншот)",
                    "Нажмите кнопку «A» на правом контроллере, чтобы зафиксировать скриншот эксперимента."
                );
                break;

            case 6:
                SetInstruction(
                    "6. Передвижение по лаборатории",
                    "Отклоните стик на контроллере в любую сторону, чтобы проверить перемещение."
                );
                _stickMovedTimer = 0f;
                break;

            case 7:
                SetupStep7UI();
                SetInstruction(
                    "7. Ввод текста с VR-клавиатуры",
                    $"На соседней панели выделите поле для ввода и напишите слово «{targetWord}»."
                );
                _typedText = string.Empty;
                _kbdFieldSelected = false;
                _step7Keyboard?.SetVisible(false);
                RefreshStep7Field();
                break;

            case 8:
                _step7Keyboard?.SetVisible(false);
                SetInstruction(
                    "8. Обучение завершено!",
                    "Вы успешно освоили все базовые действия в VR. Нажмите кнопку ниже, чтобы перейти в главное меню."
                );
                PlayerPrefs.SetInt(TutorialCompletedPrefKey, 1);
                PlayerPrefs.Save();
                break;
        }
    }

    private void AdvanceStep(int fromStep)
    {
        if (_currentStep == fromStep && _currentStep < TotalSteps)
        {
            SetStep(_currentStep + 1);
        }
    }

    private void SetInstruction(string title, string desc)
    {
        if (_titleLabel != null) _titleLabel.text = title;
        if (_descLabel != null) _descLabel.text = desc;
    }

    private void SetExtraDocVisible(UIDocument doc, bool visible)
    {
        if (doc == null) return;

        if (visible && !doc.gameObject.activeSelf)
            doc.gameObject.SetActive(true);

        if (doc.rootVisualElement != null)
            doc.rootVisualElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        if (!visible && doc.gameObject.activeSelf)
            doc.gameObject.SetActive(false);
    }

    // =========================================================
    // ШАГ 1 И ШАГ 2: ХВАТ И ПЕРЕМЕЩЕНИЕ ПРЕДМЕТА
    // =========================================================
    private void OnTestObjectGrabbed(SelectEnterEventArgs args)
    {
        if (_currentStep == 1)
        {
            AdvanceStep(1);
        }
    }

    private void CheckStep2Movement()
    {
        if (grabTestObject == null || !_step2PosCaptured) return;

        float dist = Vector3.Distance(_step2StartPos, grabTestObject.transform.position);
        if (dist >= requiredMoveDistance)
        {
            AdvanceStep(2);
        }
    }

    // =========================================================
    // ШАГ 3: СПАВН ПРОВОДА И КОННЕКТОРОВ
    // =========================================================
    private void SpawnWireAndConnectors()
    {
        _connectedEndsCount = 0;

        if (wirePrefab != null && _spawnedWire == null)
        {
            Vector3 pos = wireSpawnPoint != null ? wireSpawnPoint.position : transform.position + transform.forward * 0.6f;
            Quaternion rot = wireSpawnPoint != null ? wireSpawnPoint.rotation : Quaternion.identity;
            _spawnedWire = Instantiate(wirePrefab, pos, rot);
        }

        if (connectorPrefab != null)
        {
            if (_spawnedConnA == null)
            {
                Vector3 posA = connectorASpawnPoint != null ? connectorASpawnPoint.position : transform.position + transform.forward * 0.8f - transform.right * 0.2f;
                Quaternion rotA = connectorASpawnPoint != null ? connectorASpawnPoint.rotation : Quaternion.identity;
                _spawnedConnA = Instantiate(connectorPrefab, posA, rotA);
                SetupConnectorTrigger(_spawnedConnA);
            }

            if (_spawnedConnB == null)
            {
                Vector3 posB = connectorBSpawnPoint != null ? connectorBSpawnPoint.position : transform.position + transform.forward * 0.8f + transform.right * 0.2f;
                Quaternion rotB = connectorBSpawnPoint != null ? connectorBSpawnPoint.rotation : Quaternion.identity;
                _spawnedConnB = Instantiate(connectorPrefab, posB, rotB);
                SetupConnectorTrigger(_spawnedConnB);
            }
        }
    }

    private void SetupConnectorTrigger(GameObject connectorObj)
    {
        if (connectorObj == null) return;
        var zone = connectorObj.GetComponent<TutorialConnectorZone>();
        if (zone == null) zone = connectorObj.AddComponent<TutorialConnectorZone>();
        zone.Init(this);
    }

    public void NotifyWireEndConnected()
    {
        if (_currentStep != 3) return;

        _connectedEndsCount++;
        if (_connectedEndsCount >= 2)
        {
            AdvanceStep(3);
        }
    }

    // =========================================================
    // ШАГ 4: НАЖАТИЕ НА КНОПКУ В ОТДЕЛЬНОМ UITOOLKIT
    // =========================================================
    private void SetupStep4UI()
    {
        if (buttonStepUIDocument == null || buttonStepUIDocument.rootVisualElement == null) return;

        var root4 = buttonStepUIDocument.rootVisualElement;
        _btnStepRoot = root4.Q<VisualElement>("btn-step-root");
        _btnStepRoot?.EnableInClassList("theme-dark", _isDarkTheme);

        var btn = root4.Q<Button>("btn-test-press");
        if (btn != null && btn != _testPressBtn)
        {
            if (_testPressBtn != null)
                _testPressBtn.clicked -= OnStep4ButtonPressed;

            _testPressBtn = btn;
            _testPressBtn.clicked += OnStep4ButtonPressed;
        }
    }

    private void OnStep4ButtonPressed()
    {
        if (_currentStep == 4)
        {
            AdvanceStep(4);
        }
    }

    // =========================================================
    // ШАГ 5: КНОПКА СКРИНШОТА (КНОПКА A)
    // =========================================================
    private void CheckStep5ScreenshotButton()
    {
        bool pressed = false;

        if (screenshotButtonAction.action != null && screenshotButtonAction.action.WasPressedThisFrame())
        {
            pressed = true;
        }
        else
        {
            var rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (rightHand.isValid && rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool primaryPressed) && primaryPressed)
            {
                pressed = true;
            }
        }

        if (Keyboard.current != null && Keyboard.current.pKey.wasPressedThisFrame)
            pressed = true;

        if (pressed)
        {
            AdvanceStep(5);
        }
    }

    // =========================================================
    // ШАГ 6: ДВИЖЕНИЕ СТИКОМ
    // =========================================================
    private void CheckStep6StickMovement()
    {
        Vector2 stick = Vector2.zero;

        if (moveStickAction.action != null)
        {
            stick = moveStickAction.action.ReadValue<Vector2>();
        }
        else
        {
            var leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            if (leftHand.isValid)
                leftHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out stick);

            if (stick.sqrMagnitude < 0.05f)
            {
                var rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if (rightHand.isValid)
                    rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out stick);
            }
        }

        if (stick.sqrMagnitude < 0.05f && Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.aKey.isPressed ||
                Keyboard.current.sKey.isPressed || Keyboard.current.dKey.isPressed)
            {
                stick = Vector2.up;
            }
        }

        if (stick.magnitude > 0.25f)
        {
            _stickMovedTimer += Time.deltaTime;
            if (_stickMovedTimer >= requiredStickHoldTime)
            {
                AdvanceStep(6);
            }
        }
    }

    // =========================================================
    // ШАГ 7: ПОЛЕ ВВОДА И ЕДИНАЯ EmbeddedVRKeyboard (НАПИСАТЬ HELLO)
    // =========================================================
    private void SetupStep7UI()
    {
        if (keyboardStepUIDocument == null || keyboardStepUIDocument.rootVisualElement == null) return;

        var root7 = keyboardStepUIDocument.rootVisualElement;
        _kbdStepRoot = root7.Q<VisualElement>("kbd-step-root");
        _kbdStepRoot?.EnableInClassList("theme-dark", _isDarkTheme);

        _kbdInputField = root7.Q<TextField>("tutorial-text-input");
        _kbdHintLabel = root7.Q<Label>("kbd-hint-label");

        if (_kbdInputField != null)
        {
            _kbdInputField.isReadOnly = true;
            _kbdInputField.focusable = false;
            _kbdInputField.UnregisterCallback<PointerDownEvent>(OnStep7FieldClicked, TrickleDown.TrickleDown);
            _kbdInputField.RegisterCallback<PointerDownEvent>(OnStep7FieldClicked, TrickleDown.TrickleDown);
        }

        _step7Keyboard = new EmbeddedVRKeyboard(root7, new EmbeddedVRKeyboard.Config
        {
            bottomOffset = step7KeyboardBottomOffset,
            showNextFieldButton = false,
            submitButtonText = "Готово ↵",
            onCharacterTyped = ch =>
            {
                _typedText += ch;
                RefreshStep7Field();
                CheckStep7Word();
            },
            onBackspace = () =>
            {
                if (_typedText.Length > 0)
                    _typedText = _typedText.Substring(0, _typedText.Length - 1);
                RefreshStep7Field();
                CheckStep7Word();
            },
            onClear = () =>
            {
                _typedText = string.Empty;
                RefreshStep7Field();
            },
            onHide = () =>
            {
                _kbdFieldSelected = false;
                RefreshStep7Field();
            },
            onSubmit = CheckStep7Word
        });

        _step7Keyboard.ApplyTheme(_isDarkTheme);
        _step7Keyboard.SetVisible(false);
    }

    private void OnStep7FieldClicked(PointerDownEvent evt)
    {
        evt.StopImmediatePropagation();
        _kbdFieldSelected = true;
        _step7Keyboard?.SetVisible(true);
        RefreshStep7Field();
    }

    private void RefreshStep7Field()
    {
        string display = _typedText + (_kbdFieldSelected ? "|" : string.Empty);
        _kbdInputField?.SetValueWithoutNotify(display);
        _step7Keyboard?.UpdatePreview("ТЕКСТ:", display);
    }

    private void CheckStep7Word()
    {
        if (string.Equals(_typedText.Trim(), targetWord, StringComparison.OrdinalIgnoreCase))
        {
            if (_kbdHintLabel != null)
            {
                _kbdHintLabel.text = "Отлично! Слово введено верно.";
                _kbdHintLabel.AddToClassList("success");
            }
            _step7Keyboard?.SetVisible(false);
            AdvanceStep(7);
        }
    }

    // =========================================================
    // ТЕМА И ВЫХОД В ГЛАВНОЕ МЕНЮ (ШАГ 8)
    // =========================================================
    private void OnThemeToggled(ChangeEvent<bool> evt)
    {
        UIThemeManager.SetTheme(evt.newValue);
    }

    private void ApplyTheme(bool isDark)
    {
        _isDarkTheme = isDark;

        _root?.EnableInClassList("theme-dark", isDark);
        _btnStepRoot?.EnableInClassList("theme-dark", isDark);
        _kbdStepRoot?.EnableInClassList("theme-dark", isDark);
        _step7Keyboard?.ApplyTheme(isDark);

        if (_themeToggle != null)
            _themeToggle.SetValueWithoutNotify(isDark);
    }

    private void OnFinishButtonClickEvent(ClickEvent evt)
    {
        OnFinishTutorialClicked();
    }

    private void OnFinishTutorialClicked()
    {
        if (_isLoadingMenu) return;

        Debug.Log($"[VRTutorialController] Нажата кнопка выхода. Загрузка сцены: '{mainMenuSceneName}'");

        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
        {
            Debug.LogError("[VRTutorialController] Поле Main Menu Scene Name пустое!");
            return;
        }

        _isLoadingMenu = true;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}