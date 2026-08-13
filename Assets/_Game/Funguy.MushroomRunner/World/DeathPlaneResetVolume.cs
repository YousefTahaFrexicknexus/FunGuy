using UnityEngine;

[DisallowMultipleComponent]
public sealed class DeathPlaneResetVolume : MonoBehaviour
{
    const float ResetCooldown = 0.15f;
    const float DefaultPostResetGraceTime = 0.25f;

    [SerializeField, Tooltip("Run flow coordinator notified when this volume kills the player.")]
    RunFlowCoordinator resetCoordinator;
    [SerializeField, Tooltip("Specific player this volume should reset.")]
    MushroomRunnerPlayer trackedPlayer;
    [SerializeField, Tooltip("Transform checked against the death height each frame.")]
    Transform trackedTarget;
    [SerializeField, Tooltip("If enabled, Death Height Y is derived from this trigger's world position and scale.")]
    bool useAutomaticDeathHeight = true;
    [SerializeField, Tooltip("World-space Y threshold that kills the player when they fall below it.")]
    float deathHeightY = -12f;
    [SerializeField, Tooltip("If enabled, logs detailed death and reset diagnostics in play mode.")]
    bool enableDebugLogging = true;
    [SerializeField, Min(0f), Tooltip("Short grace window after start or reset before death detection reactivates.")]
    float postResetGraceTime = DefaultPostResetGraceTime;

    float lastResetTime = float.NegativeInfinity;
    float ignoreDeathUntil = float.NegativeInfinity;
    Collider cachedCollider;
    bool hasLoggedBelowDeathHeight;

    void Reset()
    {
        ResolveReferences();
        RemoveLegacyRigidbody();
        CacheCollider();
        RefreshDeathHeight();
        ResetCamera();
    }

    void Awake()
    {
        ResolveReferences();
        RemoveLegacyRigidbody();
        CacheCollider();
        RefreshDeathHeight();

        if (enableDebugLogging)
        {
            Debug.Log($"[DeathPlaneResetVolume] Ready. Death height Y: {deathHeightY:F2} Trigger bounds: {GetTriggerBounds()} Tracked target: {DescribeTarget()}");
        }
    }

    void OnEnable()
    {
        MushroomRunnerEvents.RunStarted += HandleRunLifecycle;
        MushroomRunnerEvents.RunReset += HandleRunLifecycle;
    }

    void OnDisable()
    {
        MushroomRunnerEvents.RunStarted -= HandleRunLifecycle;
        MushroomRunnerEvents.RunReset -= HandleRunLifecycle;
    }

    public void Configure(RunFlowCoordinator coordinator, Transform target, bool shouldFollowTargetOnZ, float targetZOffset)
    {
        resetCoordinator = coordinator;
        trackedTarget = target;
        trackedPlayer = target != null ? target.GetComponentInParent<MushroomRunnerPlayer>() : null;
        RefreshDeathHeight();

        if (enableDebugLogging)
        {
            Debug.Log($"[DeathPlaneResetVolume] Configured. Death height Y: {deathHeightY:F2} Trigger bounds: {GetTriggerBounds()} Tracked target: {DescribeTarget()}");
        }
    }

    void OnTriggerEnter(Collider other)
    {
        TryReset(other, "OnTriggerEnter");
    }

    void Update()
    {
        CheckHeightKill();
    }

    void TryReset(Collider other, string source)
    {
        if (other == null)
        {
            if (enableDebugLogging)
            {
                Debug.LogWarning($"[DeathPlaneResetVolume] {source} fired with a null collider.");
            }

            return;
        }

        MushroomRunnerPlayer player = other.GetComponentInParent<MushroomRunnerPlayer>();
        if (player == null)
        {
            return;
        }

        TryReset(player, source);
    }

    void TryReset(MushroomRunnerPlayer player, string source)
    {
        if (player == null || resetCoordinator == null)
        {
            return;
        }

        if (Time.time < lastResetTime + ResetCooldown)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[DeathPlaneResetVolume] {source} hit player '{player.name}' but reset is still on cooldown. Player position: {player.transform.position}");
            }

            return;
        }

        if (enableDebugLogging)
        {
            Debug.LogWarning($"[DeathPlaneResetVolume] {source} resetting player '{player.name}'. Player position: {player.transform.position} Trigger bounds: {GetTriggerBounds()}");
        }

        lastResetTime = Time.time;
        hasLoggedBelowDeathHeight = false;
        resetCoordinator.ReportFailure(RunFailureReason.FellBelowDeathPlane);
        ResetCamera();
    }

    void ResolveReferences()
    {
        if (trackedPlayer == null && trackedTarget != null)
        {
            trackedPlayer = trackedTarget.GetComponentInParent<MushroomRunnerPlayer>();
        }
    }

    void CacheCollider()
    {
        cachedCollider = GetComponent<Collider>();
    }

    void RefreshDeathHeight()
    {
        if (!useAutomaticDeathHeight)
        {
            return;
        }

        deathHeightY = transform.position.y + (transform.lossyScale.y * 0.5f);
    }
    
    void ResetCamera()
    {
        GameplayEvents.GameplayReset?.Invoke();
    }

    Bounds GetTriggerBounds()
    {
        if (cachedCollider == null)
        {
            CacheCollider();
        }

        return cachedCollider != null
            ? cachedCollider.bounds
            : new Bounds(transform.position, Vector3.zero);
    }

    void CheckHeightKill()
    {
        if (trackedPlayer == null || trackedTarget == null)
        {
            return;
        }

        if (Time.time < ignoreDeathUntil)
        {
            return;
        }

        if (trackedTarget.position.y > deathHeightY)
        {
            hasLoggedBelowDeathHeight = false;
            return;
        }

        if (enableDebugLogging && !hasLoggedBelowDeathHeight)
        {
            Debug.LogWarning($"[DeathPlaneResetVolume] HeightThreshold detected player below death height. Player position: {trackedTarget.position} Death height Y: {deathHeightY:F2}");
            hasLoggedBelowDeathHeight = true;
        }

        TryReset(trackedPlayer, "HeightThreshold");
    }

    string DescribeTarget()
    {
        return trackedTarget != null
            ? $"{trackedTarget.name} @ {trackedTarget.position}"
            : "<missing>";
    }

    void RemoveLegacyRigidbody()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(body);
        }
        else
        {
            DestroyImmediate(body);
        }
    }

    void HandleRunLifecycle(RunLifecycleEvent lifecycleEvent)
    {
        if (trackedPlayer == null || lifecycleEvent.Player != trackedPlayer)
        {
            return;
        }

        ignoreDeathUntil = Time.time + postResetGraceTime;
        hasLoggedBelowDeathHeight = false;
    }
}