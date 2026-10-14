using UnityEngine;

namespace RayOptics
{
    /// <summary>
    /// Represents a single ray travelling in 3D world space.
    /// </summary>
    public struct OpticsRay
    {
        public Vector3 origin;
        public Vector3 direction;   // always normalised
        public Color   color;
        public float   brightness;  // 0..1, used in Correct Brightness mode
        public int     depth;       // bounce/refraction depth

        public OpticsRay(Vector3 origin, Vector3 direction, Color color,
                         float brightness = 1f, int depth = 0)
        {
            this.origin     = origin;
            this.direction  = direction.normalized;
            this.color      = color;
            this.brightness = brightness;
            this.depth      = depth;
        }
    }

    /// <summary>
    /// Result of casting a ray against a single blocker.
    ///
    /// When a blocker refracts or redirects the ray (e.g. a lens), it sets
    /// <see cref="spawnContinuation"/> = true and fills <see cref="continuationRay"/>.
    /// The manager then continues tracing from that new ray instead of terminating.
    /// </summary>
    public struct RayHitResult
    {
        public bool          didHit;
        public Vector3       point;
        public Vector3       normal;
        public float         distance;
        public OpticsBlocker blocker;

        /// <summary>
        /// When true the blocker has computed a refracted / redirected continuation.
        /// The manager will draw the incoming segment up to <see cref="point"/>,
        /// then recurse with <see cref="continuationRay"/>.
        /// </summary>
        public bool       spawnContinuation;
        public OpticsRay  continuationRay;

        public static RayHitResult Miss =>
            new RayHitResult { didHit = false, distance = float.MaxValue };
    }
}
