using UnityEngine;

public class GameplayManager : MonoBehaviour
{
    [SerializeField] MovementTuningProfile defaultMovementTuningProfile;
    [SerializeField] MomentumSystem momentumSystem;
    [SerializeField] RunnerMovementMotor runnerMovementMotor;
    [SerializeField] DistanceScoreManager distanceScoreManager;

    void OnEnable()
    {
        SubscribeToGameplayEvents();
        BindMovementMomentum();
    }

    void OnDisable()
    {
        UnsubscribeFromGameplayEvents();
        runnerMovementMotor?.SetMomentumSystem(null);
    }

    void SubscribeToGameplayEvents()
    {
        GameplayEvents.GameplayReset += GameplayReset;
    }

    void UnsubscribeFromGameplayEvents()
    {
        GameplayEvents.GameplayReset -= GameplayReset;
    }

    void Start()
    {
        BindMovementMomentum();
        SetActiveTuningProfile(defaultMovementTuningProfile);
        momentumSystem?.BeginRun();
        distanceScoreManager?.BeginRun();
    }

    void GameplayReset()
    {
        momentumSystem?.ResetMomentum();
        distanceScoreManager?.ResetScore();
    }

    public void SetActiveTuningProfile(MovementTuningProfile _movementTuningProfile)
    {
        if (_movementTuningProfile == null)
        {
            return;
        }

        if (runnerMovementMotor != null)
        {
            MushroomRunnerPlayer player = runnerMovementMotor.GetComponentInParent<MushroomRunnerPlayer>();
            if (player != null)
            {
                player.SetTuningProfile(_movementTuningProfile);
            }
            else
            {
                runnerMovementMotor.SetTuningProfile(_movementTuningProfile);
            }
        }
        GameplayEvents.OnSetActiveTuningProfile?.Invoke(_movementTuningProfile);
    }

    void BindMovementMomentum()
    {
        // Scoring and movement must use the same tier source, including the x1 fallback.
        if (distanceScoreManager != null)
        {
            momentumSystem = distanceScoreManager.MomentumSource;
        }
        runnerMovementMotor?.SetMomentumSystem(momentumSystem);
    }
    
    void FixedUpdate()
    {
        if (momentumSystem != null && runnerMovementMotor != null)
        {
            momentumSystem.Tick(runnerMovementMotor.Velocity.z, Time.fixedDeltaTime);
        }
    }

    void OnPauseGame()
    {
        Time.timeScale = 0;
    }

    void OnUnPauseGame()
    {
        Time.timeScale = 1;
    }
}
