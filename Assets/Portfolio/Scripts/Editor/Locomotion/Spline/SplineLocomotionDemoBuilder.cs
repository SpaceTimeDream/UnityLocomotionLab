#if UNITY_EDITOR
using Portfolio.Game;
using Portfolio.Input;
using Portfolio.Locomotion.Spline;
using Portfolio.Locomotion.Spline.Path;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;

namespace Portfolio.Locomotion.Spline.Editor
{
    public static class SplineLocomotionDemoBuilder
    {
        // Fields
        private const string MenuPath = "Tools/Portfolio/Create Spline Locomotion Demo";
        private const string PrefabPath = "Assets/Portfolio/Prefabs/Locomotion/SplinePlayerCapsule.prefab";
        private const string ScenePath = "Assets/Portfolio/Scenes/SplineLocomotionDemo.unity";
        private const string InputPath = "Assets/Portfolio/Input/PortfolioInputActions.inputactions";

        // Public Methods
        [MenuItem(MenuPath)]
        public static void CreateDemo()
        {
            EnsureFolders();
            GameObject prefab = CreatePlayerPrefab();
            CreateScene(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // Private Methods
        private static void EnsureFolders()
        {
            CreateFolder("Assets/Portfolio/Prefabs");
            CreateFolder("Assets/Portfolio/Prefabs/Locomotion");
        }

        private static void CreateFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
            string folder = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folder);
        }

        private static GameObject CreatePlayerPrefab()
        {
            GameObject player = new GameObject("SplinePlayerCapsule");
            SimulatedSplineLocomotionController controller = player.AddComponent<SimulatedSplineLocomotionController>();
            player.AddComponent<SimulatedSplineMovement>();
            player.AddComponent<SimulatedSplineJump>();
            player.AddComponent<SplineGroundConstraint2D>();
            player.AddComponent<SplineLocomotionInputAdapter>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            LineRenderer renderer = visual.AddComponent<LineRenderer>();
            renderer.useWorldSpace = false;
            renderer.loop = true;
            renderer.widthMultiplier = 0.08f;
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.startColor = new Color(0.2f, 0.45f, 0.9f, 1f);
            renderer.endColor = renderer.startColor;
            ConfigureCapsule(renderer);

            SerializedObject controllerObject = new SerializedObject(controller);
            controllerObject.FindProperty("_visualRoot").objectReferenceValue = visual.transform;
            controllerObject.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PrefabPath);
            Object.DestroyImmediate(player);
            return prefab;
        }

        private static void ConfigureCapsule(LineRenderer renderer)
        {
            const int count = 24;
            renderer.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float angle = i / (float)count * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * 0.35f;
                float y = Mathf.Sin(angle) * 0.35f + (Mathf.Sin(angle) > 0f ? 0.45f : -0.45f);
                renderer.SetPosition(i, new Vector3(x, y, 0f));
            }
        }

        private static void CreateScene(GameObject prefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject systems = new GameObject("Systems");
            GameObject gameManager = new GameObject("GameManager");
            gameManager.transform.SetParent(systems.transform);
            gameManager.AddComponent<GameManager>();

            GameObject gameInput = new GameObject("GameInput");
            gameInput.transform.SetParent(systems.transform);
            GameInput input = gameInput.AddComponent<GameInput>();
            SerializedObject inputObject = new SerializedObject(input);
            inputObject.FindProperty("_inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            inputObject.ApplyModifiedPropertiesWithoutUndo();

            GameObject environment = new GameObject("Environment");
            GameObject ground = new GameObject("GroundPath");
            ground.transform.SetParent(environment.transform);
            SpriteShapeController spriteShape = ground.AddComponent<SpriteShapeController>();
            ConfigureSpline(spriteShape.spline);
            SpriteShapeSplinePath2D path = ground.AddComponent<SpriteShapeSplinePath2D>();
            LineRenderer line = ground.AddComponent<LineRenderer>();
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.widthMultiplier = 0.12f;
            line.startColor = new Color(0.35f, 0.35f, 0.35f, 1f);
            line.endColor = line.startColor;
            ground.AddComponent<SplinePathLineRenderer2D>();
            SerializedObject pathObject = new SerializedObject(path);
            pathObject.FindProperty("_spriteShapeController").objectReferenceValue = spriteShape;
            pathObject.ApplyModifiedPropertiesWithoutUndo();

            GameObject playerRoot = new GameObject("Player");
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            player.name = "SplinePlayerCapsule";
            player.transform.SetParent(playerRoot.transform);
            player.transform.position = new Vector3(-5.5f, 0.2f, 0f);
            AssignPlayerReferences(player, path, input);

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 1f, -10f);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void ConfigureSpline(UnityEngine.U2D.Spline spline)
        {
            while (spline.GetPointCount() > 0)
            {
                spline.RemovePointAt(0);
            }

            Vector3[] points = { new Vector3(-8f, 0f), new Vector3(-5f, 0f), new Vector3(-2f, 1f), new Vector3(0.3f, 3.6f), new Vector3(2.2f, 2.6f), new Vector3(4.6f, -0.9f), new Vector3(7.5f, 0f) };
            for (int i = 0; i < points.Length; i++)
            {
                spline.InsertPointAt(i, points[i]);
                spline.SetTangentMode(i, ShapeTangentMode.Continuous);
                spline.SetLeftTangent(i, Vector3.left * 0.8f);
                spline.SetRightTangent(i, Vector3.right * 0.8f);
            }

            spline.isOpenEnded = true;
        }

        private static void AssignPlayerReferences(GameObject player, SpriteShapeSplinePath2D path, GameInput input)
        {
            SerializedObject constraintObject = new SerializedObject(player.GetComponent<SplineGroundConstraint2D>());
            constraintObject.FindProperty("_path").objectReferenceValue = path;
            constraintObject.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject adapterObject = new SerializedObject(player.GetComponent<SplineLocomotionInputAdapter>());
            adapterObject.FindProperty("_gameInput").objectReferenceValue = input;
            adapterObject.FindProperty("_controller").objectReferenceValue = player.GetComponent<SimulatedSplineLocomotionController>();
            adapterObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
