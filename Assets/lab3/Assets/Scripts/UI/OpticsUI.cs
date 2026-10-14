using UnityEngine;
using UnityEngine.UI;
using TMPro; // Optional: remove if not using TextMeshPro

namespace RayOptics
{
    /// <summary>
    /// Simple HUD that exposes the mode toggle and ray count slider.
    ///
    /// Assign the fields in the Inspector, or call SetupDefaultUI() at runtime
    /// to auto-create a minimal canvas.
    ///
    /// Wire-up (Inspector):
    ///   modeToggle       → Toggle (UI)   → calls OnModeToggled(bool)
    ///   rayCountSlider   → Slider (UI)   → calls OnRayCountChanged(float)
    ///   modeLabel        → TMP_Text (UI) / Text (UI)
    /// </summary>
    [AddComponentMenu("Ray Optics/UI/Optics UI Controller")]
    public class OpticsUI : MonoBehaviour
    {
        [Header("References")]
        public OpticsManager opticsManager;

        [Header("UI Elements (optional – auto-created if null)")]
        public Toggle    modeToggle;
        public Slider    rayCountSlider;

        // Works with both legacy Text and TextMeshPro
        public Text      modeLabelLegacy;

        [Header("Labels")]
        public string regularModeLabel      = "Regular Rays";
        public string correctBrightLabel    = "Correct Brightness";

        // ─────────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────────

        private void Start()
        {
            if (opticsManager == null)
                opticsManager = FindObjectOfType<OpticsManager>();

            if (opticsManager == null)
            {
                Debug.LogWarning("[OpticsUI] No OpticsManager found in scene.");
                return;
            }

            if (modeToggle == null && rayCountSlider == null)
                SetupDefaultUI();

            // Initialise controls to match manager state
            if (modeToggle != null)
            {
                modeToggle.isOn = opticsManager.correctBrightnessMode;
                modeToggle.onValueChanged.AddListener(OnModeToggled);
            }

            if (rayCountSlider != null)
            {
                rayCountSlider.minValue = 16;
                rayCountSlider.maxValue = 1024;
                rayCountSlider.wholeNumbers = true;
                rayCountSlider.value = opticsManager.raysPerSourceCB;
                rayCountSlider.onValueChanged.AddListener(OnRayCountChanged);
                rayCountSlider.gameObject.SetActive(opticsManager.correctBrightnessMode);
            }

            UpdateLabel();
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Callbacks
        // ─────────────────────────────────────────────────────────────────────────

        public void OnModeToggled(bool isOn)
        {
            if (opticsManager == null) return;
            opticsManager.correctBrightnessMode = isOn;
            if (rayCountSlider != null)
                rayCountSlider.gameObject.SetActive(isOn);
            UpdateLabel();
        }

        public void OnRayCountChanged(float value)
        {
            if (opticsManager == null) return;
            opticsManager.raysPerSourceCB = (int)value;
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────────

        private void UpdateLabel()
        {
            if (opticsManager == null) return;
            string label = opticsManager.correctBrightnessMode
                ? correctBrightLabel : regularModeLabel;

            if (modeLabelLegacy != null)
                modeLabelLegacy.text = label;
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  Runtime canvas bootstrap
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a minimal canvas with a toggle button if no UI elements are wired up.
        /// Call this at runtime to get a working HUD without any prefab setup.
        /// </summary>
        public void SetupDefaultUI()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasGO = new GameObject("OpticsCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
                    CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            // Panel background
            var panelGO = new GameObject("OpticsPanel");
            panelGO.transform.SetParent(canvas.transform, false);
            var panelImg = panelGO.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.6f);
            var panelRT = panelGO.GetComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0f, 1f);
            panelRT.anchorMax = new Vector2(0f, 1f);
            panelRT.pivot     = new Vector2(0f, 1f);
            panelRT.anchoredPosition = new Vector2(10f, -10f);
            panelRT.sizeDelta = new Vector2(260f, 110f);

            // Mode label (legacy Text)
            var labelGO = new GameObject("ModeLabel");
            labelGO.transform.SetParent(panelGO.transform, false);
            var labelText = labelGO.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 14;
            labelText.color = Color.white;
            labelText.text = regularModeLabel;
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0f, 1f);
            labelRT.anchorMax = new Vector2(1f, 1f);
            labelRT.pivot     = new Vector2(0.5f, 1f);
            labelRT.anchoredPosition = new Vector2(0f, -10f);
            labelRT.sizeDelta = new Vector2(0f, 24f);
            modeLabelLegacy = labelText;

            // Toggle
            var toggleGO = new GameObject("ModeToggle");
            toggleGO.transform.SetParent(panelGO.transform, false);
            modeToggle = CreateToggle(toggleGO, "Correct Brightness", new Vector2(10f, -45f));
            modeToggle.isOn = opticsManager.correctBrightnessMode;
            modeToggle.onValueChanged.AddListener(OnModeToggled);

            // Ray count slider
            var sliderGO = new GameObject("RayCountSlider");
            sliderGO.transform.SetParent(panelGO.transform, false);
            rayCountSlider = CreateSlider(sliderGO, new Vector2(10f, -82f), 240f);
            rayCountSlider.minValue = 16;
            rayCountSlider.maxValue = 1024;
            rayCountSlider.wholeNumbers = true;
            rayCountSlider.value = opticsManager.raysPerSourceCB;
            rayCountSlider.onValueChanged.AddListener(OnRayCountChanged);
            rayCountSlider.gameObject.SetActive(opticsManager.correctBrightnessMode);
        }

        private static Toggle CreateToggle(GameObject go, string label, Vector2 pos)
        {
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(240f, 30f);

            var toggle = go.AddComponent<Toggle>();

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(go.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchoredPosition = new Vector2(0f, 0f);
            bgRT.sizeDelta = new Vector2(20f, 20f);
            bgRT.anchorMin = new Vector2(0f, 0.5f);
            bgRT.anchorMax = new Vector2(0f, 0.5f);
            bgRT.pivot = new Vector2(0f, 0.5f);
            bgRT.anchoredPosition = new Vector2(5f, 0f);

            // Checkmark
            var checkGO = new GameObject("Checkmark");
            checkGO.transform.SetParent(bgGO.transform, false);
            var checkImg = checkGO.AddComponent<Image>();
            checkImg.color = new Color(0.3f, 0.9f, 0.3f);
            var checkRT = checkGO.GetComponent<RectTransform>();
            checkRT.anchorMin = Vector2.zero;
            checkRT.anchorMax = Vector2.one;
            checkRT.offsetMin = new Vector2(4f, 4f);
            checkRT.offsetMax = new Vector2(-4f, -4f);

            // Label
            var lblGO = new GameObject("Label");
            lblGO.transform.SetParent(go.transform, false);
            var lblText = lblGO.AddComponent<Text>();
            lblText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lblText.fontSize = 13;
            lblText.color = Color.white;
            lblText.text = label;
            var lblRT = lblGO.GetComponent<RectTransform>();
            lblRT.anchorMin = new Vector2(0f, 0f);
            lblRT.anchorMax = new Vector2(1f, 1f);
            lblRT.offsetMin = new Vector2(30f, 0f);
            lblRT.offsetMax = Vector2.zero;

            toggle.targetGraphic = bgImg;
            toggle.graphic       = checkImg;

            return toggle;
        }

        private static Slider CreateSlider(GameObject go, Vector2 pos, float width)
        {
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot     = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 20f);

            var slider = go.AddComponent<Slider>();

            // Background
            var bgGO = new GameObject("Background");
            bgGO.transform.SetParent(go.transform, false);
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0f, 0.5f);
            bgRT.anchorMax = new Vector2(1f, 0.5f);
            bgRT.sizeDelta = new Vector2(0f, 6f);

            // Fill area
            var fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(go.transform, false);
            var fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0f, 0.5f);
            fillAreaRT.anchorMax = new Vector2(1f, 0.5f);
            fillAreaRT.sizeDelta = new Vector2(-20f, 6f);
            fillAreaRT.anchoredPosition = new Vector2(-5f, 0f);

            var fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = new Color(0.3f, 0.8f, 0.3f);
            var fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.sizeDelta = Vector2.zero;

            // Handle
            var handleAreaGO = new GameObject("Handle Slide Area");
            handleAreaGO.transform.SetParent(go.transform, false);
            var handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.sizeDelta = new Vector2(-20f, 0f);

            var handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            var handleImg = handleGO.AddComponent<Image>();
            handleImg.color = Color.white;
            var handleRT = handleGO.GetComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(20f, 20f);

            slider.fillRect        = fillRT;
            slider.handleRect      = handleRT;
            slider.targetGraphic   = handleImg;
            slider.direction       = Slider.Direction.LeftToRight;

            return slider;
        }
    }
}
