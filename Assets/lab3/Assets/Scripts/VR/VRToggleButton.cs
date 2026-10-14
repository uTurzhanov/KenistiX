using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

namespace RayOptics.VR
{
    /// <summary>
    /// A physical toggle button for VR controllers, designed to be placed by hand
    /// in the scene/prefab — NOT generated at runtime.
    ///
    /// Setup
    /// ─────
    ///   1. Create your own button GameObject (any mesh — cube, sphere, custom model).
    ///   2. Add a Collider (non-trigger) and an XRSimpleInteractable (required).
    ///   3. Add this component.
    ///   4. Assign <see cref="target"/> and <see cref="targetField"/> to choose which
    ///      boolean this button reads/writes, OR leave target empty and wire
    ///      <see cref="onToggledOn"/> / <see cref="onToggledOff"/> / <see cref="onToggled"/>
    ///      in the Inspector for full custom control.
    ///   5. Position, rotate, and scale the button GameObject exactly where you want
    ///      it — this component does not move or resize anything.
    ///
    /// Visual design:
    ///   Assign a Renderer under this GameObject (or on it directly) and this
    ///   component will tint it via MaterialPropertyBlock:
    ///     ON      → <see cref="onColor"/>
    ///     OFF     → <see cref="offColor"/>
    ///     Hovered → <see cref="hoverColor"/>
    ///
    /// Interaction:
    ///   Uses XRSimpleInteractable. Pressing the trigger while a Direct or Ray
    ///   interactor is hovering the button fires a select, which toggles the
    ///   state once per press. (XRSimpleInteractable's "activated" event is
    ///   intentionally NOT used here — it only fires for an interactable that
    ///   is already selected/held, which never applies to a stationary button.)
    /// </summary>
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
    [AddComponentMenu("Rail Optics/VR/VR Toggle Button")]
    public class VRToggleButton : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Target binding — pick a common field, or leave None and use events
        // ─────────────────────────────────────────────────────────────────────

        public enum TargetField
        {
            None,               // no automatic binding — use the UnityEvents below
            RailModule_OnRail,
            RailModule_PinnedRailPosition,
            RailModule_PinnedModuleHeight,
            LightSource_Enabled,
        }

        [Header("Target Binding")]
        [Tooltip("Component to read/write. Must match the type expected by Target Field " +
                 "(RailModule for the RailModule_* options, LightSource for LightSource_Enabled). " +
                 "Leave empty and set Target Field to None to drive this button purely via " +
                 "the OnToggled events below.")]
        public Component target;

        [Tooltip("Which boolean on Target this button reads and writes.")]
        public TargetField targetField = TargetField.None;

        [Header("Events (optional / custom binding)")]
        [Tooltip("Invoked every time the button is pressed, with the new state.")]
        public UnityEvent<bool> onToggled;

        [Tooltip("Invoked when the button is pressed and the new state is true.")]
        public UnityEvent onToggledOn;

        [Tooltip("Invoked when the button is pressed and the new state is false.")]
        public UnityEvent onToggledOff;

        // ─────────────────────────────────────────────────────────────────────
        //  Appearance
        // ─────────────────────────────────────────────────────────────────────

        [Header("Appearance")]
        [Tooltip("Renderer to tint. If left empty, the first Renderer found on this " +
                 "GameObject or its children is used.")]
        public Renderer targetRenderer;

        public Color onColor     = new Color(0.2f, 0.9f, 0.3f);
        public Color offColor    = new Color(0.25f, 0.25f, 0.25f);
        public Color hoverColor  = new Color(0.6f, 0.9f, 0.65f);

        [Header("Press Animation")]
        [Tooltip("Optional: the visual face that depresses when pressed. " +
                 "If left empty, no press animation plays (recommended when you have " +
                 "your own animated/mechanical button model).")]
        public Transform pressVisual;

        [Tooltip("How far the press visual depresses along its local -Z when activated.")]
        [Range(0f, 0.02f)]
        public float pressDepth = 0.005f;

        // ── Runtime state ────────────────────────────────────────────────────
        private bool _currentState;

        /// <summary>
        /// Whether this button currently has a valid target binding. Evaluated
        /// live (not cached) so that reassigning Target/Target Field in the
        /// Inspector — including while in Play mode — takes effect immediately,
        /// instead of being frozen at whatever was true during Awake().
        /// </summary>
        private bool HasTarget => targetField != TargetField.None && target != null;

        // ── Cached refs ───────────────────────────────────────────────────────
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable  _interactable;
        private MaterialPropertyBlock _mpb;
        private Vector3                _pressRestLocalPos;
        private bool                   _pressed;

        // Resolved shader colour property (differs between Built-in and URP/HDRP)
        private static readonly int ColorPropBuiltin = Shader.PropertyToID("_Color");
        private static readonly int ColorPropURP     = Shader.PropertyToID("_BaseColor");
        private int _resolvedColorProp;

        // ─────────────────────────────────────────────────────────────────────
        //  Initialisation
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
            _mpb          = new MaterialPropertyBlock();

            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();

            if (pressVisual != null)
                _pressRestLocalPos = pressVisual.localPosition;

            _resolvedColorProp = ResolveColorProperty();

            _currentState = ReadState();
            RefreshColor();

            // NOTE: XRSimpleInteractable.activated only fires for a SELECTED
            // (held) interactable. A stationary button needs selectEntered.
            _interactable.selectEntered.AddListener(OnPressed);
            _interactable.hoverEntered.AddListener(OnHoverEnter);
            _interactable.hoverExited.AddListener(OnHoverExit);
        }

        private void OnDestroy()
        {
            if (_interactable != null)
            {
                _interactable.selectEntered.RemoveListener(OnPressed);
                _interactable.hoverEntered.RemoveListener(OnHoverEnter);
                _interactable.hoverExited.RemoveListener(OnHoverExit);
            }
        }

        private void Update()
        {
            // Poll the bound field so external changes (e.g. the rail constraint
            // system toggling onRail, or a value edited directly in the Inspector
            // while Play mode is running) are reflected visually without needing
            // an event round-trip.
            if (HasTarget)
            {
                bool state = ReadState();
                if (state != _currentState)
                {
                    _currentState = state;
                    RefreshColor();
                }
            }

            // Animate press return
            if (_pressed && pressVisual != null)
            {
                pressVisual.localPosition = Vector3.Lerp(
                    pressVisual.localPosition, _pressRestLocalPos, Time.deltaTime * 20f);
                if (Vector3.Distance(pressVisual.localPosition, _pressRestLocalPos) < 0.0001f)
                {
                    pressVisual.localPosition = _pressRestLocalPos;
                    _pressed = false;
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Current toggle state, read-only from outside.</summary>
        public bool CurrentState => _currentState;

        /// <summary>Force-set the visual state without firing a press.</summary>
        public void SetVisualState(bool state)
        {
            _currentState = state;
            RefreshColor();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Target field read/write
        // ─────────────────────────────────────────────────────────────────────

        private bool ReadState()
        {
            if (!HasTarget) return _currentState;

            switch (targetField)
            {
                case TargetField.RailModule_OnRail:
                    return target is RailModule rm1 && rm1.onRail;
                case TargetField.RailModule_PinnedRailPosition:
                    return target is RailModule rm2 && rm2.pinnedRailPosition;
                case TargetField.RailModule_PinnedModuleHeight:
                    return target is RailModule rm3 && rm3.pinnedModuleHeight;
                case TargetField.LightSource_Enabled:
                    return target is LightSource ls && ls.enabled;
                default:
                    return _currentState;
            }
        }

        /// <summary>
        /// Attempts to write <paramref name="value"/> to the bound target field.
        /// Returns true if the write actually happened, false if it was blocked
        /// by a guard (e.g. PinnedRailPosition requires onRail == true) or there
        /// was no valid target — so callers can avoid flipping the visual state
        /// when nothing actually changed underneath.
        /// </summary>
        private bool WriteState(bool value)
        {
            if (!HasTarget) return false;

            switch (targetField)
            {
                case TargetField.RailModule_OnRail:
                    if (target is RailModule rm1) { rm1.onRail = value; return true; }
                    return false;

                case TargetField.RailModule_PinnedRailPosition:
                    // Only meaningful while on rail — mirrors the previous ModuleVRUI guard.
                    if (target is RailModule rm2 && rm2.onRail) { rm2.pinnedRailPosition = value; return true; }
                    return false;

                case TargetField.RailModule_PinnedModuleHeight:
                    if (target is RailModule rm3) { rm3.pinnedModuleHeight = value; return true; }
                    return false;

                case TargetField.LightSource_Enabled:
                    if (target is LightSource ls) { ls.enabled = value; return true; }
                    return false;

                default:
                    return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  XRI event handlers
        // ─────────────────────────────────────────────────────────────────────

        private void OnPressed(SelectEnterEventArgs args)
        {
            bool newState = !_currentState;
            bool wrote    = WriteState(newState);

            // Only commit the visual flip and fire callbacks if the underlying
            // boolean actually changed. Previously this happened unconditionally,
            // which meant a blocked write (e.g. trying to pin rail position while
            // not on rail) still flashed the button colour as if it worked —
            // then Update()'s live poll would silently revert it a frame later,
            // looking like "pressing the button does nothing."
            if (!wrote)
                return;

            _currentState = newState;
            RefreshColor();

            onToggled?.Invoke(newState);
            if (newState) onToggledOn?.Invoke();
            else           onToggledOff?.Invoke();

            if (pressVisual != null)
            {
                _pressed = true;
                pressVisual.localPosition = _pressRestLocalPos - pressVisual.forward * pressDepth;
            }
        }

        private void OnHoverEnter(HoverEnterEventArgs args)
        {
            if (targetRenderer == null) return;
            _mpb.SetColor(_resolvedColorProp, hoverColor);
            targetRenderer.SetPropertyBlock(_mpb);
        }

        private void OnHoverExit(HoverExitEventArgs args) => RefreshColor();

        // ─────────────────────────────────────────────────────────────────────
        //  Visual
        // ─────────────────────────────────────────────────────────────────────

        private void RefreshColor()
        {
            if (targetRenderer == null) return;
            _mpb.SetColor(_resolvedColorProp, _currentState ? onColor : offColor);
            targetRenderer.SetPropertyBlock(_mpb);
        }

        private int ResolveColorProperty()
        {
            if (targetRenderer == null || targetRenderer.sharedMaterial == null)
                return ColorPropBuiltin;

            var mat = targetRenderer.sharedMaterial;

            if (mat.HasProperty(ColorPropURP))     return ColorPropURP;
            if (mat.HasProperty(ColorPropBuiltin)) return ColorPropBuiltin;

            Debug.LogWarning($"[VRToggleButton] Material '{mat.name}' on '{name}' has neither " +
                             "'_BaseColor' nor '_Color'. Colour changes will not be visible.", this);
            return ColorPropBuiltin;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Guard against assigning a target component type that doesn't match
            // the selected field, to catch mistakes early in the Inspector.
            if (target == null || targetField == TargetField.None) return;

            bool ok = targetField switch
            {
                TargetField.RailModule_OnRail             => target is RailModule,
                TargetField.RailModule_PinnedRailPosition => target is RailModule,
                TargetField.RailModule_PinnedModuleHeight => target is RailModule,
                TargetField.LightSource_Enabled           => target is LightSource,
                _ => true,
            };

            if (!ok)
                Debug.LogWarning($"[VRToggleButton] On '{name}': Target Field '{targetField}' " +
                                 $"expects a different component type than the assigned Target " +
                                 $"('{target.GetType().Name}').", this);
        }
#endif
    }
}
