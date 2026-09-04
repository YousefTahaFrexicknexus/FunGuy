using System;
using UnityEngine;
using UnityEngine.Events;

public class DistanceScoreManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform playerTransform;
    [SerializeField] MomentumSystem momentumSystem;

    [Header("Scoring")]
    [SerializeField, Min(0f)] float pointsPerUnit = 10f;

    [Header("Momentum Multipliers")]
    [SerializeField, Min(0f)] float stage1Multiplier = 1f;
    [SerializeField, Min(0f)] float stage2Multiplier = 2f;
    [SerializeField, Min(0f)] float stage3Multiplier = 3f;
    [SerializeField, Min(0f)] float stage4Multiplier = 4f;

    public int CurrentScore => Mathf.FloorToInt((float)preciseScore);
    public float CurrentMultiplier => GetMultiplier();
    public bool IsScoring { get; private set; }
    public bool IsPaused { get; private set; }

    double preciseScore;
    float previousZPosition;

    void OnEnable()
    {
        if(momentumSystem != null)
        {
            momentumSystem.OnTierChanged.AddListener(OnMomentumTierChanged);
        }
    }

    void OnDisable()
    {
        if(momentumSystem != null)
        {
            momentumSystem.OnTierChanged.RemoveListener(OnMomentumTierChanged);
        }
    }

    void Update()
    {
        if(!IsScoring || IsPaused || playerTransform == null)
        {
            return;
        }

        float currentZPosition = playerTransform.position.z;
        float traveledDistance = currentZPosition - previousZPosition;

        previousZPosition = currentZPosition;

        if(traveledDistance <= 0f)
        {
            return;
        }

        AddDistanceScore(traveledDistance);
    }

    public void BeginRun()
    {
        preciseScore = 0d;
        IsScoring = true;
        IsPaused = false;
        SyncPlayerPosition();

        GameplayEvents.OnScoreChanged.Invoke(CurrentScore);
        GameplayEvents.OnMultiplierChanged.Invoke(CurrentMultiplier);
    }

    public void EndRun()
    {
        IsScoring = false;
    }

    public void SetPaused(bool _paused)
    {
        IsPaused = _paused;
        SyncPlayerPosition();
    }

    public void ResetScore()
    {
        SyncPlayerPosition();
        preciseScore = 0d;
        GameplayEvents.OnScoreChanged.Invoke(CurrentScore);
    }

    // Call after manually resetting or teleporting the player.
    public void SyncPlayerPosition()
    {
        if(playerTransform != null)
        {
            previousZPosition = playerTransform.position.z;
        }
    }

    public void AddBonusScore(int _score)
    {
        if(_score <= 0)
        {
            return;
        }

        int previousScore = CurrentScore;
        preciseScore += _score;
        NotifyScoreChanged(previousScore);
    }

    void AddDistanceScore(float _distance)
    {
        int previousScore = CurrentScore;
        preciseScore += _distance * pointsPerUnit * CurrentMultiplier;
        NotifyScoreChanged(previousScore);
    }

    void NotifyScoreChanged(int _previousScore)
    {
        if(CurrentScore != _previousScore)
        {
            GameplayEvents.OnScoreChanged.Invoke(CurrentScore);
        }
    }

    void OnMomentumTierChanged(MomentumTier _tier)
    {
        GameplayEvents.OnMultiplierChanged.Invoke(GetMultiplier(_tier));
    }

    float GetMultiplier()
    {
        return momentumSystem == null ? stage1Multiplier : GetMultiplier(momentumSystem.CurrentTier);
    }

    float GetMultiplier(MomentumTier _tier)
    {
        switch(_tier)
        {
            case MomentumTier.Medium:
                return stage2Multiplier;
            case MomentumTier.High:
                return stage3Multiplier;
            case MomentumTier.Maximum:
                return stage4Multiplier;
            default:
                return stage1Multiplier;
        }
    }
}
