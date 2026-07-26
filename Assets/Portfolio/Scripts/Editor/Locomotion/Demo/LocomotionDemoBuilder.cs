#if UNITY_EDITOR
using System;
using System.IO;
using Portfolio.Game;
using Portfolio.Input;
using Portfolio.Locomotion.Input;
using Portfolio.Locomotion.Rigidbody;
using Portfolio.Locomotion.Spline;
using Portfolio.Locomotion.Spline.Path;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;

namespace Portfolio.Locomotion.Demo.Editor
{
    public static class LocomotionDemoBuilder
    {
        // Fields
        private const string CreateAllMenuPath = "Tools/Portfolio/Locomotion Demos/Create Both Demos";
        private const string CreateRigidbodyMenuPath = "Tools/Portfolio/Locomotion Demos/Create Rigidbody2D Demo";
        private const string CreateSimulatedMenuPath = "Tools/Portfolio/Locomotion Demos/Create Simulated Spline Demo";

        private const string RigidbodyPrefabPath = "Assets/Portfolio/Prefabs/Locomotion/RigidbodyPlayerCapsule.prefab";
        private const string SimulatedPrefabPath = "Assets/Portfolio/Prefabs/Locomotion/SimulatedPlayerCapsule.prefab";
        private const string RigidbodyScenePath = "Assets/Portfolio/Scenes/RigidbodyLocomotionDemo.unity";
        private const string SimulatedScenePath = "Assets/Portfolio/Scenes/SimulatedSplineLocomotionDemo.unity";
        private const string InputPath = "Assets/Portfolio/Input/PortfolioInputActions.inputactions";
        private const string CapsuleSpritePath = "Assets/Portfolio/Art/Generated/PlaceholderCapsule.png";
        private const string GroundMaterialPath = "Assets/Portfolio/Materials/LocomotionDemoUnlit.mat";
        private const string PhysicsMaterialPath = "Assets/Portfolio/Materials/LocomotionDemoPhysics.physicsMaterial2D";

        private static readonly Vector3[] GroundPoints =
        {
            new Vector3(-12f, -2.4f, 0f),
            new Vector3(-8f, -2.4f, 0f),
            new Vector3(-4.2f, -1.1f, 0f),
            new Vector3(-1.4f, 3.8f, 0f),
            new Vector3(1.3f, 4.2f, 0f),
            new Vector3(4.2f, 0.4f, 0f),
            new Vector3(7.2f, -2.1f, 0f),
            new Vector3(11.5f, -2.1f, 0f)
        };

        // Public Methods
        [MenuItem(CreateAllMenuPath)]
        public static void CreateAllDemos()
        {
            if (!CanBuild())
            {
                return;
            }

            PrepareAssets(out Sprite capsuleSprite, out Material unlitMaterial, out PhysicsMaterial2D physicsMaterial);
            GameObject rigidbodyPrefab = CreateRigidbodyPlayerPrefab(capsuleSprite, unlitMaterial, physicsMaterial);
            GameObject simulatedPrefab = CreateSimulatedPlayerPrefab(capsuleSprite, unlitMaterial);
            CreateRigidbodyScene(rigidbodyPrefab, unlitMaterial, physicsMaterial);
            CreateSimulatedScene(simulatedPrefab, unlitMaterial, physicsMaterial);
            FinishBuild(SimulatedScenePath);
        }

        [MenuItem(CreateRigidbodyMenuPath)]
        public static void CreateRigidbodyDemo()
        {
            if (!CanBuild())
            {
                return;
            }

            PrepareAssets(out Sprite capsuleSprite, out Material unlitMaterial, out PhysicsMaterial2D physicsMaterial);
            GameObject prefab = CreateRigidbodyPlayerPrefab(capsuleSprite, unlitMaterial, physicsMaterial);
            CreateRigidbodyScene(prefab, unlitMaterial, physicsMaterial);
            FinishBuild(RigidbodyScenePath);
        }

        [MenuItem(CreateSimulatedMenuPath)]
        public static void CreateSimulatedDemo()
        {
            if (!CanBuild())
            {
                return;
            }

            PrepareAssets(out Sprite capsuleSprite, out Material unlitMaterial, out PhysicsMaterial2D physicsMaterial);
            GameObject prefab = CreateSimulatedPlayerPrefab(capsuleSprite, unlitMaterial);
            CreateSimulatedScene(prefab, unlitMaterial, physicsMaterial);
            FinishBuild(SimulatedScenePath);
        }

        // Private Methods
        private static bool CanBuild()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode &&
                   EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        private static void PrepareAssets(out Sprite capsuleSprite, out Material unlitMaterial, out PhysicsMaterial2D physicsMaterial)
        {
            EnsureFolder("Assets/Portfolio/Art");
            EnsureFolder("Assets/Portfolio/Art/Generated");
            EnsureFolder("Assets/Portfolio/Materials");
            EnsureFolder("Assets/Portfolio/Prefabs");
            EnsureFolder("Assets/Portfolio/Prefabs/Locomotion");
            EnsureFolder("Assets/Portfolio/Scenes");

            capsuleSprite = CreateCapsuleSprite();

            if (capsuleSprite == null)
            {
                throw new InvalidOperationException($"The placeholder sprite could not be created at {CapsuleSpritePath}.");
            }

            unlitMaterial = CreateUnlitMaterial();
            physicsMaterial = CreatePhysicsMaterial();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = Path.GetFileName(path);

            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static Sprite CreateCapsuleSprite()
        {
            const int width = 64;
            const int height = 112;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[width * height];
            Color32 visible = new Color32(255, 255, 255, 255);
            Color32 clear = new Color32(255, 255, 255, 0);
            float radius = width * 0.45f;
            float centerX = (width - 1) * 0.5f;
            float lowerCenter = radius;
            float upperCenter = height - 1 - radius;

            for (int y = 0; y < height; y++)
            {
                float nearestCenterY = Mathf.Clamp(y, lowerCenter, upperCenter);

                for (int x = 0; x < width; x++)
                {
                    float xDistance = x - centerX;
                    float yDistance = y - nearestCenterY;
                    bool inside = xDistance * xDistance + yDistance * yDistance <= radius * radius;
                    pixels[y * width + x] = inside ? visible : clear;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(CapsuleSpritePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(CapsuleSpritePath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(CapsuleSpritePath) as TextureImporter;

            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 64f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(CapsuleSpritePath);
        }

        private static Material CreateUnlitMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);

            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            if (shader == null)
            {
                throw new InvalidOperationException("A compatible unlit sprite shader could not be found.");
            }

            material = new Material(shader)
            {
                name = "Locomotion Demo Unlit"
            };
            AssetDatabase.CreateAsset(material, GroundMaterialPath);
            return material;
        }

        private static PhysicsMaterial2D CreatePhysicsMaterial()
        {
            PhysicsMaterial2D material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(PhysicsMaterialPath);

            if (material == null)
            {
                material = new PhysicsMaterial2D("Locomotion Demo Physics");
                AssetDatabase.CreateAsset(material, PhysicsMaterialPath);
            }

            material.friction = 0f;
            material.bounciness = 0f;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreateRigidbodyPlayerPrefab(Sprite sprite, Material material, PhysicsMaterial2D physicsMaterial)
        {
            GameObject player = new GameObject("RigidbodyPlayerCapsule");
            SpriteRenderer spriteRenderer = player.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sharedMaterial = material;
            spriteRenderer.color = new Color(0.2f, 0.65f, 1f, 1f);
            spriteRenderer.sortingOrder = 2;

            UnityEngine.Rigidbody2D body = player.AddComponent<UnityEngine.Rigidbody2D>();
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CapsuleCollider2D capsuleCollider = player.AddComponent<CapsuleCollider2D>();
            capsuleCollider.size = new Vector2(0.84f, 1.68f);
            capsuleCollider.sharedMaterial = physicsMaterial;

            RigidbodyLocomotionController controller = player.AddComponent<RigidbodyLocomotionController>();
            RigidbodyLocomotionInputAdapter adapter = player.AddComponent<RigidbodyLocomotionInputAdapter>();
            SetObjectReference(adapter, "_controller", controller);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, RigidbodyPrefabPath);
            UnityEngine.Object.DestroyImmediate(player);
            return prefab;
        }

        private static GameObject CreateSimulatedPlayerPrefab(Sprite sprite, Material material)
        {
            GameObject player = new GameObject("SimulatedPlayerCapsule");
            SimulatedSplineLocomotionController controller = player.AddComponent<SimulatedSplineLocomotionController>();
            SimulatedSplineMovement movement = player.AddComponent<SimulatedSplineMovement>();
            SimulatedSplineJump jump = player.AddComponent<SimulatedSplineJump>();
            SplineGroundProbe2D groundProbe = player.AddComponent<SplineGroundProbe2D>();
            SplineGroundConstraint2D constraint = player.AddComponent<SplineGroundConstraint2D>();
            SplineLocomotionInputAdapter adapter = player.AddComponent<SplineLocomotionInputAdapter>();

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.84f, 0f);
            SpriteRenderer spriteRenderer = visual.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sharedMaterial = material;
            spriteRenderer.color = new Color(1f, 0.55f, 0.2f, 1f);
            spriteRenderer.sortingOrder = 2;

            SetObjectReference(controller, "_visualRoot", visual.transform);
            SetObjectReference(controller, "_movement", movement);
            SetObjectReference(controller, "_jump", jump);
            SetObjectReference(controller, "_groundConstraint", constraint);
            SetObjectReference(constraint, "_groundProbe", groundProbe);
            SetObjectReference(adapter, "_controller", controller);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, SimulatedPrefabPath);
            UnityEngine.Object.DestroyImmediate(player);
            return prefab;
        }

        private static void CreateRigidbodyScene(GameObject prefab, Material material, PhysicsMaterial2D physicsMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameInput gameInput = CreateSystems();
            SpriteShapeSplinePath2D path = CreateGround(material, physicsMaterial, out _);
            GameObject playerRoot = new GameObject("Player");
            GameObject player = InstantiatePrefab(prefab, scene, playerRoot.transform);
            SplinePathSample2D spawnSample = path.Evaluate(path.TotalLength * 0.12f);
            player.transform.position = spawnSample.Position + Vector2.up * 1.05f;

            RigidbodyLocomotionInputAdapter adapter = player.GetComponent<RigidbodyLocomotionInputAdapter>();
            SetObjectReference(adapter, "_gameInput", gameInput);
            CreateCamera();
            EditorSceneManager.SaveScene(scene, RigidbodyScenePath);
        }

        private static void CreateSimulatedScene(GameObject prefab, Material material, PhysicsMaterial2D physicsMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameInput gameInput = CreateSystems();
            SpriteShapeSplinePath2D path = CreateGround(material, physicsMaterial, out EdgeCollider2D groundCollider);
            GameObject playerRoot = new GameObject("Player");
            GameObject player = InstantiatePrefab(prefab, scene, playerRoot.transform);
            SplinePathSample2D spawnSample = path.Evaluate(path.TotalLength * 0.12f);
            player.transform.position = spawnSample.Position;

            SplineGroundProbe2D groundProbe = player.GetComponent<SplineGroundProbe2D>();
            SplineGroundConstraint2D constraint = player.GetComponent<SplineGroundConstraint2D>();
            SplineLocomotionInputAdapter adapter = player.GetComponent<SplineLocomotionInputAdapter>();
            SetObjectReference(groundProbe, "_groundCollider", groundCollider);
            SetLayerMask(groundProbe, "_groundLayers", 1 << groundCollider.gameObject.layer);
            SetObjectReference(constraint, "_path", path);
            SetObjectReference(adapter, "_gameInput", gameInput);
            CreateCamera();
            EditorSceneManager.SaveScene(scene, SimulatedScenePath);
        }

        private static GameInput CreateSystems()
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);

            if (inputActions == null)
            {
                throw new InvalidOperationException($"Input actions were not found at {InputPath}.");
            }

            GameObject systems = new GameObject("Systems");
            GameObject gameManagerObject = new GameObject("GameManager");
            gameManagerObject.transform.SetParent(systems.transform, false);
            gameManagerObject.AddComponent<GameManager>();

            GameObject gameInputObject = new GameObject("GameInput");
            gameInputObject.transform.SetParent(systems.transform, false);
            GameInput gameInput = gameInputObject.AddComponent<GameInput>();
            SetObjectReference(gameInput, "_inputActions", inputActions);
            return gameInput;
        }

        private static SpriteShapeSplinePath2D CreateGround(Material material, PhysicsMaterial2D physicsMaterial, out EdgeCollider2D edgeCollider)
        {
            GameObject environment = new GameObject("Environment");
            GameObject ground = new GameObject("SpriteShape Ground");
            ground.transform.SetParent(environment.transform, false);
            SpriteShapeController spriteShapeController = ground.AddComponent<SpriteShapeController>();
            edgeCollider = ground.AddComponent<EdgeCollider2D>();
            edgeCollider.sharedMaterial = physicsMaterial;
            edgeCollider.edgeRadius = 0.03f;
            spriteShapeController.autoUpdateCollider = false;
            ConfigureSpline(spriteShapeController.spline);

            SpriteShapeSplinePath2D path = ground.AddComponent<SpriteShapeSplinePath2D>();
            SetObjectReference(path, "_spriteShapeController", spriteShapeController);

            SpriteShapeGroundCollider2D colliderDriver = ground.AddComponent<SpriteShapeGroundCollider2D>();
            SetObjectReference(colliderDriver, "_path", path);
            SetObjectReference(colliderDriver, "_edgeCollider", edgeCollider);

            LineRenderer lineRenderer = ground.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.sharedMaterial = material;
            lineRenderer.widthMultiplier = 0.18f;
            lineRenderer.numCapVertices = 4;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.startColor = new Color(0.35f, 0.9f, 0.55f, 1f);
            lineRenderer.endColor = new Color(0.15f, 0.55f, 0.35f, 1f);
            lineRenderer.sortingOrder = 0;

            SplinePathLineRenderer2D lineDriver = ground.AddComponent<SplinePathLineRenderer2D>();
            SetObjectReference(lineDriver, "_path", path);
            SetObjectReference(lineDriver, "_lineRenderer", lineRenderer);

            spriteShapeController.RefreshSpriteShape();
            path.InvalidateCache();
            colliderDriver.RefreshCollider();
            lineDriver.RefreshLine();
            return path;
        }

        private static void ConfigureSpline(Spline spline)
        {
            while (spline.GetPointCount() > 0)
            {
                spline.RemovePointAt(0);
            }

            for (int i = 0; i < GroundPoints.Length; i++)
            {
                spline.InsertPointAt(i, GroundPoints[i]);
                spline.SetTangentMode(i, ShapeTangentMode.Continuous);
                spline.SetLeftTangent(i, Vector3.left * GetTangentLength(i));
                spline.SetRightTangent(i, Vector3.right * GetTangentLength(i));
            }

            spline.isOpenEnded = true;
        }

        private static float GetTangentLength(int pointIndex)
        {
            if (pointIndex == 2 || pointIndex == 3 || pointIndex == 4 || pointIndex == 5)
            {
                return 0.85f;
            }

            return 1.2f;
        }

        private static GameObject InstantiatePrefab(GameObject prefab, Scene scene, Transform parent)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;

            if (instance == null)
            {
                throw new InvalidOperationException($"Could not instantiate {prefab.name}.");
            }

            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 7.2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.075f, 0.11f, 1f);
            cameraObject.transform.position = new Vector3(0f, 0.5f, -10f);
        }

        private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);

            if (property == null)
            {
                throw new InvalidOperationException($"{target.GetType().Name} is missing serialized property {propertyName}.");
            }

            property.objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerMask(UnityEngine.Object target, string propertyName, int value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(propertyName);

            if (property == null)
            {
                throw new InvalidOperationException($"{target.GetType().Name} is missing serialized property {propertyName}.");
            }

            property.intValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void FinishBuild(string scenePath)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            Selection.activeObject = sceneAsset;
            EditorGUIUtility.PingObject(sceneAsset);
            Debug.Log($"Locomotion demo created at {scenePath}.");
        }
    }
}
#endif
