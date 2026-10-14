using System.Collections.Generic;
using UnityEngine;

namespace RayOptics
{
    /// <summary>
    /// Central manager for the 3D ray optics simulation.
    ///
    /// Rays travel freely in 3D world space. Lenses set
    /// <c>RayHitResult.spawnContinuation</c> to redirect the ray; the manager
    /// propagates colour, brightness and incremented depth automatically.
    /// Depth is capped by <see cref="maxDepth"/>.
    /// </summary>
    public class OpticsManager : MonoBehaviour
    {
        [Header("Simulation Mode")]
        [Tooltip("Toggle between Regular (diagnostic) and Correct Brightness (irradiance) modes.")]
        public bool correctBrightnessMode = false;

        [Header("Correct Brightness Settings")]
        [Range(16, 2048)]
        public int raysPerSourceCB = 128;

        [Header("Ray Settings")]
        [Tooltip("Maximum world-space distance a ray segment travels before being clipped.")]
        public float maxRayLength = 50f;

        [Tooltip("Maximum number of refractions / reflections per ray path.")]
        [Range(1, 16)]
        public int maxDepth = 8;

        [Tooltip("Material for ray LineRenderers. 'Sprites/Default' for normal; additive for CB mode.")]
        public Material lineMaterial;

        [Range(0.002f, 0.3f)]
        public float lineWidth = 0.02f;

        [Header("Scene Objects  (auto-discovered on Start)")]
        public List<LightSource>   lightSources = new List<LightSource>();
        public List<OpticsBlocker> blockers     = new List<OpticsBlocker>();

        // ── Runtime ────────────────────────────────────────────────────────────
        private readonly List<LineRenderer> _linePool = new List<LineRenderer>();
        private int       _activeLines;
        private Transform _lineRoot;

        private void Awake()
        {
            var go = new GameObject("_RayLines");
            go.transform.SetParent(transform);
            _lineRoot = go.transform;
        }

        private void Start()  => RefreshSceneObjects();
        private void Update() => Simulate();

        public void RefreshSceneObjects()
        {
            lightSources.Clear();
            lightSources.AddRange(FindObjectsOfType<LightSource>());
            blockers.Clear();
            blockers.AddRange(FindObjectsOfType<OpticsBlocker>());
        }

        public void ToggleMode() => correctBrightnessMode = !correctBrightnessMode;

        // ── Simulation ─────────────────────────────────────────────────────────

        private void Simulate()
        {
            _activeLines = 0;

            // Tell every wall to discard last frame's hits before we fire new rays
            foreach (var blocker in blockers)
                if (blocker is WallBlocker wall)
                    wall.ClearHits();

            foreach (var src in lightSources)
            {
                if (src == null || !src.isActiveAndEnabled) continue;

                var rays = correctBrightnessMode
                    ? src.GetRaysCorrectBrightness(raysPerSourceCB)
                    : src.GetRaysRegular();

                foreach (var ray in rays)
                    TraceRay(ray);
            }

            // Render dots on walls now that all rays are traced
            foreach (var blocker in blockers)
                if (blocker is WallBlocker wall)
                    wall.FlushDots();

            for (int i = _activeLines; i < _linePool.Count; i++)
                _linePool[i].enabled = false;
        }

        // ── Recursive ray tracing ──────────────────────────────────────────────

        private void TraceRay(OpticsRay ray)
        {
            if (ray.depth > maxDepth) return;

            RayHitResult hit = CastRay(ray.origin, ray.direction, maxRayLength);

            Vector3 segEnd = hit.didHit ? hit.point : ray.origin + ray.direction * maxRayLength;

            Color col = ray.color;
            if (correctBrightnessMode)
                col.a = Mathf.Clamp01(ray.brightness);

            DrawLine(ray.origin, segEnd, col);

            if (hit.didHit)
            {
                // Notify wall so it can render a hit dot
                if (hit.blocker is WallBlocker wall)
                    wall.RegisterHit(hit.point, col);

                // Follow continuation ray through lenses
                if (hit.spawnContinuation)
                {
                    OpticsRay cont = hit.continuationRay;
                    cont.color      = ray.color;
                    cont.brightness = ray.brightness;
                    cont.depth      = ray.depth + 1;
                    TraceRay(cont);
                }
            }
        }

        public RayHitResult CastRay(Vector3 origin, Vector3 direction, float maxDist)
        {
            RayHitResult best = RayHitResult.Miss;

            foreach (var blocker in blockers)
            {
                if (blocker == null || !blocker.isActiveAndEnabled) continue;

                RayHitResult hit = blocker.Intersect(origin, direction, maxDist);
                if (hit.didHit && hit.distance < best.distance)
                    best = hit;
            }

            return best;
        }

        // ── LineRenderer pool ──────────────────────────────────────────────────

        private void DrawLine(Vector3 from, Vector3 to, Color color)
        {
            var lr = GetOrCreateLine(_activeLines++);
            lr.enabled       = true;
            lr.startColor    = color;
            lr.endColor      = color;
            lr.startWidth    = lineWidth;
            lr.endWidth      = lineWidth;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
        }

        private LineRenderer GetOrCreateLine(int index)
        {
            if (index < _linePool.Count) return _linePool[index];

            var go = new GameObject($"Ray_{index}");
            go.transform.SetParent(_lineRoot);

            var lr = go.AddComponent<LineRenderer>();
            lr.material = lineMaterial != null
                ? lineMaterial
                : new Material(Shader.Find("Sprites/Default"));
            lr.useWorldSpace        = true;
            lr.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows       = false;
            lr.sortingOrder         = 10;
            lr.generateLightingData = false;

            _linePool.Add(lr);
            return lr;
        }
    }
}
