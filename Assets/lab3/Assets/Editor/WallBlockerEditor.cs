#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using RayOptics;

namespace RayOpticsEditor
{
    /// <summary>
    /// Scene-view handles for <see cref="WallBlocker"/>.
    /// Drag the four corner spheres to resize the wall; the transform pivot stays fixed.
    /// </summary>
    [CustomEditor(typeof(WallBlocker))]
    [CanEditMultipleObjects]
    public class WallBlockerEditor : Editor
    {
        private void OnSceneGUI()
        {
            WallBlocker wall = (WallBlocker)target;

            EditorGUI.BeginChangeCheck();

            float handleSize = HandleUtility.GetHandleSize(wall.transform.position) * 0.1f;
            Handles.color = new Color(1f, 0.65f, 0f);

            // Drag each corner
            Vector3 newTL = Handles.FreeMoveHandle(wall.CornerTL, handleSize, Vector3.zero, Handles.SphereHandleCap);
            Vector3 newTR = Handles.FreeMoveHandle(wall.CornerTR, handleSize, Vector3.zero, Handles.SphereHandleCap);
            Vector3 newBR = Handles.FreeMoveHandle(wall.CornerBR, handleSize, Vector3.zero, Handles.SphereHandleCap);
            Vector3 newBL = Handles.FreeMoveHandle(wall.CornerBL, handleSize, Vector3.zero, Handles.SphereHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(wall, "Resize WallBlocker");

                // Recompute centre, width and height from the moved corners.
                // We use TL+BR and TR+BL diagonals to find centre; project
                // the half-diagonals onto the local axes for dimensions.
                Vector3 centre = (newTL + newTR + newBR + newBL) * 0.25f;

                // Keep the wall's current orientation — only change size.
                Vector3 right = wall.transform.right;
                Vector3 up    = wall.transform.up;

                // Average signed projections for half-sizes (robust to asymmetric drags)
                float hw = (Mathf.Abs(Vector3.Dot(newTR - centre, right))
                          + Mathf.Abs(Vector3.Dot(newTL - centre, right))
                          + Mathf.Abs(Vector3.Dot(newBR - centre, right))
                          + Mathf.Abs(Vector3.Dot(newBL - centre, right))) * 0.25f;

                float hh = (Mathf.Abs(Vector3.Dot(newTL - centre, up))
                          + Mathf.Abs(Vector3.Dot(newTR - centre, up))
                          + Mathf.Abs(Vector3.Dot(newBR - centre, up))
                          + Mathf.Abs(Vector3.Dot(newBL - centre, up))) * 0.25f;

                Undo.RecordObject(wall.transform, "Move WallBlocker");
                wall.transform.position = centre;
                wall.width  = Mathf.Max(0.05f, hw * 2f);
                wall.height = Mathf.Max(0.05f, hh * 2f);
            }

            // Draw filled quad handle in scene view
            Handles.color = new Color(wall.gizmoColor.r, wall.gizmoColor.g,
                                       wall.gizmoColor.b, 0.15f);
            Handles.DrawSolidRectangleWithOutline(
                new Vector3[] { wall.CornerBL, wall.CornerBR, wall.CornerTR, wall.CornerTL },
                new Color(wall.gizmoColor.r, wall.gizmoColor.g, wall.gizmoColor.b, 0.08f),
                new Color(wall.gizmoColor.r, wall.gizmoColor.g, wall.gizmoColor.b, 0.9f));
        }
    }
}
#endif
