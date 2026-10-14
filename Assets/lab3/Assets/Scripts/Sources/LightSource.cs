using System.Collections.Generic;
using UnityEngine;

namespace RayOptics
{
    /// <summary>
    /// Abstract base class for all 3D light sources.
    ///
    /// Every source emits rays in full 3D world space.
    /// The emission direction / aperture plane are driven by the GameObject's
    /// Transform, so you can freely move, rotate, and parent them anywhere.
    ///
    /// Conventions
    /// ───────────
    ///   • Emission axis  → transform.forward  (the "barrel" of the source)
    ///   • Aperture up    → transform.up
    ///   • Aperture right → transform.right
    /// </summary>
    public abstract class LightSource : MonoBehaviour
    {
        [Header("Light Source")]
        [Tooltip("Colour of emitted rays.")]
        public Color rayColor = Color.yellow;

        [Tooltip("Brightness multiplier (Correct Brightness mode).")]
        [Range(0f, 1f)]
        public float intensity = 1f;

        // ─────────────────────────────────────────────────────────────────────
        //  Abstract interface
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Small diagnostic ray set (Regular mode).
        ///   SinglePoint  → 1 ray
        ///   All others   → 9 rays (1 centre + 8 perimeter)
        /// </summary>
        public abstract List<OpticsRay> GetRaysRegular();

        /// <summary>
        /// Full ray set for Correct Brightness mode.
        /// <paramref name="count"/> is a hint; implementations may round.
        /// </summary>
        public abstract List<OpticsRay> GetRaysCorrectBrightness(int count);

        // ─────────────────────────────────────────────────────────────────────
        //  Shared helpers (3D)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>World-space position of this source.</summary>
        protected Vector3 Pos => transform.position;

        /// <summary>Emission axis — the primary ray direction.</summary>
        protected Vector3 Fwd => transform.forward;

        /// <summary>Aperture "up" axis, perpendicular to Fwd.</summary>
        protected Vector3 Up => transform.up;

        /// <summary>Aperture "right" axis, perpendicular to Fwd and Up.</summary>
        protected Vector3 Right => transform.right;

        /// <summary>Build a ray with this source's colour and intensity.</summary>
        protected OpticsRay MakeRay(Vector3 origin, Vector3 direction, float brightnessMul = 1f)
            => new OpticsRay(origin, direction, rayColor, intensity * brightnessMul);

        // ─────────────────────────────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────────────────────────────

        protected virtual void OnDrawGizmosSelected()
        {
            Gizmos.color = rayColor;
            Gizmos.DrawWireSphere(transform.position, 0.12f);
        }
    }
}
