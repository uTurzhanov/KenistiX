#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using RayOptics;

namespace RayOpticsEditor
{
    // ─────────────────────────────────────────────────────────────────────────
    //  RailManager Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [CustomEditor(typeof(RailManager))]
    public class RailManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            RailManager mgr = (RailManager)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Rail Info", EditorStyles.boldLabel);

            Vector3 axis   = mgr.railAxis.sqrMagnitude > 0f
                ? mgr.railAxis.normalized : Vector3.right;
            Vector3 centre = mgr.transform.position;
            Vector3 start  = centre - axis * (mgr.railLength * 0.5f);
            Vector3 end    = centre + axis * (mgr.railLength * 0.5f);

            EditorGUILayout.HelpBox(
                $"Axis direction : {axis:F2}\n" +
                $"Start point    : {start:F2}\n" +
                $"End point      : {end:F2}\n" +
                $"Rail Y         : {centre.y:F3}",
                MessageType.None);

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Discover Modules in Scene"))
            {
                // Force re-discover at edit time for debugging
                var modules = FindObjectsOfType<RailModule>();
                int count = 0;
                foreach (var m in modules)
                    if (m.rail == mgr) count++;
                Debug.Log($"[RailManager] Found {count} module(s) referencing this rail.");
            }

            if (Application.isPlaying) Repaint();
        }

        private void OnSceneGUI()
        {
            RailManager mgr = (RailManager)target;

            Vector3 axis   = mgr.railAxis.sqrMagnitude > 0f
                ? mgr.railAxis.normalized : Vector3.right;
            Vector3 centre = mgr.transform.position;
            float   half   = mgr.railLength * 0.5f;
            Vector3 start  = centre - axis * half;
            Vector3 end    = centre + axis * half;

            // Editable length handle — drag the end point
            EditorGUI.BeginChangeCheck();

            float handleSize = HandleUtility.GetHandleSize(end) * 0.12f;
            Handles.color = new Color(0.6f, 0.85f, 1f, 0.9f);

            Vector3 newEnd = Handles.FreeMoveHandle(
                end, handleSize, Vector3.zero, Handles.SphereHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mgr, "Resize Rail");
                float newHalf = Vector3.Dot(newEnd - centre, axis);
                mgr.railLength = Mathf.Max(0.1f, newHalf * 2f);
            }

            // Label
            Handles.color = Color.white;
            Handles.Label(end + Vector3.up * 0.15f,
                $"Rail ({mgr.railLength:F1} u)",
                EditorStyles.miniLabel);

            // Start cap label
            Handles.Label(start + Vector3.up * 0.15f,
                $"{-half:F1}",
                EditorStyles.miniLabel);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  RailModule Inspector
    // ─────────────────────────────────────────────────────────────────────────

    [CustomEditor(typeof(RailModule))]
    public class RailModuleEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            RailModule mod = (RailModule)target;

            // ── Rail Reference ────────────────────────────────────────────
            EditorGUILayout.LabelField("Rail Reference", EditorStyles.boldLabel);
            mod.rail = (RailManager)EditorGUILayout.ObjectField(
                "Rail", mod.rail, typeof(RailManager), true);

            EditorGUILayout.Space(4);

            // ── Constraints ───────────────────────────────────────────────
            EditorGUILayout.LabelField("Constraints", EditorStyles.boldLabel);

            // onRail
            mod.onRail = EditorGUILayout.Toggle(
                new GUIContent("On Rail",
                    "Attach this module to the rail. Locks perpendicular position and rotation."),
                mod.onRail);

            // pinnedRailPosition — only meaningful when onRail is true
            using (new EditorGUI.DisabledScope(!mod.onRail))
            {
                mod.pinnedRailPosition = EditorGUILayout.Toggle(
                    new GUIContent("Pinned Rail Position",
                        "Requires On Rail. Prevents sliding along the rail axis."),
                    mod.pinnedRailPosition);
            }

            // pinnedModuleHeight — independent
            mod.pinnedModuleHeight = EditorGUILayout.Toggle(
                new GUIContent("Pinned Module Height",
                    "Freezes the connector length. Works regardless of On Rail state."),
                mod.pinnedModuleHeight);

            EditorGUILayout.Space(6);

            // ── Display values ────────────────────────────────────────────
            EditorGUILayout.LabelField("Position & Height", EditorStyles.boldLabel);

            // RailPosition — editable but only meaningful on rail
            using (new EditorGUI.DisabledScope(!mod.onRail))
            {
                float newRP = EditorGUILayout.FloatField(
                    new GUIContent("Rail Position",
                        "Signed distance from rail centre along the rail axis (world units). " +
                        "Updated live when On Rail. Can be used for UI display or calculations."),
                    mod.railPosition);

                // Allow manual override of position via this field (moves the module)
                if (!mod.onRail || mod.pinnedRailPosition)
                {
                    // Read-only when off rail or pinned
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.FloatField("Rail Position (locked)", mod.railPosition);
                    EditorGUI.EndDisabledGroup();
                }
                else if (!Mathf.Approximately(newRP, mod.railPosition))
                {
                    Undo.RecordObject(mod.transform, "Set Rail Position");
                    Undo.RecordObject(mod, "Set Rail Position");
                    if (mod.rail != null)
                    {
                        Vector3 p = mod.transform.position;
                        Vector3 onAxis = mod.rail.PointOnRail(newRP);
                        Vector3 axis   = mod.rail.Axis;
                        // Move only the axis component
                        if (Mathf.Abs(Vector3.Dot(axis, Vector3.right))   > 0.9f) p.x = onAxis.x;
                        if (Mathf.Abs(Vector3.Dot(axis, Vector3.forward)) > 0.9f) p.z = onAxis.z;
                        mod.transform.position = p;
                    }
                    mod.railPosition = newRP;
                }
            }

            // ModuleHeight — editable only when onRail=true AND not pinned
            bool heightEditable = mod.onRail && !mod.pinnedModuleHeight;
            using (new EditorGUI.DisabledScope(!heightEditable))
            {
                float newH = EditorGUILayout.FloatField(
                    new GUIContent("Module Height",
                        "Length of the connector from the rail to the module payload (world units). " +
                        "Editable only when On Rail and not Pinned. " +
                        "Preserved even when the module is removed from the rail."),
                    mod.moduleHeight);

                if (heightEditable && !Mathf.Approximately(newH, mod.moduleHeight))
                {
                    Undo.RecordObject(mod, "Set Module Height");
                    mod.moduleHeight = Mathf.Max(0f, newH);
                }
            }

            EditorGUILayout.Space(4);

            // ── Status box ────────────────────────────────────────────────
            string status = BuildStatusString(mod);
            MessageType msgType = mod.rail == null ? MessageType.Warning : MessageType.Info;
            EditorGUILayout.HelpBox(status, msgType);

            // ── Target Module ────────────────────────────────────────────────
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Target Module", EditorStyles.boldLabel);

            Transform newTarget = (Transform)EditorGUILayout.ObjectField(
                new GUIContent("Target Module",
                    "Optional explicit transform that should move up/down with ModuleHeight " +
                    "(e.g. a lens, light source, or wall already in your scene). Must be a " +
                    "child of this RailModule. If left empty, the auto-created ModulePayload " +
                    "child is used instead."),
                mod.targetModule, typeof(Transform), true);

            if (newTarget != mod.targetModule)
            {
                // Guard against assigning something that isn't a child of this module —
                // the height math drives localPosition.y, so it only makes sense for children.
                if (newTarget != null && newTarget.parent != mod.transform)
                {
                    EditorUtility.DisplayDialog(
                        "Invalid Target Module",
                        "Target Module must be a direct child of this RailModule's transform, " +
                        "otherwise its position cannot be driven correctly.\n\n" +
                        $"'{newTarget.name}' is not a child of '{mod.gameObject.name}'.",
                        "OK");
                }
                else
                {
                    Undo.RecordObject(mod, "Set Target Module");
                    mod.targetModule = newTarget;
                }
            }

            if (mod.targetModule != null)
                EditorGUILayout.HelpBox(
                    $"Driving '{mod.targetModule.name}' directly. ModulePayload (if any) is unused.",
                    MessageType.None);

            // ── Connector / payload child names ─────────────────────────────
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);
            mod.connectorChildName = EditorGUILayout.TextField(
                new GUIContent("Connector Child Name",
                    "Name of the child GameObject used as the physical connector. " +
                    "Created automatically if absent."),
                mod.connectorChildName);

            using (new EditorGUI.DisabledScope(mod.targetModule != null))
            {
                mod.payloadChildName = EditorGUILayout.TextField(
                    new GUIContent("Payload Child Name",
                        "Name of the auto-created child representing the module itself. " +
                        "Ignored when Target Module is assigned above."),
                    mod.payloadChildName);
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(mod);
                EditorUtility.SetDirty(mod.transform);
            }

            if (Application.isPlaying) Repaint();
        }

        private static string BuildStatusString(RailModule mod)
        {
            if (mod.rail == null)
                return "⚠  No Rail assigned. Drag a RailManager here.";

            string railName = mod.rail.gameObject.name;

            if (!mod.onRail)
                return $"Rail: {railName}\n" +
                       $"Status: FREE (not on rail)\n" +
                       $"Connector height: {mod.moduleHeight:F3} u  " +
                       (mod.pinnedModuleHeight ? "[FROZEN]" : "[preserved, not editable off rail]");

            string posStatus = mod.pinnedRailPosition
                ? $"Rail position: {mod.railPosition:F3} u  [PINNED]"
                : $"Rail position: {mod.railPosition:F3} u  [sliding]";

            string hStatus = mod.pinnedModuleHeight
                ? $"Height: {mod.moduleHeight:F3} u  [FROZEN]"
                : $"Height: {mod.moduleHeight:F3} u  [editable]";

            return $"Rail: {railName}\nStatus: ON RAIL\n{posStatus}\n{hStatus}";
        }

        private void OnSceneGUI()
        {
            RailModule mod = (RailModule)target;
            if (mod.rail == null || !mod.onRail) return;

            // Show a handle to drag along the rail axis when not pinned
            if (!mod.pinnedRailPosition)
            {
                EditorGUI.BeginChangeCheck();

                float   handleSize = HandleUtility.GetHandleSize(mod.transform.position) * 0.12f;
                Handles.color = new Color(1f, 0.75f, 0.2f, 0.9f);

                Vector3 newPos = Handles.FreeMoveHandle(
                    mod.transform.position, handleSize,
                    Vector3.zero, Handles.SphereHandleCap);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(mod.transform, "Slide Rail Module");
                    // Only allow movement along the rail axis
                    float t = mod.rail.ProjectOntoRail(newPos, mod.rail.clampToBounds);
                    Vector3 onAxis = mod.rail.PointOnRail(t);
                    Vector3 p = mod.transform.position;
                    Vector3 axis = mod.rail.Axis;
                    if (Mathf.Abs(Vector3.Dot(axis, Vector3.right))   > 0.9f) p.x = onAxis.x;
                    if (Mathf.Abs(Vector3.Dot(axis, Vector3.forward)) > 0.9f) p.z = onAxis.z;
                    mod.transform.position = p;
                }
            }

            // Height handle — drag the TOP point (the module itself) upward/downward.
            // The rail attachment point (mod.transform) is the fixed BOTTOM and never moves;
            // dragging this handle only changes ModuleHeight.
            if (!mod.pinnedModuleHeight)
            {
                Vector3 bottom = mod.transform.position;                 // fixed, on the rail
                Vector3 top    = bottom + Vector3.up * Mathf.Max(0f, mod.moduleHeight);

                EditorGUI.BeginChangeCheck();

                float   hs = HandleUtility.GetHandleSize(top) * 0.1f;
                Handles.color = new Color(0.8f, 0.3f, 1f, 0.9f);

                Vector3 newTop = Handles.FreeMoveHandle(
                    top, hs, Vector3.zero, Handles.SphereHandleCap);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(mod, "Adjust Module Height");
                    float newH = Vector3.Dot(newTop - bottom, Vector3.up);
                    mod.moduleHeight = Mathf.Max(0f, newH);
                }

                // Draw the connector line in the scene
                Handles.color = new Color(0.8f, 0.3f, 1f, 0.5f);
                Handles.DrawLine(bottom, top);
                Handles.Label(Vector3.Lerp(bottom, top, 0.5f) + Vector3.right * 0.15f,
                    $"{mod.moduleHeight:F2} u", EditorStyles.miniLabel);
            }
        }
    }
}
#endif