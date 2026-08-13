using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunScoreService : MonoBehaviour
{
    [SerializeField, Tooltip("Player that owns the run being scored.")]
    MushroomRunnerPlayer trackedPlayer;
    [SerializeField, Tooltip("Transform whose forward Z progress is converted into score.")]
    Transform trackedTarget;
    [SerializeField, Tooltip("Combo and airtime service that modifies the score snapshot.")]
    RunMultiplierService multiplierService;
    [SerializeField, Tooltip("Points earned per world unit of forward progress before combo multiplier.")]
    float scoreScale = 1f;
    [SerializeField, Tooltip("Points earned per second of qualified airtime.")]
    float airtimeScorePerSecond = 10f;

    bool initialized;
    float furthestZ;
    float lastScoredFurthestZ;
    float scoreAccumulator;
    int currentScore;
    float lastRewardedAirtimeDuration;
    float lastAirtimeSecondsSample;

    public event Action<int> ScoreChanged;
    public event Action<RunScoreSnapshot> SnapshotChanged;

    public Transform TrackedTarget => trackedTarget;
    public int CurrentScore => currentScore;
    public float FurthestForwardZ => furthestZ;
    public int CurrentComboHits => multiplierService != null ? multiplierService.CurrentComboHits : 0;
    public float CurrentComboMultiplier => multiplierService != null ? multiplierService.CurrentComboMultiplier : 1f;
    public bool HasActiveCombo => multiplierService != null && multiplierService.HasActiveCombo;
    public bool IsComboBreakPending => multiplierService != null && multiplierService.IsComboBreakPending;
    public bool IsAirborne => multiplierService != null && multiplierService.IsAirborne;
    public float CurrentAirtimeSeconds => multiplierService != null ? multiplierService.CurrentAirtimeSeconds : 0f;
    public float RewardedAirtimeSeconds => multiplierService != null ? multiplierService.RewardedAirtimeSeconds : 0f;
    public float ComboTimeRemainingSeconds => multiplierService != null ? multiplierService.ComboTimeRemainingSeconds : 0f;
    public float ComboBreakDelaySeconds => multiplierService != null ? multiplierService.ComboBreakDelaySeconds : 0f;
    public bool HasQualifiedAirtime => multiplierService != null && multiplierService.HasQualifiedAirtime;
    public bool HasQualifiedAirtimeMultiplier => multiplierService != null && multiplierService.HasQualifiedAirtimeMultiplier;
    public RunScoreSnapshot CurrentSnapshot { get; set; }

    void Reset()
    {
        if (trackedPlayer == null)
        {
            trackedPlayer = GetComponent<MushroomRunnerPlayer>();
        }

        if (trackedPlayer != null && trackedTarget == null)
        {
            trackedTarget = trackedPlayer.transform;
        }

        if (multiplierService == null)
        {
            multiplierService = GetComponent<RunMultiplierService>();
        }
    }

    void Awake()
    {
        SyncReferences();
    }

    void OnValidate()
    {
        scoreScale = Mathf.Max(0.0001f, scoreScale);
        airtimeScorePerSecond = Mathf.Max(0f, airtimeScorePerSecond);
    }

    void Update()
    {
        SyncReferences();
        if (trackedTarget == null)
        {
            return;
        }

        Step(trackedTarget.position.z, Time.time);
    }

    public void BindPlayer(MushroomRunnerPlayer player)
    {
        trackedPlayer = player;
        trackedTarget = player != null ? player.transform : null;

        if (multiplierService == null && player != null)
        {
            multiplierService = player.GetComponent<RunMultiplierService>();
        }
    }

    public void SetTarget(Transform target)
    {
        trackedTarget = target;
        trackedPlayer = target != null ? target.GetComponentInParent<MushroomRunnerPlayer>() : null;

        if (multiplierService == null && trackedPlayer != null)
        {
            multiplierService = trackedPlayer.GetComponent<RunMultiplierService>();
        }

        initialized = false;
    }

    public void SetMultiplierService(RunMultiplierService service)
    {
        multiplierService = service;
    }

    public void ResetProgress(float startZ)
    {
        furthestZ = startZ;
        lastScoredFurthestZ = startZ;
        scoreAccumulator = 0f;
        currentScore = 0;
        initialized = true;
        lastRewardedAirtimeDuration = 0f;
        lastAirtimeSecondsSample = 0f;
        PublishSnapshot(force: true);
        ScoreChanged?.Invoke(currentScore);
    }

    public void Step(float targetZ, float currentTime)
    {
        if (!initialized)
        {
            ResetProgress(targetZ);
        }

        UpdateAirtimeScore(currentTime);

        if (targetZ > furthestZ)
        {
            furthestZ = targetZ;

            float deltaZ = furthestZ - lastScoredFurthestZ;
            if (deltaZ > 0f)
            {
                scoreAccumulator += deltaZ * scoreScale * CurrentComboMultiplier;
                lastScoredFurthestZ = furthestZ;
                SetScore(Mathf.FloorToInt(scoreAccumulator));
            }
        }

        PublishSnapshot(force: false);
    }

    void SyncReferences()
    {
        if (trackedPlayer != null && trackedTarget == null)
        {
            trackedTarget = trackedPlayer.transform;
        }

        if (trackedPlayer != null && multiplierService == null)
        {
            multiplierService = trackedPlayer.GetComponent<RunMultiplierService>();
        }
    }

    void UpdateAirtimeScore(float currentTime)
    {
        if (multiplierService == null || !multiplierService.IsAirborne)
        {
            lastRewardedAirtimeDuration = 0f;
            lastAirtimeSecondsSample = 0f;
            return;
        }

        float currentAirtimeSeconds = multiplierService.CurrentAirtimeSeconds;
        if (currentAirtimeSeconds < lastAirtimeSecondsSample)
        {
            lastRewardedAirtimeDuration = 0f;
        }

        lastAirtimeSecondsSample = currentAirtimeSeconds;

        float rewardableDuration = currentAirtimeSeconds - multiplierService.AirtimeRewardStartDelay;
        if (rewardableDuration <= lastRewardedAirtimeDuration)
        {
            return;
        }

        float previousRewardedAirtimeEnd = multiplierService.AirtimeRewardStartDelay + lastRewardedAirtimeDuration;
        float baseRewardEnd = Mathf.Min(currentAirtimeSeconds, multiplierService.AirtimeComboMultiplierStartDelay);
        float baseRewardStart = Mathf.Min(previousRewardedAirtimeEnd, multiplierService.AirtimeComboMultiplierStartDelay);
        float baseRewardDuration = Mathf.Max(0f, baseRewardEnd - baseRewardStart);
        float multipliedRewardStart = Mathf.Max(previousRewardedAirtimeEnd, multiplierService.AirtimeComboMultiplierStartDelay);
        float multipliedRewardDuration = Mathf.Max(0f, currentAirtimeSeconds - multipliedRewardStart);
        lastRewardedAirtimeDuration = Mathf.Max(0f, rewardableDuration);

        if (airtimeScorePerSecond <= 0f)
        {
            return;
        }

        if (baseRewardDuration > 0f)
        {
            scoreAccumulator += baseRewardDuration * airtimeScorePerSecond;
        }

        if (multipliedRewardDuration > 0f)
        {
            scoreAccumulator += multipliedRewardDuration * airtimeScorePerSecond * CurrentComboMultiplier;
        }

        SetScore(Mathf.FloorToInt(scoreAccumulator));
    }

    void SetScore(int nextScore)
    {
        if (nextScore == currentScore)
        {
            return;
        }

        currentScore = nextScore;
        ScoreChanged?.Invoke(currentScore);
    }

    void PublishSnapshot(bool force)
    {
        RunScoreSnapshot nextSnapshot = new(
            currentScore,
            CurrentComboHits,
            CurrentComboMultiplier,
            HasActiveCombo,
            IsComboBreakPending,
            ComboTimeRemainingSeconds,
            ComboBreakDelaySeconds,
            IsAirborne,
            CurrentAirtimeSeconds,
            RewardedAirtimeSeconds,
            HasQualifiedAirtime,
            HasQualifiedAirtimeMultiplier);

        if (!force && nextSnapshot.Equals(CurrentSnapshot))
        {
            return;
        }

        CurrentSnapshot = nextSnapshot;
        SnapshotChanged?.Invoke(CurrentSnapshot);
        MushroomRunnerEvents.RaiseRunScoreUpdated(new RunScoreUpdatedEvent(trackedPlayer, CurrentSnapshot));
    }
}