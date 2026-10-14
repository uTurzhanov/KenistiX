using UnityEngine;

namespace RayOptics
{
    /// <summary>
    /// Thick spherical lens — two refracting surfaces with independent radii of curvature.
    ///
    /// Coordinate convention  (light travels along +transform.forward)
    /// ──────────────────────────────────────────────────────────────────
    ///   Front vertex  = transform.position - forward * thickness/2
    ///   Rear  vertex  = transform.position + forward * thickness/2
    ///
    ///   Sign of R  (standard Cartesian convention):
    ///     R > 0  →  centre of curvature is in the +forward direction of the vertex
    ///     R < 0  →  centre of curvature is in the -forward direction of the vertex
    ///     R = 0  →  flat (plano) surface
    ///
    ///   Typical forms:
    ///     Biconvex      R1 > 0, R2 < 0
    ///     Biconcave     R1 < 0, R2 > 0
    ///     Plano-convex  R1 = 0, R2 < 0   (or R1 > 0, R2 = 0)
    ///     Plano-concave R1 = 0, R2 > 0   (or R1 < 0, R2 = 0)
    ///     Meniscus      R1 and R2 same sign
    ///
    /// Physics
    /// ───────
    /// At each surface: vector Snell's law
    ///   d' = η·d + (η·cosI − cosT)·n̂
    ///   where  η = n1/n2,  cosI = −dot(d, n̂),  cosT = sqrt(1 − η²(1−cosI²))
    ///   n̂ is always flipped to face the incoming ray before use.
    ///
    /// Simplifications applied (as agreed):
    ///   • No rim/edge rays — aperture check kills anything outside the clear
    ///     aperture circle; the cylindrical rim is assumed covered by a mesh.
    ///   • R values are clamped to physically sensible ranges, preventing
    ///     degenerate geometry (CoC inside the aperture, etc.).
    ///   • Total internal reflection terminates the ray (absorbed).
    /// </summary>
    [AddComponentMenu("Ray Optics/Lenses/Spherical Lens")]
    public class SphericalLens : OpticsBlocker
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Radii of Curvature   (0 = flat / plano surface)")]
        [Tooltip("R1: front surface radius. R1 > 0 → convex toward incoming light (biconvex form uses +R1).")]
        [Range(-20f, 20f)]
        public float R1 = 3f;

        [Tooltip("R2: rear surface radius. R2 < 0 → convex toward outgoing light (biconvex form uses −R2).")]
        [Range(-20f, 20f)]
        public float R2 = -3f;

        [Header("Geometry")]
        [Tooltip("Center thickness along the optical axis (world units). Must be > 0.")]
        [Range(0.02f, 8f)]
        public float thickness = 0.3f;

        [Tooltip("Clear aperture radius. Rays outside this radius are not refracted.")]
        [Range(0.05f, 8f)]
        public float apertureRadius = 1f;

        [Header("Glass")]
        [Tooltip("Refractive index of the lens material. Crown glass ≈ 1.52, flint ≈ 1.62, dense flint ≈ 1.70.")]
        [Range(1.0f, 2.5f)]
        public float refractiveIndex = 1.5f;

        // ── Derived positions ──────────────────────────────────────────────────

        private Vector3 FrontVertex => transform.position - transform.forward * (thickness * 0.5f);
        private Vector3 RearVertex  => transform.position + transform.forward * (thickness * 0.5f);

        // ── OpticsBlocker.Intersect ────────────────────────────────────────────

        public override RayHitResult Intersect(Vector3 origin, Vector3 direction, float maxDist)
        {
            Vector3 axis = transform.forward;
            float   n    = refractiveIndex;

            // ── Surface 1: outside air → glass ────────────────────────────
            if (!RefractSurface(origin, direction,
                                FrontVertex, R1, axis,
                                1f, n,
                                out float t1, out Vector3 p1, out Vector3 d1,
                                out bool tir1))
                return RayHitResult.Miss;

            if (tir1 || t1 > maxDist)
            {
                // TIR or clipped on entry — stop the ray here (absorbed / missed)
                return new RayHitResult
                {
                    didHit   = true, point = p1, distance = t1,
                    normal   = -axis, blocker = this,
                    spawnContinuation = false
                };
            }

            // ── Surface 2: glass → outside air ────────────────────────────
            Vector3 internalOrigin = p1 + d1 * 1e-5f;

            if (!RefractSurface(internalOrigin, d1,
                                RearVertex, R2, axis,
                                n, 1f,
                                out float t2, out Vector3 p2, out Vector3 d2,
                                out bool tir2))
            {
                // Ray missed rear surface entirely — absorb
                return new RayHitResult
                {
                    didHit   = true, point = p1, distance = t1,
                    normal   = -axis, blocker = this,
                    spawnContinuation = false
                };
            }

            if (tir2)
            {
                // TIR on exit — absorb (could add reflection bounce here later)
                return new RayHitResult
                {
                    didHit   = true, point = p1, distance = t1,
                    normal   = -axis, blocker = this,
                    spawnContinuation = false
                };
            }

            // ── Success: spawn the exiting ray ─────────────────────────────
            // Nudge past the exit surface to avoid self-intersection
            Vector3 exitOrigin = p2 + d2 * 1e-5f;

            // Color/brightness/depth are filled in by the manager
            var cont = new OpticsRay(exitOrigin, d2, Color.white, 1f, 0);

            return new RayHitResult
            {
                didHit            = true,
                point             = p1,
                normal            = -axis,
                distance          = t1,
                blocker           = this,
                spawnContinuation = true,
                continuationRay   = cont,
            };
        }

        // ── Single surface: intersection + Snell's law ─────────────────────────

        /// <summary>
        /// Intersect one spherical (or flat) refracting surface and compute the
        /// refracted direction.
        ///
        /// Returns false only when the ray geometrically misses the surface or
        /// falls outside the aperture.  TIR is flagged via <paramref name="tir"/>
        /// but still returns true (we have a valid hit point).
        /// </summary>
        private bool RefractSurface(
            Vector3 ro, Vector3 rd,
            Vector3 vertex, float R, Vector3 axis,
            float n1, float n2,
            out float t, out Vector3 hitPt, out Vector3 refracted, out bool tir)
        {
            t        = -1f;
            hitPt    = Vector3.zero;
            refracted = rd;
            tir      = false;

            // ── Intersection ───────────────────────────────────────────────
            Vector3 n_hat;   // surface normal (computed below, faces incoming ray after flip)

            if (Mathf.Abs(R) < 1e-6f)
            {
                // Flat surface — simple ray-plane test
                if (!RayPlane(ro, rd, vertex, axis, out t)) return false;
                hitPt = ro + rd * t;
                n_hat = axis;
            }
            else
            {
                // Spherical surface
                // CoC is at vertex + axis*R  (sign encodes which side)
                Vector3 coc  = vertex + axis * R;
                float   absR = Mathf.Abs(R);

                if (!RaySphere(ro, rd, coc, absR, out float tA, out float tB))
                    return false;

                // Pick the root on the correct cap:
                //   The surface cap we want is the one facing the incoming ray,
                //   i.e. the hit point that is on the same side of the CoC as
                //   the incoming ray origin.
                //
                //   Concretely: the front cap of a sphere with R>0 (CoC ahead of
                //   the vertex in the +forward direction) is the part with axial
                //   coordinate ≤ CoC — i.e. tA (the nearer hit) is on the front.
                //   For R<0 (CoC behind the vertex) the front cap is tB (farther hit).
                //
                //   General rule: we want the hit whose axial offset from vertex
                //   has the opposite sign to R — i.e. it lies between the vertex
                //   and the incoming side.
                t = ChooseCap(ro, rd, tA, tB, vertex, axis, R);
                if (t < 1e-5f) return false;

                hitPt = ro + rd * t;

                // Outward sphere normal at this point
                n_hat = (hitPt - coc).normalized;
            }

            // ── Aperture check — radial distance from optical axis at hitPt ──
            Vector3 toHit  = hitPt - vertex;
            float   radial = Vector3.Cross(toHit, axis).magnitude;   // |toHit × axis| = radial dist
            if (radial > apertureRadius) return false;

            // ── Flip normal to always face the incoming ray ────────────────
            if (Vector3.Dot(rd, n_hat) > 0f) n_hat = -n_hat;

            // ── Vector Snell's law ──────────────────────────────────────────
            float eta   = n1 / n2;
            float cosI  = -Vector3.Dot(rd, n_hat);            // cos of angle of incidence ≥ 0
            float sin2T = eta * eta * (1f - cosI * cosI);

            if (sin2T > 1f)
            {
                tir = true;
                // Compute TIR direction anyway (could be used for reflection later)
                refracted = (rd - 2f * Vector3.Dot(rd, n_hat) * n_hat).normalized;
                return true;
            }

            float cosT = Mathf.Sqrt(1f - sin2T);
            refracted  = (eta * rd + (eta * cosI - cosT) * n_hat).normalized;
            return true;
        }

        // ── Ray–sphere intersection ────────────────────────────────────────────

        private static bool RaySphere(Vector3 ro, Vector3 rd, Vector3 centre, float radius,
                                       out float t1, out float t2)
        {
            t1 = t2 = -1f;
            Vector3 oc   = ro - centre;
            float   b    = Vector3.Dot(oc, rd);
            float   disc = b * b - (Vector3.Dot(oc, oc) - radius * radius);
            if (disc < 0f) return false;

            float sq = Mathf.Sqrt(disc);
            t1 = -b - sq;
            t2 = -b + sq;
            return true;
        }

        // ── Cap selection ──────────────────────────────────────────────────────

        /// <summary>
        /// Given two sphere–ray intersection distances, return the one that
        /// corresponds to the correct surface cap for this lens surface.
        ///
        /// The surface is a cap of a sphere whose CoC is at vertex + axis*R.
        /// The cap we need is the one that is on the side of the CoC that faces
        /// the incoming ray — i.e. the side where (hitPoint − CoC) · axis has
        /// the OPPOSITE sign to R.
        ///
        /// Equivalently: the correct hit point has axial position (relative to
        /// the vertex) between 0 and −R (it sits between the vertex and the
        /// incoming half-space), which simplifies to:
        ///   dot(hitPt − vertex, axis) * sign(R)  ≤  0
        ///
        /// We pick the valid root (t > ε, in front of ray, satisfies cap side)
        /// with the smallest t.
        /// </summary>
        private static float ChooseCap(Vector3 ro, Vector3 rd,
                                        float tA, float tB,
                                        Vector3 vertex, Vector3 axis, float R)
        {
            // A spherical surface vertex sits at `vertex`. The centre of curvature
            // is at vertex + axis*R. The surface cap we want is the one that the
            // incoming ray encounters first — the "face" of the surface.
            //
            // For a convex front surface (R > 0, CoC is forward of the vertex):
            //   The face cap is the part of the sphere BEHIND the CoC, facing the
            //   incoming ray. Hit points on this cap have axialOff ∈ [0, R), i.e.
            //   between the vertex and the CoC — so axialOff * sign(R) > 0.
            //
            // For a concave front surface (R < 0, CoC is behind the vertex):
            //   The face cap has axialOff ∈ (R, 0] — axialOff * sign(R) > 0 again.
            //
            // In both cases the valid hit satisfies:
            //   0 ≤ axialOff * sign(R) < |R|
            // We pick the smallest positive t that satisfies this.

            float signR = Mathf.Sign(R);
            float absR  = Mathf.Abs(R);
            float best  = -1f;

            foreach (float t in new[] { tA, tB })
            {
                if (t < 1e-5f) continue;

                Vector3 p        = ro + rd * t;
                float   axialOff = Vector3.Dot(p - vertex, axis);
                float   signed   = axialOff * signR;

                // Must be on the face side: between the vertex plane and the CoC
                if (signed < -0.001f || signed > absR + 0.001f) continue;

                if (best < 0f || t < best) best = t;
            }

            return best;
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        private void OnDrawGizmos()         => DrawGizmos(false);
        private void OnDrawGizmosSelected() => DrawGizmos(true);

        private void DrawGizmos(bool selected)
        {
            Vector3 fwd = transform.forward;
            Vector3 r   = transform.right;
            Vector3 u   = transform.up;
            Vector3 fv  = FrontVertex;
            Vector3 rv  = RearVertex;
            float   ar  = apertureRadius;

            Color col = selected
                ? new Color(0.35f, 0.9f, 0.5f, 0.95f)
                : new Color(0.35f, 0.9f, 0.5f, 0.4f);
            Gizmos.color = col;

            // Compute where the surface arcs actually end at the aperture rim.
            // These are the true edge points on each curved surface at radius ar.
            Vector3[] frontRimPts = SurfaceRimPoints(fv, R1,  fwd, r, u, ar);
            Vector3[] rearRimPts  = SurfaceRimPoints(rv, R2,  fwd, r, u, ar);

            // Four axial barrel lines connecting front arc rim to rear arc rim
            for (int i = 0; i < frontRimPts.Length; i++)
                Gizmos.DrawLine(frontRimPts[i], rearRimPts[i]);

            // Surface arcs (in the forward-up cross-section plane)
            Gizmos.color = selected
                ? new Color(0.35f, 1f, 0.5f, 1f)
                : new Color(0.35f, 1f, 0.5f, 0.65f);
            DrawSurfaceArc(fv, R1,  fwd,  u, ar, 40);
            DrawSurfaceArc(rv, R2,  fwd,  u, ar, 40);

            // Aperture rim circles at the arc endpoints' axial level
            // (draw at vertex planes — these close off the cylinder visually)
            Gizmos.color = col;
            DrawCircle(fv, r, u, ar, 48);
            DrawCircle(rv, r, u, ar, 48);

            if (selected)
            {
                // Optical axis dash
                Gizmos.color = new Color(1f, 1f, 0.3f, 0.35f);
                Gizmos.DrawLine(transform.position - fwd * (ar * 2f),
                                transform.position + fwd * (ar * 2f));

                // EFL marker
                float efl = ComputeEFL();
                if (!float.IsInfinity(efl) && !float.IsNaN(efl) && Mathf.Abs(efl) < 200f)
                {
                    float   ms = ar * 0.07f;
                    Vector3 f1 = transform.position - fwd * efl;
                    Vector3 f2 = transform.position + fwd * efl;
                    Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                    Gizmos.DrawWireSphere(f1, ms);
                    Gizmos.DrawWireSphere(f2, ms);
                    Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.3f);
                    Gizmos.DrawLine(transform.position, f2);
                }
            }
        }

        /// <summary>
        /// Returns the four cardinal rim points (right, up, left, down) where the
        /// curved surface intersects the aperture cylinder at radius <paramref name="ar"/>.
        /// These are the true 3D points on the sphere (or vertex plane for flat surfaces).
        /// </summary>
        private static Vector3[] SurfaceRimPoints(Vector3 vertex, float R,
                                                   Vector3 axis, Vector3 r, Vector3 u,
                                                   float ar)
        {
            var pts = new Vector3[4];
            Vector3[] localDirs = { r, u, -r, -u };

            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Abs(R) < 1e-6f)
                {
                    // Flat surface: rim point is simply at vertex plane + radial offset
                    pts[i] = vertex + localDirs[i] * ar;
                }
                else
                {
                    // Sphere: CoC at vertex + axis*R, radius |R|
                    // The rim point at radial distance ar in direction localDirs[i]:
                    //   It lies on the sphere AND at radial distance ar from the axis.
                    //   sag = R - sign(R)*sqrt(R²-ar²)  (axial displacement from vertex)
                    float absR = Mathf.Abs(R);
                    float sag  = ar <= absR
                        ? R - Mathf.Sign(R) * Mathf.Sqrt(absR * absR - ar * ar)
                        : 0f;   // aperture larger than R — clamp to vertex plane
                    pts[i] = vertex + axis * sag + localDirs[i] * ar;
                }
            }

            return pts;
        }

        /// <summary>
        /// Draw the cross-section arc of a spherical surface in the axis-upAxis plane.
        /// The bulge direction is determined entirely by the sign of R:
        ///   R > 0  → centre of curvature is forward → surface bulges toward -axis (toward incoming)
        ///   R &lt; 0  → centre of curvature is backward → surface bulges toward +axis (away from incoming)
        /// </summary>
        private static void DrawSurfaceArc(Vector3 vertex, float R,
                                            Vector3 axis, Vector3 upAxis,
                                            float aperture, int segs)
        {
            if (Mathf.Abs(R) < 1e-6f)
            {
                Gizmos.DrawLine(vertex - upAxis * aperture, vertex + upAxis * aperture);
                return;
            }

            Vector3 coc  = vertex + axis * R;
            float   absR = Mathf.Abs(R);

            // Half-angle at CoC subtended by the aperture radius
            float sinH    = Mathf.Clamp(aperture / absR, 0f, 0.999f);
            float halfAng = Mathf.Asin(sinH);

            // Direction from CoC toward the surface cap centre (the vertex direction)
            // For R > 0: CoC is forward, cap centre is behind CoC → direction is -axis
            // For R < 0: CoC is backward, cap centre is ahead of CoC → direction is +axis
            Vector3 capDir = -axis * Mathf.Sign(R);

            Vector3 prev  = Vector3.zero;
            bool    first = true;
            for (int i = 0; i <= segs; i++)
            {
                float   ang = -halfAng + i * 2f * halfAng / segs;
                Vector3 dir = Mathf.Cos(ang) * capDir + Mathf.Sin(ang) * upAxis;
                Vector3 pt  = coc + dir * absR;

                if (!first) Gizmos.DrawLine(prev, pt);
                prev  = pt;
                first = false;
            }
        }

        private static void DrawCircle(Vector3 centre, Vector3 ax1, Vector3 ax2,
                                        float radius, int segs)
        {
            Vector3 prev = centre + ax1 * radius;
            for (int i = 1; i <= segs; i++)
            {
                float   ang = i * Mathf.PI * 2f / segs;
                Vector3 pt  = centre
                    + ax1 * (Mathf.Cos(ang) * radius)
                    + ax2 * (Mathf.Sin(ang) * radius);
                Gizmos.DrawLine(prev, pt);
                prev = pt;
            }
        }

        // ── Lensmaker's equation (thick lens, for gizmo EFL display) ──────────

        private float ComputeEFL()
        {
            float n    = refractiveIndex;
            float d    = thickness;
            float invR1 = Mathf.Abs(R1) > 1e-6f ? 1f / R1 : 0f;
            float invR2 = Mathf.Abs(R2) > 1e-6f ? 1f / R2 : 0f;

            // 1/f = (n-1) * [ (1/R1 - 1/R2) + (n-1)*d/(n*R1*R2) ]
            float inv_f = (n - 1f) * (invR1 - invR2 + (n - 1f) * d * invR1 * invR2 / n);
            return Mathf.Abs(inv_f) > 1e-9f ? 1f / inv_f : float.PositiveInfinity;
        }
    }
}
