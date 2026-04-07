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
        private const string GenerationPath = ConfigPath + "/Generation";
        private const string SpawningPath = ConfigPath + "/Spawning";
        private const string DecorationsPath = ConfigPath + "/Decorations";
        private const string ThemesPath = ConfigPath + "/Themes";
        private const string PresetsPath = ConfigPath + "/Presets";
        private const string ScenePath = ModuleRoot + "/Scenes/IdkPlatformerGameplay.unity";

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
            Material dangerMaterial = CreateMaterial($"{MaterialsPath}/Idk_Danger.mat", new Color(0.75f, 0.17f, 0.15f));

            MovementTuningProfile tuningProfile = CreateOrUpdateTuningProfile($"{ConfigPath}/MovementTuningProfile.asset");
            CreateOrUpdatePresetProfiles();
            MushroomBounceProfile standardBounceProfile = CreateOrUpdateStandardBounceProfile($"{ConfigPath}/StandardMushroomBounceProfile.asset");
            MushroomBounceProfile boostBounceProfile = CreateOrUpdateBoostBounceProfile($"{ConfigPath}/BoostMushroomBounceProfile.asset");
            MushroomBounceProfile slowBounceProfile = CreateOrUpdateSlowBounceProfile($"{ConfigPath}/SlowMushroomBounceProfile.asset");

            GameObject playerPrefab = CreatePlayerPrefab($"{PrefabsPath}/IdkPlayer.prefab", playerMaterial);
            GameObject mushroomPrefab = CreateMushroomPrefab($"{PrefabsPath}/IdkMushroom.prefab", mushroomCapMaterial, mushroomStemMaterial, standardBounceProfile);

            BounceSpawnDefinition standardSpawnDefinition = CreateOrUpdateBounceSpawnDefinition(
                $"{SpawningPath}/StandardMushroomSpawn.asset",
                mushroomPrefab,
                standardBounceProfile,
                BounceSpawnTag.Normal,
                1f);
            BounceSpawnDefinition boostSpawnDefinition = CreateOrUpdateBounceSpawnDefinition(
                $"{SpawningPath}/BoostMushroomSpawn.asset",
                mushroomPrefab,
                boostBounceProfile,
                BounceSpawnTag.Boost,
                0.85f);
            BounceSpawnDefinition slowSpawnDefinition = CreateOrUpdateBounceSpawnDefinition(
                $"{SpawningPath}/SlowMushroomSpawn.asset",
                mushroomPrefab,
                slowBounceProfile,
                BounceSpawnTag.Slow,
                0.75f);

            EnvironmentDecorationDefinition fantasyBlock01 = CreateOrUpdateDecorationDefinition(
                $"{DecorationsPath}/FantasyForest_Block01.asset",
                "Assets/_Game/Prefab/Environment/Fantasy forrest/Block_1_1 - FantasyForrest.prefab",
                new Vector3(0f, -0.15f, 0f),
                32f,
                1f);
            EnvironmentDecorationDefinition fantasyBlock02 = CreateOrUpdateDecorationDefinition(
                $"{DecorationsPath}/FantasyForest_Block02.asset",
                "Assets/_Game/Prefab/Environment/Fantasy forrest/Block_1_2 - FantasyForrest 1.prefab",
                new Vector3(0f, -0.1f, 0f),
                32f,
                0.9f);
            EnvironmentDecorationDefinition fantasyBlock03 = CreateOrUpdateDecorationDefinition(
                $"{DecorationsPath}/FantasyForest_Block03.asset",
                "Assets/_Game/Prefab/Environment/Fantasy forrest/Block_1_3- FantasyForrest 2.prefab",
                new Vector3(0f, -0.1f, 0f),
                32f,
                0.85f);
            EnvironmentDecorationDefinition gloomyBlock01 = CreateOrUpdateDecorationDefinition(
                $"{DecorationsPath}/Gloomy_Block01.asset",
                "Assets/_Game/Prefab/Environment/Gloomy/Block_1_1.prefab",
                new Vector3(0f, -0.1f, 0f),
                32f,
                1f);
            EnvironmentDecorationDefinition gloomyBlock02 = CreateOrUpdateDecorationDefinition(
                $"{DecorationsPath}/Gloomy_Block02.asset",
                "Assets/_Game/Prefab/Environment/Gloomy/Block_1_2.prefab",
                new Vector3(0f, -0.1f, 0f),
                32f,
                0.95f);

            EnvironmentThemeTierDefinition fantasyTheme = CreateOrUpdateThemeTier(
                $"{ThemesPath}/FantasyForestTheme.asset",
                0,
                fantasyBlock01,
                fantasyBlock02,
                fantasyBlock03);
            EnvironmentThemeTierDefinition gloomyTheme = CreateOrUpdateThemeTier(
                $"{ThemesPath}/GloomyTheme.asset",
                300,
                gloomyBlock01,
                gloomyBlock02);

            BounceAreaGenerationProfile generationProfile = CreateOrUpdateGenerationProfile(
                $"{GenerationPath}/BounceAreaGenerationProfile.asset",
                standardSpawnDefinition,
                boostSpawnDefinition,
                slowSpawnDefinition,
                fantasyTheme,
                gloomyTheme);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildScene(scene, tuningProfile, generationProfile, standardSpawnDefinition, playerPrefab, groundMaterial, dangerMaterial);
            EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[IdkPlatformerSceneBootstrapper] Created scene at {ScenePath}");
        }

        private static void BuildScene(
            Scene scene,
            MovementTuningProfile tuningProfile,
            BounceAreaGenerationProfile generationProfile,
            BounceSpawnDefinition startSpawnDefinition,
            GameObject playerPrefab,
            Material groundMaterial,
            Material dangerMaterial)
        {
            GameObject systemsRoot = new("_Systems");
            SceneManager.MoveGameObjectToScene(systemsRoot, scene);

            GameObject runtimeRoot = new("_Runtime");
            SceneManager.MoveGameObjectToScene(runtimeRoot, scene);

            GameObject presentationRoot = new("_Presentation");
            SceneManager.MoveGameObjectToScene(presentationRoot, scene);

            GameObject generatedMushroomsRoot = new("GeneratedMushrooms");
            SceneManager.MoveGameObjectToScene(generatedMushroomsRoot, scene);
            generatedMushroomsRoot.transform.SetParent(runtimeRoot.transform, false);

            GameObject generatedEnvironmentRoot = new("GeneratedEnvironment");
            SceneManager.MoveGameObjectToScene(generatedEnvironmentRoot, scene);
            generatedEnvironmentRoot.transform.SetParent(runtimeRoot.transform, false);

            InputHandler inputHandler = new GameObject("InputHandler").AddComponent<InputHandler>();
            inputHandler.transform.SetParent(systemsRoot.transform);

            ForwardProgressScoreTracker scoreTracker = new GameObject("ForwardProgressScoreTracker").AddComponent<ForwardProgressScoreTracker>();
            scoreTracker.transform.SetParent(systemsRoot.transform);

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

            EndlessBounceAreaStreamer areaStreamer = new GameObject("EndlessBounceAreaStreamer").AddComponent<EndlessBounceAreaStreamer>();
            areaStreamer.transform.SetParent(systemsRoot.transform);

            RunResetCoordinator runResetCoordinator = new GameObject("RunResetCoordinator").AddComponent<RunResetCoordinator>();
            runResetCoordinator.transform.SetParent(systemsRoot.transform);

            SerializedObject scoreTrackerSo = new(scoreTracker);
            scoreTrackerSo.FindProperty("trackedTarget").objectReferenceValue = playerInstance.transform;
            scoreTrackerSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject streamerSo = new(areaStreamer);
            streamerSo.FindProperty("player").objectReferenceValue = playerInstance.transform;
            streamerSo.FindProperty("mushroomRoot").objectReferenceValue = generatedMushroomsRoot.transform;
            streamerSo.FindProperty("decorationRoot").objectReferenceValue = generatedEnvironmentRoot.transform;
            streamerSo.FindProperty("generationProfile").objectReferenceValue = generationProfile;
            streamerSo.FindProperty("tuningProfile").objectReferenceValue = tuningProfile;
            streamerSo.FindProperty("scoreTracker").objectReferenceValue = scoreTracker;
            streamerSo.FindProperty("startSpawnDefinition").objectReferenceValue = startSpawnDefinition;
            streamerSo.FindProperty("startMushroomPosition").vector3Value = Vector3.zero;
            streamerSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject resetCoordinatorSo = new(runResetCoordinator);
            resetCoordinatorSo.FindProperty("player").objectReferenceValue = playerController;
            resetCoordinatorSo.FindProperty("areaStreamer").objectReferenceValue = areaStreamer;
            resetCoordinatorSo.ApplyModifiedPropertiesWithoutUndo();

            Camera camera = CreateCamera(scene, presentationRoot.transform, playerInstance.transform);
            CreateLighting(scene, presentationRoot.transform);
            CreateBackdrop(scene, runtimeRoot.transform, groundMaterial);
            CreateInstantLosePlatform(scene, runtimeRoot.transform, playerInstance.transform, runResetCoordinator, dangerMaterial);
            CreateEventSystem(scene);
            CreateHud(scene, scoreTracker, out FloatingJoystick joystick, out TouchDashButton dashButton);

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

            cameraObject.transform.position = followTarget.position + new Vector3(0f, 6f, -10.5f);
            cameraObject.transform.rotation = Quaternion.LookRotation((followTarget.position + new Vector3(0f, 0.35f, 0f)) - cameraObject.transform.position, Vector3.up);
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

        private static void CreateInstantLosePlatform(
            Scene scene,
            Transform parent,
            Transform trackedTarget,
            RunResetCoordinator runResetCoordinator,
            Material dangerMaterial)
        {
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(platform, scene);
            platform.name = "InstantLosePlatform";
            platform.transform.SetParent(parent, false);
            platform.transform.position = new Vector3(0f, -22f, 10000f);
            platform.transform.localScale = new Vector3(256f, 20f, 22000f);
            ApplyMaterial(platform, dangerMaterial);

            BoxCollider collider = platform.GetComponent<BoxCollider>();
            collider.isTrigger = true;

            InstantLosePlatform losePlatform = platform.AddComponent<InstantLosePlatform>();
            SerializedObject losePlatformSo = new(losePlatform);
            losePlatformSo.FindProperty("resetCoordinator").objectReferenceValue = runResetCoordinator;
            losePlatformSo.ApplyModifiedPropertiesWithoutUndo();
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

        private static void CreateHud(Scene scene, ForwardProgressScoreTracker scoreTracker, out FloatingJoystick joystick, out TouchDashButton dashButton)
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

            GameObject scoreObject = new("ScoreText", typeof(RectTransform), typeof(Text), typeof(ForwardProgressScoreView));
            scoreObject.transform.SetParent(canvasObject.transform, false);

            RectTransform scoreRect = scoreObject.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(0.5f, 1f);
            scoreRect.anchorMax = new Vector2(0.5f, 1f);
            scoreRect.pivot = new Vector2(0.5f, 1f);
            scoreRect.sizeDelta = new Vector2(420f, 90f);
            scoreRect.anchoredPosition = new Vector2(0f, -72f);

            Text scoreText = scoreObject.GetComponent<Text>();
            scoreText.font = font;
            scoreText.fontSize = 42;
            scoreText.fontStyle = FontStyle.Bold;
            scoreText.alignment = TextAnchor.MiddleCenter;
            scoreText.color = new Color(0.98f, 0.99f, 1f, 0.96f);
            scoreText.raycastTarget = false;
            scoreText.text = "SCORE 0000";

            ForwardProgressScoreView scoreView = scoreObject.GetComponent<ForwardProgressScoreView>();
            SerializedObject scoreViewSo = new(scoreView);
            scoreViewSo.FindProperty("scoreTracker").objectReferenceValue = scoreTracker;
            scoreViewSo.FindProperty("scoreText").objectReferenceValue = scoreText;
            scoreViewSo.ApplyModifiedPropertiesWithoutUndo();
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
            return CreateOrUpdateTuningProfile(path, CreateBalancedMomentumPreset());
        }

        private static MovementTuningProfile CreateOrUpdateTuningProfile(string path, TuningPreset preset)
        {
            MovementTuningProfile asset = LoadOrCreateAsset<MovementTuningProfile>(path);
            SerializedObject so = new(asset);
            ApplyTuningPreset(so, preset);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static void CreateOrUpdatePresetProfiles()
        {
            CreateOrUpdateTuningProfile($"{PresetsPath}/BalancedMomentum.asset", CreateBalancedMomentumPreset());
            CreateOrUpdateTuningProfile($"{PresetsPath}/MarioArcade.asset", CreateMarioArcadePreset());
            CreateOrUpdateTuningProfile($"{PresetsPath}/HollowBounce.asset", CreateHollowBouncePreset());
            CreateOrUpdateTuningProfile($"{PresetsPath}/QuakeFlow.asset", CreateQuakeFlowPreset());
            CreateOrUpdateTuningProfile($"{PresetsPath}/DeadlockBurst.asset", CreateDeadlockBurstPreset());
            CreateOrUpdateTuningProfile($"{PresetsPath}/RiskOfRainCruiser.asset", CreateRiskOfRainCruiserPreset());
        }

        private static void ApplyTuningPreset(SerializedObject so, TuningPreset preset)
        {
            so.FindProperty("moveAcceleration").floatValue = preset.MoveAcceleration;
            so.FindProperty("airControlStrength").floatValue = preset.AirControlStrength;
            so.FindProperty("forwardAirControlMultiplier").floatValue = preset.ForwardAirControlMultiplier;
            so.FindProperty("airBrakeAcceleration").floatValue = preset.AirBrakeAcceleration;
            so.FindProperty("maxControllableSpeed").floatValue = preset.MaxControllableSpeed;
            so.FindProperty("maxSpeed").floatValue = preset.MaxSpeed;
            so.FindProperty("overSpeedDrag").floatValue = preset.OverSpeedDrag;
            so.FindProperty("airDrag").floatValue = preset.AirDrag;
            so.FindProperty("gravityScale").floatValue = preset.GravityScale;
            so.FindProperty("jumpGravityMultiplier").floatValue = preset.JumpGravityMultiplier;
            so.FindProperty("fallGravityMultiplier").floatValue = preset.FallGravityMultiplier;
            so.FindProperty("baseJumpForce").floatValue = preset.BaseJumpForce;
            so.FindProperty("dashForce").floatValue = preset.DashForce;
            so.FindProperty("dashCooldown").floatValue = preset.DashCooldown;
            so.FindProperty("dashChargesPerBounce").intValue = preset.DashChargesPerBounce;
            so.FindProperty("postBounceLowControlTime").floatValue = preset.PostBounceLowControlTime;
            so.FindProperty("postBounceAirControlMultiplier").floatValue = preset.PostBounceAirControlMultiplier;
            so.FindProperty("postDashBonusControlTime").floatValue = preset.PostDashBonusControlTime;
            so.FindProperty("postDashAirControlMultiplier").floatValue = preset.PostDashAirControlMultiplier;
            so.FindProperty("bounceGraceTime").floatValue = preset.BounceGraceTime;
            so.FindProperty("dashBufferTime").floatValue = preset.DashBufferTime;
            so.FindProperty("minGroundDot").floatValue = preset.MinGroundDot;
        }

        private static TuningPreset CreateBalancedMomentumPreset()
        {
            return new TuningPreset(
                moveAcceleration: 19f,
                airControlStrength: 0.5f,
                forwardAirControlMultiplier: 0.72f,
                airBrakeAcceleration: 24f,
                maxControllableSpeed: 12f,
                maxSpeed: 18f,
                overSpeedDrag: 7.5f,
                airDrag: 0.18f,
                gravityScale: 1f,
                jumpGravityMultiplier: 1.2f,
                fallGravityMultiplier: 2.1f,
                baseJumpForce: 9f,
                dashForce: 7.5f,
                dashCooldown: 0.16f,
                dashChargesPerBounce: 1,
                postBounceLowControlTime: 0.075f,
                postBounceAirControlMultiplier: 0.48f,
                postDashBonusControlTime: 0.18f,
                postDashAirControlMultiplier: 1.25f,
                bounceGraceTime: 0.1f,
                dashBufferTime: 0.1f,
                minGroundDot: 0.65f);
        }

        private static TuningPreset CreateMarioArcadePreset()
        {
            return new TuningPreset(
                moveAcceleration: 16f,
                airControlStrength: 0.35f,
                forwardAirControlMultiplier: 0.45f,
                airBrakeAcceleration: 28f,
                maxControllableSpeed: 10.5f,
                maxSpeed: 14f,
                overSpeedDrag: 10f,
                airDrag: 0.28f,
                gravityScale: 1.1f,
                jumpGravityMultiplier: 1.05f,
                fallGravityMultiplier: 2.6f,
                baseJumpForce: 8.5f,
                dashForce: 6.5f,
                dashCooldown: 0.18f,
                dashChargesPerBounce: 1,
                postBounceLowControlTime: 0.12f,
                postBounceAirControlMultiplier: 0.28f,
                postDashBonusControlTime: 0.12f,
                postDashAirControlMultiplier: 1.1f,
                bounceGraceTime: 0.1f,
                dashBufferTime: 0.1f,
                minGroundDot: 0.65f);
        }

        private static TuningPreset CreateHollowBouncePreset()
        {
            return new TuningPreset(
                moveAcceleration: 15f,
                airControlStrength: 0.42f,
                forwardAirControlMultiplier: 0.5f,
                airBrakeAcceleration: 26f,
                maxControllableSpeed: 11f,
                maxSpeed: 15.5f,
                overSpeedDrag: 9f,
                airDrag: 0.22f,
                gravityScale: 1.15f,
                jumpGravityMultiplier: 1.15f,
                fallGravityMultiplier: 2.8f,
                baseJumpForce: 8.75f,
                dashForce: 8f,
                dashCooldown: 0.14f,
                dashChargesPerBounce: 1,
                postBounceLowControlTime: 0.09f,
                postBounceAirControlMultiplier: 0.26f,
                postDashBonusControlTime: 0.12f,
                postDashAirControlMultiplier: 1.35f,
                bounceGraceTime: 0.1f,
                dashBufferTime: 0.1f,
                minGroundDot: 0.65f);
        }

        private static TuningPreset CreateQuakeFlowPreset()
        {
            return new TuningPreset(
                moveAcceleration: 14f,
                airControlStrength: 0.28f,
                forwardAirControlMultiplier: 0.35f,
                airBrakeAcceleration: 12f,
                maxControllableSpeed: 13f,
                maxSpeed: 22f,
                overSpeedDrag: 2.5f,
                airDrag: 0.04f,
                gravityScale: 0.95f,
                jumpGravityMultiplier: 0.95f,
                fallGravityMultiplier: 1.75f,
                baseJumpForce: 9.5f,
                dashForce: 8.5f,
                dashCooldown: 0.12f,
                dashChargesPerBounce: 1,
                postBounceLowControlTime: 0.03f,
                postBounceAirControlMultiplier: 0.75f,
                postDashBonusControlTime: 0.25f,
                postDashAirControlMultiplier: 1.6f,
                bounceGraceTime: 0.1f,
                dashBufferTime: 0.1f,
                minGroundDot: 0.65f);
        }

        private static TuningPreset CreateDeadlockBurstPreset()
        {
            return new TuningPreset(
                moveAcceleration: 17.5f,
                airControlStrength: 0.38f,
                forwardAirControlMultiplier: 0.42f,
                airBrakeAcceleration: 20f,
                maxControllableSpeed: 11.5f,
                maxSpeed: 17f,
                overSpeedDrag: 6.5f,
                airDrag: 0.16f,
                gravityScale: 1.05f,
                jumpGravityMultiplier: 1.1f,
                fallGravityMultiplier: 2f,
                baseJumpForce: 9f,
                dashForce: 8.5f,
                dashCooldown: 0.16f,
                dashChargesPerBounce: 1,
                postBounceLowControlTime: 0.12f,
                postBounceAirControlMultiplier: 0.24f,
                postDashBonusControlTime: 0.22f,
                postDashAirControlMultiplier: 1.6f,
                bounceGraceTime: 0.1f,
                dashBufferTime: 0.1f,
                minGroundDot: 0.65f);
        }

        private static TuningPreset CreateRiskOfRainCruiserPreset()
        {
            return new TuningPreset(
                moveAcceleration: 13.5f,
                airControlStrength: 0.48f,
                forwardAirControlMultiplier: 0.65f,
                airBrakeAcceleration: 18f,
                maxControllableSpeed: 12.5f,
                maxSpeed: 16.5f,
                overSpeedDrag: 7f,
                airDrag: 0.12f,
                gravityScale: 1f,
                jumpGravityMultiplier: 1f,
                fallGravityMultiplier: 1.85f,
                baseJumpForce: 9.25f,
                dashForce: 7.25f,
                dashCooldown: 0.18f,
                dashChargesPerBounce: 1,
                postBounceLowControlTime: 0.08f,
                postBounceAirControlMultiplier: 0.4f,
                postDashBonusControlTime: 0.16f,
                postDashAirControlMultiplier: 1.3f,
                bounceGraceTime: 0.1f,
                dashBufferTime: 0.1f,
                minGroundDot: 0.65f);
        }

        private static MushroomBounceProfile CreateOrUpdateStandardBounceProfile(string path)
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

        private static MushroomBounceProfile CreateOrUpdateBoostBounceProfile(string path)
        {
            MushroomBounceProfile asset = LoadOrCreateAsset<MushroomBounceProfile>(path);
            SerializedObject so = new(asset);
            so.FindProperty("velocityScale").floatValue = 1.05f;
            so.FindProperty("directionalInfluence").floatValue = 0.68f;
            so.FindProperty("planarBoost").floatValue = 1.65f;
            so.FindProperty("useAbsoluteUpwardImpulse").boolValue = false;
            so.FindProperty("upwardImpulse").floatValue = 1.08f;
            so.FindProperty("impactRecoveryFactor").floatValue = 0.34f;
            so.FindProperty("localLaunchDirection").vector3Value = new Vector3(0f, 1f, 0.55f);
            so.FindProperty("upBlend").floatValue = 0.68f;
            so.FindProperty("overridePlanarDrag").boolValue = false;
            so.FindProperty("planarDragOverride").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static MushroomBounceProfile CreateOrUpdateSlowBounceProfile(string path)
        {
            MushroomBounceProfile asset = LoadOrCreateAsset<MushroomBounceProfile>(path);
            SerializedObject so = new(asset);
            so.FindProperty("velocityScale").floatValue = 0.92f;
            so.FindProperty("directionalInfluence").floatValue = 0.25f;
            so.FindProperty("planarBoost").floatValue = -0.2f;
            so.FindProperty("useAbsoluteUpwardImpulse").boolValue = false;
            so.FindProperty("upwardImpulse").floatValue = 0.96f;
            so.FindProperty("impactRecoveryFactor").floatValue = 0.2f;
            so.FindProperty("localLaunchDirection").vector3Value = Vector3.up;
            so.FindProperty("upBlend").floatValue = 0.92f;
            so.FindProperty("overridePlanarDrag").boolValue = true;
            so.FindProperty("planarDragOverride").floatValue = 0.85f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static BounceSpawnDefinition CreateOrUpdateBounceSpawnDefinition(
            string path,
            GameObject prefab,
            MushroomBounceProfile bounceProfile,
            BounceSpawnTag gameplayTag,
            float spawnWeight)
        {
            BounceSpawnDefinition asset = LoadOrCreateAsset<BounceSpawnDefinition>(path);
            SerializedObject so = new(asset);
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.FindProperty("bounceProfileOverride").objectReferenceValue = bounceProfile;
            so.FindProperty("gameplayTag").enumValueIndex = (int)gameplayTag;
            so.FindProperty("localOffset").vector3Value = Vector3.zero;
            so.FindProperty("localScale").vector3Value = Vector3.one;
            so.FindProperty("spawnWeight").floatValue = spawnWeight;
            so.FindProperty("usePooling").boolValue = true;
            so.FindProperty("limitDifficultyRange").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static EnvironmentDecorationDefinition CreateOrUpdateDecorationDefinition(
            string path,
            string prefabAssetPath,
            Vector3 localOffset,
            float blockLength,
            float spawnWeight)
        {
            EnvironmentDecorationDefinition asset = LoadOrCreateAsset<EnvironmentDecorationDefinition>(path);
            SerializedObject so = new(asset);
            so.FindProperty("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(prefabAssetPath);
            so.FindProperty("localOffset").vector3Value = localOffset;
            so.FindProperty("blockLength").floatValue = blockLength;
            so.FindProperty("spawnWeight").floatValue = spawnWeight;
            so.FindProperty("usePooling").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static EnvironmentThemeTierDefinition CreateOrUpdateThemeTier(
            string path,
            int scoreThreshold,
            params EnvironmentDecorationDefinition[] decorations)
        {
            EnvironmentThemeTierDefinition asset = LoadOrCreateAsset<EnvironmentThemeTierDefinition>(path);
            SerializedObject so = new(asset);
            so.FindProperty("scoreThreshold").intValue = scoreThreshold;

            SerializedProperty decorationsProperty = so.FindProperty("decorations");
            decorationsProperty.arraySize = decorations.Length;
            for (int index = 0; index < decorations.Length; index++)
            {
                decorationsProperty.GetArrayElementAtIndex(index).objectReferenceValue = decorations[index];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static BounceAreaGenerationProfile CreateOrUpdateGenerationProfile(
            string path,
            BounceSpawnDefinition standardSpawnDefinition,
            BounceSpawnDefinition boostSpawnDefinition,
            BounceSpawnDefinition slowSpawnDefinition,
            EnvironmentThemeTierDefinition fantasyTheme,
            EnvironmentThemeTierDefinition gloomyTheme)
        {
            BounceAreaGenerationProfile asset = LoadOrCreateAsset<BounceAreaGenerationProfile>(path);
            SerializedObject so = new(asset);
            so.FindProperty("areaLength").intValue = 32;
            so.FindProperty("spawnAheadAreas").intValue = 4;
            so.FindProperty("recycleBehindAreas").intValue = 2;
            so.FindProperty("introAreaCount").intValue = 2;
            so.FindProperty("areaHalfWidth").floatValue = 8f;
            so.FindProperty("minimumHeight").floatValue = 0f;
            so.FindProperty("maximumHeight").floatValue = 4f;
            so.FindProperty("surfaceLandingHeight").floatValue = 0.94f;
            so.FindProperty("playerCollisionRadius").floatValue = 0.45f;
            so.FindProperty("initialLandingSpeed").floatValue = 2.5f;
            so.FindProperty("minimumMainPathNodes").intValue = 4;
            so.FindProperty("maximumMainPathNodes").intValue = 6;
            so.FindProperty("minimumOptionalMushrooms").intValue = 1;
            so.FindProperty("maximumOptionalMushrooms").intValue = 3;
            so.FindProperty("candidateAttemptsPerHop").intValue = 18;
            so.FindProperty("optionalCandidateAttempts").intValue = 18;
            so.FindProperty("minimumForwardGap").floatValue = 4.5f;
            so.FindProperty("maximumForwardGap").floatValue = 8f;
            so.FindProperty("maximumAdditionalForwardGapFromDifficulty").floatValue = 3f;
            so.FindProperty("maximumLateralOffset").floatValue = 5.5f;
            so.FindProperty("maximumVerticalStep").floatValue = 2.25f;
            so.FindProperty("minimumExitBuffer").floatValue = 2.5f;
            so.FindProperty("bailoutForwardGap").floatValue = 4.25f;
            so.FindProperty("bailoutVerticalStep").floatValue = 0.55f;
            so.FindProperty("landingRadius").floatValue = 1.3f;
            so.FindProperty("landingHeightTolerance").floatValue = 1.2f;
            so.FindProperty("maxSimulationTime").floatValue = 2.3f;
            so.FindProperty("simulationTimeStep").floatValue = 0.02f;
            so.FindProperty("mainRouteClearanceRadius").floatValue = 2.5f;
            so.FindProperty("optionalMushroomClearanceRadius").floatValue = 2.1f;
            so.FindProperty("decorationSeparationRadius").floatValue = 4f;
            so.FindProperty("decorationAreaPadding").floatValue = 1.5f;
            so.FindProperty("routeHeadroomClearance").floatValue = 3.5f;
            so.FindProperty("cameraSightlineClearance").floatValue = 6f;
            so.FindProperty("difficultyRampDistance").intValue = 300;
            so.FindProperty("seed").intValue = 1337;
            so.FindProperty("randomizeSeed").boolValue = true;

            SerializedProperty mushroomsProperty = so.FindProperty("mushroomDefinitions");
            mushroomsProperty.arraySize = 3;
            mushroomsProperty.GetArrayElementAtIndex(0).objectReferenceValue = standardSpawnDefinition;
            mushroomsProperty.GetArrayElementAtIndex(1).objectReferenceValue = boostSpawnDefinition;
            mushroomsProperty.GetArrayElementAtIndex(2).objectReferenceValue = slowSpawnDefinition;

            SerializedProperty themeProperty = so.FindProperty("themeTiers");
            themeProperty.arraySize = 2;
            themeProperty.GetArrayElementAtIndex(0).objectReferenceValue = fantasyTheme;
            themeProperty.GetArrayElementAtIndex(1).objectReferenceValue = gloomyTheme;

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
            EnsureFolder(GenerationPath);
            EnsureFolder(SpawningPath);
            EnsureFolder(DecorationsPath);
            EnsureFolder(ThemesPath);
            EnsureFolder(PresetsPath);
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

        private readonly struct TuningPreset
        {
            public TuningPreset(
                float moveAcceleration,
                float airControlStrength,
                float forwardAirControlMultiplier,
                float airBrakeAcceleration,
                float maxControllableSpeed,
                float maxSpeed,
                float overSpeedDrag,
                float airDrag,
                float gravityScale,
                float jumpGravityMultiplier,
                float fallGravityMultiplier,
                float baseJumpForce,
                float dashForce,
                float dashCooldown,
                int dashChargesPerBounce,
                float postBounceLowControlTime,
                float postBounceAirControlMultiplier,
                float postDashBonusControlTime,
                float postDashAirControlMultiplier,
                float bounceGraceTime,
                float dashBufferTime,
                float minGroundDot)
            {
                MoveAcceleration = moveAcceleration;
                AirControlStrength = airControlStrength;
                ForwardAirControlMultiplier = forwardAirControlMultiplier;
                AirBrakeAcceleration = airBrakeAcceleration;
                MaxControllableSpeed = maxControllableSpeed;
                MaxSpeed = maxSpeed;
                OverSpeedDrag = overSpeedDrag;
                AirDrag = airDrag;
                GravityScale = gravityScale;
                JumpGravityMultiplier = jumpGravityMultiplier;
                FallGravityMultiplier = fallGravityMultiplier;
                BaseJumpForce = baseJumpForce;
                DashForce = dashForce;
                DashCooldown = dashCooldown;
                DashChargesPerBounce = dashChargesPerBounce;
                PostBounceLowControlTime = postBounceLowControlTime;
                PostBounceAirControlMultiplier = postBounceAirControlMultiplier;
                PostDashBonusControlTime = postDashBonusControlTime;
                PostDashAirControlMultiplier = postDashAirControlMultiplier;
                BounceGraceTime = bounceGraceTime;
                DashBufferTime = dashBufferTime;
                MinGroundDot = minGroundDot;
            }

            public float MoveAcceleration { get; }

            public float AirControlStrength { get; }

            public float ForwardAirControlMultiplier { get; }

            public float AirBrakeAcceleration { get; }

            public float MaxControllableSpeed { get; }

            public float MaxSpeed { get; }

            public float OverSpeedDrag { get; }

            public float AirDrag { get; }

            public float GravityScale { get; }

            public float JumpGravityMultiplier { get; }

            public float FallGravityMultiplier { get; }

            public float BaseJumpForce { get; }

            public float DashForce { get; }

            public float DashCooldown { get; }

            public int DashChargesPerBounce { get; }

            public float PostBounceLowControlTime { get; }

            public float PostBounceAirControlMultiplier { get; }

            public float PostDashBonusControlTime { get; }

            public float PostDashAirControlMultiplier { get; }

            public float BounceGraceTime { get; }

            public float DashBufferTime { get; }

            public float MinGroundDot { get; }
        }
    }
}
