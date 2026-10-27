using System.Collections.Generic;
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

    [Tooltip("How many blocks to pre-spawn at startup before the player starts moving.")]
    [SerializeField] private int preSpawnCount = 3;

    [Header("Despawn Settings")]
    [Tooltip("Destroy a block when the player is this many units PAST its end (i.e. behind the player).")]
    [SerializeField] private float despawnBehindDistance = 20f;

    [Tooltip("Whether to loop back to the first block after the last one is spawned.")]
    [SerializeField] private bool loopSequence = true;

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    /// <summary>The Z world position where the NEXT block should be placed.</summary>
    private float _nextSpawnZ;

    /// <summary>Index into blockSequence for the next block to spawn.</summary>
    private int _sequenceIndex = 0;

    /// <summary>Tracks every live block so we can despawn old ones.</summary>
    private readonly List<SpawnedBlock> _activeBlocks = new();

    /// <summary>True once we've exhausted the sequence and looping is disabled.</summary>
    private bool _sequenceFinished = false;
    private bool _initialized;
    private bool _runFlowManaged;

    public bool IsRunFlowManaged => _runFlowManaged;

    // -------------------------------------------------------------------------
    // Internal record: a spawned block instance + its Z boundaries
    // -------------------------------------------------------------------------
    private class SpawnedBlock
    {
        public GameObject Instance;
        public float ZStart;
        public float ZEnd;

        public SpawnedBlock(GameObject instance, float zStart, float zEnd)
        {
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
            GameObject instance = Instantiate(data.prefab, spawnPosition, Quaternion.identity, transform);
            instance.name = $"{data.prefab.name}_Z{_nextSpawnZ:F0}";
            float blockEnd = _nextSpawnZ + data.zLength;
            _activeBlocks.Add(new SpawnedBlock(instance, _nextSpawnZ, blockEnd));
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
                RemoveBlock(block.Instance);
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
        foreach (SpawnedBlock block in _activeBlocks) RemoveBlock(block.Instance);
        _activeBlocks.Clear();
        _sequenceIndex = 0;
        _nextSpawnZ = initialSpawnZ;
        _sequenceFinished = false;
        _initialized = false;
    }

    static void RemoveBlock(GameObject instance)
    {
        if (instance == null) return;
        // Destroy is deferred in play mode. Hide old colliders immediately so
        // the grid's same-frame surface probes only see the rebuilt world.
        instance.SetActive(false);
        if (Application.isPlaying) Destroy(instance);
        else DestroyImmediate(instance);
    }

    /// <summary>Returns how many blocks are currently alive in the scene.</summary>
    public int ActiveBlockCount => _activeBlocks.Count;

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
