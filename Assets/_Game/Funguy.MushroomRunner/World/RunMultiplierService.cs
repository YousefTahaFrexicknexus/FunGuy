using UnityEngine;

namespace Funguy.MushroomRunner
{
    [DisallowMultipleComponent]
    public sealed class RunMultiplierService : MonoBehaviour
    {
        [SerializeField, Tooltip("Player input source used to decide whether the combo is being maintained or broken.")]
        private MushroomRunnerPlayer player;
        [SerializeField, Tooltip("Movement motor used to detect grounded state and bounce events.")]
        private RunnerMovementMotor movementMotor;
        [SerializeField, Tooltip("How long forward input can be released before the combo breaks.")]
        private float forwardReleaseBreakDelay = 0.5f;
        [SerializeField, Tooltip("Multiplier increase added per extra combo hit after the first.")]
        private float comboStepSize = 0.25f;
        [SerializeField, Tooltip("Hard cap on the combo multiplier.")]
        private float maxComboMultiplier = 2f;
        [SerializeField, Tooltip("Minimum forward or brake input amount treated as intentional.")]
        private float forwardInputRequiredThreshold = 0.01f;
        [SerializeField, Tooltip("Airtime must last this long before it starts granting score.")]
        private float airtimeRewardStartDelay = 0.35f;
        [SerializeField, Tooltip("Airtime must last this long before HUD and status text call it out.")]
        private float airtimeStatusStartDelay = 0.85f;
        [SerializeField, Tooltip("Airtime must last this long before airtime score also gets combo multiplier.")]
        private float airtimeComboMultiplierStartDelay = 1.15f;

        private int currentComboHits;
        private float forwardIntentReleasedAt = float.NegativeInfinity;
        private bool isGrounded = true;
        private bool wasGroundedLastFrame = true;
        private float airborneStartTime = float.NegativeInfinity;
        private float lastKnownTime;
        private RunnerMovementMotor subscribedMovementMotor;

        public int CurrentComboHits => currentComboHits;
        public float CurrentComboMultiplier => ResolveComboMultiplier();
        public bool HasActiveCombo => currentComboHits > 0;
        public bool IsComboBreakPending => currentComboHits > 0 && forwardIntentReleasedAt > float.NegativeInfinity;
        public bool IsAirborne => !isGrounded;
        public float CurrentAirtimeSeconds => IsAirborne && airborneStartTime > float.NegativeInfinity
            ? Mathf.Max(0f, lastKnownTime - airborneStartTime)
            : 0f;
        public float RewardedAirtimeSeconds => Mathf.Max(0f, CurrentAirtimeSeconds - airtimeRewardStartDelay);
        public float ComboTimeRemainingSeconds
            => IsComboBreakPending
                ? Mathf.Max(0f, forwardReleaseBreakDelay - (lastKnownTime - forwardIntentReleasedAt))
                : 0f;
        public float ComboBreakDelaySeconds => forwardReleaseBreakDelay;
        public bool HasQualifiedAirtime => IsAirborne && CurrentAirtimeSeconds >= airtimeStatusStartDelay;
        public bool HasQualifiedAirtimeMultiplier => IsAirborne && CurrentAirtimeSeconds >= airtimeComboMultiplierStartDelay;
        public float AirtimeRewardStartDelay => airtimeRewardStartDelay;
        public float AirtimeComboMultiplierStartDelay => airtimeComboMultiplierStartDelay;

        private void Reset()
        {
            if (player == null)
            {
                player = GetComponent<MushroomRunnerPlayer>();
            }

            if (movementMotor == null)
            {
                movementMotor = GetComponent<RunnerMovementMotor>();
            }
        }

        private void Awake()
        {
            SyncReferences();
            ResetState(movementMotor == null || movementMotor.IsGrounded, Time.time);
        }

        private void OnEnable()
        {
            SyncReferences();
            RebindMovementMotor();
        }

        private void OnDisable()
        {
            UnbindMovementMotor();
        }

        private void OnValidate()
        {
            forwardReleaseBreakDelay = Mathf.Max(0f, forwardReleaseBreakDelay);
            comboStepSize = Mathf.Max(0f, comboStepSize);
            maxComboMultiplier = Mathf.Max(1f, maxComboMultiplier);
            forwardInputRequiredThreshold = Mathf.Max(0f, forwardInputRequiredThreshold);
            airtimeRewardStartDelay = Mathf.Max(0f, airtimeRewardStartDelay);
            airtimeStatusStartDelay = Mathf.Max(airtimeRewardStartDelay, airtimeStatusStartDelay);
            airtimeComboMultiplierStartDelay = Mathf.Max(airtimeStatusStartDelay, airtimeComboMultiplierStartDelay);
        }

        private void Update()
        {
            SyncReferences();

            if (movementMotor == null)
            {
                return;
            }

            MovementInputFrame inputFrame = player != null ? player.CurrentInputFrame : MovementInputFrame.Empty;
            Step(inputFrame, movementMotor.IsGrounded, Time.time);
        }

        public void BindPlayer(MushroomRunnerPlayer playerReference)
        {
            player = playerReference;
            if (movementMotor == null && playerReference != null)
            {
                movementMotor = playerReference.MovementMotor;
            }
        }

        public void SetMovementMotor(RunnerMovementMotor motor)
        {
            movementMotor = motor;
            RebindMovementMotor();
        }

        public void ResetState(bool grounded = true, float currentTime = 0f)
        {
            currentComboHits = 0;
            forwardIntentReleasedAt = float.NegativeInfinity;
            isGrounded = grounded;
            wasGroundedLastFrame = grounded;
            airborneStartTime = float.NegativeInfinity;
            lastKnownTime = currentTime;
        }

        public void Step(MovementInputFrame inputFrame, bool grounded, float currentTime)
        {
            lastKnownTime = currentTime;
            isGrounded = grounded;

            if (grounded)
            {
                wasGroundedLastFrame = true;
                airborneStartTime = float.NegativeInfinity;
            }
            else if (wasGroundedLastFrame || airborneStartTime <= float.NegativeInfinity)
            {
                wasGroundedLastFrame = false;
                airborneStartTime = currentTime;
            }

            if (currentComboHits > 0 && ShouldBreakCombo(inputFrame, currentTime))
            {
                ClearCombo();
            }
        }

        public void RegisterBounce(MovementInputFrame inputFrame, float currentTime)
        {
            lastKnownTime = currentTime;
            wasGroundedLastFrame = false;
            isGrounded = false;
            airborneStartTime = currentTime;

            if (IsBrakeIntentActive(inputFrame))
            {
                ClearCombo();
                return;
            }

            currentComboHits = currentComboHits <= 0 ? 1 : currentComboHits + 1;

            if (HasForwardHoldIntent(inputFrame))
            {
                forwardIntentReleasedAt = float.NegativeInfinity;
            }
        }

        private void SyncReferences()
        {
            if (player != null && movementMotor == null)
            {
                movementMotor = player.MovementMotor;
            }
        }

        private void RebindMovementMotor()
        {
            if (subscribedMovementMotor == movementMotor)
            {
                return;
            }

            UnbindMovementMotor();

            subscribedMovementMotor = movementMotor;
            if (subscribedMovementMotor != null && isActiveAndEnabled)
            {
                subscribedMovementMotor.Bounced += HandleBounced;
            }
        }

        private void UnbindMovementMotor()
        {
            if (subscribedMovementMotor == null)
            {
                return;
            }

            subscribedMovementMotor.Bounced -= HandleBounced;
            subscribedMovementMotor = null;
        }

        private void HandleBounced(BounceEventData bounceEvent)
        {
            if (bounceEvent.SurfaceCollider == null || bounceEvent.SurfaceCollider.GetComponentInParent<Mushroom>() == null)
            {
                return;
            }

            MovementInputFrame inputFrame = player != null ? player.CurrentInputFrame : MovementInputFrame.Empty;
            RegisterBounce(inputFrame, Time.time);
        }

        private bool ShouldBreakCombo(MovementInputFrame inputFrame, float currentTime)
        {
            if (IsBrakeIntentActive(inputFrame))
            {
                return true;
            }

            if (HasForwardHoldIntent(inputFrame))
            {
                forwardIntentReleasedAt = float.NegativeInfinity;
                return false;
            }

            if (forwardIntentReleasedAt <= float.NegativeInfinity)
            {
                forwardIntentReleasedAt = currentTime;
                return false;
            }

            return currentTime >= forwardIntentReleasedAt + forwardReleaseBreakDelay;
        }

        private bool HasForwardHoldIntent(MovementInputFrame inputFrame)
        {
            return inputFrame.Move.y > forwardInputRequiredThreshold;
        }

        private bool IsBrakeIntentActive(MovementInputFrame inputFrame)
        {
            return inputFrame.BrakeAmount > forwardInputRequiredThreshold;
        }

        private float ResolveComboMultiplier()
        {
            float steppedMultiplier = 1f + (Mathf.Max(0, currentComboHits - 1) * comboStepSize);
            return Mathf.Min(maxComboMultiplier, steppedMultiplier);
        }

        private void ClearCombo()
        {
            currentComboHits = 0;
            forwardIntentReleasedAt = float.NegativeInfinity;
        }
    }
}
