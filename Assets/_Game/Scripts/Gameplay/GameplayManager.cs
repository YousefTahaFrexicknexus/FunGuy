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
    }

    void OnDisable()
    {
        UnsubscribeFromGameplayEvents();
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
        if (runnerMovementMotor != null && runnerMovementMotor.TryGetComponent(out MushroomRunnerPlayer player))
        {
            player.BindScoreManager(distanceScoreManager);
        }
        SetActiveTuningProfile(defaultMovementTuningProfile);
        momentumSystem.BeginRun();
        distanceScoreManager.BeginRun();
    }

    void GameplayReset()
    {
        momentumSystem.ResetMomentum();
        distanceScoreManager.ResetScore();
    }

    public void SetActiveTuningProfile(MovementTuningProfile _movementTuningProfile)
    {
        if (runnerMovementMotor != null && runnerMovementMotor.TryGetComponent(out MushroomRunnerPlayer player))
        {
            player.SetTuningProfile(_movementTuningProfile);
        }
    }
    
    void FixedUpdate()
    {
        momentumSystem.Tick(runnerMovementMotor.rigidBody.linearVelocity.z, Time.fixedDeltaTime);
    }
}
