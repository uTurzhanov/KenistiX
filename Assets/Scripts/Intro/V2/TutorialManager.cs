using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Управляет всей логикой инструктажа:
/// - показывает текст текущего шага (счётчик + название);
/// - проигрывает видео-подсказку и/или включает 3D-объект шага в сцене;
/// - запускает нужный детектор завершения шага (реальное действие, не таймер) и слушает его;
/// - таймер работает как страховка: если детектор не сработал (или для шагов без детектора вовсе,
///   Completion Type = Timer) - по истечении времени шаг переключается САМ, без необходимости
///   нажимать кнопку (пользователь может ещё не уметь это делать на ранних шагах);
/// - зачёркивает пройденные шаги в списке слева;
/// - переключает шаги автоматически (по детектору) или по нажатию кнопки.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    [Header("Панель целиком (для открепления от камеры на отдельных шагах)")]
    [Tooltip("Сам компонент Canvas, на котором висит вся панель (TutorialPanel и т.д.). " +
             "Нужен только если хотя бы у одного шага включён Detach Panel From Camera - " +
             "иначе можно оставить пустым.")]
    [SerializeField] private Canvas tutorialCanvas;

    [Header("Данные шагов (по порядку!)")]
    [SerializeField] private List<TutorialStepData> steps = new List<TutorialStepData>();

    [Header("Список шагов слева снизу — генерируется автоматически")]
    [Tooltip("Префаб одного пункта списка (с компонентом StepListItemUI). " +
             "Количество пунктов подстроится под количество элементов в Steps — можно смело добавлять 5-й, 10-й шаг и т.д.")]
    [SerializeField] private StepListItemUI stepListItemPrefab;

    [Tooltip("Родительский объект с Vertical Layout Group (StepListPanel), куда будут добавлены сгенерированные пункты.")]
    [SerializeField] private Transform stepListContainer;

    private readonly List<StepListItemUI> stepListItems = new List<StepListItemUI>();

    [Header("Текст шапки")]
    [SerializeField] private TMP_Text stepCounterLabel; // "ШАГ 2 ИЗ 4"
    [SerializeField] private TMP_Text titleText; // "Движение"

    [Header("Видео справа снизу (опционально)")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage videoRawImage; // на неё выводится RenderTexture из VideoPlayer

    [Tooltip("Кнопка \"Пересмотреть\" - перезапускает видео текущего шага с начала. Озвучку и " +
             "детектор/таймер шага не трогает, просто переигрывает сам ролик. Видна только на " +
             "шагах, где у ассета заполнен Video Clip.")]
    [SerializeField] private Button replayVideoButton;

    [Header("Озвучка шага (отдельный AudioSource, не через VideoPlayer)")]
    [SerializeField] private AudioSource narrationAudioSource;

    [Header("Детекторы завершения шага (по одному экземпляру каждого типа в сцене)")]
    [Tooltip("Отслеживает поворот головы - используется на шагах с Completion Type = Head Look Around")]
    [SerializeField] private HeadRotationChecker headRotationChecker;

    [Tooltip("Отслеживает вращение контроллера в руке - Completion Type = Controller Rotation")]
    [SerializeField] private ControllerRotationChecker controllerRotationChecker;

    [Tooltip("Отслеживает движение стиков - Completion Type = Thumbstick Activity")]
    [SerializeField] private ThumbstickActivityChecker thumbstickActivityChecker;

    [Tooltip("Отслеживает нажатие кнопки - Completion Type = Button Press")]
    [SerializeField] private ButtonPressChecker buttonPressChecker;

    [Tooltip("Отслеживает захват предмета - Completion Type = Object Grab")]
    [SerializeField] private ObjectGrabChecker objectGrabChecker;

    [Header("Кнопка Далее")]
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text nextButtonLabel;
    [SerializeField] private CanvasGroup nextButtonCanvasGroup; // для полупрозрачности в disabled-состоянии
    [SerializeField] private string defaultButtonLabel = "Далее";

    [Header("Кнопка Назад (опционально)")]
    [Tooltip("Возврат на предыдущий шаг. Показывается, как только пользователь хотя бы раз дошёл до " +
             "шага с Show Next Button = true (и остаётся видна дальше даже при возврате на более " +
             "ранние шаги, где Show Next Button у самого шага выключен) - плюс скрыта на самом первом " +
             "шаге, назад идти некуда. В отличие от Далее, не ждёт таймер/детектор - доступна сразу.")]
    [SerializeField] private Button backButton;

    [Header("Кнопка Пропустить (опционально)")]
    [Tooltip("Пропускает весь инструктаж целиком - сразу переводит к финалу (озвучка завершения, " +
             "зона практики, кнопка Продолжить), минуя оставшиеся шаги. В отличие от Далее/Назад, " +
             "видна всегда с самого начала, не привязана к Show Next Button/hasLearnedButtonPress.")]
    [SerializeField] private Button skipButton;

    [Tooltip("Панель \"Точно пропустить?\" - показывается по нажатию Skip Button вместо немедленного " +
             "пропуска. Если оставить пустым - Skip Button пропускает сразу, без подтверждения.")]
    [SerializeField] private GameObject skipConfirmPanel;

    [Tooltip("Кнопка внутри Skip Confirm Panel, подтверждающая пропуск.")]
    [SerializeField] private Button skipConfirmYesButton;

    [Tooltip("Кнопка внутри Skip Confirm Panel, отменяющая пропуск (просто закрывает панель, ничего не меняя).")]
    [SerializeField] private Button skipConfirmCancelButton;

    [Header("Затемнение фона за панелью (опционально)")]
    [Tooltip("Полноэкранная тёмная плашка позади TutorialPanel - фокусирует внимание на инструктаже, " +
             "не давая отвлекаться на сцену вокруг. Выключается автоматически после последнего шага.")]
    [SerializeField] private GameObject backgroundDimmer;

    [Header("Что показать после последнего шага")]
    [SerializeField] private GameObject completionPanel; // необязательно, можно оставить пустым

    [Header("Закрепление результата (опционально)")]
    [Tooltip("Общая фраза-подсказка вроде \"Закрепим результат, повторите действие\" - проигрывается " +
             "один раз на любом шаге с включённым Require Reinforcement Repeat, после первого " +
             "успешного выполнения действия, перед тем как попросить повторить его ещё раз.")]
    [SerializeField] private AudioClip reinforcementNarrationClip;

    [Header("Финал — сообщение и свободная тренировка (опционально)")]
    [Tooltip("Короткий звук успеха (не голос) - проигрывается сразу в момент завершения Шага 11, " +
             "параллельно с голосовой озвучкой, отдельным AudioSource (не тем же, что для голоса, " +
             "чтобы звуки точно не перебивали друг друга).")]
    [SerializeField] private AudioClip successSfxClip;

    [Tooltip("Отдельный AudioSource для звуковых эффектов (не для голоса). Нужен, только если " +
             "заполнен Success Sfx Clip.")]
    [SerializeField] private AudioSource sfxAudioSource;

    [Tooltip("Озвучка \"Обучение завершено\" и т.п. Если задана - зона практики открывается после " +
             "её длины + 1 сек запаса вместо фиксированного Delay Before Practice.")]
    [SerializeField] private AudioClip completionNarrationClip;

    [Tooltip("Если Completion Narration Clip не задан - просто ждать это время (сек) перед тем, как открыть зону практики.")]
    [SerializeField] private float delayBeforeSceneLoad = 3f;

    [Tooltip("Контейнер с объектами для свободной тренировки (предметы для захвата, инструмент " +
             "рисования и т.п.) - включается ПОСЛЕ финальной озвучки. Может быть пустым, если " +
             "тренировка не нужна вовсе.")]
    [SerializeField] private GameObject practiceAreaRoot;

    [Tooltip("Кнопка \"Продолжить\" - показывается вместе с зоной практики, переход в другую сцену " +
             "происходит только по явному нажатию, не по таймеру, чтобы не торопить пользователя.")]
    [SerializeField] private Button continueButton;

    [Tooltip("FALLBACK: имя сцены для перехода, используется только если в проекте НЕТ AppSceneManager " +
             "(если есть - переход идёт через него, этот поле игнорируется). Сцена ОБЯЗАТЕЛЬНО должна " +
             "быть добавлена в Build Settings (File → Build Settings → Scenes In Build), иначе будет " +
             "ошибка при попытке перехода. Если оставить пустым (и AppSceneManager тоже нет) - нажатие " +
             "Continue Button просто закроет экран завершения, оставляя пользователя в зоне практики.")]
    [SerializeField] private string nextSceneName;

    [Header("События (необязательно)")]
    public UnityEngine.Events.UnityEvent onTutorialCompleted;
    public UnityEngine.Events.UnityEvent<int> onStepChanged; // индекс нового шага

    private int currentStepIndex = -1;
    private Coroutine buttonTimerCoroutine;
    private StepCompletionChecker activeChecker;
    private bool advancedByChecker; // защита от двойного перехода (детектор + кнопка одновременно)
    private Coroutine checkerActivationCoroutine; // ждёт конца озвучки перед включением детектора

    // Первое выполнение действия на шаге с Require Reinforcement Repeat уже случилось, но реальный
    // переход дальше отложен - ждём, пока пользователь повторит то же действие второй раз.
    private bool awaitingReinforcementRepeat;

    // Как только пользователь дошёл до шага с Show Next Button = true хотя бы раз - считаем, что
    // он умеет пользоваться кнопками, и это НЕ сбрасывается при возврате назад более ранними шагами
    // (иначе "Назад" пропадала бы через один клик, едва вернувшись на шаг без Show Next Button).
    private bool hasLearnedButtonPress;

    // Исходные настройки Canvas (Screen Space - Camera) - запоминаем при старте, чтобы можно было
    // корректно прикрепить панель обратно к камере после открепления на отдельном шаге.
    private Camera originalCanvasWorldCamera;
    private float originalCanvasPlaneDistance;
    private bool panelCurrentlyDetached;

    // Самый дальний по-настоящему пройденный шаг. Если пользователь кнопкой "Назад" вернулся на
    // шаг с индексом МЕНЬШЕ этого значения - считаем это "режимом просмотра": авто-переход
    // (и по детектору, и по таймеру-озвучке) для этого показа отключается, иначе пользователя
    // постоянно "обгоняет" автопереход, пока он пытается пролистать несколько шагов назад подряд
    // (особенно заметно на коротких информационных Timer-шагах).
    private int furthestStepIndexReached = -1;
    private bool currentStepIsReview;

    // Исходный материал VideoFrame (без хромакея) - запоминаем при старте, чтобы возвращать его
    // на шагах, где step.videoMaterial не задан (не остаётся хромакей-материал от предыдущего шага).
    private Material originalVideoRawImageMaterial;

    private void Start()
    {
        if (steps.Count == 0)
        {
            Debug.LogWarning("[TutorialManager] Список шагов пуст.");
            return;
        }

        if (videoRawImage != null)
            originalVideoRawImageMaterial = videoRawImage.material;

        if (tutorialCanvas != null)
        {
            originalCanvasWorldCamera = tutorialCanvas.worldCamera;
            originalCanvasPlaneDistance = tutorialCanvas.planeDistance;
        }

        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextButtonPressed);

        if (backButton != null)
            backButton.onClick.AddListener(OnBackButtonPressed);

        if (skipButton != null)
            skipButton.onClick.AddListener(OnSkipButtonPressed);

        if (skipConfirmYesButton != null)
            skipConfirmYesButton.onClick.AddListener(OnSkipConfirmed);

        if (skipConfirmCancelButton != null)
            skipConfirmCancelButton.onClick.AddListener(OnSkipCancelled);

        if (skipConfirmPanel != null)
            skipConfirmPanel.SetActive(false);

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueButtonPressed);
            continueButton.gameObject.SetActive(false);
        }

        if (replayVideoButton != null)
            replayVideoButton.onClick.AddListener(OnReplayVideoButtonPressed);

        if (practiceAreaRoot != null)
            practiceAreaRoot.SetActive(false);

        if (completionPanel != null)
            completionPanel.SetActive(false);

        BuildStepList();
        InitializeAllCheckers();
        ShowStep(0);
    }

    /// <summary>
    /// Сбрасывает (инициализирует внутренние массивы/состояние) ВСЕ детекторы сразу при старте,
    /// а не только тот, что нужен текущему шагу. Иначе Update() неактивных детекторов может
    /// выполниться раньше, чем до них дойдёт очередь через ActivateChecker(), и упасть с
    /// NullReferenceException на ещё не инициализированных полях.
    /// </summary>
    private void InitializeAllCheckers()
    {
        headRotationChecker?.ResetChecker();
        controllerRotationChecker?.ResetChecker();
        thumbstickActivityChecker?.ResetChecker();
        buttonPressChecker?.ResetChecker();
        objectGrabChecker?.ResetChecker();
    }

    private void Update()
    {
        // Опрашиваем активный детектор каждый кадр - как только реальное действие выполнено,
        // сразу переходим дальше, не дожидаясь таймера-страховки.
        // Пока activeChecker == null (озвучка ещё не доиграла, см. ArmCheckerAfterNarration) -
        // сюда просто нечего проверять, действие пользователя ещё не отслеживается.
        if (activeChecker != null && activeChecker.IsCompleted && !advancedByChecker)
        {
            TutorialStepData step = steps[currentStepIndex];

            if (step.requireReinforcementRepeat && !awaitingReinforcementRepeat)
            {
                // Первое выполнение - не переходим ещё, просим повторить то же самое действие.
                // Сбрасываем и сразу же ОТКЛЮЧАЕМ детектор (activeChecker = null) на время, пока
                // играет фраза "закрепим результат" - по той же причине, что и с основной озвучкой:
                // не хотим засчитать действие, которое пользователь случайно повторил, пока ещё
                // идёт объяснение, что нужно сделать.
                awaitingReinforcementRepeat = true;
                StepCompletionChecker checkerToRearm = activeChecker;
                checkerToRearm.ResetChecker();
                activeChecker = null;

                if (buttonTimerCoroutine != null) StopCoroutine(buttonTimerCoroutine);
                if (checkerActivationCoroutine != null) StopCoroutine(checkerActivationCoroutine);
                checkerActivationCoroutine = StartCoroutine(RearmAfterReinforcementMessage(checkerToRearm, step));
            }
            else
            {
                advancedByChecker = true;
                ShowStep(currentStepIndex + 1);
            }
        }
    }

    /// <summary>
    /// Ждёт, пока доиграет основная озвучка шага, и только ПОСЛЕ этого включает детектор и
    /// запускает страховочный таймер. Без этой задержки пользователь мог случайно выполнить
    /// действие (например повернуть голову) ещё во время объяснения, что приводило к наложению
    /// фразы "закрепим результат" поверх ещё не закончившейся основной озвучки.
    /// Для Timer-шагов (без детектора) по окончании озвучки просто сразу переходим дальше - им
    /// больше нечего ждать.
    /// </summary>
    private IEnumerator ArmCheckerAfterNarration(TutorialStepData step)
    {
        yield return WaitForClipToFinish(step.narrationClip);

        if (step.completionType == StepCompletionType.Timer)
        {
            SetNextButtonState(true);
            if (!advancedByChecker)
            {
                advancedByChecker = true;
                ShowStep(currentStepIndex + 1);
            }
            yield break;
        }

        ActivateChecker(step);

        if (buttonTimerCoroutine != null)
            StopCoroutine(buttonTimerCoroutine);
        buttonTimerCoroutine = StartCoroutine(RunFallbackTimer(step));
    }

    /// <summary>Аналогично ArmCheckerAfterNarration, но после короткой фразы "закрепим результат" вместо основной озвучки.</summary>
    private IEnumerator RearmAfterReinforcementMessage(StepCompletionChecker checker, TutorialStepData step)
    {
        PlayReinforcementMessage();
        yield return WaitForClipToFinish(reinforcementNarrationClip);

        checker.ResetChecker(); // на случай если что-то успело сработать за время ожидания
        activeChecker = checker;

        buttonTimerCoroutine = StartCoroutine(RunFallbackTimer(step));
    }

    /// <summary>
    /// Ждёт, пока указанный клип реально НАЧНЁТ играть на narrationAudioSource (может быть отложен
    /// стартом видео, см. PlayStepMedia/OnVideoPrepared), а затем - пока он закончится. Если клипа
    /// нет - выходит сразу, ждать нечего.
    /// </summary>
    private IEnumerator WaitForClipToFinish(AudioClip clip)
    {
        if (narrationAudioSource == null || clip == null)
            yield break;

        float safetyTimeout = 5f; // на случай, если клип почему-то так и не начнёт играть
        float waited = 0f;
        while (!(narrationAudioSource.isPlaying && narrationAudioSource.clip == clip) && waited < safetyTimeout)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        if (narrationAudioSource.isPlaying && narrationAudioSource.clip == clip)
            yield return new WaitForSeconds(clip.length);
    }

    private void PlayReinforcementMessage()
    {
        if (narrationAudioSource == null || reinforcementNarrationClip == null)
            return;

        narrationAudioSource.Stop();
        narrationAudioSource.clip = reinforcementNarrationClip;
        narrationAudioSource.Play();
    }

    private void BuildStepList()
    {
        stepListItems.Clear();

        if (stepListItemPrefab == null || stepListContainer == null)
        {
            Debug.LogWarning("[TutorialManager] Step List Item Prefab или Step List Container не назначены - список шагов слева не будет создан.");
            return;
        }

        // На случай повторного запуска в редакторе - чистим контейнер перед генерацией.
        for (int i = stepListContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = stepListContainer.GetChild(i);
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }

        foreach (TutorialStepData _ in steps)
        {
            StepListItemUI item = Instantiate(stepListItemPrefab, stepListContainer);
            stepListItems.Add(item);
        }

        // Принудительно пересчитываем Canvas сразу после генерации - подстраховка от того же
        // класса "первый кадр после Instantiate ещё не отрендерился" бага, что и в StepListItemUI.SetText().
        Canvas.ForceUpdateCanvases();
    }

    private void ShowStep(int index)
    {
        if (index < 0 || index >= steps.Count)
        {
            CompleteTutorial();
            return;
        }

        currentStepIndex = index;
        advancedByChecker = false;
        awaitingReinforcementRepeat = false;
        TutorialStepData step = steps[index];

        // Режим просмотра - шаг с этим индексом уже проходили раньше (index меньше самого дальнего
        // достигнутого), значит сейчас на него вернулись через "Назад", а не идут вперёд впервые.
        currentStepIsReview = index < furthestStepIndexReached;
        if (index > furthestStepIndexReached)
            furthestStepIndexReached = index;

        // Счётчик шага ("ШАГ 2 ИЗ 4")
        if (stepCounterLabel != null)
            stepCounterLabel.text = $"ШАГ {index + 1} ИЗ {steps.Count}";

        // Текст
        if (titleText != null)
            titleText.text = step.stepTitle;

        // Видео + озвучка + 3D-объект шага
        PlayStepMedia(step);
        UpdateStepSceneObject(step);

        if (replayVideoButton != null)
            replayVideoButton.gameObject.SetActive(step.videoClip != null);

        // Открепление панели от камеры (если нужно на этом шаге) или прикрепление обратно
        if (step.detachPanelFromCamera)
            DetachPanelFromCamera();
        else
            ReattachPanelToCamera();

        // Детектор завершения шага - НЕ активируем сразу. В режиме просмотра не активируем вовсе
        // (см. п.4 выше). Иначе - ждём, пока доиграет основная озвучка (ArmCheckerAfterNarration),
        // и только тогда включаем детектор - иначе пользователь мог случайно выполнить действие
        // ещё во время объяснения, что и приводило к наложению звуков.
        if (checkerActivationCoroutine != null)
            StopCoroutine(checkerActivationCoroutine);

        if (currentStepIsReview)
        {
            activeChecker = null;
        }
        else
        {
            activeChecker = null;
            checkerActivationCoroutine = StartCoroutine(ArmCheckerAfterNarration(step));
        }

        // Как только хотя бы раз увидели шаг с Show Next Button = true - запоминаем это навсегда
        // (не сбрасывается при возврате на более ранние шаги, см. комментарий у поля выше).
        if (step.showNextButton)
            hasLearnedButtonPress = true;

        // Кнопка Далее - показываем/скрываем целиком по накопленному флагу; если показана - выключаем
        // интерактивность до срабатывания детектора/озвучки/страховки
        if (nextButton != null)
            nextButton.gameObject.SetActive(hasLearnedButtonPress);
        SetNextButtonState(false);

        // Кнопка Назад - по тому же накопленному флагу, но всегда сразу доступна для клика (не ждёт
        // таймер/детектор), и скрыта на самом первом шаге - назад идти некуда
        if (backButton != null)
        {
            bool canGoBack = hasLearnedButtonPress && index > 0;
            backButton.gameObject.SetActive(canGoBack);
            backButton.interactable = canGoBack;
        }

        if (nextButtonLabel != null)
            nextButtonLabel.text = string.IsNullOrEmpty(step.customButtonLabel)
                ? defaultButtonLabel
                : step.customButtonLabel;

        if (buttonTimerCoroutine != null)
            StopCoroutine(buttonTimerCoroutine);

        if (currentStepIsReview)
        {
            // Просматриваем уже пройденный шаг - никуда торопиться не нужно, кнопка "Далее" сразу
            // доступна, никакого таймера-автоперехода не запускаем.
            SetNextButtonState(true);
        }
        // Для не-review шагов страховочный таймер запускается внутри ArmCheckerAfterNarration -
        // уже ПОСЛЕ того, как доиграет озвучка и реально включится детектор, а не раньше.

        // Обновляем визуальное состояние списка шагов слева
        RefreshStepList();

        onStepChanged?.Invoke(index);
    }

    /// <summary>Включает нужный детектор под тип текущего шага, остальные не трогает (они бездействуют, пока их IsCompleted не спрашивают).</summary>
    private void ActivateChecker(TutorialStepData step)
    {
        StepCompletionType type = step.completionType;

        activeChecker = type switch
        {
            StepCompletionType.HeadLookAround => headRotationChecker,
            StepCompletionType.ControllerRotation => controllerRotationChecker,
            StepCompletionType.ThumbstickActivity => thumbstickActivityChecker,
            StepCompletionType.ButtonPress => buttonPressChecker,
            StepCompletionType.ObjectGrab => objectGrabChecker,
            _ => null, // Timer - без детектора, только страховочный таймер и решает
        };

        if (activeChecker == null && type != StepCompletionType.Timer)
            Debug.LogWarning($"[TutorialManager] Для типа {type} не назначен соответствующий детектор в инспекторе - шаг будет завершаться только по таймеру-страховке.");

        // Настраиваем, какие именно оси/стики проверять на ЭТОМ шаге - позволяет разбивать один
        // детектор на несколько последовательных подшагов (например, "поверните влево-вправо"
        // отдельно от "посмотрите вверх-вниз") без создания отдельных типов детекторов.
        if (type == StepCompletionType.HeadLookAround)
            headRotationChecker?.SetRequiredAxes(step.headCheckYaw, step.headCheckPitch);
        else if (type == StepCompletionType.ThumbstickActivity)
            thumbstickActivityChecker?.SetRequiredAxes(step.thumbstickCheckMove, step.thumbstickCheckTurn);

        activeChecker?.ResetChecker();
    }

    /// <summary>Включает 3D-объект текущего шага (например, модели контроллеров), выключает у всех остальных шагов.</summary>
    private void UpdateStepSceneObject(TutorialStepData currentStep)
    {
        foreach (TutorialStepData step in steps)
        {
            if (step.stepSceneObject == null) continue;
            step.stepSceneObject.SetActive(step == currentStep);
        }
    }

    /// <summary>
    /// Переключает Canvas из Screen Space - Camera в World Space - панель перестаёт следовать
    /// за головой и остаётся висеть в комнате там, где была в момент переключения (Unity сама
    /// каждый кадр выставляла transform.position/rotation/localScale канваса под текущий вид
    /// камеры, пока был активен Screen Space - Camera, поэтому просто меняем renderMode, ничего
    /// не трогая в самом Transform - скачка в позиции не будет).
    /// </summary>
    private void DetachPanelFromCamera()
    {
        if (tutorialCanvas == null || panelCurrentlyDetached)
            return;

        tutorialCanvas.renderMode = RenderMode.WorldSpace;
        panelCurrentlyDetached = true;
    }

    /// <summary>Возвращает Canvas обратно в Screen Space - Camera с исходными настройками камеры/дистанции.</summary>
    private void ReattachPanelToCamera()
    {
        if (tutorialCanvas == null || !panelCurrentlyDetached)
            return;

        tutorialCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        tutorialCanvas.worldCamera = originalCanvasWorldCamera;
        tutorialCanvas.planeDistance = originalCanvasPlaneDistance;
        panelCurrentlyDetached = false;
    }

    private void PlayStepMedia(TutorialStepData step)
    {
        // Останавливаем предыдущую озвучку сразу - не должно быть слышно голос от прошлого шага,
        // пока новое видео ещё готовится.
        if (narrationAudioSource != null)
            narrationAudioSource.Stop();

        // Материал RawImage под конкретное видео этого шага (например свой хромакей-материал под
        // свой цвет фона) - если не задан, возвращаем исходный (без хромакея), чтобы не тянуть
        // материал предыдущего шага туда, где он не нужен.
        if (videoRawImage != null)
            videoRawImage.material = step.videoMaterial != null ? step.videoMaterial : originalVideoRawImageMaterial;

        if (videoPlayer == null || step.videoClip == null)
        {
            // Видео для этого шага нет (например, используется 3D-объект вместо видео) -
            // тогда озвучку (если есть) запускаем сразу, ждать нечего.
            if (videoPlayer != null) videoPlayer.Stop();
            PlayNarration(step);
            return;
        }

        // Снимаем предыдущую подписку на случай, если предыдущее видео ещё не успело подготовиться -
        // иначе при быстром переключении шагов может сработать callback от предыдущего видео поверх нового.
        videoPlayer.prepareCompleted -= OnVideoPrepared;

        videoPlayer.Stop();
        videoPlayer.clip = step.videoClip;
        videoPlayer.isLooping = step.loopVideo;
        videoPlayer.prepareCompleted += OnVideoPrepared;
        videoPlayer.Prepare();
    }

    private void OnVideoPrepared(VideoPlayer source)
    {
        source.prepareCompleted -= OnVideoPrepared;
        source.Play();

        // Озвучку стартуем в тот же момент, что и видео - не раньше (пока видео готовилось,
        // голос бы опережал картинку).
        PlayNarration(steps[currentStepIndex]);
    }

    private void PlayNarration(TutorialStepData step)
    {
        if (narrationAudioSource == null || step.narrationClip == null)
            return;

        narrationAudioSource.clip = step.narrationClip;
        narrationAudioSource.Play();
    }

    /// <summary>
    /// Страховка: по истечении времени ожидания шаг переключается САМ, кнопка "Далее" становится
    /// доступна параллельно. Запускается уже ПОСЛЕ того, как детектор реально начал слушать
    /// действие (см. ArmCheckerAfterNarration) - то есть отсчитывает время именно на само действие,
    /// не на прослушивание озвучки перед ним.
    /// </summary>
    private IEnumerator RunFallbackTimer(TutorialStepData step)
    {
        yield return new WaitForSeconds(step.delayBeforeNextButton);

        SetNextButtonState(true);

        if (!advancedByChecker)
        {
            advancedByChecker = true;
            ShowStep(currentStepIndex + 1);
        }
    }

    private void SetNextButtonState(bool enabled)
    {
        if (nextButton != null)
            nextButton.interactable = enabled;

        if (nextButtonCanvasGroup != null)
            nextButtonCanvasGroup.alpha = enabled ? 1f : 0.5f;
    }

    private void RefreshStepList()
    {
        for (int i = 0; i < stepListItems.Count; i++)
        {
            if (i >= steps.Count) break;

            stepListItems[i].SetText($"Шаг {i + 1}: {steps[i].stepTitle}");

            if (i < currentStepIndex)
                stepListItems[i].SetState(StepListItemUI.StepState.Completed);
            else if (i == currentStepIndex)
                stepListItems[i].SetState(StepListItemUI.StepState.Active);
            else
                stepListItems[i].SetState(StepListItemUI.StepState.Upcoming);
        }
    }

    private void OnNextButtonPressed()
    {
        if (nextButton == null || !nextButton.interactable || advancedByChecker)
            return;

        ShowStep(currentStepIndex + 1);
    }

    private void OnBackButtonPressed()
    {
        if (backButton == null || !backButton.interactable || currentStepIndex <= 0)
            return;

        ShowStep(currentStepIndex - 1);
    }

    private void OnSkipButtonPressed()
    {
        if (skipConfirmPanel != null)
        {
            skipConfirmPanel.SetActive(true);
            return;
        }

        // Панели подтверждения нет - пропускаем сразу, как раньше.
        PerformSkip();
    }

    private void OnSkipConfirmed()
    {
        if (skipConfirmPanel != null)
            skipConfirmPanel.SetActive(false);

        PerformSkip();
    }

    private void OnSkipCancelled()
    {
        if (skipConfirmPanel != null)
            skipConfirmPanel.SetActive(false);
    }

    private void PerformSkip()
    {
        // Останавливаем всё, что могло ждать конца озвучки текущего шага/фразы закрепления.
        if (checkerActivationCoroutine != null)
            StopCoroutine(checkerActivationCoroutine);

        if (buttonTimerCoroutine != null)
            StopCoroutine(buttonTimerCoroutine);

        activeChecker = null;

        // В отличие от обычного прохождения (детектор → CompleteTutorial → озвучка → зона практики
        // → кнопка Продолжить), подтверждённый пропуск ведёт СРАЗУ в следующую сцену - без церемонии
        // завершения, без озвучки, без зоны практики. Пользователь явно попросил пропустить всё.
        LoadNextSceneOrClosePanel();
    }

    private void CompleteTutorial()
    {
        activeChecker = null;

        // Короткий звук успеха - сразу в момент завершения, отдельным AudioSource от голоса,
        // чтобы точно не перебивал и не перебивался последующей голосовой озвучкой.
        if (sfxAudioSource != null && successSfxClip != null)
            sfxAudioSource.PlayOneShot(successSfxClip);

        // Зачёркиваем и последний шаг тоже
        if (stepListItems.Count > 0)
        {
            int lastIndex = Mathf.Min(steps.Count, stepListItems.Count) - 1;
            if (lastIndex >= 0)
                stepListItems[lastIndex].SetState(StepListItemUI.StepState.Completed);
        }

        if (videoPlayer != null)
            videoPlayer.Stop();

        if (narrationAudioSource != null)
            narrationAudioSource.Stop();

        // Выключаем 3D-объекты всех шагов
        foreach (TutorialStepData step in steps)
        {
            if (step.stepSceneObject != null)
                step.stepSceneObject.SetActive(false);
        }

        // Убираем затемнение фона - инструктаж закончен, пользователь может свободно оглядеться
        if (backgroundDimmer != null)
            backgroundDimmer.SetActive(false);

        if (completionPanel != null)
            completionPanel.SetActive(true);

        onTutorialCompleted?.Invoke();

        // Озвучиваем "обучение завершено", затем открываем зону практики (если есть) и показываем
        // кнопку "Продолжить" - переход в другую сцену только по явному нажатию, не по таймеру.
        StartCoroutine(PlayCompletionMessageThenShowPractice());
    }

    private IEnumerator PlayCompletionMessageThenShowPractice()
    {
        if (narrationAudioSource != null && completionNarrationClip != null)
        {
            narrationAudioSource.clip = completionNarrationClip;
            narrationAudioSource.Play();
            yield return new WaitForSeconds(completionNarrationClip.length + 1f);
        }
        else
        {
            yield return new WaitForSeconds(delayBeforeSceneLoad);
        }

        // Зона свободной тренировки (предметы для захвата, рисование и т.п.) - включаем, только
        // теперь, после того как объяснили, что обучение закончено.
        if (practiceAreaRoot != null)
            practiceAreaRoot.SetActive(true);

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
            continueButton.interactable = true;
        }
    }

    private void OnContinueButtonPressed()
    {
        LoadNextSceneOrClosePanel();
    }

    /// <summary>
    /// Общая логика перехода в следующую сцену - используется и кнопкой "Продолжить" (после полной
    /// церемонии завершения), и подтверждённым "Пропустить" (сразу, без церемонии). Приоритет -
    /// централизованный AppSceneManager, если он есть в проекте; прямое имя сцены (Next Scene Name)
    /// остаётся как fallback для простых случаев без единого менеджера порядка сцен.
    /// </summary>
    private void LoadNextSceneOrClosePanel()
    {
        if (AppSceneManager.Instance != null)
        {
            AppSceneManager.Instance.LoadNextScene();
            return;
        }

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        // Ни AppSceneManager, ни прямое имя сцены не заданы - просто закрываем экран завершения
        // (если он вообще успел появиться), пользователь остаётся на месте без перехода куда-либо.
        if (completionPanel != null)
            completionPanel.SetActive(false);

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// Перезапускает видео текущего шага с начала. Озвучку, детектор и таймер-страховку не
    /// трогает - только сам видеоролик, чисто "пересмотреть демонстрацию ещё раз".
    /// </summary>
    private void OnReplayVideoButtonPressed()
    {
        if (videoPlayer == null || videoPlayer.clip == null)
            return;

        videoPlayer.Stop();
        videoPlayer.Play();
    }

    /// <summary>Публичный метод, если нужно принудительно перейти на конкретный шаг (например, кнопка "Назад" или пропуск).</summary>
    public void GoToStep(int index) => ShowStep(index);

    public int CurrentStepIndex => currentStepIndex;
}