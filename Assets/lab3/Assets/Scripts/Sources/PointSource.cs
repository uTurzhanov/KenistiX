using System.Collections.Generic;
using UnityEngine;

namespace RayOptics
{
    /// <summary>
    /// A 3D cone or omnidirectional point source.
    ///
    /// The cone's axis is <c>transform.forward</c>.
    /// <see cref="emissionAngle"/> is the full apex angle of the cone (1°–360°).
    /// At 360° the source radiates in all directions (sphere).
    ///
    /// Regular mode  (9 rays)
    ///   emissionAngle == 360 → 1 forward + 8 evenly around the full sphere
    ///   emissionAngle  < 360 → 1 on-axis + 8 on the cone's rim, evenly spaced
    ///
    /// Correct Brightness mode
    ///   Rays are distributed uniformly over the solid angle of the cone using
    ///   stratified sampling in (phi, cos θ) space for even irradiance.
    /// </summary>
    [AddComponentMenu("Ray Optics/Sources/Point Source (Cone)")]
    public class PointSource : LightSource
    {
        [Header("Point Source Settings")]
        [Tooltip("Full apex angle of the emission cone in degrees. 360 = omnidirectional sphere.")]
        [Range(1f, 360f)]
        public float emissionAngle = 90f;

        // ─────────────────────────────────────────────────────────────────────
        //  LightSource implementation
        // ─────────────────────────────────────────────────────────────────────

        public override List<OpticsRay> GetRaysRegular()
        {
            var rays = new List<OpticsRay>(9);

            bool fullSphere = emissionAngle >= 360f;
            float halfAngle = emissionAngle * 0.5f * Mathf.Deg2Rad;

            if (fullSphere)
            {
                // 1 forward + 8 directions spread over the sphere
                rays.Add(MakeRay(Pos, Fwd));
                float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
                for (int i = 1; i < 9; i++)
                {
                    float phi   = i * goldenAngle;
                    float theta = Mathf.Acos(1f - 2f * i / 9f);
                    rays.Add(MakeRay(Pos, SphericalToWorld(theta, phi)));
                }
            }
            else
            {
                // 1 on-axis + 8 on the cone rim
                rays.Add(MakeRay(Pos, Fwd));
                for (int i = 0; i < 8; i++)
                {
                    float phi = i * Mathf.PI * 2f / 8f;
                    rays.Add(MakeRay(Pos, SphericalToWorld(halfAngle, phi)));
                }
            }

            return rays;
        }

        public override List<OpticsRay> GetRaysCorrectBrightness(int count)
        {
            var   rays             = new List<OpticsRay>(count);
            float perRayBrightness = intensity / count;

            bool  fullSphere = emissionAngle >= 360f;
            float halfAngle  = emissionAngle * 0.5f * Mathf.Deg2Rad;
            float cosMax     = fullSphere ? -1f : Mathf.Cos(halfAngle);

            // Stratified sampling over solid angle:
            //   cos θ ∈ [cosMax, 1],  φ ∈ [0, 2π)
            for (int i = 0; i < count; i++)
            {
                // Van der Corput / simple stratified
                float xi1  = (i + 0.5f) / count;
                float xi2  = HaltonBase2(i);

                float cosTheta = 1f - xi1 * (1f - cosMax);
                float theta    = Mathf.Acos(Mathf.Clamp(cosTheta, -1f, 1f));
                float phi      = xi2 * Mathf.PI * 2f;

                rays.Add(new OpticsRay(Pos, SphericalToWorld(theta, phi),
                                       rayColor, perRayBrightness));
            }

            return rays;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Convert spherical coordinates (measured from transform.forward)
        /// to a world-space direction vector.
        /// θ = polar angle from forward axis, φ = azimuth around forward.
        /// </summary>
        private Vector3 SphericalToWorld(float theta, float phi)
        {
            // Local direction in the frame where forward = (0,0,1)
            float sinT = Mathf.Sin(theta);
            Vector3 local = new Vector3(sinT * Mathf.Cos(phi),
                                        sinT * Mathf.Sin(phi),
                                        Mathf.Cos(theta));
            // Rotate into world space using the transform's orientation
            return transform.TransformDirection(local);
        }

        /// <summary>Halton low-discrepancy sequence, base 2.</summary>
        private static float HaltonBase2(int index)
        {
            float result = 0f, f = 1f;
            int   i      = index + 1;
            while (i > 0) { f /= 2f; result += f * (i % 2); i /= 2; }
            return result;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────────────────────────────

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            bool  fullSphere = emissionAngle >= 360f;
            float halfRad    = emissionAngle * 0.5f * Mathf.Deg2Rad;
            float coneLen    = 0.7f;

            Gizmos.color = new Color(rayColor.r, rayColor.g, rayColor.b, 0.35f);

            if (fullSphere)
            {
                Gizmos.DrawWireSphere(transform.position, coneLen * 0.5f);
            }
            else
            {
                // Draw the cone outline: four rim lines + two circles (one at rim, one mid)
                Vector3 tip = transform.position;
                float rimR  = Mathf.Sin(halfRad) * coneLen;
                float rimD  = Mathf.Cos(halfRad) * coneLen;
                Vector3 rimCentre = tip + transform.forward * rimD;

                // 4 lines from apex to rim
                for (int i = 0; i < 4; i++)
                {
                    float phi = i * Mathf.PI * 0.5f;
                    Vector3 rimPt = rimCentre
                        + transform.right * (Mathf.Cos(phi) * rimR)
                        + transform.up    * (Mathf.Sin(phi) * rimR);
                    Gizmos.DrawLine(tip, rimPt);
                }

                // Rim circle
                DrawCircleGizmo(rimCentre, transform.right, transform.up, rimR, 32);
            }
        }

        private static void DrawCircleGizmo(Vector3 centre, Vector3 axisX, Vector3 axisY,
                                             float radius, int segs)
        {
            Vector3 prev = centre + axisX * radius;
            for (int i = 1; i <= segs; i++)
            {
                float ang = i * Mathf.PI * 2f / segs;
                Vector3 pt = centre + axisX * (Mathf.Cos(ang) * radius)
                                    + axisY * (Mathf.Sin(ang) * radius);
                Gizmos.DrawLine(prev, pt);
                prev = pt;
            }
        }
    }
}
