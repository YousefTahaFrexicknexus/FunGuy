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
    readonly List<int> columnScratch = new(3);

    System.Random random;
    float generationStartZ;
    int nextSliceIndex;
    int consecutiveEmptyMushroomRows;
    bool initialized;

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
        if (buildOnStart)
        {
            BuildInitialGrid(initialStartZ);
        }
    }

    void Update()
    {
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
            RecycleSlice(activeSlices.Dequeue());
        }

        nextSliceIndex = 0;
        consecutiveEmptyMushroomRows = 0;
        initialized = false;
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

        GridSlice slice = new()
        {
            Index = nextSliceIndex,
            StartZ = generationStartZ + (nextSliceIndex * generationProfile.GridCellLength)
        };

        SpawnSliceContent(slice);
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
        while (activeSlices.Count < targetCount)
        {
            GenerateNextSlice();
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
    }

    void StreamFromTarget()
    {
        float recycleDistance = generationProfile.GridCellLength * 2f;
        while (activeSlices.Count > 0 && streamingTarget.position.z >= activeSlices.Peek().StartZ + recycleDistance)
        {
            RecycleOldestSlice();
            GenerateNextSlice();
        }
    }

    void SpawnSliceContent(GridSlice slice)
    {
        int score = Mathf.Max(0, Mathf.FloorToInt(slice.StartZ - generationStartZ));

        SpawnMushroomRow(slice, score);
        SpawnUpperLevels(slice, score);
    }

    void SpawnMushroomRow(GridSlice slice, int score)
    {
        if (!ShouldSpawnMushroomRow(score))
        {
            consecutiveEmptyMushroomRows++;
            return;
        }

        consecutiveEmptyMushroomRows = 0;
        int mushroomCount = RandomRangeInclusive(
            generationProfile.MinimumMushroomsPerActiveRow,
            generationProfile.MaximumMushroomsPerActiveRow);

        FillColumnScratch();
        for (int count = 0; count < mushroomCount && columnScratch.Count > 0; count++)
        {
            int columnIndex = RandomRangeInclusive(0, columnScratch.Count - 1);
            int column = columnScratch[columnIndex];
            columnScratch.RemoveAt(columnIndex);

            GridSpawnEntry entry = PickEntry(generationProfile.MushroomEntries, score);
            SpawnEntry(entry, GetCellPosition(column, 0, slice.StartZ) + GetMushroomPlacementOffset(), slice);
        }
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
        for (int column = 0; column < 3; column++)
        {
            for (int level = 1; level <= 2; level++)
            {
                GridSpawnEntry entry = PickUpperLevelEntry(score);
                SpawnEntry(entry, GetCellPosition(column, level, slice.StartZ), slice);
            }
        }
    }

    GridSpawnEntry PickUpperLevelEntry(int score)
    {
        bool hasCoins = HasAvailableEntry(generationProfile.CoinEntries, score);
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

        return PickEntry(generationProfile.CoinEntries, score);
    }

    void SpawnEntry(GridSpawnEntry entry, Vector3 position, GridSlice slice)
    {
        if (entry == null || entry.Prefab == null)
        {
            return;
        }

        GameObject instance = GetInstance(entry.Prefab, entry.UsePooling);
        if (instance == null)
        {
            return;
        }

        instance.transform.SetParent(spawnRoot, false);
        instance.transform.position = position + entry.LocalOffset;
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = entry.LocalScale;
        ApplyMushroomProfile(instance, entry.BounceProfileOverride);

        slice.SpawnedObjects.Add(new SpawnedRuntime(entry.Prefab, instance, entry.UsePooling));
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
        if (usePooling && pools.TryGetValue(prefab, out Stack<GameObject> pool) && pool.Count > 0)
        {
            GameObject pooled = pool.Pop();
            pooled.SetActive(true);
            return pooled;
        }

        GameObject created = Instantiate(prefab);
        created.SetActive(true);
        return created;
    }

    void RecycleSlice(GridSlice slice)
    {
        for (int index = 0; index < slice.SpawnedObjects.Count; index++)
        {
            SpawnedRuntime spawned = slice.SpawnedObjects[index];
            if (spawned.Instance == null)
            {
                continue;
            }

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
            else if (Application.isPlaying)
            {
                Destroy(spawned.Instance);
            }
            else
            {
                DestroyImmediate(spawned.Instance);
            }
        }
    }

    GridSpawnEntry PickEntry(IReadOnlyList<GridSpawnEntry> entries, int score)
    {
        if (entries == null || entries.Count == 0)
        {
            return null;
        }

        float totalWeight = 0f;
        for (int index = 0; index < entries.Count; index++)
        {
            GridSpawnEntry entry = entries[index];
            if (entry != null && entry.IsAvailable(score))
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
            if (entry == null || !entry.IsAvailable(score))
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
