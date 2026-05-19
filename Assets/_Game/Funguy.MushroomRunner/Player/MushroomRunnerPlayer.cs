using UnityEngine;

namespace Funguy.MushroomRunner
{
    public sealed class MushroomRunnerPlayer : MonoBehaviour
    {
        public enum PlayerState
        {
            Active,
            Disabled,
            Dead
        }

        [SerializeField, Tooltip("Input source that produces camera-relative movement frames for this player.")]
        private RunnerInputSource inputHandler;
        [SerializeField, Tooltip("Physics motor that actually moves, bounces, and dashes the body.")]
        private RunnerMovementMotor movementMotor;
        [SerializeField, Tooltip("Runtime movement profile pushed into the motor during Awake().")]
        private MovementTuningProfile tuningProfile;
        [SerializeField, Tooltip("Child transform the camera should follow instead of the raw player pivot.")]
        private Transform cameraFollowTarget;
        [SerializeField, Tooltip("Player state applied on startup before the first run begins.")]
        private PlayerState initialState = PlayerState.Active;

        private int availableDashCharges;
        private float lastDashTime = float.NegativeInfinity;
        private PlayerState state;

        public PlayerState State => state;
        public MovementInputFrame CurrentInputFrame { get; private set; } = MovementInputFrame.Empty;
        public RunnerMovementMotor MovementMotor => movementMotor;
        public RunnerInputSource InputSource => inputHandler;
        public Transform CameraFollowTarget => cameraFollowTarget != null ? cameraFollowTarget : transform;

        private void Reset()
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

        private void Awake()
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

        private void Start()
        {
            MushroomRunnerEvents.RaisePlayerRegistered(new PlayerRegisteredEvent(
                this,
                movementMotor,
                inputHandler,
                CameraFollowTarget));
        }

        private void OnEnable()
        {
            if (movementMotor == null)
            {
                return;
            }

            movementMotor.Bounced += HandleBounce;
            movementMotor.Dashed += HandleDash;
        }

        private void OnDisable()
        {
            if (movementMotor == null)
            {
                return;
            }

            movementMotor.Bounced -= HandleBounce;
            movementMotor.Dashed -= HandleDash;
        }

        private void Update()
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

        private bool TryConsumeDashCharge()
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

        private void HandleBounce(BounceEventData bounceEvent)
        {
            RestoreDashCharges();
            MushroomRunnerEvents.RaisePlayerBounced(new PlayerBouncedEvent(this, bounceEvent));
        }

        private void HandleDash()
        {
            MushroomRunnerEvents.RaisePlayerDashed(new PlayerDashedEvent(
                this,
                movementMotor != null ? movementMotor.Velocity : Vector3.zero));
        }

        private void RestoreDashCharges()
        {
            availableDashCharges = tuningProfile != null ? tuningProfile.DashChargesPerBounce : 1;
        }
    }
}
