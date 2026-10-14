#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using RayOptics;

namespace RayOpticsEditor
{
    /// <summary>
    /// Menu: Tools > Ray Optics > ...
    /// Creates sources, blockers, and lenses at the current scene view pivot.
    /// </summary>
    public static class OpticsSceneSetup
    {
        // ── Demo scene ─────────────────────────────────────────────────────────

        [MenuItem("Tools/Ray Optics/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            CreateManager();

            // Sources: all fire in +Z (transform.forward = +Z when rotation = identity)
            CreateSinglePoint(new Vector3(-7f,  0f, 0f), Quaternion.identity,  Color.yellow);
            CreatePointSource(new Vector3(-7f,  2f, 0f), Quaternion.identity,  Color.cyan,    30f);
            CreatePointSource(new Vector3(-7f, -2f, 0f), Quaternion.identity,  Color.green,  360f);
            CreateBeamCircle( new Vector3(-5f,  1f, 0f), Quaternion.identity,  Color.red,    0.5f);
            CreateBeamSquare( new Vector3(-5f, -1f, 0f), Quaternion.identity,  Color.magenta,0.5f);
            CreateBeamTriangle(new Vector3(-5f,  3f, 0f), Quaternion.identity, Color.green,   0.5f);
            CreateBeamTShape(  new Vector3(-5f, -3f, 0f), Quaternion.identity, new Color(1f, 0.5f, 0f), 0.5f);

            // Ideal converging lens in the path
            CreateIdealLens(new Vector3(0f, 0f, 0f), Quaternion.identity, 4f, 1.5f);

            // Biconvex spherical lens a little further on
            CreateSphericalLens(new Vector3(5f, 0f, 0f), Quaternion.identity,
                                 R1: 3f, R2: -3f, thickness: 0.4f, aperture: 1.2f, n: 1.52f);

            // Blocking wall beyond the lenses
            CreateWall(new Vector3(9f, 0f, 0f), Quaternion.identity, 3f, 4f);

            Debug.Log("[Ray Optics] Demo scene created. Press Play to simulate.");
        }

        // ── Individual menu items ──────────────────────────────────────────────

        [MenuItem("Tools/Ray Optics/Create/Optics Manager")]
        public static GameObject CreateManager()
        {
            var existing = Object.FindObjectOfType<OpticsManager>();
            if (existing != null)
            {
                Debug.LogWarning("[Ray Optics] An OpticsManager already exists.");
                return existing.gameObject;
            }
            var go  = new GameObject("OpticsManager");
            var mgr = go.AddComponent<OpticsManager>();
            mgr.lineMaterial = new Material(Shader.Find("Sprites/Default")) { name = "RayMaterial" };
            Undo.RegisterCreatedObjectUndo(go, "Create OpticsManager");
            return go;
        }

        [MenuItem("Tools/Ray Optics/Create/Single Point Source")]
        public static void MenuSinglePoint() =>
            Selection.activeGameObject = CreateSinglePoint(GetPivot(), Quaternion.identity, Color.yellow);

        [MenuItem("Tools/Ray Optics/Create/Point Source (Cone)")]
        public static void MenuPointSource() =>
            Selection.activeGameObject = CreatePointSource(GetPivot(), Quaternion.identity, Color.cyan, 60f);

        [MenuItem("Tools/Ray Optics/Create/Beam Source (Circle)")]
        public static void MenuBeamCircle() =>
            Selection.activeGameObject = CreateBeamCircle(GetPivot(), Quaternion.identity, Color.red, 0.5f);

        [MenuItem("Tools/Ray Optics/Create/Beam Source (Square)")]
        public static void MenuBeamSquare() =>
            Selection.activeGameObject = CreateBeamSquare(GetPivot(), Quaternion.identity, Color.magenta, 0.5f);

        [MenuItem("Tools/Ray Optics/Create/Beam Source (Triangle)")]
        public static void MenuBeamTriangle() =>
            Selection.activeGameObject = CreateBeamTriangle(GetPivot(), Quaternion.identity, Color.green, 0.5f);

        [MenuItem("Tools/Ray Optics/Create/Beam Source (T-Shape)")]
        public static void MenuBeamTShape() =>
            Selection.activeGameObject = CreateBeamTShape(GetPivot(), Quaternion.identity, new Color(1f, 0.5f, 0f), 0.5f);

        [MenuItem("Tools/Ray Optics/Create/Wall Blocker")]
        public static void MenuWall() =>
            Selection.activeGameObject = CreateWall(GetPivot(), Quaternion.identity, 2f, 2f);

        [MenuItem("Tools/Ray Optics/Create/Ideal Lens")]
        public static void MenuIdealLens() =>
            Selection.activeGameObject = CreateIdealLens(GetPivot(), Quaternion.identity, 3f, 1.2f);

        [MenuItem("Tools/Ray Optics/Create/Spherical Lens (Biconvex)")]
        public static void MenuBiconvex() =>
            Selection.activeGameObject = CreateSphericalLens(
                GetPivot(), Quaternion.identity, 3f, -3f, 0.4f, 1.0f, 1.52f);

        [MenuItem("Tools/Ray Optics/Create/Spherical Lens (Biconcave)")]
        public static void MenuBiconcave() =>
            Selection.activeGameObject = CreateSphericalLens(
                GetPivot(), Quaternion.identity, -3f, 3f, 0.4f, 1.0f, 1.52f);

        [MenuItem("Tools/Ray Optics/Create/Spherical Lens (Plano-Convex)")]
        public static void MenuPlanoConvex() =>
            Selection.activeGameObject = CreateSphericalLens(
                GetPivot(), Quaternion.identity, 0f, -3f, 0.3f, 1.0f, 1.52f);

        // ── Factories ──────────────────────────────────────────────────────────

        public static GameObject CreateSinglePoint(Vector3 pos, Quaternion rot, Color col)
        {
            var go = new GameObject("SinglePointSource");
            go.transform.SetPositionAndRotation(pos, rot);
            go.AddComponent<SinglePointSource>().rayColor = col;
            Undo.RegisterCreatedObjectUndo(go, "Create SinglePointSource");
            return go;
        }

        public static GameObject CreatePointSource(Vector3 pos, Quaternion rot, Color col, float angle)
        {
            var go  = new GameObject("PointSource");
            go.transform.SetPositionAndRotation(pos, rot);
            var src = go.AddComponent<PointSource>();
            src.rayColor      = col;
            src.emissionAngle = angle;
            Undo.RegisterCreatedObjectUndo(go, "Create PointSource");
            return go;
        }

        public static GameObject CreateBeamCircle(Vector3 pos, Quaternion rot, Color col, float size)
        {
            var go  = new GameObject("BeamSource_Circle");
            go.transform.SetPositionAndRotation(pos, rot);
            var src = go.AddComponent<BeamSource>();
            src.rayColor      = col;
            src.apertureShape = BeamSource.ApertureShape.Circle;
            src.apertureSize  = size;
            Undo.RegisterCreatedObjectUndo(go, "Create BeamSource Circle");
            return go;
        }

        public static GameObject CreateBeamSquare(Vector3 pos, Quaternion rot, Color col, float size)
        {
            var go  = new GameObject("BeamSource_Square");
            go.transform.SetPositionAndRotation(pos, rot);
            var src = go.AddComponent<BeamSource>();
            src.rayColor      = col;
            src.apertureShape = BeamSource.ApertureShape.Square;
            src.apertureSize  = size;
            Undo.RegisterCreatedObjectUndo(go, "Create BeamSource Square");
            return go;
        }

        public static GameObject CreateBeamTriangle(Vector3 pos, Quaternion rot, Color col, float size)
        {
            var go  = new GameObject("BeamSource_Triangle");
            go.transform.SetPositionAndRotation(pos, rot);
            var src = go.AddComponent<BeamSource>();
            src.rayColor      = col;
            src.apertureShape = BeamSource.ApertureShape.Triangle;
            src.apertureSize  = size;
            Undo.RegisterCreatedObjectUndo(go, "Create BeamSource Triangle");
            return go;
        }

        public static GameObject CreateBeamTShape(Vector3 pos, Quaternion rot, Color col, float size)
        {
            var go  = new GameObject("BeamSource_TShape");
            go.transform.SetPositionAndRotation(pos, rot);
            var src = go.AddComponent<BeamSource>();
            src.rayColor      = col;
            src.apertureShape = BeamSource.ApertureShape.TShape;
            src.apertureSize  = size;
            Undo.RegisterCreatedObjectUndo(go, "Create BeamSource TShape");
            return go;
        }

        public static GameObject CreateWall(Vector3 pos, Quaternion rot, float width, float height)
        {
            var go   = new GameObject("WallBlocker");
            go.transform.SetPositionAndRotation(pos, rot);
            var wall = go.AddComponent<WallBlocker>();
            wall.width  = width;
            wall.height = height;
            Undo.RegisterCreatedObjectUndo(go, "Create WallBlocker");
            return go;
        }

        public static GameObject CreateIdealLens(Vector3 pos, Quaternion rot,
                                                  float focalLength, float aperture)
        {
            var go   = new GameObject("IdealLens");
            go.transform.SetPositionAndRotation(pos, rot);
            var lens = go.AddComponent<IdealLens>();
            lens.focalLength    = focalLength;
            lens.apertureRadius = aperture;
            Undo.RegisterCreatedObjectUndo(go, "Create IdealLens");
            return go;
        }

        public static GameObject CreateSphericalLens(Vector3 pos, Quaternion rot,
                                                      float R1, float R2,
                                                      float thickness, float aperture, float n)
        {
            // Name reflects lens type
            bool flat1 = Mathf.Abs(R1) < 1e-4f;
            bool flat2 = Mathf.Abs(R2) < 1e-4f;
            string form = (flat1 || flat2) ? "PlanoLens"
                        : (R1 > 0f && R2 < 0f) ? "BiconvexLens"
                        : (R1 < 0f && R2 > 0f) ? "BiconcaveLens"
                        : "SphericalLens";

            var go   = new GameObject(form);
            go.transform.SetPositionAndRotation(pos, rot);
            var lens = go.AddComponent<SphericalLens>();
            lens.R1              = R1;
            lens.R2              = R2;
            lens.thickness       = thickness;
            lens.apertureRadius  = aperture;
            lens.refractiveIndex = n;
            Undo.RegisterCreatedObjectUndo(go, $"Create {form}");
            return go;
        }

        // ── Utility ────────────────────────────────────────────────────────────

        [MenuItem("Tools/Ray Optics/Create/Rail Manager")]
        public static void MenuRailManager() =>
            Selection.activeGameObject = CreateRailManager(GetPivot(), 10f, Vector3.right);

        [MenuItem("Tools/Ray Optics/Create/Rail Module")]
        public static void MenuRailModule() =>
            Selection.activeGameObject = CreateRailModule(GetPivot(), null);

        public static GameObject CreateRailManager(Vector3 pos, float length, Vector3 axis)
        {
            var go  = new GameObject("RailManager");
            go.transform.position = pos;
            var mgr = go.AddComponent<RailManager>();
            mgr.railLength = length;
            mgr.railAxis   = axis.normalized;
            Undo.RegisterCreatedObjectUndo(go, "Create RailManager");
            return go;
        }

        public static GameObject CreateRailModule(Vector3 pos, RailManager rail)
        {
            var go  = new GameObject("RailModule");
            go.transform.position = pos;
            var mod = go.AddComponent<RailModule>();
            mod.rail         = rail;
            mod.moduleHeight = 1f;
            Undo.RegisterCreatedObjectUndo(go, "Create RailModule");
            return go;
        }

        private static Vector3 GetPivot()
        {
            var sv = SceneView.lastActiveSceneView;
            return sv != null ? sv.pivot : Vector3.zero;
        }
    }
}
#endif
