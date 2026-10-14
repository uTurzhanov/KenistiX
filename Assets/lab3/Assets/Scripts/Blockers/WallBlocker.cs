using System.Collections.Generic;
using UnityEngine;

namespace RayOptics
{
    /// <summary>
    /// A rectangular planar wall that blocks rays in 3D.
    ///
    /// The wall is an infinite-thin rectangle in the plane defined by
    /// <c>transform.right</c> (width) and <c>transform.up</c> (height).
    /// Its normal is <c>transform.forward</c>.
    ///
    /// Each frame the manager calls <see cref="RegisterHit"/> for every ray
    /// that lands on this wall, then <see cref="FlushDots"/> to render them
    /// as coloured dots slightly proud of the surface.
    /// </summary>
    [AddComponentMenu("Ray Optics/Blockers/Wall (Plane Blocker)")]
    public class WallBlocker : OpticsBlocker
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Wall Dimensions")]
        [Tooltip("Total width of the wall (along transform.right).")]
        [Range(0.05f, 30f)]
        public float width = 2f;

        [Tooltip("Total height of the wall (along transform.up).")]
        [Range(0.05f, 30f)]
        public float height = 2f;

        [Tooltip("Draw the wall at 50 % alpha in the Scene view.")]
        public bool semiTransparent = false;

        [Header("Hit Dots")]
        [Tooltip("Show coloured dots where rays land on this wall.")]
        public bool showHitDots = true;

        [Tooltip("Radius of each hit dot in world units.")]
        [Range(0.005f, 0.5f)]
        public float dotRadius = 0.04f;

        [Tooltip("How far in front of the wall surface to draw the dots (prevents z-fighting).")]
        [Range(0f, 0.05f)]
        public float dotOffset = 0.005f;

        [Tooltip("Number of line segments used to draw each dot circle.")]
        [Range(4, 32)]
        public int dotSegments = 12;

        [Tooltip("Material for dot LineRenderers. Leave empty to share the manager's ray material.")]
        public Material dotMaterial;

        // ─────────────────────────────────────────────────────────────────────
        //  Derived geometry
        // ─────────────────────────────────────────────────────────────────────

        public Vector3 Normal   => transform.forward;
        private Vector3 HalfW  => transform.right * (width  * 0.5f);
        private Vector3 HalfH  => transform.up    * (height * 0.5f);

        public Vector3 CornerTL => transform.position - HalfW + HalfH;
        public Vector3 CornerTR => transform.position + HalfW + HalfH;
        public Vector3 CornerBR => transform.position + HalfW - HalfH;
        public Vector3 CornerBL => transform.position - HalfW - HalfH;

        // ─────────────────────────────────────────────────────────────────────
        //  Hit dot pool
        // ─────────────────────────────────────────────────────────────────────

        // Pending hits collected during TraceRay, flushed to renderers at end of frame
        private readonly List<(Vector3 point, Color color)> _pendingHits
            = new List<(Vector3, Color)>(64);

        // Each dot uses one LineRenderer drawing a small circle
        private readonly List<LineRenderer> _dotPool = new List<LineRenderer>();
        private int _activeDots;
        private Transform _dotRoot;

        private void Awake()
        {
            var go = new GameObject("_HitDots");
            go.transform.SetParent(transform);
            _dotRoot = go.transform;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Called by OpticsManager each frame
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Clear pending hits at the start of each simulation frame.
        /// Called by OpticsManager before firing rays.
        /// </summary>
        public void ClearHits()
        {
            _pendingHits.Clear();
        }

        /// <summary>
        /// Record a ray hit on this wall. Called by OpticsManager during TraceRay.
        /// </summary>
        public void RegisterHit(Vector3 worldPoint, Color color)
        {
            if (!showHitDots) return;
            _pendingHits.Add((worldPoint, color));
        }

        /// <summary>
        /// Render all collected hits as dots and hide surplus pool entries.
        /// Called by OpticsManager after all rays are traced.
        /// </summary>
        public void FlushDots()
        {
            _activeDots = 0;

            if (showHitDots)
            {
                foreach (var (pt, col) in _pendingHits)
                    DrawDot(pt, col);
            }

            // Hide surplus pooled renderers
            for (int i = _activeDots; i < _dotPool.Count; i++)
                _dotPool[i].enabled = false;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  OpticsBlocker implementation
        // ─────────────────────────────────────────────────────────────────────

        public override RayHitResult Intersect(Vector3 origin, Vector3 direction, float maxDist)
        {
            Vector3 normal = Normal;

            if (!RayPlane(origin, direction, transform.position, normal, out float t))
                return RayHitResult.Miss;

            if (t > maxDist) return RayHitResult.Miss;

            Vector3 hit   = origin + direction * t;
            Vector3 local = hit - transform.position;
            float   projU = Vector3.Dot(local, transform.right);
            float   projV = Vector3.Dot(local, transform.up);

            if (Mathf.Abs(projU) > width  * 0.5f) return RayHitResult.Miss;
            if (Mathf.Abs(projV) > height * 0.5f) return RayHitResult.Miss;

            if (Vector3.Dot(normal, direction) > 0f) normal = -normal;

            return new RayHitResult
            {
                didHit   = true,
                point    = hit,
                normal   = normal,
                distance = t,
                blocker  = this,
                spawnContinuation = false,
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Dot rendering
        // ─────────────────────────────────────────────────────────────────────

        private void DrawDot(Vector3 worldPoint, Color color)
        {
            // Offset the dot slightly in front of the wall so it doesn't z-fight
            // with any mesh sitting exactly on the wall plane.
            Vector3 normal   = Normal;
            Vector3 centre   = worldPoint + normal * dotOffset;

            // Build a small circle in the wall's local UV plane
            Vector3 axisR = transform.right;
            Vector3 axisU = transform.up;
            int     segs  = dotSegments;

            var lr = GetOrCreateDot(_activeDots++);
            lr.enabled       = true;
            lr.loop          = true;
            lr.positionCount = segs;
            lr.startColor    = color;
            lr.endColor      = color;
            lr.startWidth    = dotRadius * 0.35f;
            lr.endWidth      = dotRadius * 0.35f;

            for (int i = 0; i < segs; i++)
            {
                float   ang = i * Mathf.PI * 2f / segs;
                Vector3 pt  = centre
                    + axisR * (Mathf.Cos(ang) * dotRadius)
                    + axisU * (Mathf.Sin(ang) * dotRadius);
                lr.SetPosition(i, pt);
            }
        }

        private LineRenderer GetOrCreateDot(int index)
        {
            if (index < _dotPool.Count) return _dotPool[index];

            var go = new GameObject($"Dot_{index}");
            go.transform.SetParent(_dotRoot);

            var lr = go.AddComponent<LineRenderer>();
            lr.material = dotMaterial != null
                ? dotMaterial
                : new Material(Shader.Find("Sprites/Default"));
            lr.useWorldSpace        = true;
            lr.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows       = false;
            lr.sortingOrder         = 11;   // above rays
            lr.generateLightingData = false;
            lr.loop                 = true;

            _dotPool.Add(lr);
            return lr;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            Color c = gizmoColor;
            if (semiTransparent) c.a = 0.5f;
            Gizmos.color = c;
            DrawOutline();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f - gizmoColor.r, 1f - gizmoColor.g,
                                     1f - gizmoColor.b, 1f);
            DrawOutline();

            Gizmos.color = Color.white;
            float ms = 0.06f;
            foreach (var c in new[] { CornerTL, CornerTR, CornerBR, CornerBL })
                Gizmos.DrawWireSphere(c, ms);

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, Normal * 0.3f);
        }

        private void DrawOutline()
        {
            Gizmos.DrawLine(CornerTL, CornerTR);
            Gizmos.DrawLine(CornerTR, CornerBR);
            Gizmos.DrawLine(CornerBR, CornerBL);
            Gizmos.DrawLine(CornerBL, CornerTL);
            Gizmos.DrawLine(CornerTL, CornerBR);
            Gizmos.DrawLine(CornerTR, CornerBL);
        }
    }
}
