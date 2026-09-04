using System;
using UnityEngine;
using UnityEngine.Events;

using System.Collections.Generic;

public class MomentumSystem : MonoBehaviour
{
    #region Instance | Singleton

    static MomentumSystem _instance;
    public static MomentumSystem Instance
    {
        get
        {
            if (!_instance)
                _instance = GameObject.FindAnyObjectByType<MomentumSystem>();
            return _instance;
        }
    }
    #endregion --- Instance | Singleton ---

    [Serializable] public class MomentumValueEvent : UnityEvent<float> { }
    [Serializable] public class MomentumTierEvent : UnityEvent<MomentumTier> { }

    [Header("Momentum")]
    [SerializeField, Min(1f)] float maximumMomentum = 100f;
    [SerializeField, Min(0f)] float startingMomentum = 0f;

    [Header("Forward Speed (World Units / Second)")]
    [SerializeField, Min(0f)] float minimumMomentumSpeed = 8f;
    [SerializeField, Min(0f)] float maximumMomentumSpeed = 20f;

    [Tooltip("Set to zero if speed should only maintain momentum.")]
    [SerializeField, Min(0f)] float momentumGainPerSecond = 2f;
    [SerializeField, Min(0f)] float momentumDecayPerSecond = 5f;

    [Header("Landings")]
    [SerializeField, Min(0f)] float perfectLandingBonus = 15f;
    [SerializeField, Min(0f)] float goodLandingBonus = 7.5f;
    [SerializeField, Min(0f)] float badLandingPenalty = 5f;

    [Header("Mistakes")]
    [SerializeField, Min(0f)] float sideHitPenalty = 25f;
    [SerializeField, Min(0f)] float weakMushroomPenalty = 10f;
    [SerializeField] bool resetMomentumOnFall = true;
    [SerializeField, Min(0f)] float fallPenalty = 50f;

    [Header("Tier Thresholds (Fraction Of Maximum)")]
    [SerializeField, Range(0f, 1f)] float mediumThreshold = 0.25f;
    [SerializeField, Range(0f, 1f)] float highThreshold = 0.5f;
    [SerializeField, Range(0f, 1f)] float maximumThreshold = 0.8f;

    [Header("Multiplier")]
    List<float> multipliers = new List<float>();

    [Header("Events")]
    public MomentumValueEvent OnMomentumChanged = new MomentumValueEvent();
    public MomentumValueEvent OnNormalizedMomentumChanged = new MomentumValueEvent();
    public MomentumTierEvent OnTierChanged = new MomentumTierEvent();

    public float CurrentMomentum { get; set; }
    public float NormalizedMomentum => CurrentMomentum / maximumMomentum;
    public MomentumTier LastTier { get; set; }
    public MomentumTier CurrentTier { get; set; }
    public bool IsRunning { get; set; }
    public bool IsPaused { get; set; }

    [SerializeField] float currentMomentumRef;

    bool CanUpdate => isActiveAndEnabled && IsRunning && !IsPaused && Time.timeScale > 0f;

    void Awake()
    {
        ValidateSettings();
        CurrentMomentum = Mathf.Clamp(startingMomentum, 0f, maximumMomentum);
        CurrentTier = CalculateTier();
    }

    void OnValidate()
    {
        ValidateSettings();
    }

    void ValidateSettings()
    {
        maximumMomentum = Mathf.Max(1f, maximumMomentum);
        startingMomentum = Mathf.Clamp(startingMomentum, 0f, maximumMomentum);
        minimumMomentumSpeed = Mathf.Max(0f, minimumMomentumSpeed);
        maximumMomentumSpeed = Mathf.Max(minimumMomentumSpeed + 0.01f, maximumMomentumSpeed);
        mediumThreshold = Mathf.Clamp01(mediumThreshold);
        highThreshold = Mathf.Clamp(highThreshold, mediumThreshold, 1f);
        maximumThreshold = Mathf.Clamp(maximumThreshold, highThreshold, 1f);
    }

    // Call after listeners are connected, whenever a new run starts.
    public void BeginRun()
    {
        IsRunning = true;
        IsPaused = false;
        SetMomentum(startingMomentum, true);
    }

    public void EndRun()
    {
        IsRunning = false;
    }

    public void SetPaused(bool _paused)
    {
        IsPaused = _paused;
    }

    // Call once per movement update. Use deltaTime OR fixedDeltaTime to match that loop.
    // Pass forward speed only, not total velocity magnitude.
    public void Tick(float _forwardSpeed, float _deltaTime)
    {
        if(!CanUpdate || _deltaTime <= 0f || float.IsNaN(_deltaTime) || float.IsInfinity(_deltaTime) || float.IsNaN(_forwardSpeed) || float.IsInfinity(_forwardSpeed))
        {
            return;
        }

        float speedProgress = Mathf.InverseLerp(minimumMomentumSpeed, maximumMomentumSpeed, _forwardSpeed);
        float changePerSecond = _forwardSpeed < minimumMomentumSpeed ? -momentumDecayPerSecond : momentumGainPerSecond * speedProgress;
        SetMomentum(CurrentMomentum + changePerSecond * _deltaTime);
    }

    // Call exactly once per landing, from the existing landing detector.
    public void OnMushroomLanded(LandingQuality _quality)
    {
        switch(_quality)
        {
            case LandingQuality.Perfect:
            {
                AddMomentum(perfectLandingBonus);
                break;
            }

            case LandingQuality.Good:
            {
                AddMomentum(goodLandingBonus);
                break;
            }

            case LandingQuality.Bad:
            {
                RemoveMomentum(badLandingPenalty);
                break;
            }
        }
    }

    public void OnSideHit()
    {
        RemoveMomentum(sideHitPenalty);
    }

    public void OnWeakMushroomLanded()
    {
        RemoveMomentum(weakMushroomPenalty);
    }

    // Does not end the run; call EndRun separately if falling means game over.
    public void OnFall()
    {
        RemoveMomentum(resetMomentumOnFall ? maximumMomentum : fallPenalty);
    }

    public void AddMomentum(float _amount)
    {
        if(CanUpdate && _amount > 0f && !float.IsInfinity(_amount))
        {
            SetMomentum(CurrentMomentum + _amount);
        }
    }

    public void RemoveMomentum(float _amount)
    {
        if(CanUpdate && _amount > 0f && !float.IsInfinity(_amount))
        {
            SetMomentum(CurrentMomentum - _amount);
        }
    }

    public void ResetMomentum()
    {
        if(CanUpdate)
        {
            SetMomentum(0f);
        }
    }

    void SetMomentum(float _value, bool _forceNotify = false)
    {
        float nextMomentum = Mathf.Clamp(_value, 0f, maximumMomentum);

        if(!_forceNotify && nextMomentum == CurrentMomentum)
        {
            return;
        }

        MomentumTier previousTier = CurrentTier;
        CurrentMomentum = nextMomentum;
        currentMomentumRef = CurrentMomentum;
        GameplayEvents.OnMomentumChanged?.Invoke(Mathf.Clamp01(CurrentMomentum * 0.01f));

        CurrentTier = CalculateTier();

        if(CurrentTier != previousTier)
        {
            GameplayEvents.OnMultiplierChanged?.Invoke(GetActiveMultiplier());
        }

        OnMomentumChanged.Invoke(CurrentMomentum);
        OnNormalizedMomentumChanged.Invoke(NormalizedMomentum);

        if(_forceNotify || previousTier != CurrentTier)
        {
            OnTierChanged.Invoke(CurrentTier);
        }
    }

    MomentumTier CalculateTier()
    {
        if(NormalizedMomentum >= maximumThreshold)
        {
            return MomentumTier.Maximum;
        }

        if(NormalizedMomentum >= highThreshold)
        {
            return MomentumTier.High;
        }

        if(NormalizedMomentum >= mediumThreshold)
        {
            return MomentumTier.Medium;
        }

        return MomentumTier.Low;
    }

    public int GetActiveMultiplier()
    {
        switch (CurrentTier)
        {
            case MomentumTier.Medium:
            {
                return 2;
            }

            case MomentumTier.High:
            {
                return 3;
            }

            case MomentumTier.Maximum:
            {
                return 4;
            }

            case MomentumTier.Low:
            default:
            {
                return 1;
            }
        }
    }
}

public enum LandingQuality
{
    Bad, Good, Perfect
}

public enum MomentumTier
{
    Low, Medium, High, Maximum
}