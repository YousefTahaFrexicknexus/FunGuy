using UnityEngine;

public sealed class MushroomRunnerPlayer : MonoBehaviour
{
    public enum PlayerState
    {
        Active,
        Disabled,
        Dead
    }

    [SerializeField, Tooltip("Input source that produces camera-relative movement frames for this player.")]
    RunnerInputSource inputHandler;
    [SerializeField, Tooltip("Physics motor that actually moves, bounces, and dashes the body.")]
    RunnerMovementMotor movementMotor;
    [SerializeField, Tooltip("Runtime movement profile pushed into the motor during Awake().")]
    MovementTuningProfile tuningProfile;
    [SerializeField, Tooltip("Child transform the camera should follow instead of the raw player pivot.")]
    Transform cameraFollowTarget;
    [SerializeField, Tooltip("Player state applied on startup before the first run begins.")]
    PlayerState initialState = PlayerState.Active;

    int availableDashCharges;
    float lastDashTime = float.NegativeInfinity;
    PlayerState state;

    public PlayerState State => state;
    public MovementInputFrame CurrentInputFrame { get; set; } = MovementInputFrame.Empty;
    public RunnerMovementMotor MovementMotor => movementMotor;
    public RunnerInputSource InputSource => inputHandler;
    public Transform CameraFollowTarget => cameraFollowTarget != null ? cameraFollowTarget : transform;

    void Reset()
    {
        movementMotor = GetComponent<RunnerMovementMotor>();

        if (cameraFollowTarget == null)
        {
            Transform child = transform.Find("CameraFollowTarget");
            if (child != null)
            {
                cameraFollowTarget = child;
            }
        }
    }

    void Awake()
    {
        if (movementMotor == null)
        {
            movementMotor = GetComponent<RunnerMovementMotor>();
        }

        if (movementMotor != null)
        {
            movementMotor.SetTuningProfile(tuningProfile);
            movementMotor.SetDashResourceHandler(TryConsumeDashCharge);
        }

        RestoreDashCharges();
        SetState(initialState);
    }

    void Start()
    {
        MushroomRunnerEvents.RaisePlayerRegistered(new PlayerRegisteredEvent(
            this,
            movementMotor,
            inputHandler,
            CameraFollowTarget));
    }

    void OnEnable()
    {
        if (movementMotor == null)
        {
            return;
        }

        movementMotor.Bounced += HandleBounce;
        movementMotor.Dashed += HandleDash;
    }

    void OnDisable()
    {
        if (movementMotor == null)
        {
            return;
        }

        movementMotor.Bounced -= HandleBounce;
        movementMotor.Dashed -= HandleDash;
    }

    void Update()
    {
        if (movementMotor == null)
        {
            return;
        }

        if (state != PlayerState.Active)
        {
            CurrentInputFrame = MovementInputFrame.Empty;
            movementMotor.SetInput(MovementInputFrame.Empty);
            return;
        }

        CurrentInputFrame = inputHandler != null ? inputHandler.CurrentFrame : MovementInputFrame.Empty;
        movementMotor.SetInput(CurrentInputFrame);

        if (CurrentInputFrame.DashPressed)
        {
            movementMotor.RequestDash();
        }
    }

    public void SetState(PlayerState nextState)
    {
        PlayerState previousState = state;
        state = nextState;

        if (movementMotor != null)
        {
            movementMotor.SetMotorEnabled(state == PlayerState.Active);
        }

        if (previousState != nextState)
        {
            MushroomRunnerEvents.RaisePlayerStateChanged(new PlayerStateChangedEvent(this, previousState, nextState));
        }
    }

    public void SetRunnerInputSource(RunnerInputSource newRunnerInputSource)
    {
        inputHandler = newRunnerInputSource;
    }

    public void SetTuningProfile(MovementTuningProfile profile)
    {
        tuningProfile = profile;

        if (movementMotor != null)
        {
            movementMotor.SetTuningProfile(profile);
        }

        RestoreDashCharges();
    }

    public void SetCameraFollowTarget(Transform followTarget)
    {
        cameraFollowTarget = followTarget;
    }

    public void ResetRun(Vector3 worldPosition, Quaternion worldRotation)
    {
        lastDashTime = float.NegativeInfinity;
        RestoreDashCharges();
        CurrentInputFrame = MovementInputFrame.Empty;

        if (movementMotor == null)
        {
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            SetState(PlayerState.Active);
            return;
        }

        SetState(PlayerState.Disabled);
        movementMotor.ResetMotion(worldPosition, worldRotation);
        SetState(PlayerState.Active);
    }

    bool TryConsumeDashCharge()
    {
        if (state != PlayerState.Active || tuningProfile == null)
        {
            return false;
        }

        if (availableDashCharges <= 0)
        {
            return false;
        }

        if (Time.time < lastDashTime + tuningProfile.DashCooldown)
        {
            return false;
        }

        availableDashCharges--;
        lastDashTime = Time.time;
        return true;
    }

    void HandleBounce(BounceEventData bounceEvent)
    {
        RestoreDashCharges();
        MushroomRunnerEvents.RaisePlayerBounced(new PlayerBouncedEvent(this, bounceEvent));
    }

    void HandleDash()
    {
        MushroomRunnerEvents.RaisePlayerDashed(new PlayerDashedEvent(
            this,
            movementMotor != null ? movementMotor.Velocity : Vector3.zero));
    }

    void RestoreDashCharges()
    {
        availableDashCharges = tuningProfile != null ? tuningProfile.DashChargesPerBounce : 1;
    }
}
