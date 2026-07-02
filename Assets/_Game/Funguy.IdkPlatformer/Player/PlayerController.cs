using UnityEngine;

namespace Funguy.IdkPlatformer
{
    public sealed class PlayerController : MonoBehaviour
    {
        public enum PlayerState
        {
            Active,
            Disabled,
            Dead
        }

        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private MovementMotor movementMotor;
        [SerializeField] private MovementTuningProfile tuningProfile;
        [SerializeField] private PlayerState initialState = PlayerState.Active;

        private int availableDashCharges;
        private float lastDashTime = float.NegativeInfinity;
        private PlayerState state;

        public PlayerState State => state;

        private void Reset()
        {
            movementMotor = GetComponent<MovementMotor>();
            inputHandler = FindFirstObjectByType<InputHandler>();
        }

        private void Awake()
        {
            if (movementMotor == null)
            {
                movementMotor = GetComponent<MovementMotor>();
            }

            if (movementMotor != null)
            {
                movementMotor.SetTuningProfile(tuningProfile);
                movementMotor.SetDashResourceHandler(TryConsumeDashCharge);
            }

            RestoreDashCharges();
            SetState(initialState);
        }

        private void OnEnable()
        {
            if (movementMotor == null)
            {
                return;
            }

            movementMotor.Bounced += HandleBounce;
        }

        private void OnDisable()
        {
            if (movementMotor == null)
            {
                return;
            }

            movementMotor.Bounced -= HandleBounce;
        }

        private void Update()
        {
            if (movementMotor == null)
            {
                return;
            }

            if (state != PlayerState.Active)
            {
                movementMotor.SetInput(MovementInputFrame.Empty);
                return;
            }

            MovementInputFrame inputFrame = inputHandler != null ? inputHandler.CurrentFrame : MovementInputFrame.Empty;
            movementMotor.SetInput(inputFrame);

            if (inputFrame.DashPressed)
            {
                movementMotor.RequestDash();
            }
        }

        public void SetState(PlayerState nextState)
        {
            state = nextState;

            if (movementMotor != null)
            {
                movementMotor.SetMotorEnabled(state == PlayerState.Active);
            }
        }

        public void SetInputHandler(InputHandler newInputHandler)
        {
            inputHandler = newInputHandler;
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

        public void ResetRun(Vector3 worldPosition, Quaternion worldRotation)
        {
            lastDashTime = float.NegativeInfinity;
            RestoreDashCharges();

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
        }

        private void RestoreDashCharges()
        {
            availableDashCharges = tuningProfile != null ? tuningProfile.DashChargesPerBounce : 1;
        }
    }
}
