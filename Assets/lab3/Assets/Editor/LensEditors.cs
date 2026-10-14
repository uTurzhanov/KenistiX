#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using RayOptics;

namespace RayOpticsEditor
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Ideal Lens Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [CustomEditor(typeof(IdealLens))]
    public class IdealLensEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var lens = (IdealLens)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Computed Properties", EditorStyles.boldLabel);

            string kind = lens.focalLength > 0f ? "Converging (positive)" : "Diverging (negative)";
            EditorGUILayout.HelpBox(
                $"Type: {kind}\n" +
                $"Focal length: {lens.focalLength:F3} world units\n" +
                $"Front focal point: {-lens.focalLength:F3} from lens centre\n" +
                $"Rear  focal point: {+lens.focalLength:F3} from lens centre",
                MessageType.None);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Spherical Lens Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [CustomEditor(typeof(SphericalLens))]
    public class SphericalLensEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var lens = (SphericalLens)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Computed Properties", EditorStyles.boldLabel);

            // Thick-lens lensmaker's equation
            float n    = lens.refractiveIndex;
            float d    = lens.thickness;
            float invR1 = Mathf.Abs(lens.R1) > 1e-6f ? 1f / lens.R1 : 0f;
            float invR2 = Mathf.Abs(lens.R2) > 1e-6f ? 1f / lens.R2 : 0f;
            float inv_f = (n - 1f) * (invR1 - invR2 + (n - 1f) * d * invR1 * invR2 / n);

            string eflStr;
            string typeStr;
            if (Mathf.Abs(inv_f) < 1e-9f)
            {
                eflStr  = "∞  (afocal / flat)";
                typeStr = "Afocal";
            }
            else
            {
                float efl = 1f / inv_f;
                eflStr  = $"{efl:F3} world units";
                typeStr = efl > 0f ? "Converging" : "Diverging";
            }

            // Warn about physically impossible configs
            bool r1TooSmall = Mathf.Abs(lens.R1) > 1e-6f && Mathf.Abs(lens.R1) < lens.apertureRadius;
            bool r2TooSmall = Mathf.Abs(lens.R2) > 1e-6f && Mathf.Abs(lens.R2) < lens.apertureRadius;

            EditorGUILayout.HelpBox(
                $"Type: {typeStr}\n" +
                $"EFL (thick lens): {eflStr}\n" +
                $"Refractive index: {n:F3}\n" +
                $"Center thickness: {d:F3}",
                MessageType.None);

            if (r1TooSmall)
                EditorGUILayout.HelpBox(
                    $"|R1| ({Mathf.Abs(lens.R1):F2}) is smaller than apertureRadius ({lens.apertureRadius:F2}).\n" +
                    "The sphere's CoC falls inside the aperture — increase |R1| or reduce the aperture.",
                    MessageType.Warning);

            if (r2TooSmall)
                EditorGUILayout.HelpBox(
                    $"|R2| ({Mathf.Abs(lens.R2):F2}) is smaller than apertureRadius ({lens.apertureRadius:F2}).\n" +
                    "The sphere's CoC falls inside the aperture — increase |R2| or reduce the aperture.",
                    MessageType.Warning);

            // Quick-set buttons for common lens forms
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Presets", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Biconvex"))   SetRadii(lens,  3f, -3f);
            if (GUILayout.Button("Biconcave"))  SetRadii(lens, -3f,  3f);
            if (GUILayout.Button("Plano-cvx"))  SetRadii(lens,  0f, -3f);
            if (GUILayout.Button("Plano-ccv"))  SetRadii(lens,  0f,  3f);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Meniscus+")) SetRadii(lens,  2f,  4f);
            if (GUILayout.Button("Meniscus−")) SetRadii(lens, -2f, -4f);
            EditorGUILayout.EndHorizontal();
        }

        private void SetRadii(SphericalLens lens, float r1, float r2)
        {
            Undo.RecordObject(lens, "Set Lens Radii");
            lens.R1 = r1;
            lens.R2 = r2;
        }
    }
}
#endif
