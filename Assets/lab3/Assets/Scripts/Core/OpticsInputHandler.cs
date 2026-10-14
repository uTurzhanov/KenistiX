using UnityEngine;

namespace RayOptics
{
    /// <summary>
    /// Optional runtime input handler.
    /// Attach to any GameObject alongside or separate from the OpticsManager.
    ///
    /// Default keybindings (all rebindable in Inspector):
    ///   B  – Toggle Regular / Correct Brightness mode
    ///   R  – Refresh scene objects (pick up newly spawned sources / blockers)
    ///   +  – Increase ray count (CB mode)
    ///   -  – Decrease ray count (CB mode)
    /// </summary>
    [AddComponentMenu("Ray Optics/Input Handler")]
    public class OpticsInputHandler : MonoBehaviour
    {
        [Header("References")]
        public OpticsManager opticsManager;

        [Header("Keybindings")]
        public KeyCode toggleModeKey    = KeyCode.B;
        public KeyCode refreshKey       = KeyCode.R;
        public KeyCode increaseRaysKey  = KeyCode.Equals;   // '+'
        public KeyCode decreaseRaysKey  = KeyCode.Minus;

        [Header("Ray Count Step")]
        [Tooltip("How many rays to add/remove per key press in CB mode.")]
        public int rayCountStep = 32;

        // ─────────────────────────────────────────────────────────────────────────

        private void Start()
        {
            if (opticsManager == null)
                opticsManager = FindObjectOfType<OpticsManager>();
        }

        private void Update()
        {
            if (opticsManager == null) return;

            if (Input.GetKeyDown(toggleModeKey))
            {
                opticsManager.correctBrightnessMode = !opticsManager.correctBrightnessMode;
                Debug.Log($"[Ray Optics] Mode → {(opticsManager.correctBrightnessMode ? "Correct Brightness" : "Regular")}");
            }

            if (Input.GetKeyDown(refreshKey))
                opticsManager.RefreshSceneObjects();

            if (opticsManager.correctBrightnessMode)
            {
                if (Input.GetKeyDown(increaseRaysKey))
                    opticsManager.raysPerSourceCB = Mathf.Clamp(
                        opticsManager.raysPerSourceCB + rayCountStep, 16, 4096);

                if (Input.GetKeyDown(decreaseRaysKey))
                    opticsManager.raysPerSourceCB = Mathf.Clamp(
                        opticsManager.raysPerSourceCB - rayCountStep, 16, 4096);
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        //  On-screen hint
        // ─────────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (opticsManager == null) return;

            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            float y = Screen.height - 90f;

            GUI.Label(new Rect(10, y,      300, 20), $"[{toggleModeKey}]  Toggle Mode");
            GUI.Label(new Rect(10, y + 20, 300, 20), $"[{refreshKey}]  Refresh Scene");
            if (opticsManager.correctBrightnessMode)
            {
                GUI.Label(new Rect(10, y + 40, 300, 20),
                    $"[+/-]  Ray count: {opticsManager.raysPerSourceCB}");
            }
            string modeStr = opticsManager.correctBrightnessMode
                ? "Correct Brightness" : "Regular Rays";
            GUI.color = opticsManager.correctBrightnessMode
                ? new Color(0.3f, 1f, 0.4f) : new Color(0.6f, 0.8f, 1f);
            GUI.Label(new Rect(10, y + 65, 300, 20), $"Mode: {modeStr}");
        }
    }
}
