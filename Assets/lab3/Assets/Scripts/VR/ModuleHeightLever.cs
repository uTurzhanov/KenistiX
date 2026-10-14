using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

namespace RayOptics.VR
{
    /// <summary>
    /// A spring-loaded, three-position VR lever that adjusts
    /// <see cref="RailModule.moduleHeight"/> live while held.
    ///
    /// Physical model
    /// ───────────────
    ///   The lever has three states: Neutral (centre), Positive (pushed one way),
    ///   and Negative (pushed the other way). It is spring-loaded: whenever the
    ///   student is NOT holding it, it automatically returns to Neutral and does
    ///   nothing. While the student holds one end:
    ///     • Positive side held → moduleHeight increases toward heightMax
    ///     • Negative side held → moduleHeight decreases toward heightMin
    ///   Releasing at any point stops the change immediately and the lever
    ///   snaps back to Neutral (moduleHeight itself is NOT reset — only the
    ///   lever's visual position resets).
    ///
    /// Setup
    /// ─────
    ///   1. Build a small seesaw/lever mesh with a pivot at its centre. This
    ///      GameObject (or a child) is the pivot — assign it as leverPivot,
    ///      or leave empty to use this.transform.
    ///   2. Create two grabbable "paddle" ends as children: one for Positive,
    ///      one for Negative. Each needs its own Collider + XRSimpleInteractable
    ///      (added automatically if missing) — assign them to positiveGrabPoint
    ///      and negativeGrabPoint.
    ///   3. Assign railModule to the RailModule whose moduleHeight this drives.
    ///   4. Set heightMin / heightMax / changeSpeed in the Inspector.
    ///
    /// This component uses XRSimpleInteractable's selectEntered/selectExited
    /// (not XRGrabInteractable) on each paddle — the paddles are pressed and
    /// held, not carried around freely.
    /// </summary>
    [AddComponentMenu("Rail Optics/VR/Module Height Lever")]
    public class ModuleHeightLever : MonoBehaviour
    {
        public enum LeverState { Neutral, Positive, Negative }

        [Header("Target")]
        [Tooltip("The RailModule whose moduleHeight this lever adjusts.")]
        public RailModule railModule;

        [Header("Height Limits")]
        [Tooltip("Minimum allowed moduleHeight while using this lever.")]
        public float heightMin = 0f;

        [Tooltip("Maximum allowed moduleHeight while using this lever.")]
        public float heightMax = 2f;

        [Tooltip("How fast moduleHeight changes per second while a paddle is held, " +
                 "in world units per second.")]
        [Range(0.01f, 5f)]
        public float changeSpeed = 0.5f;

        [Header("Grab Points (place these by hand)")]
        [Tooltip("The paddle end that, while held, increases moduleHeight.")]
        public UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable positiveGrabPoint;

        [Tooltip("The paddle end that, while held, decreases moduleHeight.")]
        public UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable negativeGrabPoint;

        [Header("Lever Visual")]
        [Tooltip("The transform that physically rotates/tilts to show lever state. " +
                 "Defaults to this GameObject's transform if left empty.")]
        public Transform leverPivot;

        [Tooltip("Local Euler angle (degrees) the pivot rotates to when fully Positive.")]
        public Vector3 positiveTiltEuler = new Vector3(-20f, 0f, 0f);

        [Tooltip("Local Euler angle (degrees) the pivot rotates to when fully Negative.")]
        public Vector3 negativeTiltEuler = new Vector3(20f, 0f, 0f);

        [Tooltip("How fast the visual tilts toward its target angle (higher = snappier).")]
        [Range(1f, 30f)]
        public float tiltSpeed = 12f;

        // ── Runtime state ────────────────────────────────────────────────────
        public LeverState CurrentState { get; private set; } = LeverState.Neutral;

        private bool _positiveHeld;
        private bool _negativeHeld;
        private Quaternion _neutralLocalRotation;

        // ─────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            if (leverPivot == null)
                leverPivot = transform;

            _neutralLocalRotation = leverPivot.localRotation;

            if (positiveGrabPoint != null)
            {
                positiveGrabPoint.selectEntered.AddListener(OnPositivePressed);
                positiveGrabPoint.selectExited.AddListener(OnPositiveReleased);
            }
            else
            {
                Debug.LogWarning($"[ModuleHeightLever] '{name}' has no Positive Grab Point assigned.", this);
            }

            if (negativeGrabPoint != null)
            {
                negativeGrabPoint.selectEntered.AddListener(OnNegativePressed);
                negativeGrabPoint.selectExited.AddListener(OnNegativeReleased);
            }
            else
            {
                Debug.LogWarning($"[ModuleHeightLever] '{name}' has no Negative Grab Point assigned.", this);
            }

            if (railModule == null)
                Debug.LogWarning($"[ModuleHeightLever] '{name}' has no RailModule assigned.", this);
        }

        private void OnDestroy()
        {
            if (positiveGrabPoint != null)
            {
                positiveGrabPoint.selectEntered.RemoveListener(OnPositivePressed);
                positiveGrabPoint.selectExited.RemoveListener(OnPositiveReleased);
            }
            if (negativeGrabPoint != null)
            {
                negativeGrabPoint.selectEntered.RemoveListener(OnNegativePressed);
                negativeGrabPoint.selectExited.RemoveListener(OnNegativeReleased);
            }
        }

        private void Update()
        {
            UpdateState();
            ApplyHeightChange();
            ApplyVisualTilt();
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Paddle press/release callbacks
        // ─────────────────────────────────────────────────────────────────────

        private void OnPositivePressed(SelectEnterEventArgs args)  => _positiveHeld = true;
        private void OnPositiveReleased(SelectExitEventArgs args)  => _positiveHeld = false;
        private void OnNegativePressed(SelectEnterEventArgs args)  => _negativeHeld = true;
        private void OnNegativeReleased(SelectExitEventArgs args)  => _negativeHeld = false;

        // ─────────────────────────────────────────────────────────────────────
        //  State machine
        // ─────────────────────────────────────────────────────────────────────

        private void UpdateState()
        {
            // If both are somehow held at once (edge case with two controllers),
            // treat it as Neutral rather than picking one arbitrarily — safer
            // than fighting between two opposite directions.
            if (_positiveHeld && _negativeHeld)
                CurrentState = LeverState.Neutral;
            else if (_positiveHeld)
                CurrentState = LeverState.Positive;
            else if (_negativeHeld)
                CurrentState = LeverState.Negative;
            else
                CurrentState = LeverState.Neutral;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Height adjustment
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyHeightChange()
        {
            if (railModule == null) return;
            if (CurrentState == LeverState.Neutral) return;

            // Respect the module's own height-pin — if the height is pinned,
            // the lever should not be able to override that constraint.
            if (railModule.pinnedModuleHeight) return;

            float delta = changeSpeed * Time.deltaTime;
            float newHeight = railModule.moduleHeight
                + (CurrentState == LeverState.Positive ? delta : -delta);

            railModule.moduleHeight = Mathf.Clamp(newHeight, heightMin, heightMax);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Visual tilt
        // ─────────────────────────────────────────────────────────────────────

        private void ApplyVisualTilt()
        {
            if (leverPivot == null) return;

            Quaternion target = CurrentState switch
            {
                LeverState.Positive => _neutralLocalRotation * Quaternion.Euler(positiveTiltEuler),
                LeverState.Negative => _neutralLocalRotation * Quaternion.Euler(negativeTiltEuler),
                _                   => _neutralLocalRotation,
            };

            leverPivot.localRotation = Quaternion.Slerp(
                leverPivot.localRotation, target, Time.deltaTime * tiltSpeed);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 pivotPos = leverPivot != null ? leverPivot.position : transform.position;

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(pivotPos, 0.02f);

            if (positiveGrabPoint != null)
            {
                Gizmos.color = new Color(0.3f, 0.9f, 0.3f, 0.9f);
                Gizmos.DrawLine(pivotPos, positiveGrabPoint.transform.position);
                Gizmos.DrawWireSphere(positiveGrabPoint.transform.position, 0.015f);
            }

            if (negativeGrabPoint != null)
            {
                Gizmos.color = new Color(0.9f, 0.3f, 0.3f, 0.9f);
                Gizmos.DrawLine(pivotPos, negativeGrabPoint.transform.position);
                Gizmos.DrawWireSphere(negativeGrabPoint.transform.position, 0.015f);
            }
        }
#endif
    }
}
