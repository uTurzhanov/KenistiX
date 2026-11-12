using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

[System.Serializable]
public class InstructionStepData
{
    public string title;
    [TextArea(3, 5)]
    public string description;
    public Texture2D image;
}

public class LabDashboardController : MonoBehaviour
{
    [Header("Данные студента и эксперимента")]
    public string studentName = "КОНСТАНТИНОПОЛЬСКИЙ-СЕВЕРСКИЙ АЛЕКСАНДР АЛЕКСАНДРОВИЧ";
    [TextArea(2, 4)]
    public string experimentTitle = "ИССЛЕДОВАНИЕ ЗАКОНОВ ПОСЛЕДОВАТЕЛЬНОГО И ПАРАЛЛЕЛЬНОГО СОЕДИНЕНИЯ ЭЛЕКТРИЧЕСКИХ ЦЕПЕЙ";

    [Header("Секция видео")]
    public string videoTitle = "DEMO: ЭКСПЕРИМЕНТАЛЬНАЯ УСТАНОВКА";
    public VideoClip labVideoClip;

    [Header("Пошаговое руководство")]
    public List<InstructionStepData> instructionSteps = new List<InstructionStepData>();

    private VisualElement root;
    private VisualElement appRoot;
    private Button btnVideo, btnInstructions, btnCloseModal;
    private Toggle themeToggle;
    private VisualElement sectionVideo, sectionInstructions, modalOverlay, modalImageFull;
    private Label modalTitleLabel, modalNoImageText;
    private ScrollView instructionsScrollView;

    void OnEnable()
    {
        var uiDocument = GetComponent<UIDocument>();
        if (uiDocument == null) return;
        
        root = uiDocument.rootVisualElement;
        appRoot = root.Q<VisualElement>("app-root");

        btnVideo = root.Q<Button>("tab-video");
        btnInstructions = root.Q<Button>("tab-instructions");
        themeToggle = root.Q<Toggle>("theme-toggle");
        
        sectionVideo = root.Q<VisualElement>("section-video");
        sectionInstructions = root.Q<VisualElement>("section-instructions");
        instructionsScrollView = root.Q<ScrollView>("instructions-scroll-view");

        modalOverlay = root.Q<VisualElement>("modal-overlay");
        btnCloseModal = root.Q<Button>("btn-close-modal");
        modalImageFull = root.Q<VisualElement>("modal-image-full");
        modalTitleLabel = root.Q<Label>("modal-title");
        modalNoImageText = root.Q<Label>("modal-no-image-text");

        btnVideo.clicked += () => SwitchTab(btnVideo);
        btnInstructions.clicked += () => SwitchTab(btnInstructions);
        themeToggle.RegisterValueChangedCallback(OnThemeToggled);
        btnCloseModal?.RegisterCallback<ClickEvent>(evt => CloseModal());
        modalOverlay?.RegisterCallback<ClickEvent>(evt => { if (evt.target == modalOverlay) CloseModal(); });

        PopulateDashboardData();
    }

    private void PopulateDashboardData()
    {
        root.Q<Label>("label-student-name").text = studentName;
        root.Q<Label>("label-experiment-name").text = experimentTitle;
        root.Q<Label>("label-video-title").text = videoTitle;

        // --- ИНТЕГРАЦИЯ ВИДЕО ---
        var videoPlaceholder = root.Q<VisualElement>("video-placeholder");
        var player = GetComponent<VideoPlayer>();
        
        if (player != null && player.targetTexture != null)
        {
            if (labVideoClip != null) player.clip = labVideoClip;
            
            videoPlaceholder.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(player.targetTexture));
            videoPlaceholder.style.backgroundColor = new StyleColor(Color.white);
            videoPlaceholder.style.unityBackgroundImageTintColor = new StyleColor(Color.white);

            // Находим новые элементы управления
            var btnPlayPause = root.Q<Button>("btn-play-pause");
            var btnRewind = root.Q<Button>("btn-rewind");
            var btnForward = root.Q<Button>("btn-forward");
            var timeLabel = root.Q<Label>("label-video-time");

            // Логика кнопок
            btnPlayPause?.RegisterCallback<ClickEvent>(evt => 
            {
                if (player.isPlaying) player.Pause();
                else player.Play();
            });

            btnRewind?.RegisterCallback<ClickEvent>(evt => 
            {
                player.time = Mathf.Max(0, (float)player.time - 5f);
            });

            btnForward?.RegisterCallback<ClickEvent>(evt => 
            {
                player.time = Mathf.Min((float)player.length, (float)player.time + 5f);
            });

            // Цикл обновления (вызывается ~60 раз в секунду)
            videoPlaceholder.schedule.Execute(() => 
            {
                videoPlaceholder.MarkDirtyRepaint(); // Отрисовка кадров видео

                if (player.isPrepared)
                {
                    // Обновляем иконку Плей/Пауза
                    if (btnPlayPause != null)
                        btnPlayPause.text = player.isPlaying ? "⏸" : "▶";

                    // Обновляем таймер
                    if (timeLabel != null)
                    {
                        string current = string.Format("{0:00}:{1:00}", (int)player.time / 60, (int)player.time % 60);
                        string total = string.Format("{0:00}:{1:00}", (int)player.length / 60, (int)player.length % 60);
                        timeLabel.text = $"{current} / {total}";
                    }
                }
            }).Every(16);

            player.Play();
        }

        // --- ГЕНЕРАЦИЯ ШАГОВ ИНСТРУКЦИИ ---
        instructionsScrollView.Clear();

        for (int i = 0; i < instructionSteps.Count; i++)
        {
            var stepData = instructionSteps[i];
            string stepNumberTitle = $"{i + 1}. {stepData.title}";

            var stepElement = new VisualElement();
            stepElement.AddToClassList("instruction-step");

            var titleLabel = new Label(stepNumberTitle);
            titleLabel.AddToClassList("step-title");
            stepElement.Add(titleLabel);

            var descLabel = new Label(stepData.description);
            descLabel.AddToClassList("step-desc");
            stepElement.Add(descLabel);

            var imgPlaceholder = new VisualElement();
            imgPlaceholder.AddToClassList("step-image-placeholder");
            
            if (stepData.image != null)
            {
                imgPlaceholder.style.backgroundImage = new StyleBackground(stepData.image);
                imgPlaceholder.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }

            var btnEnlarge = new Button();
            btnEnlarge.text = "↗ Увеличить";
            btnEnlarge.AddToClassList("step-image-btn");
            
            Texture2D imgToPass = stepData.image;
            btnEnlarge.clicked += () => OpenModal(imgToPass, stepNumberTitle);
            
            imgPlaceholder.Add(btnEnlarge);
            stepElement.Add(imgPlaceholder);

            instructionsScrollView.Add(stepElement);
        }
    }

    private void SwitchTab(Button clickedButton)
    {
        btnVideo.RemoveFromClassList("active");
        btnInstructions.RemoveFromClassList("active");
        clickedButton.AddToClassList("active");

        if (clickedButton == btnVideo)
        {
            sectionVideo.style.display = DisplayStyle.Flex;
            sectionInstructions.style.display = DisplayStyle.None;
        }
        else if (clickedButton == btnInstructions)
        {
            sectionVideo.style.display = DisplayStyle.None;
            sectionInstructions.style.display = DisplayStyle.Flex;
        }
    }

    private void OnThemeToggled(ChangeEvent<bool> evt)
    {
        if (evt.newValue)
        {
            appRoot.RemoveFromClassList("theme-dark");
            appRoot.AddToClassList("theme-light");
        }
        else
        {
            appRoot.RemoveFromClassList("theme-light");
            appRoot.AddToClassList("theme-dark");
        }
    }

    private void OpenModal(Texture2D image, string stepTitle)
    {
        if (modalOverlay == null) return;

        modalTitleLabel.text = stepTitle;

        if (image != null)
        {
            modalImageFull.style.backgroundImage = new StyleBackground(image);
            modalNoImageText.style.display = DisplayStyle.None;
        }
        else
        {
            modalImageFull.style.backgroundImage = null;
            modalNoImageText.style.display = DisplayStyle.Flex;
        }

        modalOverlay.style.display = DisplayStyle.Flex;
    }

    private void CloseModal()
    {
        if (modalOverlay != null)
        {
            modalOverlay.style.display = DisplayStyle.None;
            modalImageFull.style.backgroundImage = null; 
        }
    }
}