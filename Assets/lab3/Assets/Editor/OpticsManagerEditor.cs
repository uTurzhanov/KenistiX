#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using RayOptics;

namespace RayOpticsEditor
{
    [CustomEditor(typeof(OpticsManager))]
    public class OpticsManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            OpticsManager mgr = (OpticsManager)target;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Quick Controls", EditorStyles.boldLabel);

            Color oldBg = GUI.backgroundColor;
            GUI.backgroundColor = mgr.correctBrightnessMode
                ? new Color(0.3f, 0.8f, 0.4f)
                : new Color(0.4f, 0.6f, 1.0f);

            string modeLabel = mgr.correctBrightnessMode
                ? "▶  Switch to Regular Rays"
                : "▶  Switch to Correct Brightness";

            if (GUILayout.Button(modeLabel, GUILayout.Height(32)))
            {
                Undo.RecordObject(mgr, "Toggle Optics Mode");
                mgr.correctBrightnessMode = !mgr.correctBrightnessMode;
            }
            GUI.backgroundColor = oldBg;

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                mgr.correctBrightnessMode
                    ? $"MODE: Correct Brightness  ({mgr.raysPerSourceCB} rays / source)"
                    : "MODE: Regular Rays  (1 for SinglePoint, 9 for others)",
                MessageType.Info);

            if (GUILayout.Button("Refresh Scene Objects"))
                mgr.RefreshSceneObjects();

            if (Application.isPlaying) Repaint();
        }
    }
}
#endif
