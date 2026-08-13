using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunFlowCoordinator : MonoBehaviour
{
    [SerializeField, Tooltip("Player reset and announced by this run flow.")]
    MushroomRunnerPlayer player;
    [SerializeField, Tooltip("Course streamer rebuilt whenever the run starts or resets.")]
    RunnerCourseStreamer areaStreamer;
    [SerializeField, Tooltip("Optional bridge that resets older BlockSpawner content with the new run loop.")]
    LegacyEnvironmentResetAdapter legacyEnvironmentResetAdapter;
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

    void Reset()
    {
        CacheSpawnPoseIfNeeded();
    }

    void Awake()
    {
        CacheSpawnPoseIfNeeded();
    }

    void Start()
    {
        if (initializeRunOnStart)
        {
            StartRun();
        }
    }

    public void StartRun()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogWarning("[RunFlowCoordinator] Cannot start the run because the player reference is missing.");
            return;
        }

        resetCount = 0;
        RunInternal(isStartEvent: true);
    }

    public void ResetRun()
    {
        if (!HasRequiredReferences())
        {
            Debug.LogWarning("[RunFlowCoordinator] Cannot reset the run because the player reference is missing.");
            return;
        }

        resetCount++;
        RunInternal(isStartEvent: false);
    }

    public void ReportFailure(RunFailureReason reason)
    {
        if (!HasRequiredReferences())
        {
            Debug.LogWarning($"[RunFlowCoordinator] Cannot report failure '{reason}' because the player reference is missing.");
            return;
        }

        MushroomRunnerEvents.RaiseRunFailed(new RunFailedEvent(player, reason, player.transform.position));
        ResetRun();
    }

    public void SetSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
        hasCachedSpawnPose = false;
        CacheSpawnPoseIfNeeded();
    }

    public void Configure(MushroomRunnerPlayer playerController, RunnerCourseStreamer streamer, LegacyEnvironmentResetAdapter environmentResetAdapter = null)
    {
        player = playerController;
        areaStreamer = streamer;
        legacyEnvironmentResetAdapter = environmentResetAdapter;

        if (!hasCachedSpawnPose)
        {
            CacheSpawnPoseIfNeeded();
        }
    }

    public void SetSpawnPose(Vector3 worldPosition, Quaternion worldRotation)
    {
        cachedSpawnPosition = worldPosition;
        cachedSpawnRotation = worldRotation;
        hasCachedSpawnPose = true;
    }

    bool HasRequiredReferences()
    {
        return player != null;
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
        CacheSpawnPoseIfNeeded();
        player.ResetRun(cachedSpawnPosition, cachedSpawnRotation);

        if (areaStreamer != null)
        {
            areaStreamer.BuildInitialWorld();
        }

        legacyEnvironmentResetAdapter?.ResetEnvironment();

        RunLifecycleEvent lifecycleEvent = new(player, cachedSpawnPosition, cachedSpawnRotation, resetCount);

        if (isStartEvent)
        {
            MushroomRunnerEvents.RaiseRunStarted(lifecycleEvent);
        }
        else
        {
            MushroomRunnerEvents.RaiseRunReset(lifecycleEvent);
        }
    }
}