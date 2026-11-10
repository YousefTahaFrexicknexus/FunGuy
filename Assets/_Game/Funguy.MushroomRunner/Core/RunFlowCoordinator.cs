using System;
using System.Collections;
using System.IO;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

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
    [SerializeField, Tooltip("Optional FunGuy logo wipe on death; other resets stay immediate.")]
    FunGuyDeathTransition deathTransition;
    [SerializeField, Tooltip("If enabled, the player's initial transform is cached as the fallback spawn pose.")]
    bool captureInitialPlayerPose = true;
    [SerializeField, Tooltip("If enabled, the first run begins automatically on Start.")]
    bool initializeRunOnStart = true;

    [Header("Startup Loading / Staged Warm-up")]
    [SerializeField, Tooltip("Warm the existing streamed world and its prefab pools before enabling the first run. " +
                             "Loading uses the authored FunGuy logo. Disable to preserve immediate startup.")]
    bool stageWorldBeforeFirstRun = true;
    [SerializeField, Range(1, 24), Tooltip("Maximum grid-cell advances processed per frame while preloading.")]
    int warmupMaxStepsPerFrame = 6;
    [SerializeField, Range(0.5f, 12f), Tooltip("Soft CPU time budget (milliseconds) for each frame of streamed warm-up.")]
    float warmupMillisecondsPerFrame = 3f;
    [SerializeField, Min(1f), Tooltip("Maximum seconds spent staging before falling back to normal gameplay startup.")]
    float maximumWarmupSeconds = 18f;
    [SerializeField, Min(64), Tooltip("Soft limit on extra reserved Unity memory during streaming preparation, in MiB. " +
                                     "This is NOT a GPU memory limit; monitor real GPU memory separately.")]
    int maximumExtraReservedMemoryMiB = 768;
    [SerializeField, Tooltip("Optional collection of project-specific shader variants to prewarm during loading. " +
                             "Leave empty until actual game variants are recorded; broad WarmupAllShaders is avoided.")]
    ShaderVariantCollection gameplayShaderVariants;
    [SerializeField, Range(1, 128), Tooltip("Shader variants prepared per loading frame if a collection is assigned.")]
    int shaderVariantsPerFrame = 16;
    [SerializeField, Tooltip("Prewarm the captured DX12 pipeline states before first gameplay on Windows when graphics API and quality profile match.")]
    bool prewarmRecordedWindowsGraphicsStates = true;
    [SerializeField, Range(1, 64), Tooltip("DX12 pipeline states to schedule per loading-frame warmup batch.")]
    int graphicsStateBatchSize = 12;

    Vector3 cachedSpawnPosition;
    Quaternion cachedSpawnRotation = Quaternion.identity;
    bool hasCachedSpawnPose;
    int resetCount;
    bool hasStartedRun;
    bool isRebuilding;
    bool ownsGeneration;
    bool isStartupWarming;
    bool startupReady;
    bool startupHitMemoryBudget;
    Coroutine startupWarmupRoutine;
    GameObject startupStreamCursor;
    FunGuyStartupLoadingOverlay startupOverlay;
    float startupProgress;
    string startupStage = "Not started";
    float startupDurationMilliseconds;

    public bool IsStartupReady => startupReady && !isStartupWarming;
    public bool IsStartupWarming => isStartupWarming;
    public bool StartupHitMemoryBudget => startupHitMemoryBudget;
    public float StartupProgress => startupProgress;
    public string StartupStage => startupStage;
    public float StartupDurationMilliseconds => startupDurationMilliseconds;

    void Reset()
    {
        CacheSpawnPoseIfNeeded();
    }

    void Awake()
    {
        if (deathTransition == null) deathTransition = GetComponent<FunGuyDeathTransition>();
        ResolveReferences();
        TakeGenerationControl();
        CacheSpawnPoseIfNeeded();
        if (initializeRunOnStart && stageWorldBeforeFirstRun && HasRequiredReferences())
            player.SetState(MushroomRunnerPlayer.PlayerState.Disabled);
    }

    void OnEnable()
    {
        GameplayEvents.GameplayReset += ResetRun;
    }

    void OnDisable()
    {
        GameplayEvents.GameplayReset -= ResetRun;
        CancelStartupWarmup();
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
        if (!initializeRunOnStart || hasStartedRun)
            return;

        if (stageWorldBeforeFirstRun && HasRequiredReferences())
            startupWarmupRoutine = StartCoroutine(WarmupAndStartRun());
        else
            StartRun();
    }

    public void StartRun()
    {
        CancelStartupWarmup();
        RunInternal(isStartEvent: true);
    }

    public void ResetRun()
    {
        // External reset during preloading cancels the temporary stream cursor.
        CancelStartupWarmup();
        RunInternal(isStartEvent: !hasStartedRun);
    }

    public void ReportFailure(RunFailureReason reason)
    {
        if (isRebuilding || !HasRequiredReferences())
        {
            return;
        }

        // The death volume can report repeatedly while the player remains below the plane.
        // Do not fire duplicate failure events or reset before the logo has covered the screen.
        if (deathTransition != null && deathTransition.IsPlaying) return;

        MushroomRunnerEvents.RaiseRunFailed(new RunFailedEvent(player, reason, player.transform.position));

        // Intentional final-scene and manual resets bypass the death wipe.
        bool isPlayerDeath = reason != RunFailureReason.Unknown && reason != RunFailureReason.ManualReset;
        if (isPlayerDeath && deathTransition != null &&
            deathTransition.TryPlay(() => GameplayEvents.GameplayReset?.Invoke()))
        {
            player.SetState(MushroomRunnerPlayer.PlayerState.Disabled);
            return;
        }

        // Fall back to the existing immediate reset if no transition is assigned.
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

    /// <summary>
    /// Runs the existing world streamer over the course before starting gameplay.
    /// This uses precisely the objects/pools that the real run will use, so first
    /// instantiation and GPU-resident renderer registration occur behind the cover.
    /// Shader variants still need a recorded collection for proper GPU prewarming.
    /// </summary>
    IEnumerator WarmupAndStartRun()
    {
        isStartupWarming = true;
        startupReady = false;
        player.SetState(MushroomRunnerPlayer.PlayerState.Disabled);
        startupHitMemoryBudget = false;
        startupProgress = 0f;
        var total = System.Diagnostics.Stopwatch.StartNew();
        previousBackgroundValue = Application.runInBackground;
        backgroundOverrideActive = true;
        Application.runInBackground = true;

        try
        {
            startupOverlay = FunGuyStartupLoadingOverlay.AcquireForGameplay(
                gameObject.scene, deathTransition != null ? deathTransition.StartupLogo : null);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[FunGuy Warm-up] Loading overlay unavailable: " + ex.Message, this);
        }

        UpdateStartupProgress(0f, "Growing the forest...");
        // The loading cover must render before any large prefab is instantiated.
        yield return null;

        if (prewarmRecordedWindowsGraphicsStates)
            yield return WarmUpRecordedWindowsGraphicsStates(total);

        if (gameplayShaderVariants != null && gameplayShaderVariants.variantCount > 0)
        {
            UpdateStartupProgress(0.01f, "Preparing forest shaders...");
            bool shaderDone = false;
            while (!shaderDone && total.Elapsed.TotalSeconds < maximumWarmupSeconds)
            {
                try
                {
                    shaderDone = gameplayShaderVariants.WarmUpProgressively(
                        Mathf.Max(1, shaderVariantsPerFrame));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[FunGuy Warm-up] Shader warm-up skipped: " + ex.Message, this);
                    break;
                }

                float shaderFraction = gameplayShaderVariants.variantCount > 0
                    ? gameplayShaderVariants.warmedUpVariantCount / (float)gameplayShaderVariants.variantCount : 1f;
                UpdateStartupProgress(Mathf.Lerp(0.01f, 0.1f, shaderFraction),
                    "Preparing forest shaders...");
                yield return null;
            }
        }

        float warmEndZ = Mathf.Min(blockSpawner.GenerationEndZ, gridStreamer.GenerationEndZ);
        bool canTraverse = !float.IsInfinity(warmEndZ) && !float.IsNaN(warmEndZ) && warmEndZ > 0f;
        if (canTraverse)
        {
            try
            {
                // These stages do not emit RunStarted. The single real StartRun
                // happens only once after the world is reset to its true start.
                float gridStartZ = cachedSpawnPosition.z + gridStreamer.InitialStartZ;
                blockSpawner.ResetSpawner();
                blockSpawner.EnsureSpawnedThrough(gridStreamer.GetInitialEnvironmentEndZ(gridStartZ));
                gridStreamer.BuildInitialGrid(gridStartZ);

                startupStreamCursor = new GameObject("__FunGuyStartupWarmupCursor");
                startupStreamCursor.hideFlags = HideFlags.HideAndDontSave;
                startupStreamCursor.transform.position = cachedSpawnPosition;
                blockSpawner.SetStreamingTarget(startupStreamCursor.transform);
                gridStreamer.SetStreamingTarget(startupStreamCursor.transform);
            }
            catch (Exception ex)
            {
                canTraverse = false;
                Debug.LogWarning("[FunGuy Warm-up] World prep stopped; using normal start: " + ex, this);
            }
        }

        if (canTraverse)
        {
            float z = 0f;
            float stepLength = Mathf.Max(0.25f, gridStreamer.GridCellLength);
            int maxSteps = Mathf.Max(1, warmupMaxStepsPerFrame);
            float frameBudget = Mathf.Max(0.5f, warmupMillisecondsPerFrame);
            long reservedBefore = Profiler.GetTotalReservedMemoryLong();
            long allocatedBefore = Profiler.GetTotalAllocatedMemoryLong();
            int memoryBudgetMiB = Mathf.Max(64, maximumExtraReservedMemoryMiB);
            int ramMiB = SystemInfo.systemMemorySize;
            if (ramMiB > 0)
                memoryBudgetMiB = Mathf.Min(memoryBudgetMiB, Mathf.Max(64, ramMiB / 12));
            long memoryBudgetBytes = (long)memoryBudgetMiB * 1024L * 1024L;
            var frameTimer = new System.Diagnostics.Stopwatch();
            bool failed = false;

            while (z < warmEndZ && total.Elapsed.TotalSeconds < maximumWarmupSeconds)
            {
                frameTimer.Restart();
                for (int i = 0; i < maxSteps && z < warmEndZ; i++)
                {
                    // A single activation can exceed the soft time budget.
                    // That cost is paid behind the loading cover, not in gameplay.
                    if (i > 0 && frameTimer.Elapsed.TotalMilliseconds >= frameBudget)
                        break;

                    z = Mathf.Min(warmEndZ, z + stepLength);
                    Vector3 point = startupStreamCursor.transform.position;
                    point.z = cachedSpawnPosition.z + z;
                    startupStreamCursor.transform.position = point;

                    try
                    {
                        blockSpawner.EnsureSpawnedThrough(gridStreamer.RequiredEnvironmentEndZ);
                        blockSpawner.UpdateStreaming();
                        gridStreamer.UpdateStreaming();
                    }
                    catch (Exception ex)
                    {
                        failed = true;
                        Debug.LogWarning("[FunGuy Warm-up] Streaming stopped early: " + ex, this);
                        break;
                    }
                }

                UpdateStartupProgress(0.1f + 0.86f * Mathf.Clamp01(z / warmEndZ),
                    "Preparing the forest...");

                long extraReserved = Math.Max(0L, Profiler.GetTotalReservedMemoryLong() - reservedBefore);
                long extraAllocated = Math.Max(0L, Profiler.GetTotalAllocatedMemoryLong() - allocatedBefore);
                if (extraReserved > memoryBudgetBytes || extraAllocated > memoryBudgetBytes)
                {
                    startupHitMemoryBudget = true;
                    Debug.LogWarning("[FunGuy Warm-up] Reached soft additional-memory budget (" +
                        memoryBudgetMiB + " MiB) at Z=" + z.ToString("F0") +
                        ". Starting gameplay with the prepared resources.", this);
                    break;
                }
                if (failed)
                    break;
                yield return null;
            }

            Debug.Log("[FunGuy Warm-up] Prepared Z=" + z.ToString("F0") +
                "/" + warmEndZ.ToString("F0") +
                ", block instances=" + blockSpawner.TotalInstancesCreated +
                ", grid instances=" + gridStreamer.TotalInstancesCreated +
                ", pooled blocks=" + blockSpawner.PooledBlockCount +
                ", pooled grid=" + gridStreamer.PooledInstanceCount +
                ", elapsed=" + total.Elapsed.TotalMilliseconds.ToString("F0") + " ms.", this);
        }

        UpdateStartupProgress(0.98f, "Ready to play!");
        // Restore the real player target and clear generated world exactly once
        // through the existing run startup, not through a fake gameplay reset.
        CleanupStartupWarmup(keepOverlayVisible: true);
        startupWarmupRoutine = null;
        startupDurationMilliseconds = (float)total.Elapsed.TotalMilliseconds;
        RunInternal(isStartEvent: true);
        UpdateStartupProgress(1f, "Ready!");
        // Keep covering the scene until the initial ready frame has rendered.
        yield return null;
        startupOverlay?.Close();
        startupOverlay = null;
    }

    /// <summary>
    /// Progressively compile the specific D3D12 pipeline states captured from a
    /// real Windows Development Player. The recording is quality/API-specific:
    /// when the build uses another tier or device, safely skip this stage.
    /// </summary>
    IEnumerator WarmUpRecordedWindowsGraphicsStates(System.Diagnostics.Stopwatch startup)
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer ||
            SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
            yield break;

        string quality = QualitySettings.names[QualitySettings.GetQualityLevel()];
        string filename = quality == "PC"
            ? "PSO_D3D12_Windows_PC_20261110.graphicsstate"
            : quality == "Mobile"
                ? "PSO_D3D12_Windows_Mobile_20261110.graphicsstate"
                : null;
        if (filename == null)
            yield break;
        string collectionPath = Path.Combine(Application.streamingAssetsPath, "FunGuy", filename);
        if (!File.Exists(collectionPath))
            yield break;

        GraphicsStateCollection collection = null;
        bool loaded = false;
        try
        {
            collection = new GraphicsStateCollection();
            loaded = collection.LoadFromFile(collectionPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[FunGuy Warm-up] Could not load recorded DX12 graphics states: " +
                ex.Message, this);
        }

        if (!loaded || collection == null)
        {
            if (collection != null) Destroy(collection);
            yield break;
        }

        string activeQuality = QualitySettings.names[QualitySettings.GetQualityLevel()];
        if (collection.graphicsDeviceType != SystemInfo.graphicsDeviceType ||
            (!string.IsNullOrEmpty(collection.qualityLevelName) &&
             collection.qualityLevelName != activeQuality))
        {
            Debug.Log("[FunGuy Warm-up] Skipping recorded graphics states for " +
                collection.qualityLevelName + "/" + collection.graphicsDeviceType +
                " while running " + activeQuality + "/" + SystemInfo.graphicsDeviceType, this);
            Destroy(collection);
            yield break;
        }

        int totalStates = collection.totalGraphicsStateCount;
        if (totalStates < 1)
        {
            Destroy(collection);
            yield break;
        }

        UpdateStartupProgress(Mathf.Max(startupProgress, 0.01f), "Preparing GPU pipelines...");
        JobHandle pendingJob = default;
        bool pending = false;
        int lastCompleted = -1;
        int stagnant = 0;
        try
        {
            while (!collection.isWarmedUp &&
                   startup.Elapsed.TotalSeconds < maximumWarmupSeconds)
            {
                try
                {
                    pendingJob = collection.WarmUpProgressively(
                        Mathf.Max(1, graphicsStateBatchSize));
                    pending = true;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[FunGuy Warm-up] DX12 graphics-state warm-up skipped: " +
                        ex.Message, this);
                    break;
                }

                // Do not block the loading UI while the graphics driver works.
                while (!pendingJob.IsCompleted)
                    yield return null;

                pendingJob.Complete();
                pending = false;
                int completed = collection.completedWarmupCount;
                UpdateStartupProgress(0.01f + 0.09f *
                    Mathf.Clamp01(completed / (float)totalStates),
                    "Preparing GPU pipelines...");

                if (completed <= lastCompleted)
                {
                    if (++stagnant >= 3)
                    {
                        Debug.LogWarning("[FunGuy Warm-up] GPU pipeline progress stalled at " +
                            completed + "/" + totalStates + "; continuing safely.", this);
                        break;
                    }
                }
                else
                    stagnant = 0;

                lastCompleted = completed;
                yield return null;
            }

            Debug.Log("[FunGuy Warm-up] DX12 pipeline states warmed=" +
                collection.completedWarmupCount + "/" + totalStates +
                " quality=" + activeQuality + " elapsed=" +
                startup.Elapsed.TotalMilliseconds.ToString("F0") + " ms.", this);
        }
        finally
        {
            // If gameplay is cancelled mid-load, the job must finish before the
            // graphics-state collection backing its work is destroyed.
            if (pending) pendingJob.Complete();
            if (collection != null) Destroy(collection);
        }
    }

    private bool backgroundOverrideActive;
    private bool previousBackgroundValue;

    private void UpdateStartupProgress(float progress, string stage)
    {
        startupProgress = Mathf.Max(startupProgress, Mathf.Clamp01(progress));
        startupStage = stage;
        startupOverlay?.UpdateProgress(startupProgress, stage);
    }

    private void CleanupStartupWarmup(bool keepOverlayVisible = false)
    {
        if (blockSpawner != null && player != null)
            blockSpawner.SetStreamingTarget(player.transform);
        if (gridStreamer != null && player != null)
            gridStreamer.SetStreamingTarget(player.transform);

        if (startupStreamCursor != null)
            Destroy(startupStreamCursor);
        startupStreamCursor = null;

        if (!keepOverlayVisible)
        {
            startupOverlay?.Close();
            startupOverlay = null;
        }
        isStartupWarming = false;

        if (backgroundOverrideActive)
        {
            Application.runInBackground = previousBackgroundValue;
            backgroundOverrideActive = false;
        }
    }

    private void CancelStartupWarmup()
    {
        if (startupWarmupRoutine != null)
        {
            StopCoroutine(startupWarmupRoutine);
            startupWarmupRoutine = null;
        }
        if (isStartupWarming || startupStreamCursor != null || startupOverlay != null)
            CleanupStartupWarmup();
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
            startupReady = true;
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
