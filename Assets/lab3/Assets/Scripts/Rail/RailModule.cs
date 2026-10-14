using UnityEngine;

namespace RayOptics
{
    /// <summary>
    /// A module that can be placed on a <see cref="RailManager"/> track.
    ///
    /// Hierarchy expected
    /// ──────────────────
    ///   [RailModule GameObject]       ← this component lives here; this transform
    ///   │                                is the RAIL ATTACHMENT POINT — the BOTTOM
    ///   │                                of the module, connected to the rail. It
    ///   │                                stays pinned to the rail line (X/Z/Y locked)
    ///   │                                and never moves as ModuleHeight changes.
    ///   │
    ///   ├─ Connector                  ← child named "Connector" (auto-created).
    ///   │      Grows UPWARD from the rail attachment point as ModuleHeight increases.
    ///   │      Its localScale.y = ModuleHeight and its localPosition.y = +ModuleHeight/2
    ///   │      (its centre sits halfway up). At ModuleHeight = 0 it sits at local Y = 0,
    ///   │      exactly on the rail attachment point — not offset.
    ///   │
    ///   └─ ModulePayload               ← child named "ModulePayload" (auto-created).
    ///          THIS is "the module itself" — put your lens/source/wall mesh and
    ///          optics components as children of this transform (or on it directly).
    ///          Its localPosition.y = +ModuleHeight, so it rises UP and away from the
    ///          rail as the connector grows, while the rail attachment point at the
    ///          bottom stays fixed on the rail.
    ///
    /// Attach your visual connector mesh (a thin cylinder, rod, etc.) to the
    /// Connector child; scale the mesh so that localScale.y = 1 equals the
    /// desired world length. Attach your actual optics GameObject to ModulePayload.
    ///
    /// Behaviour summary
    /// ──────────────────
    ///  onRail = true
    ///    • The rail attachment point (this transform, the BOTTOM of the module) is
    ///      locked to the rail's perpendicular position (e.g. Z axis stays fixed at
    ///      rail.Z) and snapped to the rail's Y coordinate. It faces along the rail
    ///      axis; flips 180° if the user rotates past ±90°.
    ///    • The Connector and ModulePayload rise UP above it by ModuleHeight.
    ///    • pinnedRailPosition = true → also locks the position along the rail axis.
    ///    • pinnedModuleHeight = false → ModuleHeight can be changed freely.
    ///    • pinnedModuleHeight = true  → ModuleHeight is frozen at its current value.
    ///
    ///  onRail = false
    ///    • The rail attachment point moves freely in 3D. Rail constraints are not applied.
    ///    • The connector length (ModuleHeight) is still preserved — it does NOT reset.
    ///    • pinnedModuleHeight = true → ModuleHeight stays frozen regardless of onRail.
    ///    • pinnedModuleHeight = false → ModuleHeight is NOT editable while off-rail
    ///      (there is no rail to measure from, so changing it would be meaningless).
    ///
    /// RailPosition and ModuleHeight
    /// ──────────────────────────────
    ///   RailPosition : read-only display value — signed distance along the rail from
    ///                  the rail centre (updated every frame when onRail is true).
    ///                  Can be read by UI or other scripts for labelling.
    ///   ModuleHeight : the desired connector length in world units, measured upward
    ///                  from the rail attachment point at the bottom. Editable only
    ///                  when onRail=true AND pinnedModuleHeight=false. Drives both the
    ///                  Connector's scale/position and the ModulePayload's position
    ///                  each frame. At ModuleHeight = 0 the connector and payload sit
    ///                  exactly at the rail attachment point.
    /// </summary>
    [AddComponentMenu("Rail Optics/Rail Module")]
    public class RailModule : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Rail Reference")]
        [Tooltip("The RailManager this module belongs to.")]
        public RailManager rail;

        [Header("Constraints")]
        [Tooltip("When true the module is attached to the rail — position and " +
                 "rotation are constrained to the rail geometry.")]
        public bool onRail = false;

        [Tooltip("Requires onRail = true. When true the module cannot slide " +
                 "along the rail axis — it is pinned at its current RailPosition.")]
        public bool pinnedRailPosition = false;

        [Tooltip("When true the connector length (ModuleHeight) is frozen and " +
                 "cannot be changed, regardless of onRail state.")]
        public bool pinnedModuleHeight = false;

        [Header("Display / Data")]
        [Tooltip("Signed distance of this module from the rail centre along the " +
                 "rail axis (world units). Read-only when onRail is true — updated " +
                 "automatically. Can be used for UI labelling or external calculations.")]
        public float railPosition = 0f;

        [Tooltip("Length of the connector between the rail and the module payload " +
                 "(world units). Editable only when onRail=true and pinnedModuleHeight=false.")]
        [Min(0f)]
        public float moduleHeight = 0.5f;

        [Header("Connector")]
        [Tooltip("Name of the child GameObject used as the connector. Created automatically " +
                 "if missing. Attach your rod/cylinder mesh here.")]
        public string connectorChildName = "Connector";

        [Tooltip("Name of the child GameObject that represents the module itself " +
                 "(the actual optics payload). Created automatically if missing. " +
                 "This is the transform that moves up/down as ModuleHeight changes — " +
                 "attach your lens/source/wall GameObject here, or parent it under this child. " +
                 "Ignored if Target Module is assigned below.")]
        public string payloadChildName = "ModulePayload";

        [Header("Target Module")]
        [Tooltip("Optional explicit reference to the transform that should move up/down " +
                 "with ModuleHeight (e.g. a lens, light source, or wall already in your scene). " +
                 "If assigned, this is used instead of the auto-created ModulePayload child — " +
                 "its localPosition.y is driven directly, so it must be a child of this " +
                 "RailModule's transform (or its position math will be wrong). " +
                 "If left empty, the ModulePayload child is created/used as before.")]
        public Transform targetModule;

        // ─────────────────────────────────────────────────────────────────────
        //  Runtime state
        // ─────────────────────────────────────────────────────────────────────

        // Cached connector transform (created/found in Awake)
        private Transform _connector;

        // Cached payload transform — "the module itself" — created/found in Awake
        private Transform _payload;

        // Frozen connector length — set when pinnedModuleHeight becomes true
        private float _frozenHeight = -1f;   // -1 = not yet frozen

        // The rail axis position at the moment pinnedRailPosition was enabled
        private float _pinnedAxisValue;
        private bool  _pinnedAxisValueSet = false;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            EnsureConnector();
            EnsurePayload();
        }

        private void Start()
        {
            if (rail != null)
                rail.RegisterModule(this);

            // Initialise frozen height from current moduleHeight if already pinned
            if (pinnedModuleHeight)
                _frozenHeight = moduleHeight;
        }

        private void OnDestroy()
        {
            if (rail != null)
                rail.UnregisterModule(this);
        }

        private void OnEnable()
        {
            if (rail != null)
                rail.RegisterModule(this);
        }

        private void OnDisable()
        {
            if (rail != null)
                rail.UnregisterModule(this);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Core constraint application — called by RailManager.LateUpdate
        // ─────────────────────────────────────────────────────────────────────

        // Set by VRGrabbable while the student is holding this module.
        // Suspends rail positional constraints so the module can move freely.
        private bool _isGrabbed = false;

        /// <summary>
        /// Called by VRGrabbable when the student picks up or releases this module.
        /// While grabbed, positional rail constraints are suspended.
        /// The connector height constraint (pinnedModuleHeight) is always respected.
        /// </summary>
        public void SetGrabbed(bool grabbed)
        {
            _isGrabbed = grabbed;
        }

        /// <summary>
        /// Called every LateUpdate by the <see cref="RailManager"/>.
        /// Applies all active constraints to this module's transform.
        /// </summary>
        public void ApplyRailConstraints(RailManager mgr)
        {
            // Connector height is always maintained (even while grabbed)
            ApplyConnectorHeight(mgr);

            // While grabbed, XRI controls position — don't fight it
            if (_isGrabbed) return;

            if (!onRail)
                return;

            if (rail == null || rail != mgr)
                return;

            Vector3 axis   = mgr.Axis;
            Vector3 pos    = transform.position;

            // ── 1. Lock perpendicular position (keep on rail line) ────────
            // Project current position onto the rail axis line, then reconstruct
            // the position as railLine + perp_of_rail_Y_plane.
            // "Perpendicular" here means: only the axis-aligned and Y components
            // are free; the remaining axis is locked to the rail's position.
            float  axisT       = mgr.ProjectOntoRail(pos, mgr.clampToBounds);
            Vector3 onAxisPt   = mgr.PointOnRail(axisT);

            // Lock the non-axis, non-Y component to the rail's value.
            // E.g. for X rail: X = axisT position, Y = free, Z = rail.Z
            Vector3 newPos;
            newPos.x = IsAxisAligned(axis, Vector3.right)
                ? onAxisPt.x
                : mgr.Centre.x;                  // non-axis: snap to rail centre X
            newPos.z = IsAxisAligned(axis, Vector3.forward)
                ? onAxisPt.z
                : mgr.Centre.z;                  // non-axis: snap to rail centre Z

            // Y is driven by the rail surface minus connector height
            newPos.y = mgr.RailY;

            // ── 2. Pinned rail position ────────────────────────────────────
            if (pinnedRailPosition)
            {
                if (!_pinnedAxisValueSet)
                {
                    // Capture the position at the moment of pinning
                    _pinnedAxisValue    = axisT;
                    _pinnedAxisValueSet = true;
                }

                Vector3 pinnedPt = mgr.PointOnRail(_pinnedAxisValue);
                if (IsAxisAligned(axis, Vector3.right))   newPos.x = pinnedPt.x;
                if (IsAxisAligned(axis, Vector3.forward)) newPos.z = pinnedPt.z;
            }
            else
            {
                _pinnedAxisValueSet = false;   // reset so next pin captures fresh position
            }

            transform.position = newPos;

            // ── 3. Update RailPosition display value ───────────────────────
            railPosition = mgr.ProjectOntoRail(transform.position);

            // ── 4. Rotation: face along the rail axis ─────────────────────
            ApplyFacingRotation(axis);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Connector height
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyConnectorHeight(RailManager mgr)
        {
            EnsureConnector();

            // Only auto-create/find the ModulePayload child if no explicit
            // targetModule has been assigned — if the user wired up their own
            // transform, we don't need the fallback child at all.
            if (targetModule == null)
                EnsurePayload();

            // Determine the effective height to use
            float effectiveHeight;

            if (pinnedModuleHeight)
            {
                // Freeze: capture current value if not yet done, then hold it
                if (_frozenHeight < 0f)
                    _frozenHeight = moduleHeight;

                effectiveHeight = _frozenHeight;
                // Write back so the Inspector field shows the frozen value
                moduleHeight = _frozenHeight;
            }
            else
            {
                // Unfreeze
                _frozenHeight = -1f;

                // Whether on rail or not, just use the current moduleHeight.
                // (Editability is enforced by the Inspector, not here — this
                // method only ever applies whatever value moduleHeight holds.)
                effectiveHeight = Mathf.Max(0f, moduleHeight);
            }

            effectiveHeight = Mathf.Max(0f, effectiveHeight);

            // ── The module itself: moves up/away from the rail ─────────────
            // The rail attachment point (this.transform, the BOTTOM of the
            // module) stays fixed on the rail; the moved transform rises
            // effectiveHeight above it in local space.
            //
            // If targetModule is explicitly assigned, drive that transform
            // directly. Otherwise fall back to the auto-created ModulePayload
            // child as before.
            Transform moved = targetModule != null ? targetModule : _payload;
            if (moved != null)
                moved.localPosition = new Vector3(0f, effectiveHeight, 0f);

            // ── Connector: visually spans the gap between rail and module ──
            // Centre of the connector sits halfway between the rail attachment
            // point (local Y = 0) and the moved transform (local Y = +effectiveHeight).
            // At effectiveHeight = 0 the connector's position is exactly 0 —
            // it does not jump to a non-zero offset.
            if (_connector != null)
            {
                Vector3 s = _connector.localScale;
                s.y = Mathf.Max(0.001f, effectiveHeight);
                _connector.localScale    = s;
                _connector.localPosition = new Vector3(0f, effectiveHeight * 0.5f, 0f);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Facing rotation
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyFacingRotation(Vector3 axis)
        {
            // Read the current Y rotation to detect which way the user has
            // rotated the module, so we can decide ±axis direction.
            float currentYaw = transform.eulerAngles.y;

            // Normalise to [−180, 180]
            if (currentYaw > 180f) currentYaw -= 360f;

            // If the user has rotated more than 90° away from the default
            // facing direction, flip to face the negative axis direction.
            bool facingPositive = Mathf.Abs(currentYaw) <= 90f;
            Vector3 faceDir = facingPositive ? axis : -axis;

            // Only rotate around Y (keep module upright)
            if (faceDir.sqrMagnitude > 0.001f)
            {
                Quaternion target = Quaternion.LookRotation(faceDir, Vector3.up);
                transform.rotation = target;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Find or create the Connector child transform.
        /// </summary>
        private void EnsureConnector()
        {
            if (_connector != null) return;

            Transform found = transform.Find(connectorChildName);
            if (found != null)
            {
                _connector = found;
                return;
            }

            // Create a new connector child
            var go = new GameObject(connectorChildName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;
            _connector = go.transform;
        }

        /// <summary>
        /// Find or create the ModulePayload child transform — "the module itself".
        /// This is the transform that moves up/down as ModuleHeight changes, while
        /// the RailModule's own transform stays pinned to the rail.
        /// </summary>
        private void EnsurePayload()
        {
            if (_payload != null) return;

            Transform found = transform.Find(payloadChildName);
            if (found != null)
            {
                _payload = found;
                return;
            }

            var go = new GameObject(payloadChildName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;
            _payload = go.transform;
        }

        /// <summary>
        /// Returns true if <paramref name="axis"/> is substantially aligned with
        /// <paramref name="candidate"/> (dot product > 0.9 in either direction).
        /// </summary>
        private static bool IsAxisAligned(Vector3 axis, Vector3 candidate)
            => Mathf.Abs(Vector3.Dot(axis.normalized, candidate.normalized)) > 0.9f;

        // ─────────────────────────────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            if (!onRail || rail == null) return;
            DrawModuleGizmo(new Color(1f, 0.75f, 0.2f, 0.4f));
        }

        private void OnDrawGizmosSelected()
        {
            DrawModuleGizmo(new Color(1f, 0.75f, 0.2f, 1f));

            // Show constraint state as coloured indicators
            if (pinnedRailPosition)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(transform.position, 0.08f);
            }

            if (pinnedModuleHeight)
            {
                Gizmos.color = Color.magenta;
                Vector3 bot = PayloadWorldPosition();
                Gizmos.DrawWireSphere(bot, 0.06f);
            }
        }

        private void DrawModuleGizmo(Color col)
        {
            Gizmos.color = col;
            Vector3 bottom = transform.position;          // rail attachment point — fixed on rail, BOTTOM
            Vector3 top    = PayloadWorldPosition();        // module itself — rises with height

            // Connector line: from rail attachment point up to the payload
            Gizmos.DrawLine(bottom, top);

            // Module body box at the payload position ("the module itself")
            float bs = 0.12f;
            Gizmos.DrawWireCube(top, new Vector3(bs * 2f, bs, bs * 2f));

            // Rail snap indicator (small sphere at the rail attachment point, bottom)
            Gizmos.DrawWireSphere(bottom, 0.05f);

            // Facing arrow, drawn from the payload position
            if (rail != null && onRail)
            {
                Gizmos.color = new Color(col.r, col.g, col.b, col.a * 0.7f);
                Gizmos.DrawRay(top, transform.forward * 0.25f);
            }
        }

        /// <summary>
        /// World-space position of the module payload — "the module itself" —
        /// computed the same way ApplyConnectorHeight positions it, so the gizmo
        /// always matches the actual runtime behaviour even before Play mode
        /// (when _payload may not yet be assigned). Rises directly above the
        /// rail attachment point (the bottom) by moduleHeight.
        /// </summary>
        private Vector3 PayloadWorldPosition()
        {
            float h = Mathf.Max(0f, moduleHeight);
            return transform.position + Vector3.up * h;
        }
    }
}