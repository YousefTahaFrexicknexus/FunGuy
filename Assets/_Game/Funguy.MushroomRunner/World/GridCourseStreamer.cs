using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GridCourseStreamer : MonoBehaviour
{
    sealed class GridSlice
    {
        public int Index;
        public float StartZ;
        public readonly List<SpawnedRuntime> SpawnedObjects = new();
    }

    readonly struct SpawnedRuntime
    {
        public SpawnedRuntime(GameObject poolKey, GameObject instance, bool usePooling)
        {
            PoolKey = poolKey;
            Instance = instance;
            UsePooling = usePooling;
        }

        public GameObject PoolKey { get; }

        public GameObject Instance { get; }

        public bool UsePooling { get; }
    }

    [SerializeField, Tooltip("Profile that controls the 3x3 grid layout and spawn rules.")]
    GridCourseGenerationProfile generationProfile;
    [SerializeField, Tooltip("Optional transform used only as a Z cursor for FIFO streaming.")]
    Transform streamingTarget;
    [SerializeField, Tooltip("Parent transform for generated objects. Defaults to this transform.")]
    Transform spawnRoot;
    [SerializeField, Tooltip("If enabled, the initial visible grid is built on Start.")]
    bool buildOnStart = true;
    [SerializeField, Tooltip("World Z used by Build Initial Grid on Start.")]
    float initialStartZ = 0f;

    [Header("Debug Grid")]
    [SerializeField, Tooltip("Draws the 3 wide by 3 tall grid cells in the Scene view.")]
    bool drawDebugGrid = true;
    [SerializeField, Tooltip("If enabled, the debug grid only draws while this object is selected.")]
    bool drawDebugGridOnlyWhenSelected;
    [SerializeField, Range(0.1f, 1f), Tooltip("How much of the actual grid cell volume the debug box fills. 1 shows full cells with no gaps.")]
    float debugCellSize = 1f;
    [SerializeField, Tooltip("Debug color for level 0 mushroom cells.")]
    Color debugMushroomLevelColor = new(0.25f, 1f, 0.45f, 0.75f);
    [SerializeField, Tooltip("Debug color for upper coin/obstacle cells.")]
    Color debugUpperLevelColor = new(1f, 0.8f, 0.25f, 0.65f);
    [SerializeField, Tooltip("Debug color for lines connecting matching cells between slices.")]
    Color debugConnectionColor = new(0.35f, 0.8f, 1f, 0.35f);
    [SerializeField, Tooltip("Debug color for the FIFO recycle threshold of the oldest slice.")]
    Color debugRecycleLineColor = new(1f, 0.2f, 0.15f, 0.9f);
    [SerializeField, Tooltip("Debug color for the streaming target Z cursor.")]
    Color debugTargetLineColor = Color.white;

    readonly Queue<GridSlice> activeSlices = new();
    readonly Dictionary<GameObject, Stack<GameObject>> pools = new();
    readonly Dictionary<GameObject, Collectable[]> instanceCoins = new();
    readonly List<SpawnedRuntime> pendingRecycles = new();
    readonly List<int> columnScratch = new(3);
    readonly GridSpawnEntry[] upperEntryScratch = new GridSpawnEntry[6];
    readonly Vector3[] columnPlacementOffsets = new Vector3[3];
    readonly bool[] columnHasMushroom = new bool[3];
    readonly int[] eligibleCoinCells = new int[6];

    System.Random random;
    float generationStartZ;
    int nextSliceIndex;
    int consecutiveEmptyMushroomRows;
    int consecutiveEmptyCoinRows;
    bool initialized;
    bool runFlowManaged;

    public bool IsRunFlowManaged => runFlowManaged;

    public float InitialStartZ => initialStartZ;

    public float GetInitialEnvironmentEndZ(float startZ) => generationProfile != null
        ? startZ + (generationProfile.VisibleGridCount * generationProfile.GridCellLength)
        : startZ;

    public float RequiredEnvironmentEndZ
    {
        get
        {
            if (generationProfile == null) return generationStartZ;
            float length = generationProfile.GridCellLength;
            float endZ = generationStartZ + nextSliceIndex * length;
            if (streamingTarget != null)
                endZ = Mathf.Max(endZ, streamingTarget.position.z + activeSlices.Count * length);
            return endZ + length * 0.5f;
        }
    }

    void Reset()
    {
        spawnRoot = transform;
    }

    void Awake()
    {
        ResolveSpawnRoot();
    }

    void Start()
    {
        if (!runFlowManaged && buildOnStart && !initialized)
        {
            BuildInitialGrid(initialStartZ);
        }
    }

    void Update()
    {
        if (!runFlowManaged) UpdateStreaming();
    }

    public void SetRunFlowManaged(bool managed) => runFlowManaged = managed;

    public void UpdateStreaming()
    {
        RecycleFinishedBundles();
        if (!initialized || generationProfile == null || streamingTarget == null)
        {
            return;
        }

        StreamFromTarget();
    }

    void OnDrawGizmos()
    {
        if (drawDebugGrid && !drawDebugGridOnlyWhenSelected)
        {
            DrawDebugGrid();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (drawDebugGrid && drawDebugGridOnlyWhenSelected)
        {
            DrawDebugGrid();
        }
    }

    public void BuildInitialGrid(float startZ = 0f)
    {
        if (!HasGenerationProfile())
        {
            return;
        }

        ClearGeneratedWorld();
        BeginGeneration(startZ);
        GenerateToSliceCount(generationProfile.VisibleGridCount);
    }

    public void ClearGeneratedWorld()
    {
        while (activeSlices.Count > 0)
        {
            RecycleSlice(activeSlices.Dequeue(), true);
        }

        // A reset cancels attraction rather than carrying coins into the next run.
        foreach (SpawnedRuntime spawned in pendingRecycles) RecycleInstance(spawned);
        pendingRecycles.Clear();

        nextSliceIndex = 0;
        consecutiveEmptyMushroomRows = 0;
        initialized = false;
        consecutiveEmptyCoinRows = 0;
    }

    public void GenerateNextSlice()
    {
        if (!HasGenerationProfile())
        {
            return;
        }

        if (!initialized)
        {
            BeginGeneration(initialStartZ);
        }

        bool surfaceTransformsSynced = false;
        GenerateNextSlice(ref surfaceTransformsSynced);
    }

    void GenerateNextSlice(ref bool surfaceTransformsSynced)
    {
        GridSlice slice = new()
        {
            Index = nextSliceIndex,
            StartZ = generationStartZ + (nextSliceIndex * generationProfile.GridCellLength)
        };

        SpawnSliceContent(slice, ref surfaceTransformsSynced);
        activeSlices.Enqueue(slice);
        nextSliceIndex++;
    }

    public void RecycleOldestSlice()
    {
        if (activeSlices.Count == 0)
        {
            return;
        }

        RecycleSlice(activeSlices.Dequeue());
    }

    public void SetStreamingTarget(Transform target)
    {
        streamingTarget = target;
    }

    public void GenerateToSliceCount(int count)
    {
        if (!HasGenerationProfile())
        {
            return;
        }

        if (!initialized)
        {
            BeginGeneration(initialStartZ);
        }

        int targetCount = Mathf.Max(0, count);
        bool surfaceTransformsSynced = false;
        while (activeSlices.Count < targetCount)
        {
            GenerateNextSlice(ref surfaceTransformsSynced);
        }

        while (activeSlices.Count > targetCount)
        {
            RecycleOldestSlice();
        }
    }

    void BeginGeneration(float startZ)
    {
        ResolveSpawnRoot();
        random = new System.Random(generationProfile.GetSeed());
        generationStartZ = startZ;
        nextSliceIndex = 0;
        consecutiveEmptyMushroomRows = 0;
        initialized = true;
        consecutiveEmptyCoinRows = 0;
    }

    void StreamFromTarget()
    {
        float recycleDistance = generationProfile.GridCellLength * 2f;
        bool surfaceTransformsSynced = false;
        while (activeSlices.Count > 0 && streamingTarget.position.z >= activeSlices.Peek().StartZ + recycleDistance)
        {
            RecycleOldestSlice();
            GenerateNextSlice(ref surfaceTransformsSynced);
        }
    }

    void SpawnSliceContent(GridSlice slice, ref bool surfaceTransformsSynced)
    {
        int score = Mathf.Max(0, Mathf.FloorToInt(slice.StartZ - generationStartZ));

        // Columns without a mushroom keep their normal grid position.
        System.Array.Clear(columnPlacementOffsets, 0, columnPlacementOffsets.Length);
        System.Array.Clear(columnHasMushroom, 0, columnHasMushroom.Length);
        SpawnMushroomRow(slice, score, ref surfaceTransformsSynced);
        SpawnUpperLevels(slice, score);
    }

    void SpawnMushroomRow(GridSlice slice, int score, ref bool surfaceTransformsSynced)
    {
        if (!ShouldSpawnMushroomRow(score))
        {
            if (consecutiveEmptyMushroomRows < int.MaxValue) consecutiveEmptyMushroomRows++;
            return;
        }

        bool hasMushroom = false;
        bool checkSurface = HasAvailableMushroomSurfaceRules(score);
        int mushroomCount = RandomRangeInclusive(
            generationProfile.MinimumMushroomsPerActiveRow,
            generationProfile.MaximumMushroomsPerActiveRow);

        FillColumnScratch();
        for (int count = 0; count < mushroomCount && columnScratch.Count > 0; count++)
        {
            int columnIndex = RandomRangeInclusive(0, columnScratch.Count - 1);
            int column = columnScratch[columnIndex];
            columnScratch.RemoveAt(columnIndex);

            GridSpawnEntry entry;
            Vector3 offset;
            Vector3 position = GetCellPosition(column, 0, slice.StartZ);
            if (checkSurface)
            {
                offset = GetMushroomPlacementOffset();
                position += offset;
                if (!surfaceTransformsSynced)
                {
                    // Initial grids and catch-up streaming share one sync per batch.
                    Physics.SyncTransforms();
                    surfaceTransformsSynced = true;
                }

                float startHeight = generationProfile.MushroomSurfaceStartHeight;
                bool hitSurface = Physics.Raycast(
                    position + Vector3.up * startHeight,
                    Vector3.down,
                    out RaycastHit hit,
                    startHeight + generationProfile.MushroomSurfaceDownwardReach,
                    generationProfile.MushroomSurfaceLayers,
                    QueryTriggerInteraction.Collide);
                // Read only the closest collider's own tag, once for every candidate.
                string surfaceTag = hitSurface ? hit.collider.tag : null;
                entry = PickEntry(generationProfile.MushroomEntries, score, true, surfaceTag);
            }
            else
            {
                // Preserve the existing random draw order when surface rules are unused.
                entry = PickEntry(generationProfile.MushroomEntries, score);
                offset = GetMushroomPlacementOffset();
                position += offset;
            }

            if (!SpawnEntry(entry, position, slice)) continue;
            columnPlacementOffsets[column] = offset;
            columnHasMushroom[column] = true;
            hasMushroom = true;
        }

        consecutiveEmptyMushroomRows = hasMushroom ? 0
            : (consecutiveEmptyMushroomRows < int.MaxValue ? consecutiveEmptyMushroomRows + 1 : int.MaxValue);
    }

    bool HasAvailableMushroomSurfaceRules(int score)
    {
        IReadOnlyList<GridSpawnEntry> entries = generationProfile.MushroomEntries;
        if (entries == null) return false;
        for (int index = 0; index < entries.Count; index++)
        {
            GridSpawnEntry entry = entries[index];
            if (entry != null && entry.IsAvailable(score) && entry.HasSurfaceRules) return true;
        }
        return false;
    }

    bool ShouldSpawnMushroomRow(int score)
    {
        if (nextSliceIndex == 0)
        {
            return true;
        }

        int maximumEmptyRows = generationProfile.GetCurrentMaximumEmptyMushroomRows(score);
        if (maximumEmptyRows <= 0 || consecutiveEmptyMushroomRows >= maximumEmptyRows)
        {
            return true;
        }

        return random.NextDouble() >= generationProfile.EvaluateEmptyMushroomRowChance(score);
    }

    void SpawnUpperLevels(GridSlice slice, int score)
    {
        bool hasCoinRow = false;
        int eligibleCoinCount = 0;
        for (int cell = 0; cell < upperEntryScratch.Length; cell++)
        {
            // Even indices are middle-level cells; odd indices are top-level cells.
            bool allowCoins = cell % 2 == 1 || columnHasMushroom[cell / 2];
            if (allowCoins) eligibleCoinCells[eligibleCoinCount++] = cell;
            upperEntryScratch[cell] = PickUpperLevelEntry(score, allowCoins, out bool isCoin);
            hasCoinRow |= isCoin;
        }

        if (!hasCoinRow && eligibleCoinCount > 0
            && consecutiveEmptyCoinRows >= generationProfile.MaximumEmptyCoinRows)
        {
            GridSpawnEntry guaranteedCoin = PickEntry(generationProfile.CoinEntries, score);
            if (guaranteedCoin != null)
            {
                // Replace the planned cell before spawning so coins never overlap an obstacle.
                int cell = eligibleCoinCells[RandomRangeInclusive(0, eligibleCoinCount - 1)];
                upperEntryScratch[cell] = guaranteedCoin;
                hasCoinRow = true;
            }
        }

        // Saturate the counter even if no coin entries are unlocked yet.
        consecutiveEmptyCoinRows = hasCoinRow ? 0
            : (consecutiveEmptyCoinRows < int.MaxValue ? consecutiveEmptyCoinRows + 1 : int.MaxValue);

        for (int column = 0; column < 3; column++)
        {
            for (int level = 1; level <= 2; level++)
            {
                GridSpawnEntry entry = upperEntryScratch[column * 2 + level - 1];
                SpawnEntry(entry, GetCellPosition(column, level, slice.StartZ) + columnPlacementOffsets[column], slice);
            }
        }
    }

    GridSpawnEntry PickUpperLevelEntry(int score, bool allowCoins, out bool isCoin)
    {
        isCoin = false;
        bool hasCoins = allowCoins && HasAvailableEntry(generationProfile.CoinEntries, score);
        bool hasObstacles = HasAvailableEntry(generationProfile.ObstacleEntries, score);
        float coinChance = hasCoins ? generationProfile.EvaluateCoinChance(score) : 0f;
        float obstacleChance = hasObstacles ? generationProfile.EvaluateObstacleChance(score) : 0f;
        float totalChance = coinChance + obstacleChance;
        if (totalChance <= 0f || random.NextDouble() >= Mathf.Clamp01(totalChance))
        {
            return null;
        }

        float roll = RandomRange(0f, totalChance);
        if (roll < obstacleChance)
        {
            return PickEntry(generationProfile.ObstacleEntries, score);
        }

        if (!hasCoins) return null;
        GridSpawnEntry coin = PickEntry(generationProfile.CoinEntries, score);
        isCoin = coin != null;
        return coin;
    }

    bool SpawnEntry(GridSpawnEntry entry, Vector3 position, GridSlice slice)
    {
        if (entry == null || entry.Prefab == null)
        {
            return false;
        }

        GameObject instance = GetInstance(entry.Prefab, entry.UsePooling);
        if (instance == null)
        {
            return false;
        }

        instance.transform.SetParent(spawnRoot, false);
        instance.transform.position = position + entry.LocalOffset;
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = entry.LocalScale;
        ApplyMushroomProfile(instance, entry.BounceProfileOverride);
        // Enable only after the bundle and all its coins have been reset and placed.
        instance.SetActive(true);

        slice.SpawnedObjects.Add(new SpawnedRuntime(entry.Prefab, instance, entry.UsePooling));
        return true;
    }

    void ApplyMushroomProfile(GameObject instance, MushroomBounceProfile profile)
    {
        if (instance == null || profile == null)
        {
            return;
        }

        Mushroom mushroom = instance.GetComponent<Mushroom>();
        if (mushroom == null)
        {
            mushroom = instance.GetComponentInChildren<Mushroom>();
        }

        if (mushroom != null)
        {
            mushroom.SetBounceProfile(profile);
            return;
        }

        SimpleBounceMushroom simpleMushroom = instance.GetComponent<SimpleBounceMushroom>();
        if (simpleMushroom == null)
        {
            simpleMushroom = instance.GetComponentInChildren<SimpleBounceMushroom>();
        }

        if (simpleMushroom != null)
        {
            simpleMushroom.SetBounceProfile(profile);
        }
    }

    GameObject GetInstance(GameObject prefab, bool usePooling)
    {
        GameObject instance;
        if (usePooling && pools.TryGetValue(prefab, out Stack<GameObject> pool) && pool.Count > 0)
        {
            instance = pool.Pop();
        }
        else
        {
            instance = Instantiate(prefab);
            instance.SetActive(false);
            // Include collected/inactive children; only allocate this array once per instance.
            instanceCoins.Add(instance, instance.GetComponentsInChildren<Collectable>(true));
        }

        foreach (Collectable coin in instanceCoins[instance])
        {
            if (coin == null) continue;
            coin.ResetForReuse();
            // Keep the root inactive until SpawnEntry has positioned it.
            if (coin.gameObject != instance) coin.gameObject.SetActive(true);
        }

        return instance;
    }

    bool HasAttractingCoins(GameObject instance)
    {
        if (instance == null || !instanceCoins.TryGetValue(instance, out Collectable[] coins)) return false;
        foreach (Collectable coin in coins)
        {
            if (coin != null && coin.IsBeingAttracted) return true;
        }
        return false;
    }

    void RecycleFinishedBundles()
    {
        for (int index = pendingRecycles.Count - 1; index >= 0; index--)
        {
            SpawnedRuntime spawned = pendingRecycles[index];
            if (HasAttractingCoins(spawned.Instance)) continue;
            RecycleInstance(spawned);
            // Order does not matter; avoid shifting the remaining bundles.
            int last = pendingRecycles.Count - 1;
            pendingRecycles[index] = pendingRecycles[last];
            pendingRecycles.RemoveAt(last);
        }
    }

    void RecycleSlice(GridSlice slice, bool force = false)
    {
        for (int index = 0; index < slice.SpawnedObjects.Count; index++)
        {
            SpawnedRuntime spawned = slice.SpawnedObjects[index];
            if (spawned.Instance == null)
            {
                continue;
            }

            // Let coins finish flying without holding up forward grid generation.
            if (!force && HasAttractingCoins(spawned.Instance))
            {
                pendingRecycles.Add(spawned);
            }
            else RecycleInstance(spawned);
        }
    }

    void RecycleInstance(SpawnedRuntime spawned)
    {
        if (spawned.Instance == null) return;
        if (spawned.UsePooling && spawned.PoolKey != null)
        {
            spawned.Instance.SetActive(false);
            if (!pools.TryGetValue(spawned.PoolKey, out Stack<GameObject> pool))
            {
                pool = new Stack<GameObject>();
                pools.Add(spawned.PoolKey, pool);
            }
            pool.Push(spawned.Instance);
        }
        else
        {
            instanceCoins.Remove(spawned.Instance);
            if (Application.isPlaying) Destroy(spawned.Instance);
            else DestroyImmediate(spawned.Instance);
        }
    }

    GridSpawnEntry PickEntry(IReadOnlyList<GridSpawnEntry> entries, int score,
        bool filterBySurface = false, string surfaceTag = null)
    {
        if (entries == null || entries.Count == 0)
        {
            return null;
        }

        float totalWeight = 0f;
        for (int index = 0; index < entries.Count; index++)
        {
            GridSpawnEntry entry = entries[index];
            if (entry != null && entry.IsAvailable(score)
                && (!filterBySurface || entry.AllowsSurface(surfaceTag)))
            {
                totalWeight += entry.SpawnWeight;
            }
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float roll = RandomRange(0f, totalWeight);
        float cursor = 0f;
        for (int index = 0; index < entries.Count; index++)
        {
            GridSpawnEntry entry = entries[index];
            if (entry == null || !entry.IsAvailable(score)
                || (filterBySurface && !entry.AllowsSurface(surfaceTag)))
            {
                continue;
            }

            cursor += entry.SpawnWeight;
            if (roll <= cursor)
            {
                return entry;
            }
        }

        return null;
    }

    bool HasAvailableEntry(IReadOnlyList<GridSpawnEntry> entries, int score)
    {
        if (entries == null)
        {
            return false;
        }

        for (int index = 0; index < entries.Count; index++)
        {
            if (entries[index] != null && entries[index].IsAvailable(score))
            {
                return true;
            }
        }

        return false;
    }

    Vector3 GetCellPosition(int column, int level, float z)
    {
        float x = transform.position.x + ((column - 1) * generationProfile.ColumnSpacing);
        float y = transform.position.y + (level * generationProfile.LevelHeight);
        return new Vector3(x, y, z);
    }

    Vector3 GetMushroomPlacementOffset()
    {
        return new Vector3(
            RandomRange(-generationProfile.MushroomSideToSideOffset, generationProfile.MushroomSideToSideOffset),
            0f,
            RandomRange(-generationProfile.MushroomForwardBackOffset, generationProfile.MushroomForwardBackOffset));
    }

    void FillColumnScratch()
    {
        columnScratch.Clear();
        columnScratch.Add(0);
        columnScratch.Add(1);
        columnScratch.Add(2);
    }

    void ResolveSpawnRoot()
    {
        if (spawnRoot == null)
        {
            spawnRoot = transform;
        }
    }

    bool HasGenerationProfile()
    {
        if (generationProfile != null)
        {
            return true;
        }

        Debug.LogWarning("[GridCourseStreamer] Missing generation profile.", this);
        return false;
    }

    int RandomRangeInclusive(int minimum, int maximum)
    {
        if (maximum <= minimum)
        {
            return minimum;
        }

        return random.Next(minimum, maximum + 1);
    }

    float RandomRange(float minimum, float maximum)
    {
        if (maximum <= minimum)
        {
            return minimum;
        }

        return minimum + ((float)random.NextDouble() * (maximum - minimum));
    }

    void DrawDebugGrid()
    {
        if (generationProfile == null)
        {
            return;
        }

        int sliceCount = initialized && activeSlices.Count > 0
            ? activeSlices.Count
            : generationProfile.VisibleGridCount;
        if (sliceCount <= 0)
        {
            return;
        }

        bool hasPreviousSlice = false;
        float previousSliceZ = 0f;
        bool drewFirstSlice = false;
        float firstSliceZ = 0f;
        float lastSliceZ = 0f;

        if (initialized && activeSlices.Count > 0)
        {
            foreach (GridSlice slice in activeSlices)
            {
                DrawDebugSlice(slice.StartZ, hasPreviousSlice, previousSliceZ);

                if (!drewFirstSlice)
                {
                    firstSliceZ = slice.StartZ;
                    drewFirstSlice = true;
                }

                previousSliceZ = slice.StartZ;
                lastSliceZ = slice.StartZ;
                hasPreviousSlice = true;
            }
        }
        else
        {
            for (int index = 0; index < sliceCount; index++)
            {
                float sliceZ = initialStartZ + (index * generationProfile.GridCellLength);
                DrawDebugSlice(sliceZ, hasPreviousSlice, previousSliceZ);

                if (!drewFirstSlice)
                {
                    firstSliceZ = sliceZ;
                    drewFirstSlice = true;
                }

                previousSliceZ = sliceZ;
                lastSliceZ = sliceZ;
                hasPreviousSlice = true;
            }
        }

        DrawDebugRecycleLine(firstSliceZ);
        DrawDebugTargetLine(firstSliceZ, lastSliceZ);
    }

    void DrawDebugSlice(float sliceZ, bool hasPreviousSlice, float previousSliceZ)
    {
        for (int column = 0; column < 3; column++)
        {
            for (int level = 0; level < 3; level++)
            {
                Vector3 position = GetCellPosition(column, level, sliceZ);
                Gizmos.color = level == 0 ? debugMushroomLevelColor : debugUpperLevelColor;
                Gizmos.DrawWireCube(position, GetDebugCellSize());

                if (!hasPreviousSlice)
                {
                    continue;
                }

                Gizmos.color = debugConnectionColor;
                Gizmos.DrawLine(GetCellPosition(column, level, previousSliceZ), position);
            }
        }
    }

    void DrawDebugRecycleLine(float oldestSliceZ)
    {
        float recycleZ = oldestSliceZ + (generationProfile.GridCellLength * 2f);
        DrawDebugVerticalMarker(recycleZ, debugRecycleLineColor);
    }

    void DrawDebugTargetLine(float firstSliceZ, float lastSliceZ)
    {
        if (streamingTarget == null)
        {
            return;
        }

        float z = Mathf.Clamp(
            streamingTarget.position.z,
            firstSliceZ - generationProfile.GridCellLength,
            lastSliceZ + generationProfile.GridCellLength);
        DrawDebugVerticalMarker(z, debugTargetLineColor);
    }

    void DrawDebugVerticalMarker(float z, Color color)
    {
        float width = Mathf.Max(0.5f, generationProfile.ColumnSpacing * 2.35f);
        float height = Mathf.Max(0.5f, generationProfile.LevelHeight * 2.35f);
        float x = transform.position.x;
        float y = transform.position.y + (generationProfile.LevelHeight);

        Gizmos.color = color;
        Gizmos.DrawLine(new Vector3(x - width * 0.5f, y, z), new Vector3(x + width * 0.5f, y, z));
        Gizmos.DrawLine(new Vector3(x, transform.position.y - height * 0.15f, z), new Vector3(x, transform.position.y + height, z));
    }

    Vector3 GetDebugCellSize()
    {
        return new Vector3(
            Mathf.Max(0.25f, generationProfile.ColumnSpacing * debugCellSize),
            Mathf.Max(0.25f, generationProfile.LevelHeight * debugCellSize),
            Mathf.Max(0.25f, generationProfile.GridCellLength * debugCellSize));
    }
}
