using UnityEngine;

namespace RayOptics
{
    /// <summary>
    /// Ideal thin lens in 3D.
    ///
    /// The lens lies in the plane whose normal is <c>transform.forward</c> (the optical axis).
    /// Its aperture is circular with radius <see cref="apertureRadius"/>.
    ///
    /// Physics
    /// ───────
    /// For a thin lens of focal length f, a ray hitting the lens plane at world
    /// point P is redirected so that:
    ///   • A ray parallel to the axis converges to the rear focal point F2.
    ///   • A ray through the front focal point F1 emerges parallel.
    ///   • A ray through the centre passes undeviated.
    ///
    /// The unified formula (works for all cases):
    ///   Let h = lateral offset of P from the optical axis (a 3D vector in the lens plane).
    ///   The outgoing direction d' is found by demanding the ray passes through the
    ///   image of the source point at infinity — i.e. the focal plane.
    ///
    ///   d_out = normalize( d_in  -  (dot(d_in, axis) / f) * h_unit * |h| ... )
    ///
    /// More precisely, using the standard thin-lens ray transfer:
    ///   The component of direction along the axis is unchanged in sign but the
    ///   transverse component gets a "kick" proportional to h/f:
    ///
    ///   d_out_transverse = d_in_transverse  -  (h / f) * axis_dot_d_in
    ///   d_out_axial      = d_in_axial   (sign preserved — ray keeps going forward)
    ///
    /// This correctly handles oblique rays in 3D.
    ///
    /// Sign convention  (consistent with the lensmaker's equation):
    ///   f > 0  → converging lens
    ///   f < 0  → diverging lens
    /// </summary>
    [AddComponentMenu("Ray Optics/Lenses/Ideal Lens")]
    public class IdealLens : OpticsBlocker
    {
        [Header("Ideal Lens")]
        [Tooltip("Focal length in world units. Positive = converging, negative = diverging.")]
        public float focalLength = 3f;

        [Tooltip("Aperture radius in world units. Rays outside this circle are not refracted.")]
        [Range(0.05f, 10f)]
        public float apertureRadius = 1f;

        // ─────────────────────────────────────────────────────────────────────
        //  OpticsBlocker
        // ─────────────────────────────────────────────────────────────────────

        public override RayHitResult Intersect(Vector3 origin, Vector3 direction, float maxDist)
        {
            Vector3 axis   = transform.forward;   // optical axis direction
            Vector3 centre = transform.position;

            // ── 1. Ray–plane intersection ──────────────────────────────────
            if (!RayPlane(origin, direction, centre, axis, out float t))
                return RayHitResult.Miss;

            if (t > maxDist) return RayHitResult.Miss;

            Vector3 hitPoint = origin + direction * t;

            // ── 2. Aperture check (circular) ───────────────────────────────
            Vector3 offset = hitPoint - centre;
            // Project offset onto the lens plane (remove any axial component due to float error)
            Vector3 h = offset - Vector3.Dot(offset, axis) * axis;
            float   r = h.magnitude;

            if (r > apertureRadius) return RayHitResult.Miss;

            // ── 3. Thin-lens direction transformation ──────────────────────
            // Decompose incoming direction into axial + transverse components
            float   dAxial      = Vector3.Dot(direction, axis);   // signed, typically > 0
            Vector3 dTransverse = direction - dAxial * axis;

            // The lens adds a transverse kick of  -(h / f) * dAxial
            // (derived from paraxial matrix optics, valid for any angle in 3D)
            Vector3 newTransverse = dTransverse - (h / focalLength) * dAxial;
            Vector3 newDirection  = (newTransverse + dAxial * axis).normalized;

            // ── 4. Build continuation ray ──────────────────────────────────
            // Nudge the start point slightly past the lens to avoid self-hit
            Vector3 continuationOrigin = hitPoint + axis * 1e-4f * Mathf.Sign(dAxial);

            // Color, brightness and depth are injected by OpticsManager.TraceRay
            // after it receives this result — only origin and direction matter here.
            var cont = new OpticsRay(continuationOrigin, newDirection, Color.white, 1f, 0);

            return new RayHitResult
            {
                didHit            = true,
                point             = hitPoint,
                normal            = dAxial >= 0f ? -axis : axis,
                distance          = t,
                blocker           = this,
                spawnContinuation = true,
                continuationRay   = cont,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            DrawLensGizmo(new Color(0.3f, 0.8f, 1f, 0.4f), false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawLensGizmo(new Color(0.3f, 0.8f, 1f, 0.9f), true);
        }

        private void DrawLensGizmo(Color col, bool selected)
        {
            Gizmos.color = col;
            Vector3 c   = transform.position;
            Vector3 fwd = transform.forward;
            Vector3 r   = transform.right;
            Vector3 u   = transform.up;
            float   ar  = apertureRadius;

            // Aperture circle
            int segs = 48;
            Vector3 prev = c + r * ar;
            for (int i = 1; i <= segs; i++)
            {
                float ang = i * Mathf.PI * 2f / segs;
                Vector3 pt = c + r * (Mathf.Cos(ang) * ar) + u * (Mathf.Sin(ang) * ar);
                Gizmos.DrawLine(prev, pt);
                prev = pt;
            }

            bool converging = focalLength > 0f;
            float f = Mathf.Abs(focalLength);

            // Lens symbol: double-convex or double-concave arrows
            float tipH = ar * 0.85f;
            float bulge = ar * 0.22f * (converging ? 1f : -1f);

            // Vertical bar with bulging midpoint (approximate lens cross-section)
            Vector3 top = c + u * tipH;
            Vector3 bot = c - u * tipH;
            Vector3 mid = c + fwd * bulge;
            Gizmos.DrawLine(top, mid);
            Gizmos.DrawLine(mid, bot);

            // Arrowheads
            float ah = ar * 0.12f;
            Gizmos.DrawLine(top, top - u * ah + fwd * (ah * (converging ? 1f : -1f)));
            Gizmos.DrawLine(top, top - u * ah - fwd * (ah * (converging ? 1f : -1f)));
            Gizmos.DrawLine(bot, bot + u * ah + fwd * (ah * (converging ? 1f : -1f)));
            Gizmos.DrawLine(bot, bot + u * ah - fwd * (ah * (converging ? 1f : -1f)));

            if (selected)
            {
                // Focal point markers
                Gizmos.color = new Color(1f, 1f, 0f, 0.8f);
                Vector3 f1 = c - fwd * focalLength;
                Vector3 f2 = c + fwd * focalLength;
                float ms = ar * 0.08f;
                Gizmos.DrawWireSphere(f1, ms);
                Gizmos.DrawWireSphere(f2, ms);
                // Optical axis
                Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
                Gizmos.DrawLine(c - fwd * (f + ar), c + fwd * (f + ar));

                // Label focal length via debug line length indicator
                Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.5f);
                Gizmos.DrawLine(c, f2);
            }
        }
    }
}
