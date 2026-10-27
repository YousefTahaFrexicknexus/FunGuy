using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunFlowCoordinator : MonoBehaviour
{
    [SerializeField, Tooltip("Player reset and announced by this run flow.")]
    MushroomRunnerPlayer player;
    [SerializeField, Tooltip("Environment generated before the grid on every start, reset, and streaming update. If empty, resolves the unique active BlockSpawner in this scene.")]
    BlockSpawner blockSpawner;
    [SerializeField, Tooltip("Grid generated after the environment. Initial Start Z is added to the run spawn Z. If empty, resolves the unique active grid streamer in this scene.")]
    GridCourseStreamer gridStreamer;
    [SerializeField, Tooltip("Optional authored spawn transform. If empty, the coordinator falls back to the cached initial player pose.")]
    Transform spawnPoint;
    [SerializeField, Tooltip("If enabled, the player's initial transform is cached as the fallback spawn pose.")]
    bool captureInitialPlayerPose = true;
    [SerializeField, Tooltip("If enabled, the first run begins automatically on Start.")]
    bool initializeRunOnStart = true;

    Vector3 cachedSpawnPosition;
    Quaternion cachedSpawnRotation = Quaternion.identity;
    bool hasCachedSpawnPose;
    int resetCount;
    bool hasStartedRun;
    bool isRebuilding;
    bool ownsGeneration;

    void Reset()
    {
        CacheSpawnPoseIfNeeded();
    }

    void Awake()
    {
        ResolveReferences();
        TakeGenerationControl();
        CacheSpawnPoseIfNeeded();
    }

    void OnEnable()
    {
        GameplayEvents.GameplayReset += ResetRun;
    }

    void OnDisable()
    {
        GameplayEvents.GameplayReset -= ResetRun;
    }

    void OnDestroy()
    {
        ReleaseGenerationControl();
    }

    void Update()
    {
        if (!hasStartedRun || isRebuilding || !HasRequiredReferences()) return;
        blockSpawner.EnsureSpawnedThrough(gridStreamer.RequiredEnvironmentEndZ);
        blockSpawner.UpdateStreaming();
        gridStreamer.UpdateStreaming();
    }

    void Start()
    {
        if (initializeRunOnStart && !hasStartedRun)
        {
            StartRun();
        }
    }

    public void StartRun()
    {
        RunInternal(isStartEvent: true);
    }

    public void ResetRun()
    {
        // A reset requested before Start initializes once; Start then does no work.
        RunInternal(isStartEvent: !hasStartedRun);
    }

    public void ReportFailure(RunFailureReason reason)
    {
        if (isRebuilding || !HasRequiredReferences())
        {
            return;
        }

        MushroomRunnerEvents.RaiseRunFailed(new RunFailedEvent(player, reason, player.transform.position));
        // One reset request updates gameplay state and rebuilds this world once.
        GameplayEvents.GameplayReset?.Invoke();
    }

    public void SetSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
        hasCachedSpawnPose = false;
        CacheSpawnPoseIfNeeded();
    }

    public void Configure(MushroomRunnerPlayer playerController, BlockSpawner environmentSpawner, GridCourseStreamer courseStreamer)
    {
        ReleaseGenerationControl();
        if (player != playerController) hasCachedSpawnPose = false;
        player = playerController;
        blockSpawner = environmentSpawner;
        gridStreamer = courseStreamer;
        TakeGenerationControl();
        CacheSpawnPoseIfNeeded();
    }

    public void SetSpawnPose(Vector3 worldPosition, Quaternion worldRotation)
    {
        cachedSpawnPosition = worldPosition;
        cachedSpawnRotation = worldRotation;
        hasCachedSpawnPose = true;
    }

    bool HasRequiredReferences()
    {
        return player != null && blockSpawner != null && gridStreamer != null;
    }

    void ResolveReferences()
    {
        // Resolve once at startup for existing scenes; normal run/reset/update
        // paths use cached typed references and never reflection-based scans.
        if (player == null) player = FindSceneComponent<MushroomRunnerPlayer>();
        if (blockSpawner == null) blockSpawner = FindSceneComponent<BlockSpawner>();
        if (gridStreamer == null) gridStreamer = FindSceneComponent<GridCourseStreamer>();
    }

    T FindSceneComponent<T>() where T : Behaviour
    {
        T result = null;
        foreach (T candidate in FindObjectsByType<T>(FindObjectsSortMode.None))
        {
            if (!candidate.isActiveAndEnabled || candidate.gameObject.scene != gameObject.scene) continue;
            if (result != null)
            {
                Debug.LogWarning($"[RunFlowCoordinator] Multiple active {typeof(T).Name} components in this scene. Assign the intended reference explicitly.", this);
                return null;
            }
            result = candidate;
        }
        return result;
    }

    void TakeGenerationControl()
    {
        if (!HasRequiredReferences()) return;
        // Awake claims both before any scene component's Start can self-build.
        blockSpawner.SetRunFlowManaged(true);
        gridStreamer.SetRunFlowManaged(true);
        ownsGeneration = true;
        blockSpawner.SetStreamingTarget(player.transform);
        gridStreamer.SetStreamingTarget(player.transform);
    }

    void ReleaseGenerationControl()
    {
        if (!ownsGeneration) return;
        if (blockSpawner != null) blockSpawner.SetRunFlowManaged(false);
        if (gridStreamer != null) gridStreamer.SetRunFlowManaged(false);
        ownsGeneration = false;
    }

    void CacheSpawnPoseIfNeeded()
    {
        if (hasCachedSpawnPose)
        {
            return;
        }

        if (spawnPoint != null)
        {
            cachedSpawnPosition = spawnPoint.position;
            cachedSpawnRotation = spawnPoint.rotation;
            hasCachedSpawnPose = true;
            return;
        }

        if (captureInitialPlayerPose && player != null)
        {
            cachedSpawnPosition = player.transform.position;
            cachedSpawnRotation = player.transform.rotation;
            hasCachedSpawnPose = true;
        }
    }

    void RunInternal(bool isStartEvent)
    {
        if (isRebuilding) return;
        if (!HasRequiredReferences())
        {
            Debug.LogWarning("[RunFlowCoordinator] Assign a player, BlockSpawner, and GridCourseStreamer before starting the run.", this);
            return;
        }

        isRebuilding = true;
        try
        {
            TakeGenerationControl();
            CacheSpawnPoseIfNeeded();
            player.SetState(MushroomRunnerPlayer.PlayerState.Disabled);
            gridStreamer.ClearGeneratedWorld();

            float gridStartZ = cachedSpawnPosition.z + gridStreamer.InitialStartZ;
            blockSpawner.ResetSpawner();
            blockSpawner.EnsureSpawnedThrough(gridStreamer.GetInitialEnvironmentEndZ(gridStartZ));
            gridStreamer.BuildInitialGrid(gridStartZ);
            player.ResetRun(cachedSpawnPosition, cachedSpawnRotation);

            resetCount = isStartEvent ? 0 : resetCount + 1;
            hasStartedRun = true;
            RunLifecycleEvent lifecycleEvent = new(player, cachedSpawnPosition, cachedSpawnRotation, resetCount);
            if (isStartEvent) MushroomRunnerEvents.RaiseRunStarted(lifecycleEvent);
            else MushroomRunnerEvents.RaiseRunReset(lifecycleEvent);
        }
        finally
        {
            isRebuilding = false;
        }
    }
}
