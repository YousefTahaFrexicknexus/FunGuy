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
        GameplayEvents.OnSetActiveTuningProfile?.Invoke(_movementTuningProfile);
    }
    
    void FixedUpdate()
    {
        momentumSystem.Tick(runnerMovementMotor.rigidBody.linearVelocity.z, Time.fixedDeltaTime);
    }
}
