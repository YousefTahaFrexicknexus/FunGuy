using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Funguy.IdkPlatformer.Editor
{
    public static class IdkPlatformerSceneBootstrapper
    {
        private const string ModuleRoot = "Assets/_Game/Funguy.IdkPlatformer";
        private const string MaterialsPath = ModuleRoot + "/Materials";
        private const string PrefabsPath = ModuleRoot + "/Prefabs";
        private const string ConfigPath = ModuleRoot + "/ScriptableObjects/Config";
        private const string ScenePath = ModuleRoot + "/Scenes/IdkPlatformerGameplay.unity";

        private static readonly Vector3[] MushroomPositions =
        {
            new(0f, 0f, 0f),
            new(2.5f, 0.65f, 6f),
            new(-2.5f, 1.1f, 12f),
            new(0f, 1.75f, 18f),
            new(2.7f, 1.35f, 24f),
            new(-2.6f, 1.95f, 30f),
            new(0f, 2.35f, 36f),
            new(2.4f, 1.65f, 42f),
            new(-2.25f, 1f, 48f),
            new(0f, 0.45f, 54f)
        };

        [MenuItem("Funguy/IdkPlatformer/Create Fresh Gameplay Scene")]
        public static void CreateFreshGameplayScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            CreateSceneAssets();
        }

        private static void CreateSceneAssets()
        {
            EnsureFolders();

            Material playerMaterial = CreateMaterial($"{MaterialsPath}/Idk_Player.mat", new Color(0.18f, 0.74f, 0.70f));
            Material mushroomCapMaterial = CreateMaterial($"{MaterialsPath}/Idk_MushroomCap.mat", new Color(0.93f, 0.39f, 0.31f));
            Material mushroomStemMaterial = CreateMaterial($"{MaterialsPath}/Idk_MushroomStem.mat", new Color(0.96f, 0.91f, 0.78f));
            Material groundMaterial = CreateMaterial($"{MaterialsPath}/Idk_Ground.mat", new Color(0.29f, 0.38f, 0.27f));

            MovementTuningProfile tuningProfile = CreateOrUpdateTuningProfile($"{ConfigPath}/MovementTuningProfile.asset");
            MushroomBounceProfile bounceProfile = CreateOrUpdateBounceProfile($"{ConfigPath}/StandardMushroomBounceProfile.asset");

            GameObject playerPrefab = CreatePlayerPrefab($"{PrefabsPath}/IdkPlayer.prefab", playerMaterial);
            GameObject mushroomPrefab = CreateMushroomPrefab($"{PrefabsPath}/IdkMushroom.prefab", mushroomCapMaterial, mushroomStemMaterial, bounceProfile);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildScene(scene, tuningProfile, playerPrefab, mushroomPrefab, groundMaterial);
            EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[IdkPlatformerSceneBootstrapper] Created scene at {ScenePath}");
        }

        private static void BuildScene(
            Scene scene,
            MovementTuningProfile tuningProfile,
            GameObject playerPrefab,
            GameObject mushroomPrefab,
            Material groundMaterial)
        {
            GameObject systemsRoot = new("_Systems");
            SceneManager.MoveGameObjectToScene(systemsRoot, scene);

            GameObject runtimeRoot = new("_Runtime");
            SceneManager.MoveGameObjectToScene(runtimeRoot, scene);

            GameObject presentationRoot = new("_Presentation");
            SceneManager.MoveGameObjectToScene(presentationRoot, scene);

            InputHandler inputHandler = new GameObject("InputHandler").AddComponent<InputHandler>();
            inputHandler.transform.SetParent(systemsRoot.transform);

            GameObject playerInstance = PrefabUtility.InstantiatePrefab(playerPrefab, scene) as GameObject;
            playerInstance.name = "IdkPlayer";
            playerInstance.transform.SetParent(runtimeRoot.transform);
            playerInstance.transform.position = new Vector3(0f, 1.55f, 0f);

            PlayerController playerController = playerInstance.GetComponent<PlayerController>();
            MovementMotor movementMotor = playerInstance.GetComponent<MovementMotor>();
            Rigidbody playerBody = playerInstance.GetComponent<Rigidbody>();

            SerializedObject motorSo = new(movementMotor);
            motorSo.FindProperty("body").objectReferenceValue = playerBody;
            motorSo.FindProperty("tuningProfile").objectReferenceValue = tuningProfile;
            motorSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject controllerSo = new(playerController);
            controllerSo.FindProperty("inputHandler").objectReferenceValue = inputHandler;
            controllerSo.FindProperty("movementMotor").objectReferenceValue = movementMotor;
            controllerSo.FindProperty("tuningProfile").objectReferenceValue = tuningProfile;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            Camera camera = CreateCamera(scene, presentationRoot.transform, playerInstance.transform);
            CreateLighting(scene, presentationRoot.transform);
            CreateBackdrop(scene, runtimeRoot.transform, groundMaterial);
            CreateSampleCourse(scene, runtimeRoot.transform, mushroomPrefab);
            CreateEventSystem(scene);
            CreateHud(scene, out FloatingJoystick joystick, out TouchDashButton dashButton);

            SerializedObject inputSo = new(inputHandler);
            inputSo.FindProperty("movementJoystick").objectReferenceValue = joystick;
            inputSo.FindProperty("dashButton").objectReferenceValue = dashButton;
            inputSo.FindProperty("movementCamera").objectReferenceValue = camera;
            inputSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static Camera CreateCamera(Scene scene, Transform parent, Transform followTarget)
        {
            GameObject cameraObject = new("Main Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);
            cameraObject.transform.SetParent(parent, false);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300f;
            camera.fieldOfView = 50f;
            cameraObject.AddComponent<AudioListener>();

            SimpleCameraFollow follow = cameraObject.AddComponent<SimpleCameraFollow>();
            follow.SetTarget(followTarget);

            cameraObject.transform.position = followTarget.position + new Vector3(0f, 4.5f, -9.5f);
            cameraObject.transform.rotation = Quaternion.LookRotation((followTarget.position + Vector3.up) - cameraObject.transform.position, Vector3.up);
            return camera;
        }

        private static void CreateLighting(Scene scene, Transform parent)
        {
            GameObject lightObject = new("Directional Light");
            SceneManager.MoveGameObjectToScene(lightObject, scene);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(34f, -28f, 0f);

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.color = new Color(1f, 0.96f, 0.9f);
        }

        private static void CreateBackdrop(Scene scene, Transform parent, Material groundMaterial)
        {
            GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Plane);
            SceneManager.MoveGameObjectToScene(backdrop, scene);
            backdrop.name = "BackdropFloor";
            backdrop.transform.SetParent(parent, false);
            backdrop.transform.position = new Vector3(0f, -6f, 26f);
            backdrop.transform.localScale = new Vector3(8f, 1f, 8f);
            ApplyMaterial(backdrop, groundMaterial);
            RemoveCollider(backdrop);
        }

        private static void CreateSampleCourse(Scene scene, Transform parent, GameObject mushroomPrefab)
        {
            GameObject courseRoot = new("Mushrooms");
            SceneManager.MoveGameObjectToScene(courseRoot, scene);
            courseRoot.transform.SetParent(parent, false);

            for (int index = 0; index < MushroomPositions.Length; index++)
            {
                GameObject mushroom = PrefabUtility.InstantiatePrefab(mushroomPrefab, scene) as GameObject;
                mushroom.name = $"Mushroom_{index + 1:D2}";
                mushroom.transform.SetParent(courseRoot.transform);
                mushroom.transform.position = MushroomPositions[index];
                mushroom.transform.rotation = Quaternion.identity;
            }
        }

        private static void CreateEventSystem(Scene scene)
        {
            GameObject eventSystemObject = new("EventSystem");
            SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
            eventSystemObject.AddComponent<EventSystem>();

            Type inputSystemUiModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemUiModuleType != null)
            {
                eventSystemObject.AddComponent(inputSystemUiModuleType);
            }
            else
            {
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }
        }

        private static void CreateHud(Scene scene, out FloatingJoystick joystick, out TouchDashButton dashButton)
        {
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject canvasObject = new("HUD");
            SceneManager.MoveGameObjectToScene(canvasObject, scene);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            GameObject joystickArea = new("JoystickArea", typeof(RectTransform), typeof(Image), typeof(FloatingJoystick));
            joystickArea.transform.SetParent(canvasObject.transform, false);

            RectTransform joystickAreaRect = joystickArea.GetComponent<RectTransform>();
            joystickAreaRect.anchorMin = new Vector2(0f, 0f);
            joystickAreaRect.anchorMax = new Vector2(0.55f, 1f);
            joystickAreaRect.offsetMin = Vector2.zero;
            joystickAreaRect.offsetMax = Vector2.zero;

            Image joystickAreaImage = joystickArea.GetComponent<Image>();
            joystickAreaImage.sprite = uiSprite;
            joystickAreaImage.type = Image.Type.Sliced;
            joystickAreaImage.color = new Color(1f, 1f, 1f, 0.001f);

            GameObject joystickVisual = new("JoystickVisual", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            joystickVisual.transform.SetParent(joystickArea.transform, false);

            RectTransform joystickVisualRect = joystickVisual.GetComponent<RectTransform>();
            joystickVisualRect.anchorMin = new Vector2(0f, 0f);
            joystickVisualRect.anchorMax = new Vector2(0f, 0f);
            joystickVisualRect.pivot = new Vector2(0.5f, 0.5f);
            joystickVisualRect.sizeDelta = new Vector2(220f, 220f);
            joystickVisualRect.anchoredPosition = new Vector2(190f, 210f);

            Image joystickVisualImage = joystickVisual.GetComponent<Image>();
            joystickVisualImage.sprite = uiSprite;
            joystickVisualImage.type = Image.Type.Sliced;
            joystickVisualImage.color = new Color(0.1f, 0.14f, 0.18f, 0.34f);

            CanvasGroup joystickCanvasGroup = joystickVisual.GetComponent<CanvasGroup>();

            GameObject handleObject = new("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(joystickVisual.transform, false);

            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(92f, 92f);
            handleRect.anchoredPosition = Vector2.zero;

            Image handleImage = handleObject.GetComponent<Image>();
            handleImage.sprite = uiSprite;
            handleImage.type = Image.Type.Sliced;
            handleImage.color = new Color(0.96f, 0.98f, 1f, 0.9f);

            joystick = joystickArea.GetComponent<FloatingJoystick>();
            SerializedObject joystickSo = new(joystick);
            joystickSo.FindProperty("joystickRoot").objectReferenceValue = joystickVisualRect;
            joystickSo.FindProperty("handle").objectReferenceValue = handleRect;
            joystickSo.FindProperty("movementArea").objectReferenceValue = joystickAreaRect;
            joystickSo.FindProperty("visuals").objectReferenceValue = joystickCanvasGroup;
            joystickSo.FindProperty("movementRadius").floatValue = 78f;
            joystickSo.FindProperty("repositionToPointer").boolValue = true;
            joystickSo.FindProperty("hideWhenInactive").boolValue = true;
            joystickSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject dashButtonObject = new("DashButton", typeof(RectTransform), typeof(Image), typeof(TouchDashButton));
            dashButtonObject.transform.SetParent(canvasObject.transform, false);

            RectTransform dashRect = dashButtonObject.GetComponent<RectTransform>();
            dashRect.anchorMin = new Vector2(1f, 0f);
            dashRect.anchorMax = new Vector2(1f, 0f);
            dashRect.pivot = new Vector2(0.5f, 0.5f);
            dashRect.sizeDelta = new Vector2(190f, 190f);
            dashRect.anchoredPosition = new Vector2(-170f, 210f);

            Image dashImage = dashButtonObject.GetComponent<Image>();
            dashImage.sprite = uiSprite;
            dashImage.type = Image.Type.Sliced;
            dashImage.color = new Color(0.92f, 0.37f, 0.29f, 0.9f);

            GameObject dashLabelObject = new("Label", typeof(RectTransform), typeof(Text));
            dashLabelObject.transform.SetParent(dashButtonObject.transform, false);

            RectTransform dashLabelRect = dashLabelObject.GetComponent<RectTransform>();
            dashLabelRect.anchorMin = Vector2.zero;
            dashLabelRect.anchorMax = Vector2.one;
            dashLabelRect.offsetMin = Vector2.zero;
            dashLabelRect.offsetMax = Vector2.zero;

            Text dashLabel = dashLabelObject.GetComponent<Text>();
            dashLabel.font = font;
            dashLabel.fontSize = 40;
            dashLabel.fontStyle = FontStyle.Bold;
            dashLabel.alignment = TextAnchor.MiddleCenter;
            dashLabel.color = Color.white;
            dashLabel.raycastTarget = false;
            dashLabel.text = "DASH";

            dashButton = dashButtonObject.GetComponent<TouchDashButton>();
        }

        private static Material CreateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static MovementTuningProfile CreateOrUpdateTuningProfile(string path)
        {
            MovementTuningProfile asset = LoadOrCreateAsset<MovementTuningProfile>(path);
            SerializedObject so = new(asset);
            so.FindProperty("moveAcceleration").floatValue = 22f;
            so.FindProperty("airControlStrength").floatValue = 1f;
            so.FindProperty("maxControllableSpeed").floatValue = 12f;
            so.FindProperty("maxSpeed").floatValue = 18f;
            so.FindProperty("overSpeedDrag").floatValue = 7.5f;
            so.FindProperty("airDrag").floatValue = 0.35f;
            so.FindProperty("gravityScale").floatValue = 1f;
            so.FindProperty("jumpGravityMultiplier").floatValue = 0.82f;
            so.FindProperty("fallGravityMultiplier").floatValue = 1.5f;
            so.FindProperty("baseJumpForce").floatValue = 9f;
            so.FindProperty("dashForce").floatValue = 7.5f;
            so.FindProperty("dashCooldown").floatValue = 0.16f;
            so.FindProperty("dashChargesPerBounce").intValue = 1;
            so.FindProperty("bounceGraceTime").floatValue = 0.1f;
            so.FindProperty("dashBufferTime").floatValue = 0.1f;
            so.FindProperty("minGroundDot").floatValue = 0.65f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static MushroomBounceProfile CreateOrUpdateBounceProfile(string path)
        {
            MushroomBounceProfile asset = LoadOrCreateAsset<MushroomBounceProfile>(path);
            SerializedObject so = new(asset);
            so.FindProperty("velocityScale").floatValue = 1f;
            so.FindProperty("directionalInfluence").floatValue = 0.45f;
            so.FindProperty("planarBoost").floatValue = 0.65f;
            so.FindProperty("useAbsoluteUpwardImpulse").boolValue = false;
            so.FindProperty("upwardImpulse").floatValue = 1f;
            so.FindProperty("impactRecoveryFactor").floatValue = 0.3f;
            so.FindProperty("localLaunchDirection").vector3Value = new Vector3(0f, 1f, 0.35f);
            so.FindProperty("upBlend").floatValue = 0.78f;
            so.FindProperty("overridePlanarDrag").boolValue = false;
            so.FindProperty("planarDragOverride").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static GameObject CreatePlayerPrefab(string path, Material playerMaterial)
        {
            GameObject root = new("IdkPlayer");
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotation;

            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.45f;

            MovementMotor movementMotor = root.AddComponent<MovementMotor>();
            PlayerController playerController = root.AddComponent<PlayerController>();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            ApplyMaterial(visual, playerMaterial);
            RemoveCollider(visual);

            SerializedObject motorSo = new(movementMotor);
            motorSo.FindProperty("body").objectReferenceValue = body;
            motorSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject controllerSo = new(playerController);
            controllerSo.FindProperty("movementMotor").objectReferenceValue = movementMotor;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, path);
        }

        private static GameObject CreateMushroomPrefab(
            string path,
            Material capMaterial,
            Material stemMaterial,
            MushroomBounceProfile bounceProfile)
        {
            GameObject root = new("IdkMushroom");
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.center = new Vector3(0f, 0.16f, 0f);
            collider.radius = 0.78f;

            Mushroom mushroom = root.AddComponent<Mushroom>();

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Cap";
            cap.transform.SetParent(root.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            cap.transform.localScale = new Vector3(1.35f, 0.58f, 1.35f);
            ApplyMaterial(cap, capMaterial);
            RemoveCollider(cap);

            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Stem";
            stem.transform.SetParent(root.transform, false);
            stem.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            stem.transform.localScale = new Vector3(0.24f, 0.58f, 0.24f);
            ApplyMaterial(stem, stemMaterial);
            RemoveCollider(stem);

            SerializedObject mushroomSo = new(mushroom);
            mushroomSo.FindProperty("bounceProfile").objectReferenceValue = bounceProfile;
            mushroomSo.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, path);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Game");
            EnsureFolder(ModuleRoot);
            EnsureFolder($"{ModuleRoot}/Editor");
            EnsureFolder($"{ModuleRoot}/Materials");
            EnsureFolder($"{ModuleRoot}/Prefabs");
            EnsureFolder($"{ModuleRoot}/Scenes");
            EnsureFolder($"{ModuleRoot}/ScriptableObjects");
            EnsureFolder($"{ModuleRoot}/ScriptableObjects/Config");
        }

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath))
            {
                return;
            }

            int slashIndex = assetPath.LastIndexOf('/');
            string parent = assetPath[..slashIndex];
            string folderName = assetPath[(slashIndex + 1)..];
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void RemoveCollider(GameObject gameObject)
        {
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }
        }

        private static void ApplyMaterial(GameObject gameObject, Material material)
        {
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }
        }
    }
}
