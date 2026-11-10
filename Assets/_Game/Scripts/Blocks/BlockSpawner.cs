using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

/// <summary>
/// Spawns environment blocks sequentially along the Z-axis.
/// Tracks a running Z cursor so each block is placed exactly where the last one ended.
/// Spawning is triggered by the player's world Z position crossing a threshold ahead
/// of the last spawned block.
/// </summary>
public class BlockSpawner : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector
    // -------------------------------------------------------------------------

    [Header("Block List (Sequential)")]
    [Tooltip("Ordered list of blocks to spawn. Spawner walks through this list in order.")]
    [SerializeField] private List<BlockData> blockSequence = new();

    [Header("Player Reference")]
    [Tooltip("The player Transform used to determine when to spawn the next block.")]
    [SerializeField] private Transform player;

    [Header("Spawn Settings")]
    [Tooltip("Spawn the next block when the player is within this many units of the END of the last spawned block.")]
    [SerializeField] private float spawnAheadDistance = 60f;

    [Tooltip("World Z position where the very first block will be placed.")]
    [SerializeField] private float initialSpawnZ = 0f;

    [Tooltip("Shared world-Z endpoint, in world units. Match Maximum Distance on the Grid Course Generation Profile so both stop at the same boundary, regardless of starting offsets. 0 means unlimited. The final block is kept whole.")]
    [SerializeField, Min(0f)] private float maximumDistance = 0f;

    [Tooltip("How many blocks to pre-spawn at startup before the player starts moving.")]
    [SerializeField] private int preSpawnCount = 3;

    [Header("Despawn Settings")]
    [Tooltip("Destroy a block when the player is this many units PAST its end (i.e. behind the player).")]
    [SerializeField] private float despawnBehindDistance = 20f;

    [Tooltip("Whether to loop back to the first block after the last one is spawned.")]
    [SerializeField] private bool loopSequence = true;

    [Header("Block Reuse")]
    [Tooltip("Keep a bounded number of recently despawned blocks for reuse on later loops or restarts.")]
    [SerializeField] private bool poolDespawnedBlocks = true;
    [Tooltip("Global cap for inactive environment chunks. One slot per distinct authored prefab avoids recreating expensive blocks on later runs; higher caps trade memory for smoother restarts.")]
    [SerializeField, Min(0)] private int maxPooledBlocks = 6;
    [SerializeField, Min(0)] private int maxPooledPerPrefab = 2;
    [SerializeField, Tooltip("Keep one inactive instance of each distinct environment prefab where possible. " +
                             "Avoids repeated giant prefab instantiation after a reset. Disable to enforce the manual global cap.")]
    private bool automaticallyCoverBlockSequence = true;

    [Header("First-Use Asset Preloading")]
    [Tooltip("At scene startup, touch shared meshes, materials and shaders for upcoming environment prefabs. " +
             "Does not instantiate blocks or duplicate materials.")]
    [SerializeField] private bool preloadFutureBlockRenderAssets = true;

    private static readonly ProfilerMarker PreloadMarker =
        new ProfilerMarker("FunGuy.BlockSpawner.PreloadFutureBlockRenderAssets");
    private static readonly ProfilerMarker InstantiateMarker =
        new ProfilerMarker("FunGuy.BlockSpawner.InstantiatePrefab");
    private static readonly ProfilerMarker ReactivateMarker =
        new ProfilerMarker("FunGuy.BlockSpawner.ReactivatePrefab");

    public int PreloadedFuturePrefabCount { get; private set; }
    public int PreloadedFutureMeshCount { get; private set; }
    public int PreloadedFutureMaterialCount { get; private set; }
    public float PreloadDurationMilliseconds { get; private set; }

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    /// <summary>The Z world position where the NEXT block should be placed.</summary>
    private float _nextSpawnZ;

    /// <summary>Index into blockSequence for the next block to spawn.</summary>
    private int _sequenceIndex = 0;

    /// <summary>Tracks every live block so we can despawn old ones.</summary>
    private readonly List<SpawnedBlock> _activeBlocks = new();
    private readonly Dictionary<GameObject, Stack<GameObject>> _pooledBlocks = new();
    private int _pooledBlockCount;
    private int _effectivePoolCapacity;

    public int EffectivePoolCapacity => _effectivePoolCapacity;
    public int TotalInstancesCreated { get; private set; }
    public int TotalPoolHits { get; private set; }
    public int TotalInstancesDestroyed { get; private set; }
    public int TotalPoolStores { get; private set; }

    /// <summary>True once we've exhausted the sequence and looping is disabled.</summary>
    private bool _sequenceFinished = false;
    private bool _initialized;
    private bool _runFlowManaged;

    public bool IsRunFlowManaged => _runFlowManaged;

    public float MaximumDistance => Mathf.Max(0f, maximumDistance);

    public float GenerationEndZ => MaximumDistance > 0f ? MaximumDistance : float.PositiveInfinity;

    // -------------------------------------------------------------------------
    // Internal record: a spawned block instance + its Z boundaries
    // -------------------------------------------------------------------------
    private readonly struct SpawnedBlock
    {
        public readonly GameObject Prefab;
        public readonly GameObject Instance;
        public readonly float ZStart;
        public readonly float ZEnd;

        public SpawnedBlock(GameObject prefab, GameObject instance, float zStart, float zEnd)
        {
            Prefab = prefab;
            Instance = instance;
            ZStart   = zStart;
            ZEnd     = zEnd;
        }
    }

    // -------------------------------------------------------------------------
    // Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        if (player == null)
            Debug.LogWarning("[BlockSpawner] No player Transform assigned. Spawner will not work.");

        if (blockSequence == null || blockSequence.Count == 0)
            Debug.LogWarning("[BlockSpawner] Block sequence is empty. Nothing will spawn.");

        UpdatePoolCapacity();

        if (Application.isPlaying && preloadFutureBlockRenderAssets)
            PreloadFutureBlockRenderAssets();
    }

    /// <summary>
    /// Touch shared render assets once, before streaming begins. The initial
    /// pre-spawned prefabs are already loaded during normal initialization, so
    /// only later distinct prefabs need preloading.
    /// Does not precompile shader variants or change renderer activation.
    /// </summary>
    private void PreloadFutureBlockRenderAssets()
    {
        if (blockSequence == null || blockSequence.Count == 0)
            return;

        using (PreloadMarker.Auto())
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var initialPrefabs = new HashSet<GameObject>();
            int initialCount = Mathf.Min(Mathf.Max(0, preSpawnCount), blockSequence.Count);
            for (int i = 0; i < initialCount; i++)
            {
                BlockData data = blockSequence[i];
                if (data != null && data.prefab != null)
                    initialPrefabs.Add(data.prefab);
            }

            var visitedPrefabs = new HashSet<GameObject>();
            var visitedMaterials = new HashSet<Material>();
            var visitedMeshes = new HashSet<Mesh>();
            for (int i = initialCount; i < blockSequence.Count; i++)
            {
                BlockData data = blockSequence[i];
                GameObject prefab = data != null ? data.prefab : null;
                if (prefab == null || initialPrefabs.Contains(prefab) || !visitedPrefabs.Add(prefab))
                    continue;

                foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != null && visitedMaterials.Add(material))
                            _ = material.shader;
                    }

                    if (renderer is SkinnedMeshRenderer skin)
                        TouchMeshMetadata(skin.sharedMesh, visitedMeshes);
                }

                foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                    TouchMeshMetadata(filter.sharedMesh, visitedMeshes);

                foreach (MeshCollider collider in prefab.GetComponentsInChildren<MeshCollider>(true))
                    TouchMeshMetadata(collider.sharedMesh, visitedMeshes);
            }

            PreloadedFuturePrefabCount = visitedPrefabs.Count;
            PreloadedFutureMaterialCount = visitedMaterials.Count;
            PreloadedFutureMeshCount = visitedMeshes.Count;
            PreloadDurationMilliseconds = (float)stopwatch.Elapsed.TotalMilliseconds;
        }
    }

    private static void TouchMeshMetadata(Mesh mesh, HashSet<Mesh> visitedMeshes)
    {
        if (mesh != null && visitedMeshes.Add(mesh))
            _ = mesh.vertexCount; // Metadata only; avoid duplicating mesh CPU data.
    }

    private void Start()
    {
        if (!_runFlowManaged && !_initialized) ResetSpawner();
    }

    private void Update()
    {
        if (!_runFlowManaged) UpdateStreaming();
    }

    public void SetRunFlowManaged(bool managed) => _runFlowManaged = managed;

    public void SetStreamingTarget(Transform target) => player = target;

    public void UpdateStreaming()
    {
        if (!_initialized || player == null) return;
        CheckAndSpawn();
        CheckAndDespawn();
    }

    // Called before grid generation so short blocks cannot leave its probes
    // looking beyond the environment's normal player-relative lookahead.
    public void EnsureSpawnedThrough(float worldZ)
    {
        if (!_initialized) ResetSpawner();
        while (!_sequenceFinished && _nextSpawnZ <= worldZ)
        {
            if (!TrySpawnNext()) break;
        }
    }

    // -------------------------------------------------------------------------
    // Spawn logic
    // -------------------------------------------------------------------------

    /// <summary>
    /// Spawns the next block if the player is close enough to the end of the
    /// last spawned block.
    /// </summary>
    private void CheckAndSpawn()
    {
        // Keep spawning until we're far enough ahead of the player.
        // This handles cases where spawnAheadDistance is large or the player
        // teleports forward.
        EnsureSpawnedThrough(player.position.z + spawnAheadDistance);
    }

    /// <summary>
    /// Attempts to spawn the next block in the sequence.
    /// Returns false if there are no more blocks and looping is disabled.
    /// </summary>
    private bool TrySpawnNext()
    {
        if (blockSequence == null || blockSequence.Count == 0 || _sequenceFinished)
            return false;

        if (_nextSpawnZ >= GenerationEndZ)
            return false;

        // Bound skips so a looping sequence containing only invalid entries
        // cannot hang initial generation or a catch-up streaming frame.
        for (int attempt = 0; attempt < blockSequence.Count; attempt++)
        {
            if (_sequenceIndex >= blockSequence.Count)
            {
                if (loopSequence) _sequenceIndex = 0;
                else
                {
                    _sequenceFinished = true;
                    return false;
                }
            }

            int entryIndex = _sequenceIndex++;
            BlockData data = blockSequence[entryIndex];
            if (data == null || data.prefab == null || data.zLength <= 0f
                || float.IsNaN(data.zLength) || float.IsInfinity(data.zLength))
            {
                Debug.LogWarning($"[BlockSpawner] Entry {entryIndex} needs a prefab and a finite positive length. Skipping.", this);
                continue;
            }

            Vector3 spawnPosition = new Vector3(0f, 0f, _nextSpawnZ);
            GameObject instance = AcquireBlock(data.prefab, spawnPosition);
            instance.name = $"{data.prefab.name}_Z{_nextSpawnZ:F0}";
            float blockEnd = _nextSpawnZ + data.zLength;
            _activeBlocks.Add(new SpawnedBlock(data.prefab, instance, _nextSpawnZ, blockEnd));
            _nextSpawnZ = blockEnd;
            return true;
        }

        _sequenceFinished = true;
        return false;
    }

    // -------------------------------------------------------------------------
    // Despawn logic
    // -------------------------------------------------------------------------

    /// <summary>
    /// Destroys blocks that are far enough behind the player to no longer be needed.
    /// </summary>
    private void CheckAndDespawn()
    {
        for (int i = _activeBlocks.Count - 1; i >= 0; i--)
        {
            SpawnedBlock block = _activeBlocks[i];

            // Despawn when the player has moved past the block's end by the threshold.
            if (player.position.z - block.ZEnd > despawnBehindDistance)
            {
                RecycleBlock(block);
                _activeBlocks.RemoveAt(i);
            }
        }
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resets the spawner: destroys all active blocks and restarts from the beginning.
    /// Useful for game restarts or level reloads.
    /// </summary>
    public void ResetSpawner()
    {
        UpdatePoolCapacity();
        ClearSpawnedBlocks();
        _initialized = true;

        for (int i = 0; i < preSpawnCount; i++)
        {
            if (!TrySpawnNext())
                break;
        }

    }

    public void ClearSpawnedBlocks()
    {
        foreach (SpawnedBlock block in _activeBlocks) RecycleBlock(block);
        _activeBlocks.Clear();
        _sequenceIndex = 0;
        _nextSpawnZ = initialSpawnZ;
        _sequenceFinished = false;
        _initialized = false;
    }

    private void UpdatePoolCapacity()
    {
        _effectivePoolCapacity = Mathf.Max(0, maxPooledBlocks);
        if (!automaticallyCoverBlockSequence || !poolDespawnedBlocks || blockSequence == null)
            return;

        // Authored sequences often contain many heavyweight unique chunks.
        // A cap smaller than the distinct prefab count permanently reserves all
        // slots for the early chunks, forcing later chunks to be destroyed every
        // run even though their prefabs repeat on the next run.
        var unique = new HashSet<GameObject>();
        foreach (BlockData data in blockSequence)
        {
            if (data != null && data.prefab != null)
                unique.Add(data.prefab);
        }
        _effectivePoolCapacity = Mathf.Max(_effectivePoolCapacity, unique.Count);
    }

    private GameObject AcquireBlock(GameObject prefab, Vector3 spawnPosition)
    {
        if (Application.isPlaying && poolDespawnedBlocks && _pooledBlocks.TryGetValue(prefab, out var pool))
        {
            while (pool.Count > 0)
            {
                GameObject instance = pool.Pop();
                _pooledBlockCount--;
                if (instance == null) continue; // May have been destroyed by another system.
                Transform blockTransform = instance.transform;
                blockTransform.SetParent(transform, true);
                blockTransform.SetPositionAndRotation(spawnPosition, Quaternion.identity);
                blockTransform.localScale = prefab.transform.localScale;
                using (ReactivateMarker.Auto())
                    instance.SetActive(true); // Enable after positioning: child OnEnable uses world positions.
                TotalPoolHits++;
                return instance;
            }
        }

        GameObject created;
        using (InstantiateMarker.Auto())
            created = Instantiate(prefab, spawnPosition, Quaternion.identity, transform);
        TotalInstancesCreated++;
        return created;
    }

    private void RecycleBlock(SpawnedBlock block)
    {
        GameObject instance = block.Instance;
        if (instance == null) return;
        // Destroy is deferred in play mode. Hide old colliders immediately so
        // the grid's same-frame surface probes only see the rebuilt world.
        instance.SetActive(false);

        if (Application.isPlaying && poolDespawnedBlocks && block.Prefab != null &&
            _pooledBlockCount < _effectivePoolCapacity && maxPooledPerPrefab > 0)
        {
            if (!_pooledBlocks.TryGetValue(block.Prefab, out var pool))
            {
                pool = new Stack<GameObject>();
                _pooledBlocks.Add(block.Prefab, pool);
            }
            if (pool.Count < maxPooledPerPrefab)
            {
                pool.Push(instance);
                _pooledBlockCount++;
                TotalPoolStores++;
                return;
            }
        }

        TotalInstancesDestroyed++;
        if (Application.isPlaying) Destroy(instance);
        else DestroyImmediate(instance);
    }

    private void OnDestroy()
    {
        // Inactive pooled blocks remain parented here, so Unity destroys them with the spawner.
        _pooledBlocks.Clear();
        _pooledBlockCount = 0;
    }

    /// <summary>Returns how many blocks are currently alive in the scene.</summary>
    public int ActiveBlockCount => _activeBlocks.Count;

    /// <summary>Inactive environment instances retained for reuse; bounded by maxPooledBlocks.</summary>
    public int PooledBlockCount => _pooledBlockCount;

    /// <summary>Returns the Z position where the next block will be placed.</summary>
    public float NextSpawnZ => _nextSpawnZ;

    // -------------------------------------------------------------------------
    // Gizmos (editor visualisation)
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Draw each active block's Z range as a wire cube.
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.4f);
        foreach (SpawnedBlock block in _activeBlocks)
        {
            float length = block.ZEnd - block.ZStart;
            Vector3 center = new Vector3(0f, 0f, block.ZStart + length * 0.5f);
            Gizmos.DrawWireCube(center, new Vector3(5f, 5f, length));
        }

        // Draw the next spawn position.
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(new Vector3(0f, 0f, _nextSpawnZ), 2f);

        // Draw the spawn-ahead threshold from the player.
        if (player != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                new Vector3(-10f, 0f, player.position.z + spawnAheadDistance),
                new Vector3( 10f, 0f, player.position.z + spawnAheadDistance)
            );
        }
    }
#endif
}
