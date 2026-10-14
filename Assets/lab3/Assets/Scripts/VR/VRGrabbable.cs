using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace RayOptics.VR
{
    /// <summary>
    /// Attach to any module root GameObject to make it grabbable in VR.
    ///
    /// Requires: XRGrabInteractable on the same GameObject (added automatically),
    /// plus a Rigidbody set to isKinematic=true.
    ///
    /// Rail detection uses two independent distance thresholds, checked every
    /// frame regardless of whether the module is currently being held — so a
    /// module resting near a rail (e.g. placed there manually, or left over
    /// from a previous session) still shows its onRail button and can still
    /// auto-connect without needing to be picked up again:
    ///
    ///   railDetectionDistance      — if the module is within this distance of
    ///                                a rail, the onRail button becomes visible
    ///                                (via ModuleVRUI) so the student can choose
    ///                                to attach it. This does NOT attach the
    ///                                module by itself.
    ///
    ///   railAutoConnectionDistance — a much smaller distance (essentially
    ///                                "touching" the rail). If the module comes
    ///                                within this distance, onRail is toggled on
    ///                                automatically — no button press required,
    ///                                since the module is already physically
    ///                                placed on the rail. Should be noticeably
    ///                                smaller than railDetectionDistance.
    ///
    /// Toggling onRail off (via the button, or any other code path) pushes the
    /// module a small distance away from the rail — see <see cref="unsnapPushDistance"/> —
    /// so it lands outside railAutoConnectionDistance and doesn't immediately
    /// re-trigger auto-connect on the very next frame.
    /// </summary>
    [RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Rail Optics/VR/VR Grabbable")]
    public class VRGrabbable : MonoBehaviour
    {
        [Header("Grab Settings")]
        [Tooltip("While grabbed, the RailModule's onRail constraint is temporarily suspended " +
                 "so the student can freely move the module.")]
        public bool suspendRailWhileGrabbed = true;

        [Header("Rail Detection (two-tier)")]
        [Tooltip("Distance within which the onRail button becomes visible while holding " +
                 "this module, so the student can manually attach it. Does not auto-attach.")]
        [Range(0.05f, 2f)]
        public float railDetectionDistance = 0.4f;

        [Tooltip("Distance within which the module auto-attaches to the rail (onRail = true) " +
                 "without needing a button press — effectively \"touching\" the rail. " +
                 "Should be much smaller than Rail Detection Distance.")]
        [Range(0.005f, 0.5f)]
        public float railAutoConnectionDistance = 0.03f;

        [Tooltip("When onRail transitions from true to false, the module is pushed this far " +
                 "away from the rail (perpendicular to the rail axis) so it lands outside " +
                 "Rail Auto Connection Distance and doesn't immediately re-snap. Should be " +
                 "somewhat larger than Rail Auto Connection Distance.")]
        [Range(0.01f, 1f)]
        public float unsnapPushDistance = 0.1f;

        // ── Cached refs ───────────────────────────────────────────────────────
        private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grab;
        private Rigidbody          _rb;
        private RailModule         _railModule;    // may be null if not a rail module
        private ModuleVRUI         _vrUI;          // may be null
        private bool               _isGrabbed;

        // Tracks the previous onRail value so we can detect true→false transitions
        // (e.g. the student pressing the onRail button to detach) and push the
        // module away from the rail so it doesn't immediately re-snap.
        private bool _wasOnRail;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            _grab       = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            _rb         = GetComponent<Rigidbody>();
            _railModule = GetComponent<RailModule>();
            _vrUI       = GetComponent<ModuleVRUI>();

            if (_railModule != null)
                _wasOnRail = _railModule.onRail;

            // Kinematic rigidbody — physics engine doesn't move it, XRI does
            _rb.isKinematic = true;
            _rb.useGravity  = false;

            // Configure grab interactable
            _grab.movementType  = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Kinematic;
            _grab.trackPosition = true;
            _grab.trackRotation = true;
            _grab.throwOnDetach = false;

            // Hook events
            _grab.selectEntered.AddListener(OnGrabbed);
            _grab.selectExited.AddListener(OnReleased);
        }

        private void OnDestroy()
        {
            if (_grab != null)
            {
                _grab.selectEntered.RemoveListener(OnGrabbed);
                _grab.selectExited.RemoveListener(OnReleased);
            }
        }

        private void Update()
        {
            if (_railModule == null) return;

            // ── Detect onRail being turned off (e.g. via the VR button) and
            //    push the module away so it doesn't immediately re-trigger
            //    railAutoConnectionDistance on the very next frame. ──────────
            if (_wasOnRail && !_railModule.onRail)
                PushAwayFromRail();
            _wasOnRail = _railModule.onRail;

            // ── Proximity detection now runs regardless of whether the module
            //    is currently held, so a module simply resting near a rail still
            //    shows its onRail button and can still auto-connect. ─────────
            var (nearestRail, distance) = FindNearestRailWithDistance();

            if (_vrUI != null)
                _vrUI.SetNearRail(nearestRail != null && distance <= railDetectionDistance);

            // Tier 2: auto-attach once genuinely touching the rail — no button needed.
            // Still gated to only happen while NOT freshly pushed away this same
            // frame (PushAwayFromRail already guarantees distance > auto-connect
            // range immediately after, so no extra guard is needed here).
            if (nearestRail != null && distance <= railAutoConnectionDistance && !_railModule.onRail)
            {
                _railModule.rail   = nearestRail;
                _railModule.onRail = true;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Grab / Release callbacks
        // ─────────────────────────────────────────────────────────────────────

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            _isGrabbed = true;
            if (_railModule == null) return;

            if (suspendRailWhileGrabbed)
                _railModule.SetGrabbed(true);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            _isGrabbed = false;
            if (_vrUI != null) _vrUI.SetNearRail(false);
            if (_railModule == null) return;

            _railModule.SetGrabbed(false);

            // No forced snap-on-release logic here anymore — attachment is now
            // handled live during Update() via railAutoConnectionDistance, and
            // manual attachment via the onRail button (shown when within
            // railDetectionDistance). If neither happened while held, the
            // module simply stays at its released position with onRail
            // whatever it already was (false, unless auto-connected in-flight).
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Rail proximity
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Finds the nearest RailManager to this module's current position and
        /// the distance to it. Returns (null, float.MaxValue) if no rails exist.
        /// </summary>
        private (RailManager rail, float distance) FindNearestRailWithDistance()
        {
            RailManager[] allRails = FindObjectsOfType<RailManager>();
            RailManager   best     = null;
            float         bestDist = float.MaxValue;

            foreach (var rail in allRails)
            {
                float   t    = rail.ProjectOntoRail(transform.position, clamp: true);
                Vector3 pt   = rail.PointOnRail(t);
                float   dist = Vector3.Distance(transform.position, pt);

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best     = rail;
                }
            }

            return (best, bestDist);
        }

        /// <summary>
        /// Nudges the module away from whichever rail it was just detached from,
        /// perpendicular to the rail axis, by <see cref="unsnapPushDistance"/>.
        /// This guarantees the module lands outside railAutoConnectionDistance
        /// immediately after unsnapping, so it doesn't re-trigger auto-connect
        /// on the very next Update() — while still leaving it within
        /// railDetectionDistance so the onRail button stays available to
        /// re-attach with a single press if desired.
        /// </summary>
        private void PushAwayFromRail()
        {
            var (rail, distance) = FindNearestRailWithDistance();
            if (rail == null) return;

            // Only push if we're actually still close enough that re-snapping
            // would otherwise be immediate — no need to move a module that's
            // already far from any rail.
            if (distance > railAutoConnectionDistance * 2f) return;

            float   t         = rail.ProjectOntoRail(transform.position, clamp: true);
            Vector3 onRailPt  = rail.PointOnRail(t);
            Vector3 fromRail  = transform.position - onRailPt;

            // Pick a push direction perpendicular to the rail axis. If the module
            // is essentially exactly on the rail line (fromRail ~ zero, e.g. right
            // after auto-connecting), fall back to the rail's "up" reference so
            // there is always a well-defined direction to push toward.
            Vector3 pushDir = fromRail.sqrMagnitude > 1e-6f
                ? fromRail.normalized
                : Vector3.ProjectOnPlane(Vector3.up, rail.Axis).normalized;

            if (pushDir.sqrMagnitude < 1e-6f)
                pushDir = Vector3.ProjectOnPlane(Vector3.forward, rail.Axis).normalized;

            transform.position += pushDir * unsnapPushDistance;
        }
    }
}
