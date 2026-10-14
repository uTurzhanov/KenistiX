#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using RayOptics;

namespace RayOpticsEditor
{
    /// <summary>
    /// Shared Scene-view ray preview for all <see cref="LightSource"/> subclasses.
    /// Shows a lightweight preview while selected; the OpticsManager draws the live sim.
    /// </summary>
    [CustomEditor(typeof(LightSource), true)]
    [CanEditMultipleObjects]
    public class LightSourceEditor : Editor
    {
        private void OnSceneGUI()
        {
            LightSource src = (LightSource)target;
            var mgr = FindObjectOfType<OpticsManager>();
            bool cb = mgr != null && mgr.correctBrightnessMode;

            Handles.color = new Color(src.rayColor.r, src.rayColor.g, src.rayColor.b, 0.45f);

            var rays = cb
                ? src.GetRaysCorrectBrightness(Mathf.Min(32, mgr != null ? mgr.raysPerSourceCB : 32))
                : src.GetRaysRegular();

            float previewLen = mgr != null ? mgr.maxRayLength : 20f;

            foreach (var ray in rays)
            {
                Vector3 end = ray.origin + ray.direction * previewLen;
                Handles.DrawLine(ray.origin, end);
            }
        }
    }
}
#endif
