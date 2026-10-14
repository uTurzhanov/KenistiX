using System.Collections.Generic;
using UnityEngine;

namespace RayOptics
{
    /// <summary>
    /// Parallel beam (collimated light) from a shaped aperture.
    ///
    /// All rays travel in <c>transform.forward</c>.
    /// The aperture plane is spanned by <c>transform.right</c> (U) and
    /// <c>transform.up</c> (V), centred on <c>transform.position</c>.
    ///
    /// Aperture shapes and their Regular-mode ray layouts:
    ///
    ///   Circle   : 1 centre + 8 on circumference (every 45°)         = 9 rays
    ///   Square   : 1 centre + 4 corners + 4 edge midpoints            = 9 rays
    ///   Triangle : 1 centre + 3 corners + 3 edge midpoints            = 7 rays
    ///   T-Shape  : 1 centre + 2 bar ends + bar/stem junction
    ///              + mid-stem + stem bottom                            = 6 rays
    ///              (T bar runs along U, stem runs down −V)
    ///
    /// Correct Brightness mode uses a dense fill of the same shape.
    /// </summary>
    [AddComponentMenu("Ray Optics/Sources/Beam Source")]
    public class BeamSource : LightSource
    {
        public enum ApertureShape { Circle, Square, Triangle, TShape }

        [Header("Beam Settings")]
        public ApertureShape apertureShape = ApertureShape.Circle;

        [Tooltip("Overall scale of the aperture in world units.\n" +
                 "Circle: radius.  Square: half-width.  Triangle: circumradius.  T: half-bar-width.")]
        [Range(0.05f, 5f)]
        public float apertureSize = 0.5f;

        // ─────────────────────────────────────────────────────────────────────
        //  Regular mode
        // ─────────────────────────────────────────────────────────────────────

        public override List<OpticsRay> GetRaysRegular()
        {
            var rays = new List<OpticsRay>(9);

            switch (apertureShape)
            {
                case ApertureShape.Circle:
                    rays.Add(MakeRay(Pos, Fwd));
                    for (int i = 0; i < 8; i++)
                    {
                        float   ang = i * Mathf.PI * 2f / 8f;
                        Vector3 off = Right * (Mathf.Cos(ang) * apertureSize)
                                    + Up    * (Mathf.Sin(ang) * apertureSize);
                        rays.Add(MakeRay(Pos + off, Fwd));
                    }
                    break;

                case ApertureShape.Square:
                {
                    float h  = apertureSize;
                    float[] us = {  0,  h,  h, -h, -h,  h, -h,  0,  0 };
                    float[] vs = {  0,  h, -h,  h, -h,  0,  0,  h, -h };
                    foreach (var (u, v) in Zip(us, vs))
                        rays.Add(MakeRay(Pos + Right * u + Up * v, Fwd));
                    break;
                }

                case ApertureShape.Triangle:
                    rays.AddRange(TriangleRaysRegular());
                    break;

                case ApertureShape.TShape:
                    rays.AddRange(TShapeRaysRegular());
                    break;
            }

            return rays;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Correct Brightness mode
        // ─────────────────────────────────────────────────────────────────────

        public override List<OpticsRay> GetRaysCorrectBrightness(int count)
        {
            var   rays   = new List<OpticsRay>(count);
            float bright = intensity / count;

            switch (apertureShape)
            {
                case ApertureShape.Circle:
                    // Vogel / sunflower spiral — uniform disk density
                    float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
                    for (int i = 0; i < count; i++)
                    {
                        float   r   = apertureSize * Mathf.Sqrt((float)i / count);
                        float   ang = i * goldenAngle;
                        Vector3 off = Right * (Mathf.Cos(ang) * r)
                                    + Up    * (Mathf.Sin(ang) * r);
                        rays.Add(new OpticsRay(Pos + off, Fwd, rayColor, bright));
                    }
                    break;

                case ApertureShape.Square:
                {
                    // Uniform UV grid
                    int   side = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(count)));
                    float step = side == 1 ? 0f : apertureSize * 2f / (side - 1);
                    for (int iv = 0; iv < side; iv++)
                    for (int iu = 0; iu < side; iu++)
                    {
                        float u = -apertureSize + iu * step;
                        float v = -apertureSize + iv * step;
                        rays.Add(new OpticsRay(Pos + Right * u + Up * v, Fwd, rayColor, bright));
                    }
                    break;
                }

                case ApertureShape.Triangle:
                    rays.AddRange(TriangleRaysCB(count, bright));
                    break;

                case ApertureShape.TShape:
                    rays.AddRange(TShapeRaysCB(count, bright));
                    break;
            }

            return rays;
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Triangle helpers
        // ─────────────────────────────────────────────────────────────────────

        // The triangle sits with:
        //   top apex    at  (0,  s)       — tip points up (+V)
        //   bottom-left at  (-s·√3/2, -s/2)
        //   bottom-right at (+s·√3/2, -s/2)
        // where s = apertureSize (circumradius).

        private Vector3[] TriangleVertices()
        {
            float s  = apertureSize;
            float bx = s * Mathf.Sqrt(3f) * 0.5f;   // half base = s·sin(60°)
            float by = s * 0.5f;                      // below centre = s·cos(60°)

            return new Vector3[]
            {
                Pos + Up * s,                            // top apex
                Pos - Right * bx - Up * by,              // bottom-left
                Pos + Right * bx - Up * by,              // bottom-right
            };
        }

        private IEnumerable<OpticsRay> TriangleRaysRegular()
        {
            Vector3[] v = TriangleVertices();

            // Centre
            yield return MakeRay(Pos, Fwd);

            // 3 corners
            foreach (var corner in v)
                yield return MakeRay(corner, Fwd);

            // 3 edge midpoints
            for (int i = 0; i < 3; i++)
                yield return MakeRay((v[i] + v[(i + 1) % 3]) * 0.5f, Fwd);
        }

        /// <summary>
        /// CB mode: fill the triangle with a jittered lattice using rejection sampling.
        /// We sample a bounding-box grid and keep points inside the triangle.
        /// </summary>
        private IEnumerable<OpticsRay> TriangleRaysCB(int count, float bright)
        {
            Vector3[] v = TriangleVertices();

            // Precompute barycentric test coefficients (in UV local space)
            // Vertices in local UV (Right=X, Up=Y)
            Vector2 a = ToUV(v[0]), b = ToUV(v[1]), c = ToUV(v[2]);

            // Bounding box of triangle in UV
            float uMin = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
            float uMax = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            float vMin = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
            float vMax = Mathf.Max(a.y, Mathf.Max(b.y, c.y));

            // Estimate grid density: triangle area ≈ 0.433·s² * (√3/4 * base²/... )
            // Simpler: oversample by ~2× and reject, targeting `count` accepted rays.
            int   gridN = Mathf.Max(2, Mathf.RoundToInt(Mathf.Sqrt(count * 2.5f)));
            float du    = (uMax - uMin) / (gridN - 1);
            float dv    = (vMax - vMin) / (gridN - 1);

            var pts = new List<Vector2>(gridN * gridN);
            for (int j = 0; j < gridN; j++)
            for (int i = 0; i < gridN; i++)
            {
                float pu = uMin + i * du;
                float pv = vMin + j * dv;
                if (PointInTriangle(new Vector2(pu, pv), a, b, c))
                    pts.Add(new Vector2(pu, pv));
            }

            // Re-normalise brightness to actual ray count
            float actualBright = pts.Count > 0 ? intensity / pts.Count : bright;
            foreach (var pt in pts)
                yield return new OpticsRay(Pos + Right * pt.x + Up * pt.y,
                                           Fwd, rayColor, actualBright);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  T-shape helpers
        // ─────────────────────────────────────────────────────────────────────

        // T-shape layout (s = apertureSize):
        //
        //   Horizontal bar:  V = +s/2,  U ∈ [−s, +s]   (top half)
        //   Vertical stem:   U = 0,     V ∈ [−s, +s/2] (runs from bar down to bottom)
        //
        // The bar is centred at (0, +s/2) and the stem runs from
        // (0, +s/2) down to (0, −s), so overall height = 1.5·s.
        //
        // Regular rays (6):
        //   bar left end (−s, +s/2),  bar right end (+s, +s/2),
        //   bar+stem junction (0, +s/2),
        //   mid-stem (0, −s/4),  stem bottom (0, −s),
        //   centre of bounding box (0, −s/4 ... actually the visual centroid)
        //
        // We define "centre" as the geometric centroid of the T region.

        private IEnumerable<OpticsRay> TShapeRaysRegular()
        {
            float s  = apertureSize;
            float hy = s * 0.5f;   // bar's V coordinate

            // Centroid of T (visual centre ray)
            // T = bar (2s × hs) + stem (hs × 1.5s), where hs = stem width (= s*0.3 approx)
            // For ray purposes we just use the bounding-box centre as the "centre" ray.
            Vector3 centre = Pos + Up * (-s * 0.25f);   // midpoint of vertical extent [−s, +hy]
            yield return MakeRay(centre, Fwd);

            // Horizontal bar: left, junction, right
            yield return MakeRay(Pos + Right * (-s) + Up * hy, Fwd);
            yield return MakeRay(Pos                + Up * hy, Fwd);   // junction (top of stem)
            yield return MakeRay(Pos + Right * ( s) + Up * hy, Fwd);

            // Vertical stem below junction: mid and bottom
            yield return MakeRay(Pos + Up * (-s * 0.25f), Fwd);        // mid-stem
            yield return MakeRay(Pos + Up * (-s),          Fwd);        // stem bottom
        }

        /// <summary>
        /// CB mode: fill the T-shape by separately filling the bar and stem
        /// regions proportionally to their areas, then merging.
        /// </summary>
        private IEnumerable<OpticsRay> TShapeRaysCB(int count, float bright)
        {
            float s  = apertureSize;
            float hy = s * 0.5f;

            // Bar region:  U ∈ [−s, +s],  V ∈ [hy*0.5, hy*1.5]  → height = hy
            // For simplicity define the bar thickness = apertureSize * 0.35
            float barThick  = s * 0.35f;
            float barTop    = hy + barThick * 0.5f;
            float barBot    = hy - barThick * 0.5f;

            // Stem region: U ∈ [−barThick/2, +barThick/2], V ∈ [−s, barBot]
            float stemHW    = barThick * 0.5f;
            float stemBot   = -s;
            float stemTop   = barBot;

            // Area ratio for ray allocation
            float barArea  = (2f * s) * barThick;
            float stemArea = barThick * (stemTop - stemBot);
            float total    = barArea + stemArea;

            int barCount  = Mathf.Max(1, Mathf.RoundToInt(count * barArea  / total));
            int stemCount = Mathf.Max(1, count - barCount);

            float actualBright = intensity / (barCount + stemCount);

            // Bar: grid
            int barCols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(barCount * 2f * s / barThick)));
            int barRows = Mathf.Max(1, Mathf.RoundToInt((float)barCount / barCols));
            for (int row = 0; row < barRows; row++)
            for (int col = 0; col < barCols; col++)
            {
                float u = Mathf.Lerp(-s,     s,      (barCols  == 1 ? 0.5f : (float)col / (barCols  - 1)));
                float v = Mathf.Lerp(barBot, barTop, (barRows  == 1 ? 0.5f : (float)row / (barRows  - 1)));
                yield return new OpticsRay(Pos + Right * u + Up * v, Fwd, rayColor, actualBright);
            }

            // Stem: grid
            int stemCols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(stemCount * barThick / (stemTop - stemBot))));
            int stemRows = Mathf.Max(1, stemCount / stemCols);
            for (int row = 0; row < stemRows; row++)
            for (int col = 0; col < stemCols; col++)
            {
                float u = Mathf.Lerp(-stemHW,  stemHW,  (stemCols == 1 ? 0.5f : (float)col / (stemCols - 1)));
                float v = Mathf.Lerp(stemBot, stemTop,   (stemRows == 1 ? 0.5f : (float)row / (stemRows - 1)));
                yield return new OpticsRay(Pos + Right * u + Up * v, Fwd, rayColor, actualBright);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Geometry utilities
        // ─────────────────────────────────────────────────────────────────────

        /// Convert a world point on the aperture plane to local UV coordinates.
        private Vector2 ToUV(Vector3 worldPt)
        {
            Vector3 d = worldPt - Pos;
            return new Vector2(Vector3.Dot(d, Right), Vector3.Dot(d, Up));
        }

        /// Barycentric point-in-triangle test.
        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
            => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        /// Simple zip helper (C# 7 value-tuple).
        private static IEnumerable<(float, float)> Zip(float[] a, float[] b)
        {
            for (int i = 0; i < Mathf.Min(a.Length, b.Length); i++)
                yield return (a[i], b[i]);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────────────────────────────

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Color col = new Color(rayColor.r, rayColor.g, rayColor.b, 0.65f);
            Gizmos.color = col;

            Vector3 p   = transform.position;
            Vector3 fwd = transform.forward;
            Vector3 r   = transform.right;
            Vector3 u   = transform.up;
            float   s   = apertureSize;
            float   ext = s * 0.5f;   // barrel depth

            switch (apertureShape)
            {
                case ApertureShape.Circle:
                    DrawApertureCircle(p, r, u, s, 36);
                    DrawBarrelLines(p, fwd, r, u, s, ext, 4);
                    break;

                case ApertureShape.Square:
                    DrawApertureRect(p, fwd, r, u, s, s, ext);
                    break;

                case ApertureShape.Triangle:
                    DrawApertureTriangle(p, fwd, r, u, s, ext);
                    break;

                case ApertureShape.TShape:
                    DrawApertureTShape(p, fwd, r, u, s, ext);
                    break;
            }

            // Central forward arrow
            Gizmos.color = rayColor;
            Gizmos.DrawRay(p, fwd * (s + ext));
        }

        // ── Aperture outline drawers ──────────────────────────────────────────

        private static void DrawApertureCircle(Vector3 centre, Vector3 r, Vector3 u,
                                                float radius, int segs)
        {
            Vector3 prev = centre + r * radius;
            for (int i = 1; i <= segs; i++)
            {
                float   ang = i * Mathf.PI * 2f / segs;
                Vector3 pt  = centre + r * (Mathf.Cos(ang) * radius)
                                     + u * (Mathf.Sin(ang) * radius);
                Gizmos.DrawLine(prev, pt);
                prev = pt;
            }
        }

        private static void DrawBarrelLines(Vector3 centre, Vector3 fwd,
                                             Vector3 r, Vector3 u,
                                             float radius, float ext, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float   ang = i * Mathf.PI * 2f / count;
                Vector3 rim = centre + r * (Mathf.Cos(ang) * radius)
                                     + u * (Mathf.Sin(ang) * radius);
                Gizmos.DrawLine(rim, rim + fwd * ext);
            }
        }

        private static void DrawApertureRect(Vector3 p, Vector3 fwd,
                                              Vector3 r, Vector3 u,
                                              float hw, float hh, float ext)
        {
            Vector3[] corners =
            {
                p + r * hw + u * hh,
                p + r * hw - u * hh,
                p - r * hw - u * hh,
                p - r * hw + u * hh,
            };
            // Front aperture
            for (int i = 0; i < 4; i++) Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
            // Barrel
            foreach (var c in corners) Gizmos.DrawLine(c, c + fwd * ext);
            // Back cap
            for (int i = 0; i < 4; i++)
                Gizmos.DrawLine(corners[i] + fwd * ext, corners[(i + 1) % 4] + fwd * ext);
        }

        private static void DrawApertureTriangle(Vector3 p, Vector3 fwd,
                                                  Vector3 r, Vector3 u,
                                                  float s, float ext)
        {
            float bx = s * Mathf.Sqrt(3f) * 0.5f;
            float by = s * 0.5f;

            Vector3[] v =
            {
                p + u * s,
                p - r * bx - u * by,
                p + r * bx - u * by,
            };
            // Front aperture
            for (int i = 0; i < 3; i++) Gizmos.DrawLine(v[i], v[(i + 1) % 3]);
            // Barrel
            foreach (var pt in v) Gizmos.DrawLine(pt, pt + fwd * ext);
            // Back cap
            for (int i = 0; i < 3; i++)
                Gizmos.DrawLine(v[i] + fwd * ext, v[(i + 1) % 3] + fwd * ext);
        }

        private static void DrawApertureTShape(Vector3 p, Vector3 fwd,
                                                Vector3 r, Vector3 u,
                                                float s, float ext)
        {
            float hy        = s * 0.5f;
            float barThick  = s * 0.35f;
            float barTop    = hy + barThick * 0.5f;
            float barBot    = hy - barThick * 0.5f;
            float stemHW    = barThick * 0.5f;
            float stemBot   = -s;

            // T outline: 8 vertices going clockwise from top-left
            Vector3[] outline =
            {
                p - r * s     + u * barTop,   // 0 top-left of bar
                p + r * s     + u * barTop,   // 1 top-right of bar
                p + r * s     + u * barBot,   // 2 bottom-right of bar
                p + r * stemHW + u * barBot,  // 3 inner-right shoulder
                p + r * stemHW + u * stemBot, // 4 bottom-right of stem
                p - r * stemHW + u * stemBot, // 5 bottom-left of stem
                p - r * stemHW + u * barBot,  // 6 inner-left shoulder
                p - r * s     + u * barBot,   // 7 bottom-left of bar
            };

            // Front aperture outline
            for (int i = 0; i < outline.Length; i++)
                Gizmos.DrawLine(outline[i], outline[(i + 1) % outline.Length]);

            // Barrel lines at corners
            foreach (var pt in outline)
                Gizmos.DrawLine(pt, pt + fwd * ext);

            // Back cap
            for (int i = 0; i < outline.Length; i++)
                Gizmos.DrawLine(outline[i] + fwd * ext, outline[(i + 1) % outline.Length] + fwd * ext);
        }
    }
}
