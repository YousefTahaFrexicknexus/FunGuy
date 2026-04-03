using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FunGuy.Runner.Editor
{
    public static class RunnerSceneBootstrapper
    {
        private const string RunnerRoot = "Assets/_Game/Runner";
        private const string MaterialsPath = RunnerRoot + "/Materials";
        private const string PrefabsPath = RunnerRoot + "/Prefabs";
        private const string ConfigPath = RunnerRoot + "/ScriptableObjects/Config";
        private const string SpawningPath = RunnerRoot + "/ScriptableObjects/Spawning";
        private const string ScenePath = RunnerRoot + "/Scenes/RunnerGameplay.unity";

        [MenuItem("FunGuy/Runner/Create Fresh Runner Scene")]
        public static void CreateFreshRunnerScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            CreateSceneAssets();
        }

        public static void CreateFreshRunnerSceneBatchMode()
        {
            CreateSceneAssets();
        }

        private static void CreateSceneAssets()
        {
            EnsureFolders();

            Material platformCapMaterial = CreateMaterial($"{MaterialsPath}/Runner_PlatformCap.mat", new Color(0.92f, 0.38f, 0.29f));
            Material platformStemMaterial = CreateMaterial($"{MaterialsPath}/Runner_PlatformStem.mat", new Color(0.95f, 0.90f, 0.80f));
            Material hazardMaterial = CreateMaterial($"{MaterialsPath}/Runner_Hazard.mat", new Color(0.65f, 0.14f, 0.12f));
            Material collectibleMaterial = CreateMaterial($"{MaterialsPath}/Runner_Collectible.mat", new Color(1.0f, 0.82f, 0.18f));
            Material playerMaterial = CreateMaterial($"{MaterialsPath}/Runner_Player.mat", new Color(0.20f, 0.70f, 0.66f));
            Material supportPlatformMaterial = CreateMaterial($"{MaterialsPath}/Runner_SupportPlatform.mat", new Color(0.61f, 0.82f, 0.53f));

            GameObject platformPrefab = CreatePlatformPrefab($"{PrefabsPath}/MushroomPlatform.prefab", platformCapMaterial, platformStemMaterial);
            GameObject supportPlatformPrefab = CreateSupportPlatformPrefab($"{PrefabsPath}/SupportPlatform.prefab", supportPlatformMaterial);
            GameObject hazardPrefab = CreateHazardPrefab($"{PrefabsPath}/HazardTile.prefab", hazardMaterial);
            GameObject collectiblePrefab = CreateCollectiblePrefab($"{PrefabsPath}/CollectibleOrb.prefab", collectibleMaterial);
            GameObject playerPrefab = CreatePlayerPrefab($"{PrefabsPath}/RunnerPlayer.prefab", playerMaterial);

            RunnerGridConfig gridConfig = CreateOrUpdateGridConfig($"{ConfigPath}/RunnerGridConfig.asset");
            RunnerPlayerConfig playerConfig = CreateOrUpdatePlayerConfig($"{ConfigPath}/RunnerPlayerConfig.asset");

            SpawnableDefinition platformDefinition = CreateOrUpdateSpawnableDefinition(
                $"{SpawningPath}/PlatformDefinition.asset",
                SpawnableCategory.Platform,
                platformPrefab,
                new Vector3(0f, -0.65f, 0f),
                Vector3.one);

            SpawnableDefinition supportPlatformDefinition = CreateOrUpdateSpawnableDefinition(
                $"{SpawningPath}/SupportPlatformDefinition.asset",
                SpawnableCategory.Platform,
                supportPlatformPrefab,
                Vector3.zero,
                Vector3.one);

            SpawnableDefinition hazardDefinition = CreateOrUpdateSpawnableDefinition(
                $"{SpawningPath}/HazardDefinition.asset",
                SpawnableCategory.Hazard,
                hazardPrefab,
                new Vector3(0f, -0.65f, 0f),
                Vector3.one);

            SpawnableDefinition collectibleDefinition = CreateOrUpdateSpawnableDefinition(
                $"{SpawningPath}/CollectibleDefinition.asset",
                SpawnableCategory.Collectible,
                collectiblePrefab,
                new Vector3(0f, 0.65f, 0f),
                Vector3.one);

            RunnerGenerationProfile generationProfile = CreateOrUpdateGenerationProfile(
                $"{ConfigPath}/RunnerGenerationProfile.asset",
                platformDefinition,
                supportPlatformDefinition,
                hazardDefinition,
                collectibleDefinition);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildScene(scene, gridConfig, playerConfig, generationProfile, playerPrefab);
            EditorSceneManager.SaveScene(scene, ScenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[RunnerSceneBootstrapper] Created scene at {ScenePath}");
        }

        private static void BuildScene(
            Scene scene,
            RunnerGridConfig gridConfig,
            RunnerPlayerConfig playerConfig,
            RunnerGenerationProfile generationProfile,
            GameObject playerPrefab)
        {
            GameObject systemsRoot = new("_Systems");
            SceneManager.MoveGameObjectToScene(systemsRoot, scene);

            GameObject runtimeRoot = new("_Runtime");
            SceneManager.MoveGameObjectToScene(runtimeRoot, scene);

            GameObject presentationRoot = new("_Presentation");
            SceneManager.MoveGameObjectToScene(presentationRoot, scene);

            RunnerGridSystem gridSystem = new GameObject("RunnerGridSystem").AddComponent<RunnerGridSystem>();
            gridSystem.transform.SetParent(systemsRoot.transform);

            GridWorld gridWorld = new GameObject("GridWorld").AddComponent<GridWorld>();
            gridWorld.transform.SetParent(systemsRoot.transform);

            RunnerInputHandler inputHandler = new GameObject("RunnerInputHandler").AddComponent<RunnerInputHandler>();
            inputHandler.transform.SetParent(systemsRoot.transform);

            EndlessGridSpawner spawner = new GameObject("EndlessGridSpawner").AddComponent<EndlessGridSpawner>();
            spawner.transform.SetParent(runtimeRoot.transform);

            GameObject playerInstance = PrefabUtility.InstantiatePrefab(playerPrefab, scene) as GameObject;
            playerInstance.name = "RunnerPlayer";
            playerInstance.transform.SetParent(runtimeRoot.transform);
            playerInstance.transform.position = new Vector3(0f, 0.2f, 0f);
            PlayerRunnerController playerController = playerInstance.GetComponent<PlayerRunnerController>();

            GameObject cameraObject = new("Main Camera");
            cameraObject.transform.SetParent(presentationRoot.transform);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300f;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 50f;
            cameraObject.AddComponent<AudioListener>();
            RunnerCameraFollow cameraFollow = null;

            if (!RunnerCinemachineSceneTools.TryConfigureSceneCamera(scene, presentationRoot.transform, cameraObject, playerInstance.transform))
            {
                cameraFollow = cameraObject.AddComponent<RunnerCameraFollow>();
            }

            GameObject lightObject = new("Directional Light");
            lightObject.transform.SetParent(presentationRoot.transform);
            lightObject.transform.rotation = Quaternion.Euler(40f, -20f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.color = new Color(1f, 0.97f, 0.92f);

            RunnerGameManager gameManager = new GameObject("RunnerGameManager").AddComponent<RunnerGameManager>();
            gameManager.transform.SetParent(systemsRoot.transform);

            CreateHud(scene, out RunnerExtraJumpCounter counter, out Image[] icons);

            SerializedObject motorSo = new(playerInstance.GetComponent<GridJumpMotor>());
            motorSo.FindProperty("visualRoot").objectReferenceValue = playerInstance.transform.GetChild(0);
            motorSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject counterSo = new(counter);
            SerializedProperty iconsProp = counterSo.FindProperty("jumpIcons");
            iconsProp.arraySize = icons.Length;
            for (int i = 0; i < icons.Length; i++)
            {
                iconsProp.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
            }
            counterSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject managerSo = new(gameManager);
            managerSo.FindProperty("gridConfig").objectReferenceValue = gridConfig;
            managerSo.FindProperty("playerConfig").objectReferenceValue = playerConfig;
            managerSo.FindProperty("generationProfile").objectReferenceValue = generationProfile;
            managerSo.FindProperty("gridSystem").objectReferenceValue = gridSystem;
            managerSo.FindProperty("gridWorld").objectReferenceValue = gridWorld;
            managerSo.FindProperty("inputHandler").objectReferenceValue = inputHandler;
            managerSo.FindProperty("spawner").objectReferenceValue = spawner;
            managerSo.FindProperty("playerController").objectReferenceValue = playerController;
            managerSo.FindProperty("cameraFollow").objectReferenceValue = cameraFollow;
            managerSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void CreateHud(Scene scene, out RunnerExtraJumpCounter counter, out Image[] icons)
        {
            GameObject canvasObject = new("HUD");
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            Font hudFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            GameObject counterObject = new("ExtraJumpCounter", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(RunnerExtraJumpCounter));
            counterObject.transform.SetParent(canvasObject.transform, false);

            RectTransform counterRect = counterObject.GetComponent<RectTransform>();
            counterRect.anchorMin = new Vector2(0f, 1f);
            counterRect.anchorMax = new Vector2(0f, 1f);
            counterRect.pivot = new Vector2(0f, 1f);
            counterRect.anchoredPosition = new Vector2(36f, -36f);
            counterRect.sizeDelta = new Vector2(320f, 80f);

            HorizontalLayoutGroup layout = counterObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;

            counter = counterObject.GetComponent<RunnerExtraJumpCounter>();
            icons = new Image[5];
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            for (int i = 0; i < icons.Length; i++)
            {
                GameObject iconObject = new($"Jump_{i + 1}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconObject.transform.SetParent(counterObject.transform, false);
                RectTransform iconRect = iconObject.GetComponent<RectTransform>();
                iconRect.sizeDelta = new Vector2(48f, 48f);

                Image image = iconObject.GetComponent<Image>();
                image.sprite = uiSprite;
                image.color = Color.white;

                LayoutElement element = iconObject.GetComponent<LayoutElement>();
                element.minWidth = 48f;
                element.minHeight = 48f;
                icons[i] = image;
            }

            GameObject scoreObject = new("ScoreText", typeof(RectTransform), typeof(Text), typeof(Outline), typeof(RunnerScoreView));
            scoreObject.transform.SetParent(canvasObject.transform, false);

            RectTransform scoreRect = scoreObject.GetComponent<RectTransform>();
            scoreRect.anchorMin = new Vector2(1f, 1f);
            scoreRect.anchorMax = new Vector2(1f, 1f);
            scoreRect.pivot = new Vector2(1f, 1f);
            scoreRect.anchoredPosition = new Vector2(-40f, -36f);
            scoreRect.sizeDelta = new Vector2(320f, 120f);

            Text scoreText = scoreObject.GetComponent<Text>();
            scoreText.font = hudFont;
            scoreText.fontSize = 54;
            scoreText.fontStyle = FontStyle.Bold;
            scoreText.alignment = TextAnchor.UpperRight;
            scoreText.horizontalOverflow = HorizontalWrapMode.Overflow;
            scoreText.verticalOverflow = VerticalWrapMode.Overflow;
            scoreText.color = new Color(1f, 0.97f, 0.88f, 1f);
            scoreText.raycastTarget = false;
            scoreText.text = "SCORE\n00000";

            Outline scoreOutline = scoreObject.GetComponent<Outline>();
            scoreOutline.effectColor = new Color(0f, 0f, 0f, 0.32f);
            scoreOutline.effectDistance = new Vector2(2f, -2f);

            GameObject countdownObject = new("CountdownText", typeof(RectTransform), typeof(CanvasGroup), typeof(Text), typeof(RunnerCountdownView));
            countdownObject.transform.SetParent(canvasObject.transform, false);

            RectTransform countdownRect = countdownObject.GetComponent<RectTransform>();
            countdownRect.anchorMin = new Vector2(0.5f, 0.5f);
            countdownRect.anchorMax = new Vector2(0.5f, 0.5f);
            countdownRect.pivot = new Vector2(0.5f, 0.5f);
            countdownRect.anchoredPosition = new Vector2(0f, 80f);
            countdownRect.sizeDelta = new Vector2(280f, 280f);

            Text countdownText = countdownObject.GetComponent<Text>();
            countdownText.font = hudFont;
            countdownText.fontSize = 180;
            countdownText.fontStyle = FontStyle.Bold;
            countdownText.alignment = TextAnchor.MiddleCenter;
            countdownText.color = new Color(1f, 0.97f, 0.88f, 1f);
            countdownText.raycastTarget = false;
            countdownText.text = string.Empty;
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

        private static RunnerGridConfig CreateOrUpdateGridConfig(string path)
        {
            RunnerGridConfig asset = LoadOrCreateAsset<RunnerGridConfig>(path);
            asset.laneCount = 3;
            asset.minLayer = -1;
            asset.maxLayer = 2;
            asset.cellSize = new Vector3(2.75f, 2f, 5.35f);
            asset.worldOrigin = Vector3.zero;
            asset.previewForwardCells = 18;
            asset.Sanitize();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static RunnerPlayerConfig CreateOrUpdatePlayerConfig(string path)
        {
            RunnerPlayerConfig asset = LoadOrCreateAsset<RunnerPlayerConfig>(path);
            SerializedObject so = new(asset);
            so.FindProperty("startLane").intValue = 1;
            so.FindProperty("startLayer").intValue = 0;
            so.FindProperty("initialBounceDelay").floatValue = 0.55f;
            so.FindProperty("timeBetweenBounces").floatValue = 0.22f;
            so.FindProperty("minimumTimeBetweenBounces").floatValue = 0.12f;
            so.FindProperty("anticipationDuration").floatValue = 0.08f;
            so.FindProperty("anticipationDip").floatValue = 0.1f;
            so.FindProperty("jumpDuration").floatValue = 0.6f;
            so.FindProperty("minimumJumpDuration").floatValue = 0.38f;
            so.FindProperty("jumpArcHeight").floatValue = 1.55f;
            so.FindProperty("landingPause").floatValue = 0.12f;
            so.FindProperty("minimumLandingPause").floatValue = 0.05f;
            so.FindProperty("speedRampDistanceCells").intValue = 320;
            so.FindProperty("retargetLockProgress").floatValue = 0.92f;
            so.FindProperty("maxExtraJumps").intValue = 3;
            so.FindProperty("extraForwardCells").intValue = 1;
            so.FindProperty("extraJumpUpCells").intValue = 1;
            so.FindProperty("scorePerForwardCell").intValue = 10;
            so.FindProperty("scorePerCollectible").intValue = 25;
            so.FindProperty("failFallDuration").floatValue = 0.35f;
            so.FindProperty("deathDepthCells").floatValue = 3f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static SpawnableDefinition CreateOrUpdateSpawnableDefinition(
            string path,
            SpawnableCategory category,
            GameObject prefab,
            Vector3 localOffset,
            Vector3 localScale)
        {
            SpawnableDefinition asset = LoadOrCreateAsset<SpawnableDefinition>(path);
            SerializedObject so = new(asset);
            so.FindProperty("category").enumValueIndex = (int)category;
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.FindProperty("localOffset").vector3Value = localOffset;
            so.FindProperty("localScale").vector3Value = localScale;
            so.FindProperty("spawnWeight").floatValue = 1f;
            so.FindProperty("usePooling").boolValue = true;
            so.FindProperty("limitDifficultyRange").boolValue = false;
            so.FindProperty("minimumDifficulty").enumValueIndex = (int)RunnerDifficultyTier.Easy;
            so.FindProperty("maximumDifficulty").enumValueIndex = (int)RunnerDifficultyTier.Hard;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static RunnerGenerationProfile CreateOrUpdateGenerationProfile(
            string path,
            SpawnableDefinition platformDefinition,
            SpawnableDefinition supportPlatformDefinition,
            SpawnableDefinition hazardDefinition,
            SpawnableDefinition collectibleDefinition)
        {
            RunnerGenerationProfile asset = LoadOrCreateAsset<RunnerGenerationProfile>(path);
            SerializedObject so = new(asset);
            so.FindProperty("chunkLength").intValue = 14;
            so.FindProperty("spawnAheadChunks").intValue = 4;
            so.FindProperty("recycleBehindChunks").intValue = 2;
            so.FindProperty("introFilledSlices").intValue = 5;
            so.FindProperty("introFillAllLanes").boolValue = true;
            so.FindProperty("introDisableRandomContent").boolValue = true;
            so.FindProperty("difficultyRampDistanceCells").intValue = 180;
            so.FindProperty("startingLaneChangeChance").floatValue = 0.08f;
            so.FindProperty("startingLayerChangeChance").floatValue = 0f;
            so.FindProperty("startingBonusPlatformChance").floatValue = 0.1f;
            so.FindProperty("startingHazardChance").floatValue = 0f;
            so.FindProperty("startingCollectibleChance").floatValue = 0.58f;
            so.FindProperty("startingSupportPlatformChance").floatValue = 0.12f;
            so.FindProperty("laneChangeChance").floatValue = 0.45f;
            so.FindProperty("layerChangeChance").floatValue = 0.24f;
            so.FindProperty("bonusPlatformChance").floatValue = 0.3f;
            so.FindProperty("hazardChance").floatValue = 0.22f;
            so.FindProperty("collectibleChance").floatValue = 0.38f;
            so.FindProperty("supportPlatformChance").floatValue = 0.2f;
            so.FindProperty("alwaysSpawnSupportPlatformOnClimb").boolValue = true;
            so.FindProperty("minimumSlicesBetweenSupportPlatforms").intValue = 4;
            so.FindProperty("seed").intValue = 1337;
            so.FindProperty("randomizeSeed").boolValue = true;
            SetSingleObjectList(so.FindProperty("platformDefinitions"), platformDefinition);
            SetSingleObjectList(so.FindProperty("supportPlatformDefinitions"), supportPlatformDefinition);
            SetSingleObjectList(so.FindProperty("hazardDefinitions"), hazardDefinition);
            SetSingleObjectList(so.FindProperty("collectibleDefinitions"), collectibleDefinition);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            SerializedObject hazardSo = new(hazardDefinition);
            hazardSo.FindProperty("limitDifficultyRange").boolValue = true;
            hazardSo.FindProperty("minimumDifficulty").enumValueIndex = (int)RunnerDifficultyTier.Medium;
            hazardSo.FindProperty("maximumDifficulty").enumValueIndex = (int)RunnerDifficultyTier.Hard;
            hazardSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hazardDefinition);

            return asset;
        }

        private static GameObject CreatePlayerPrefab(string path, Material playerMaterial)
        {
            GameObject root = new("RunnerPlayer");
            root.AddComponent<PlayerRunnerController>();
            root.AddComponent<GridJumpMotor>();
            root.AddComponent<RunnerSimpleTrail>();

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
            ApplyMaterial(visual, playerMaterial);
            RemoveCollider(visual);

            GameObject cameraAnchor = new("CameraAnchor");
            cameraAnchor.transform.SetParent(root.transform, false);
            RunnerCameraAnchor anchor = cameraAnchor.AddComponent<RunnerCameraAnchor>();
            anchor.ApplyAnchorOffset();

            return SavePrefab(root, path);
        }

        private static GameObject CreatePlatformPrefab(string path, Material capMaterial, Material stemMaterial)
        {
            GameObject root = new("MushroomPlatform");
            root.AddComponent<GridSurfaceActor>();
            GroundedStemVisual groundedStemVisual = root.AddComponent<GroundedStemVisual>();
            root.AddComponent<MushroomLandingFeedback>();

            GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cap.name = "Cap";
            cap.transform.SetParent(root.transform, false);
            cap.transform.localPosition = new Vector3(0f, 0.08f, 0f);
            cap.transform.localScale = new Vector3(1.25f, 0.55f, 1.25f);
            ApplyMaterial(cap, capMaterial);
            RemoveCollider(cap);

            GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Stem";
            stem.transform.SetParent(root.transform, false);
            stem.transform.localPosition = new Vector3(0f, -0.46f, 0f);
            stem.transform.localScale = new Vector3(0.22f, 0.5f, 0.22f);
            ApplyMaterial(stem, stemMaterial);
            RemoveCollider(stem);

            SerializedObject groundedStemSo = new(groundedStemVisual);
            groundedStemSo.FindProperty("stem").objectReferenceValue = stem.transform;
            groundedStemSo.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, path);
        }

        private static GameObject CreateSupportPlatformPrefab(string path, Material supportPlatformMaterial)
        {
            GameObject root = new("SupportPlatform");
            root.AddComponent<GridSurfaceActor>();
            root.AddComponent<SideApproachPlatformVisual>();

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            body.transform.localScale = new Vector3(1.05f, 0.34f, 1.05f);
            ApplyMaterial(body, supportPlatformMaterial);
            RemoveCollider(body);

            return SavePrefab(root, path);
        }

        private static GameObject CreateHazardPrefab(string path, Material hazardMaterial)
        {
            GameObject root = new("HazardTile");
            root.AddComponent<GridSurfaceActor>();

            GameObject baseTile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseTile.name = "Base";
            baseTile.transform.SetParent(root.transform, false);
            baseTile.transform.localPosition = new Vector3(0f, -0.48f, 0f);
            baseTile.transform.localScale = new Vector3(1.15f, 0.15f, 1.15f);
            ApplyMaterial(baseTile, hazardMaterial);
            RemoveCollider(baseTile);

            for (int i = 0; i < 3; i++)
            {
                GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
                spike.name = $"Spike_{i + 1}";
                spike.transform.SetParent(root.transform, false);
                spike.transform.localPosition = new Vector3(-0.3f + i * 0.3f, -0.1f, 0f);
                spike.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                spike.transform.localScale = new Vector3(0.18f, 0.18f, 0.18f);
                ApplyMaterial(spike, hazardMaterial);
                RemoveCollider(spike);
            }

            return SavePrefab(root, path);
        }

        private static GameObject CreateCollectiblePrefab(string path, Material collectibleMaterial)
        {
            GameObject root = new("CollectibleOrb");
            root.AddComponent<GridCollectibleActor>();

            GameObject orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "Orb";
            orb.transform.SetParent(root.transform, false);
            orb.transform.localPosition = Vector3.zero;
            orb.transform.localScale = new Vector3(0.45f, 0.45f, 0.45f);
            ApplyMaterial(orb, collectibleMaterial);
            RemoveCollider(orb);

            return SavePrefab(root, path);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Game");
            EnsureFolder(RunnerRoot);
            EnsureFolder($"{RunnerRoot}/Materials");
            EnsureFolder($"{RunnerRoot}/Prefabs");
            EnsureFolder($"{RunnerRoot}/Scenes");
            EnsureFolder($"{RunnerRoot}/ScriptableObjects");
            EnsureFolder($"{RunnerRoot}/ScriptableObjects/Config");
            EnsureFolder($"{RunnerRoot}/ScriptableObjects/Spawning");
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

        private static void SetSingleObjectList(SerializedProperty property, Object value)
        {
            property.arraySize = 1;
            property.GetArrayElementAtIndex(0).objectReferenceValue = value;
        }
    }
}
