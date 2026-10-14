using System.Collections.Generic;
using UnityEngine;

namespace RayOptics
{
    /// <summary>
    /// Emits a single ray along <c>transform.forward</c>.
    ///
    /// Regular mode         → 1 ray
    /// Correct Brightness   → 1 ray  (pencil beam — no area to sample)
    ///
    /// Aim the source by rotating the GameObject freely in 3D.
    /// </summary>
    [AddComponentMenu("Ray Optics/Sources/Single Point Source")]
    public class SinglePointSource : LightSource
    {
        public override List<OpticsRay> GetRaysRegular()
            => new List<OpticsRay> { MakeRay(Pos, Fwd) };

        public override List<OpticsRay> GetRaysCorrectBrightness(int count)
            => new List<OpticsRay> { MakeRay(Pos, Fwd, 1f) };

        // ─────────────────────────────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────────────────────────────

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            Gizmos.color = rayColor;
            Vector3 p   = transform.position;
            Vector3 fwd = transform.forward;
            Gizmos.DrawRay(p, fwd * 0.6f);
            // Arrowhead in the forward-up plane
            Vector3 tip = p + fwd * 0.6f;
            Gizmos.DrawLine(tip, tip + (-fwd + transform.up)   * 0.12f);
            Gizmos.DrawLine(tip, tip + (-fwd - transform.up)   * 0.12f);
            Gizmos.DrawLine(tip, tip + (-fwd + transform.right) * 0.12f);
            Gizmos.DrawLine(tip, tip + (-fwd - transform.right) * 0.12f);
        }
    }
}
