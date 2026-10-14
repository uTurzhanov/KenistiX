using UnityEngine;

namespace RayOptics
{
    /// <summary>
    /// Abstract base class for all 3D ray blockers.
    ///
    /// Subclasses implement <see cref="Intersect"/> for their specific 3D geometry.
    /// </summary>
    public abstract class OpticsBlocker : MonoBehaviour
    {
        [Header("Blocker")]
        public Color gizmoColor = new Color(0.85f, 0.85f, 0.85f, 1f);

        // ─────────────────────────────────────────────────────────────────────
        //  Abstract interface
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Test intersection with the incoming 3D ray.
        /// </summary>
        /// <param name="origin">Ray origin in world space.</param>
        /// <param name="direction">Normalised ray direction.</param>
        /// <param name="maxDist">Clip distance.</param>
        public abstract RayHitResult Intersect(Vector3 origin, Vector3 direction, float maxDist);

        // ─────────────────────────────────────────────────────────────────────
        //  Shared 3D geometry helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Ray–infinite-plane intersection.
        /// Returns true and sets <paramref name="t"/> (distance along ray) if the ray
        /// hits the plane defined by <paramref name="planePoint"/> and <paramref name="planeNormal"/>.
        /// A small epsilon prevents self-intersection.
        /// </summary>
        protected static bool RayPlane(Vector3 ro, Vector3 rd,
                                       Vector3 planePoint, Vector3 planeNormal,
                                       out float t)
        {
            t = -1f;
            float denom = Vector3.Dot(rd, planeNormal);
            if (Mathf.Abs(denom) < 1e-8f) return false;   // parallel

            t = Vector3.Dot(planePoint - ro, planeNormal) / denom;
            return t > 1e-5f;                              // in front of origin
        }
    }
}
