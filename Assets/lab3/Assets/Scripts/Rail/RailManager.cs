using System.Collections.Generic;
using UnityEngine;

namespace RayOptics
{
    /// <summary>
    /// Defines a single physical rail — a straight track along which
    /// <see cref="RailModule"/> components can slide.
    ///
    /// Setup
    /// ─────
    /// Place this component on a GameObject at the rail's centre position.
    /// Set <see cref="railAxis"/> (default X) and <see cref="railLength"/>.
    /// The rail runs from  position − axis*(length/2)  to  position + axis*(length/2).
    ///
    /// The rail's position and orientation are NOT meant to be changed at runtime
    /// by the user — treat this as infrastructure, not an interactive object.
    ///
    /// Module discovery
    /// ────────────────
    /// On Start the manager finds all <see cref="RailModule"/> instances in the
    /// scene that reference this manager. It then drives their constraints every
    /// frame via <c>LateUpdate</c> (after any user/physics movement has settled).
    /// Modules can also call <see cref="RegisterModule"/> / <see cref="UnregisterModule"/>
    /// if they are spawned or destroyed at runtime.
    /// </summary>
    [AddComponentMenu("Rail Optics/Rail Manager")]
    public class RailManager : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────────────
        //  Inspector
        // ─────────────────────────────────────────────────────────────────────

        [Header("Rail Geometry")]
        [Tooltip("The axis along which modules slide. Typically Vector3.right (X). " +
                 "Must be a unit vector — it will be normalised on Start.")]
        public Vector3 railAxis = Vector3.right;

        [Tooltip("Total length of the rail in world units.")]
        [Range(0.1f, 100f)]
        public float railLength = 10f;

        [Tooltip("If true, modules are clamped so they cannot slide past the rail ends.")]
        public bool clampToBounds = true;

        // ─────────────────────────────────────────────────────────────────────
        //  Derived geometry (computed once in Start, then read-only)
        // ─────────────────────────────────────────────────────────────────────

        /// Normalised rail direction in world space.
        public Vector3 Axis        { get; private set; }

        /// World-space centre of the rail (equals transform.position).
        public Vector3 Centre      { get; private set; }

        /// World-space start point (low end of the axis).
        public Vector3 StartPoint  { get; private set; }

        /// World-space end point (high end of the axis).
        public Vector3 EndPoint    { get; private set; }

        /// World-space Y coordinate of the rail surface (modules hang below this).
        public float   RailY       { get; private set; }

        // ─────────────────────────────────────────────────────────────────────
        //  Module registry
        // ─────────────────────────────────────────────────────────────────────

        private readonly List<RailModule> _modules = new List<RailModule>();

        // ─────────────────────────────────────────────────────────────────────
        //  Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────

        private void Start()
        {
            BakeGeometry();
            DiscoverModules();
        }

        private void LateUpdate()
        {
            // Drive all registered modules after normal Update() has run
            foreach (var m in _modules)
            {
                if (m != null && m.isActiveAndEnabled)
                    m.ApplyRailConstraints(this);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Keep gizmos correct while editing in the Inspector
            BakeGeometry();
        }
#endif

        // ─────────────────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────────────────

        public void RegisterModule(RailModule module)
        {
            if (!_modules.Contains(module))
                _modules.Add(module);
        }

        public void UnregisterModule(RailModule module)
        {
            _modules.Remove(module);
        }

        /// <summary>
        /// Project a world-space position onto the rail axis and return the
        /// signed distance from the rail centre (negative = start side).
        /// Optionally clamp to [−length/2, +length/2].
        /// </summary>
        public float ProjectOntoRail(Vector3 worldPos, bool clamp = false)
        {
            float t = Vector3.Dot(worldPos - Centre, Axis);
            if (clamp)
                t = Mathf.Clamp(t, -railLength * 0.5f, railLength * 0.5f);
            return t;
        }

        /// <summary>
        /// Return the world-space point on the rail at signed offset
        /// <paramref name="t"/> from the centre.
        /// </summary>
        public Vector3 PointOnRail(float t) => Centre + Axis * t;

        // ─────────────────────────────────────────────────────────────────────
        //  Internals
        // ─────────────────────────────────────────────────────────────────────

        private void BakeGeometry()
        {
            Axis       = railAxis.sqrMagnitude > 0f ? railAxis.normalized : Vector3.right;
            Centre     = transform.position;
            RailY      = Centre.y;
            StartPoint = Centre - Axis * (railLength * 0.5f);
            EndPoint   = Centre + Axis * (railLength * 0.5f);
        }

        private void DiscoverModules()
        {
            _modules.Clear();
            var all = FindObjectsOfType<RailModule>();
            foreach (var m in all)
                if (m.rail == this)
                    _modules.Add(m);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Gizmos
        // ─────────────────────────────────────────────────────────────────────

        private void OnDrawGizmos()
        {
            BakeGeometry();
            DrawRailGizmo(new Color(0.6f, 0.85f, 1f, 0.5f));
        }

        private void OnDrawGizmosSelected()
        {
            BakeGeometry();
            DrawRailGizmo(new Color(0.6f, 0.85f, 1f, 1f));

            // Axis arrow
            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.9f);
            Vector3 tip = EndPoint;
            Gizmos.DrawRay(Centre, Axis * (railLength * 0.5f));
            // Arrowhead
            Vector3 perp = Vector3.Cross(Axis, Vector3.up).normalized;
            if (perp.sqrMagnitude < 0.01f) perp = Vector3.Cross(Axis, Vector3.forward).normalized;
            float hs = railLength * 0.04f;
            Gizmos.DrawLine(tip, tip - Axis * hs + perp * hs);
            Gizmos.DrawLine(tip, tip - Axis * hs - perp * hs);
        }

        private void DrawRailGizmo(Color col)
        {
            Gizmos.color = col;

            // Main rail bar
            Gizmos.DrawLine(StartPoint, EndPoint);

            // End caps
            Vector3 perp = Vector3.Cross(Axis, Vector3.up).normalized;
            if (perp.sqrMagnitude < 0.01f) perp = Vector3.Cross(Axis, Vector3.forward).normalized;
            float capH = Mathf.Min(railLength * 0.03f, 0.15f);
            Gizmos.DrawLine(StartPoint - perp * capH, StartPoint + perp * capH);
            Gizmos.DrawLine(EndPoint   - perp * capH, EndPoint   + perp * capH);

            // Centre marker
            Gizmos.DrawWireSphere(Centre, capH * 0.5f);
        }
    }
}
