using System;
using System.Collections.Generic;
using UnityEngine;

namespace FunGuy.Runner
{
    public sealed class EndlessGridSpawner : MonoBehaviour
    {
        private sealed class ActiveChunk
        {
            public int Index;
            public readonly List<SpawnedRuntime> SpawnedObjects = new();
        }

        private readonly struct SpawnedRuntime
        {
            public SpawnedRuntime(
                SpawnableDefinition definition,
                GameObject instance,
                GridSurfaceActor surface,
                GridCollectibleActor collectible,
                bool usesFallbackSupportPool)
            {
                Definition = definition;
                Instance = instance;
                Surface = surface;
                Collectible = collectible;
                UsesFallbackSupportPool = usesFallbackSupportPool;
            }

            public SpawnableDefinition Definition { get; }
            public GameObject Instance { get; }
            public GridSurfaceActor Surface { get; }
            public GridCollectibleActor Collectible { get; }
            public bool UsesFallbackSupportPool { get; }
        }

        [SerializeField] private Transform spawnRoot;
        [SerializeField] private RunnerGridSystem gridSystem;
        [SerializeField] private GridWorld gridWorld;
        [SerializeField] private RunnerGenerationProfile generationProfile;
        [SerializeField] private RunnerPlayerConfig playerConfig;
        [SerializeField] private PlayerRunnerController trackedPlayer;

        private readonly Queue<ActiveChunk> activeChunks = new();
        private readonly Dictionary<SpawnableDefinition, Stack<GameObject>> pools = new();
        private readonly Stack<GameObject> fallbackSupportPool = new();
        private readonly List<Vector3Int> sliceCandidates = new(16);

        private System.Random random;
        private Vector3Int safeCursor;
        private Vector3Int runStartCell;
        private bool initialized;
        private bool runIsPlaying;
        private int nextChunkIndexToGenerate;
        private int lastSupportPlatformSliceZ;
        private float startupDelaySeconds;
        private static Material fallbackSupportMaterial;

        private void OnEnable()
        {
            RunnerGameEvents.GameStateChanged += HandleGameStateChanged;
        }

        private void OnDisable()
        {
            RunnerGameEvents.GameStateChanged -= HandleGameStateChanged;
        }

        private void Update()
        {
            if (!initialized || trackedPlayer == null || generationProfile == null)
            {
                return;
            }

            EnsureChunksForPlayer(trackedPlayer.CurrentCell.z);
            RecycleChunksBehindPlayer(trackedPlayer.CurrentCell.z);
        }

        public void Configure(
            RunnerGridSystem runnerGridSystem,
            GridWorld runnerGridWorld,
            RunnerGenerationProfile runnerGenerationProfile,
            RunnerPlayerConfig runnerPlayerConfig,
            PlayerRunnerController playerController)
        {
            gridSystem = runnerGridSystem;
            gridWorld = runnerGridWorld;
            generationProfile = runnerGenerationProfile;
            playerConfig = runnerPlayerConfig;
            trackedPlayer = playerController;
            random = new System.Random(generationProfile != null ? generationProfile.GetSeed() : Environment.TickCount);

            if (spawnRoot == null)
            {
                spawnRoot = transform;
            }
        }

        public void SetStartupDelay(float delaySeconds)
        {
            startupDelaySeconds = Mathf.Max(0f, delaySeconds);
        }

        public void BuildInitialWorld(Vector3Int startCell)
        {
            if (gridSystem == null || gridWorld == null || generationProfile == null)
            {
                Debug.LogError("[EndlessGridSpawner] Missing setup dependencies.");
                return;
            }

            ClearSpawnedWorld();

            safeCursor = startCell;
            runStartCell = startCell;
            nextChunkIndexToGenerate = 0;
            runIsPlaying = false;
            lastSupportPlatformSliceZ = startCell.z - (generationProfile != null ? generationProfile.MinimumSlicesBetweenSupportPlatforms : 1);
            initialized = true;

            EnsureChunksForPlayer(startCell.z);
        }

        private void EnsureChunksForPlayer(int playerZ)
        {
            int playerChunk = Mathf.Max(0, playerZ / generationProfile.ChunkLength);
            int targetChunk = playerChunk + generationProfile.SpawnAheadChunks;

            while (nextChunkIndexToGenerate <= targetChunk)
            {
                GenerateChunk(nextChunkIndexToGenerate);
                nextChunkIndexToGenerate++;
            }
        }

        private void GenerateChunk(int chunkIndex)
        {
            ActiveChunk chunk = new() { Index = chunkIndex };
            int chunkStartZ = chunkIndex * generationProfile.ChunkLength;
            int chunkEndExclusive = chunkStartZ + generationProfile.ChunkLength;
            int introEndZ = runStartCell.z + generationProfile.IntroFilledSlices - 1;

            for (int z = chunkStartZ; z < chunkEndExclusive; z++)
            {
                int traveledCells = Mathf.Max(0, z - runStartCell.z);

                if (z <= introEndZ)
                {
                    SpawnIntroSlice(z, chunk);
                    safeCursor = new Vector3Int(runStartCell.x, runStartCell.y, z);
                    continue;
                }

                Vector3Int previousSafeCell = safeCursor;
                safeCursor = GetNextSafeCell(previousSafeCell, z);
                bool usedSupportPlatform = false;

                if (!TrySpawnSafePathSurface(previousSafeCell, safeCursor, traveledCells, chunk, out usedSupportPlatform))
                {
                    TrySpawnSurfaceAt(safeCursor, PickDefinition(generationProfile.PlatformDefinitions, traveledCells, true), chunk);
                }

                if (!usedSupportPlatform)
                {
                    TrySpawnCollectibleAt(
                        safeCursor,
                        PickDefinition(generationProfile.CollectibleDefinitions, traveledCells, false),
                        chunk,
                        generationProfile.GetCollectibleChance(traveledCells));
                }

                SpawnSliceExtras(z, safeCursor, usedSupportPlatform, chunk);
            }

            activeChunks.Enqueue(chunk);
        }

        private void SpawnIntroSlice(int z, ActiveChunk chunk)
        {
            int minLane = generationProfile.IntroFillAllLanes ? 0 : runStartCell.x;
            int maxLane = generationProfile.IntroFillAllLanes ? gridSystem.LaneCount - 1 : runStartCell.x;
            int traveledCells = Mathf.Max(0, z - runStartCell.z);

            for (int lane = minLane; lane <= maxLane; lane++)
            {
                Vector3Int cell = new(lane, runStartCell.y, z);
                TrySpawnSurfaceAt(cell, PickDefinition(generationProfile.PlatformDefinitions, traveledCells, true), chunk);
            }
        }

        private void SpawnSliceExtras(int z, Vector3Int safeCell, bool safeCellUsesSupportPlatform, ActiveChunk chunk)
        {
            if (gridSystem == null)
            {
                return;
            }

            if (generationProfile.IntroDisableRandomContent &&
                z < runStartCell.z + generationProfile.IntroFilledSlices)
            {
                return;
            }

            int traveledCells = Mathf.Max(0, z - runStartCell.z);

            sliceCandidates.Clear();

            for (int lane = 0; lane < gridSystem.LaneCount; lane++)
            {
                for (int layer = gridSystem.MinLayer; layer <= gridSystem.MaxLayer; layer++)
                {
                    Vector3Int candidate = new(lane, layer, z);

                    if (candidate == safeCell)
                    {
                        continue;
                    }

                    if (safeCellUsesSupportPlatform && candidate.x == safeCell.x)
                    {
                        continue;
                    }

                    sliceCandidates.Add(candidate);
                }
            }

            if (sliceCandidates.Count == 0)
            {
                return;
            }

            Vector3Int collectibleTarget = safeCell;
            bool hasBonusPlatform = false;

            if (Roll(generationProfile.GetBonusPlatformChance(traveledCells)))
            {
                int bonusIndex = random.Next(sliceCandidates.Count);
                Vector3Int bonusCell = sliceCandidates[bonusIndex];
                hasBonusPlatform = TrySpawnSurfaceAt(
                    bonusCell,
                    PickDefinition(generationProfile.PlatformDefinitions, traveledCells, true),
                    chunk);
                collectibleTarget = bonusCell;
                sliceCandidates.RemoveAt(bonusIndex);
            }

            if (sliceCandidates.Count > 0 && Roll(generationProfile.GetHazardChance(traveledCells)))
            {
                int hazardIndex = random.Next(sliceCandidates.Count);
                Vector3Int hazardCell = sliceCandidates[hazardIndex];
                TrySpawnSurfaceAt(
                    hazardCell,
                    PickDefinition(generationProfile.HazardDefinitions, traveledCells, false),
                    chunk);
            }

            if (hasBonusPlatform)
            {
                TrySpawnCollectibleAt(
                    collectibleTarget,
                    PickDefinition(generationProfile.CollectibleDefinitions, traveledCells, false),
                    chunk,
                    generationProfile.GetCollectibleChance(traveledCells) * 0.4f);
            }
        }

        private Vector3Int GetNextSafeCell(Vector3Int previousSafeCell, int nextZ)
        {
            int nextLane = previousSafeCell.x;
            int nextLayer = previousSafeCell.y;
            int traveledCells = Mathf.Max(0, nextZ - runStartCell.z);

            if (gridSystem.LaneCount > 1 && Roll(generationProfile.GetLaneChangeChance(traveledCells)))
            {
                nextLane += random.Next(0, 2) == 0 ? -1 : 1;
                nextLane = Mathf.Clamp(nextLane, 0, gridSystem.LaneCount - 1);
            }

            if (gridSystem.MaxLayer > gridSystem.MinLayer && Roll(generationProfile.GetLayerChangeChance(traveledCells)))
            {
                nextLayer += random.Next(0, 2) == 0 ? -1 : 1;
                nextLayer = Mathf.Clamp(nextLayer, gridSystem.MinLayer, gridSystem.MaxLayer);
            }

            return new Vector3Int(nextLane, nextLayer, nextZ);
        }

        private bool TrySpawnSurfaceAt(Vector3Int cell, SpawnableDefinition definition, ActiveChunk chunk)
        {
            if (definition == null || definition.Prefab == null)
            {
                return false;
            }

            if (gridWorld.GetLandingType(cell, out _) != GridLandingType.Missing)
            {
                return false;
            }

            GameObject instance = GetInstance(definition);
            instance.transform.SetParent(spawnRoot, false);
            instance.transform.position = gridSystem.CellToWorld(cell) + definition.LocalOffset;
            instance.transform.localScale = definition.LocalScale;
            instance.transform.rotation = Quaternion.identity;
            InitializeSpawnedInstance(instance, cell);

            GridSurfaceActor surface = instance.GetComponent<GridSurfaceActor>();
            if (surface == null)
            {
                surface = instance.AddComponent<GridSurfaceActor>();
            }

            surface.Bind(definition, cell);
            gridWorld.RegisterSurface(surface);

            chunk.SpawnedObjects.Add(new SpawnedRuntime(definition, instance, surface, null, false));
            return true;
        }

        private bool TrySpawnSafePathSurface(Vector3Int previousSafeCell, Vector3Int safeCell, int traveledCells, ActiveChunk chunk, out bool usedSupportPlatform)
        {
            usedSupportPlatform = false;

            if (!ShouldUseSupportPlatform(previousSafeCell, safeCell, traveledCells))
            {
                return TrySpawnSurfaceAt(safeCell, PickDefinition(generationProfile.PlatformDefinitions, traveledCells, true), chunk);
            }

            if (TrySpawnSupportSurfaceAt(safeCell, traveledCells, chunk, true))
            {
                usedSupportPlatform = true;
                lastSupportPlatformSliceZ = safeCell.z;
                return true;
            }

            return TrySpawnSurfaceAt(safeCell, PickDefinition(generationProfile.PlatformDefinitions, traveledCells, true), chunk);
        }

        private bool TrySpawnSupportSurfaceAt(Vector3Int cell, int traveledCells, ActiveChunk chunk, bool timedArrival)
        {
            if (gridSystem == null || gridWorld == null || !gridSystem.IsWithinPlayableBounds(cell))
            {
                return false;
            }

            if (gridWorld.GetLandingType(cell, out _) != GridLandingType.Missing)
            {
                return false;
            }

            SpawnableDefinition definition = PickDefinition(generationProfile.SupportPlatformDefinitions, traveledCells, false);

            if (definition != null && definition.Prefab != null)
            {
                return TrySpawnSpawnableSurfaceAt(cell, definition, chunk, timedArrival);
            }

            GameObject instance = GetFallbackSupportInstance();
            instance.transform.SetParent(spawnRoot, false);
            instance.transform.position = gridSystem.CellToWorld(cell);
            instance.transform.localScale = Vector3.one;
            instance.transform.rotation = Quaternion.identity;

            GridSurfaceActor surface = instance.GetComponent<GridSurfaceActor>();
            if (surface == null)
            {
                surface = instance.AddComponent<GridSurfaceActor>();
            }

            surface.Bind(null, cell);
            surface.SetLandingEnabled(!timedArrival);
            gridWorld.RegisterSurface(surface);
            InitializeSpawnedInstance(instance, cell);
            ConfigureSupportPlatformTiming(instance, surface, cell, timedArrival);

            chunk.SpawnedObjects.Add(new SpawnedRuntime(null, instance, surface, null, true));
            return true;
        }

        private bool TrySpawnCollectibleAt(Vector3Int cell, SpawnableDefinition definition, ActiveChunk chunk, float chance)
        {
            if (!Roll(chance) || definition == null || definition.Prefab == null)
            {
                return false;
            }

            if (gridWorld.GetLandingType(cell, out _) != GridLandingType.Safe || gridWorld.HasCollectible(cell))
            {
                return false;
            }

            GameObject instance = GetInstance(definition);
            instance.transform.SetParent(spawnRoot, false);
            instance.transform.position = gridSystem.CellToWorld(cell) + definition.LocalOffset;
            instance.transform.localScale = definition.LocalScale;
            instance.transform.rotation = Quaternion.identity;
            InitializeSpawnedInstance(instance, cell);

            GridCollectibleActor collectible = instance.GetComponent<GridCollectibleActor>();
            if (collectible == null)
            {
                collectible = instance.AddComponent<GridCollectibleActor>();
            }

            collectible.Bind(definition, cell);
            gridWorld.RegisterCollectible(collectible);

            chunk.SpawnedObjects.Add(new SpawnedRuntime(definition, instance, null, collectible, false));
            return true;
        }

        private GameObject GetInstance(SpawnableDefinition definition)
        {
            if (definition.UsePooling &&
                pools.TryGetValue(definition, out Stack<GameObject> pool) &&
                pool.Count > 0)
            {
                GameObject pooled = pool.Pop();
                pooled.SetActive(true);
                return pooled;
            }

            GameObject created = Instantiate(definition.Prefab);
            created.SetActive(true);
            return created;
        }

        private GameObject GetFallbackSupportInstance()
        {
            if (fallbackSupportPool.Count > 0)
            {
                GameObject pooled = fallbackSupportPool.Pop();
                pooled.SetActive(true);
                return pooled;
            }

            GameObject root = new("SupportCubePlatform");
            root.AddComponent<GridSurfaceActor>();
            root.AddComponent<SideApproachPlatformVisual>();

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            body.transform.localScale = new Vector3(1.05f, 0.34f, 1.05f);

            Renderer bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                bodyRenderer.sharedMaterial = GetFallbackSupportMaterial();
            }

            Collider bodyCollider = body.GetComponent<Collider>();
            if (bodyCollider != null)
            {
                Destroy(bodyCollider);
            }

            return root;
        }

        private bool TrySpawnSpawnableSurfaceAt(Vector3Int cell, SpawnableDefinition definition, ActiveChunk chunk, bool timedArrival)
        {
            if (definition == null || definition.Prefab == null)
            {
                return false;
            }

            if (gridWorld.GetLandingType(cell, out _) != GridLandingType.Missing)
            {
                return false;
            }

            GameObject instance = GetInstance(definition);
            instance.transform.SetParent(spawnRoot, false);
            instance.transform.position = gridSystem.CellToWorld(cell) + definition.LocalOffset;
            instance.transform.localScale = definition.LocalScale;
            instance.transform.rotation = Quaternion.identity;

            GridSurfaceActor surface = instance.GetComponent<GridSurfaceActor>();
            if (surface == null)
            {
                surface = instance.AddComponent<GridSurfaceActor>();
            }

            surface.Bind(definition, cell);
            surface.SetLandingEnabled(!timedArrival);
            gridWorld.RegisterSurface(surface);
            InitializeSpawnedInstance(instance, cell);
            ConfigureSupportPlatformTiming(instance, surface, cell, timedArrival);

            chunk.SpawnedObjects.Add(new SpawnedRuntime(definition, instance, surface, null, false));
            return true;
        }

        private void InitializeSpawnedInstance(GameObject instance, Vector3Int cell)
        {
            Transform stem = instance.transform.Find("Stem");
            if (stem != null && instance.GetComponent<GroundedStemVisual>() == null)
            {
                instance.AddComponent<GroundedStemVisual>();
            }

            Transform cap = instance.transform.Find("Cap");
            if (cap != null && instance.GetComponent<MushroomLandingFeedback>() == null)
            {
                instance.AddComponent<MushroomLandingFeedback>();
            }

            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IRunnerSpawnInitializable initializable)
                {
                    initializable.InitializeOnSpawn(gridSystem, cell);
                }
            }
        }

        private void RecycleChunksBehindPlayer(int playerZ)
        {
            int playerChunk = Mathf.Max(0, playerZ / generationProfile.ChunkLength);

            while (activeChunks.Count > 0 && playerChunk - activeChunks.Peek().Index > generationProfile.RecycleBehindChunks)
            {
                RecycleChunk(activeChunks.Dequeue());
            }
        }

        private void RecycleChunk(ActiveChunk chunk)
        {
            for (int i = 0; i < chunk.SpawnedObjects.Count; i++)
            {
                SpawnedRuntime spawned = chunk.SpawnedObjects[i];

                if (spawned.Surface != null)
                {
                    gridWorld.UnregisterSurface(spawned.Surface);
                }

                if (spawned.Collectible != null)
                {
                    gridWorld.UnregisterCollectible(spawned.Collectible);
                }

                if (spawned.UsesFallbackSupportPool)
                {
                    spawned.Instance.SetActive(false);
                    fallbackSupportPool.Push(spawned.Instance);
                }
                else if (spawned.Definition != null && spawned.Definition.UsePooling)
                {
                    spawned.Instance.SetActive(false);

                    if (!pools.TryGetValue(spawned.Definition, out Stack<GameObject> pool))
                    {
                        pool = new Stack<GameObject>();
                        pools.Add(spawned.Definition, pool);
                    }

                    pool.Push(spawned.Instance);
                }
                else if (spawned.Instance != null)
                {
                    Destroy(spawned.Instance);
                }
            }
        }

        private void ClearSpawnedWorld()
        {
            while (activeChunks.Count > 0)
            {
                RecycleChunk(activeChunks.Dequeue());
            }

            gridWorld.ClearAll();
        }

        private SpawnableDefinition PickDefinition(
            IReadOnlyList<SpawnableDefinition> definitions,
            int traveledCells,
            bool allowAnyFallback)
        {
            if (definitions == null || definitions.Count == 0)
            {
                return null;
            }

            RunnerDifficultyTier targetDifficulty = generationProfile != null
                ? generationProfile.EvaluateDifficultyTier(traveledCells)
                : RunnerDifficultyTier.Hard;

            float totalWeight = 0f;
            bool foundEligibleDefinition = false;

            for (int i = 0; i < definitions.Count; i++)
            {
                SpawnableDefinition definition = definitions[i];

                if (definition != null && definition.AllowsDifficulty(targetDifficulty))
                {
                    totalWeight += definition.SpawnWeight;
                    foundEligibleDefinition = true;
                }
            }

            if (!foundEligibleDefinition)
            {
                if (!allowAnyFallback)
                {
                    return null;
                }

                totalWeight = 0f;

                for (int i = 0; i < definitions.Count; i++)
                {
                    if (definitions[i] != null)
                    {
                        totalWeight += definitions[i].SpawnWeight;
                    }
                }
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float value = (float)random.NextDouble() * totalWeight;
            float cursor = 0f;

            for (int i = 0; i < definitions.Count; i++)
            {
                SpawnableDefinition definition = definitions[i];

                if (definition == null)
                {
                    continue;
                }

                if (foundEligibleDefinition && !definition.AllowsDifficulty(targetDifficulty))
                {
                    continue;
                }

                cursor += definition.SpawnWeight;

                if (value <= cursor)
                {
                    return definition;
                }
            }

            for (int i = definitions.Count - 1; i >= 0; i--)
            {
                SpawnableDefinition definition = definitions[i];

                if (definition == null)
                {
                    continue;
                }

                if (foundEligibleDefinition && !definition.AllowsDifficulty(targetDifficulty))
                {
                    continue;
                }

                return definition;
            }

            return null;
        }

        private bool Roll(float chance)
        {
            return chance > 0f && random.NextDouble() <= chance;
        }

        private void HandleGameStateChanged(RunnerGameState state)
        {
            runIsPlaying = state == RunnerGameState.Playing;
        }

        private bool ShouldUseSupportPlatform(Vector3Int previousSafeCell, Vector3Int safeCell, int traveledCells)
        {
            if (generationProfile == null ||
                safeCell.z <= runStartCell.z + generationProfile.IntroFilledSlices ||
                safeCell.z - lastSupportPlatformSliceZ < generationProfile.MinimumSlicesBetweenSupportPlatforms)
            {
                return false;
            }

            float chance = generationProfile.GetSupportPlatformChance(traveledCells);

            if (generationProfile.AlwaysSpawnSupportPlatformOnClimb && safeCell.y > previousSafeCell.y)
            {
                chance = Mathf.Max(chance, 0.4f);
            }

            return Roll(chance);
        }

        private void ConfigureSupportPlatformTiming(GameObject instance, GridSurfaceActor surface, Vector3Int cell, bool timedArrival)
        {
            if (!timedArrival || instance == null)
            {
                return;
            }

            SideApproachPlatformVisual movingPlatform = instance.GetComponent<SideApproachPlatformVisual>();

            if (movingPlatform == null)
            {
                return;
            }

            float secondsUntilArrival = EstimateSecondsUntilArrival(cell.z);
            movingPlatform.ScheduleArrival(surface, secondsUntilArrival);
        }

        private float EstimateSecondsUntilArrival(int targetZ)
        {
            if (!runIsPlaying)
            {
                return startupDelaySeconds + EstimateTravelDuration(runStartCell.z, targetZ);
            }

            if (trackedPlayer != null)
            {
                float playerEstimate = trackedPlayer.EstimateTimeToReachCellZ(targetZ);

                if (!float.IsInfinity(playerEstimate) && !float.IsNaN(playerEstimate))
                {
                    return Mathf.Max(0f, playerEstimate);
                }
            }

            return EstimateTravelDuration(runStartCell.z, targetZ);
        }

        private float EstimateTravelDuration(int fromZ, int targetZ)
        {
            if (playerConfig == null || targetZ <= fromZ)
            {
                return 0f;
            }

            float totalTime = 0f;

            for (int hopStartZ = fromZ; hopStartZ < targetZ; hopStartZ++)
            {
                totalTime += playerConfig.GetJumpDuration(hopStartZ);

                if (hopStartZ + 1 < targetZ)
                {
                    int landingZ = hopStartZ + 1;
                    totalTime += playerConfig.GetTimeBetweenBounces(landingZ);
                    totalTime += playerConfig.GetLandingPause(landingZ);
                }
            }

            return totalTime;
        }

        private static Material GetFallbackSupportMaterial()
        {
            if (fallbackSupportMaterial != null)
            {
                return fallbackSupportMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            fallbackSupportMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            Color helperColor = new(0.61f, 0.82f, 0.53f, 1f);

            if (fallbackSupportMaterial.HasProperty("_BaseColor"))
            {
                fallbackSupportMaterial.SetColor("_BaseColor", helperColor);
            }

            if (fallbackSupportMaterial.HasProperty("_Color"))
            {
                fallbackSupportMaterial.SetColor("_Color", helperColor);
            }

            return fallbackSupportMaterial;
        }
    }
}
