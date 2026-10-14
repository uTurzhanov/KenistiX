using UnityEngine;

namespace RayOptics.VR
{
    /// <summary>
    /// Manages contextual visibility for the three rail-constraint buttons on a
    /// <see cref="RailModule"/>.
    ///
    /// This component does NOT create any buttons. Place your own button
    /// GameObjects (each with a <see cref="VRToggleButton"/>) anywhere on the
    /// module in the editor, wire each one's Target/Target Field in its own
    /// Inspector, then drag those three buttons into the slots below so this
    /// component can show/hide them at the right times.
    ///
    /// Button visibility rules
    /// ────────────────────────
    ///   [onRail button]             — shown when the module is near a rail
    ///                                 (within <see cref="RailGrabbable.railDetectionDistance"/>,
    ///                                 reported via <see cref="SetNearRail"/>) OR when
    ///                                 already on rail.
    ///
    ///   [pinnedRailPosition button] — shown only while onRail == true.
    ///
    ///   [pinnedModuleHeight button] — shown while onRail == true, or while
    ///                                 pinnedModuleHeight == true (so a frozen
    ///                                 height stays reachable even off-rail).
    /// </summary>
    [AddComponentMenu("Rail Optics/VR/Module VR UI")]
    public class ModuleVRUI : MonoBehaviour
    {
        [Header("Button References")]
        [Tooltip("The button that toggles RailModule.onRail.")]
        public VRToggleButton onRailButton;

        [Tooltip("The button that toggles RailModule.pinnedRailPosition.")]
        public VRToggleButton pinnedPositionButton;

        [Tooltip("The button that toggles RailModule.pinnedModuleHeight.")]
        public VRToggleButton pinnedHeightButton;

        [Header("Near-Rail Detection")]
        [Tooltip("Set by VRGrabbable when this module is within railDetectionDistance " +
                 "of a rail. Controls visibility of the onRail button.")]
        public bool nearRail = false;

        // ── Cached refs ───────────────────────────────────────────────────────
        private RailModule _rail;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _rail = GetComponent<RailModule>();
        }

        private void Update()
        {
            if (_rail == null) return;
            UpdateVisibility();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Visibility logic
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateVisibility()
        {
            bool on     = _rail.onRail;
            bool pinned = _rail.pinnedModuleHeight;

            // onRail button: visible when near rail (so student can snap on)
            // OR when already on rail (so student can unsnap)
            bool showOnRail = nearRail || on;

            // pinnedRailPosition: only relevant while on rail
            bool showPinnedPos = on;

            // pinnedModuleHeight: shown when on rail (can set it), or when
            // it's already pinned (so student can unpin it even off rail)
            bool showPinnedHeight = on || pinned;

            SetButtonVisible(onRailButton,          showOnRail);
            SetButtonVisible(pinnedPositionButton,   showPinnedPos);
            SetButtonVisible(pinnedHeightButton,     showPinnedHeight);

            // Grey out pinnedRailPosition if onRail is false (button visible but inactive)
            if (pinnedPositionButton != null)
                pinnedPositionButton.enabled = on;
        }

        private static void SetButtonVisible(VRToggleButton btn, bool visible)
        {
            if (btn != null && btn.gameObject.activeSelf != visible)
                btn.gameObject.SetActive(visible);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Called by VRGrabbable to update near-rail state
        // ─────────────────────────────────────────────────────────────────────

        public void SetNearRail(bool near)
        {
            nearRail = near;
        }
    }
}
