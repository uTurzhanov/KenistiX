using UnityEngine;

namespace RayOptics.VR
{
    /// <summary>
    /// Keeps a hand-placed VR toggle button in sync with a light source's on/off
    /// height (if mounted on a rail) and provides a convenient single place to
    /// wire the light-enable button for this source.
    ///
    /// This component does NOT create any button. Place your own button
    /// GameObject (with a <see cref="VRToggleButton"/> configured with
    /// Target Field = LightSource_Enabled and Target = this LightSource) anywhere
    /// you like under the light source, then assign it to <see cref="lightButton"/>
    /// below.
    ///
    /// If the light source also has a <see cref="RailModule"/> (i.e. it is
    /// mounted on a rail), this component will keep the button's local Y
    /// position equal to RailModule.moduleHeight every frame, so the button
    /// rises/falls together with the module as the student adjusts its height.
    /// Set <see cref="followModuleHeight"/> to false to disable this and use
    /// the button's own placed position unmodified.
    /// </summary>
    [AddComponentMenu("Rail Optics/VR/Light Source VR Toggle")]
    public class LightSourceVRToggle : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("The LightSource this toggle affects. Defaults to one on this GameObject.")]
        public LightSource targetSource;

        [Header("Button Reference (place this by hand in the scene)")]
        [Tooltip("The hand-placed button that toggles targetSource.enabled. " +
                 "Configure its VRToggleButton with Target Field = LightSource_Enabled " +
                 "and Target = targetSource.")]
        public VRToggleButton lightButton;

        [Header("Height Follow (optional)")]
        [Tooltip("If true and a RailModule is present on this GameObject, the button's " +
                 "local Y position is overridden each frame to match RailModule.moduleHeight, " +
                 "so it visually stays attached to the top of the module as height changes.")]
        public bool followModuleHeight = true;

        // ── Cached refs ───────────────────────────────────────────────────────
        private RailModule _railModule;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (targetSource == null)
                targetSource = GetComponent<LightSource>();

            if (targetSource == null)
                Debug.LogWarning($"[LightSourceVRToggle] No LightSource found on {gameObject.name}. " +
                                 "Assign targetSource in the Inspector.", this);

            _railModule = GetComponent<RailModule>();
        }

        private void Update()
        {
            if (!followModuleHeight || _railModule == null || lightButton == null) return;

            Vector3 pos = lightButton.transform.localPosition;
            pos.y = _railModule.moduleHeight;
            lightButton.transform.localPosition = pos;
        }
    }
}
