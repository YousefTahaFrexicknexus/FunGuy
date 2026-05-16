using System;
using UnityEngine;

namespace Funguy.IdkPlatformer
{
    [DisallowMultipleComponent]
    public sealed class ForwardProgressScoreTracker : MonoBehaviour
    {
        [SerializeField] private Transform trackedTarget;
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private MovementMotor movementMotor;
        [SerializeField] private bool autoFindMovementMotor = true;
        [SerializeField] private InputHandler inputHandler;
        [SerializeField] private bool autoFindInputHandler = true;
        [SerializeField] private float scoreScale = 1f;
        [SerializeField] private float forwardReleaseBreakDelay = 0.5f;
        [SerializeField] private float comboStepSize = 0.25f;
        [SerializeField] private float maxComboMultiplier = 2f;
        [SerializeField] private float forwardInputRequiredThreshold = 0.01f;
        [SerializeField] private float airtimeScorePerSecond = 10f;
        [SerializeField] private float airtimeRewardStartDelay = 0.35f;
        [SerializeField] private float airtimeStatusStartDelay = 0.85f;
        [SerializeField] private float airtimeComboMultiplierStartDelay = 1.15f;

        private bool initialized;
        private float furthestZ;
        private float lastScoredFurthestZ;
        private float scoreAccumulator;
        private int currentScore;
        private int currentComboHits;
        private float forwardIntentReleasedAt = float.NegativeInfinity;
        private bool wasGroundedLastFrame = true;
        private float airborneStartTime = float.NegativeInfinity;
        private float lastRewardedAirtimeDuration;
        private MovementMotor subscribedMovementMotor;

        public event Action<int> ScoreChanged;

        public Transform TrackedTarget => trackedTarget;

        public int CurrentScore => currentScore;

        public float FurthestForwardZ => furthestZ;

        public int CurrentComboHits => currentComboHits;

        public float CurrentComboMultiplier => ResolveComboMultiplier();

        public bool HasActiveCombo => currentComboHits > 0;

        public bool IsComboBreakPending => currentComboHits > 0 && forwardIntentReleasedAt > float.NegativeInfinity;

        public bool IsAirborne => ResolveMovementMotor() && !movementMotor.IsGrounded;

        public float CurrentAirtimeSeconds
        {
            get
            {
                if (!IsAirborne || airborneStartTime <= float.NegativeInfinity)
                {
                    return 0f;
                }

                return Mathf.Max(0f, Time.time - airborneStartTime);
            }
        }

        public float RewardedAirtimeSeconds => Mathf.Max(0f, CurrentAirtimeSeconds - airtimeRewardStartDelay);

        public float ComboTimeRemainingSeconds
            => IsComboBreakPending
                ? Mathf.Max(0f, forwardReleaseBreakDelay - (Time.time - forwardIntentReleasedAt))
                : 0f;

        public float ComboBreakDelaySeconds => forwardReleaseBreakDelay;

        public bool HasQualifiedAirtime => IsAirborne && CurrentAirtimeSeconds >= airtimeStatusStartDelay;

        public bool HasQualifiedAirtimeMultiplier => IsAirborne && CurrentAirtimeSeconds >= airtimeComboMultiplierStartDelay;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            RebindMovementMotor();
        }

        private void OnDisable()
        {
            UnbindMovementMotor();
        }

        private void OnValidate()
        {
            scoreScale = Mathf.Max(0.0001f, scoreScale);
            forwardReleaseBreakDelay = Mathf.Max(0f, forwardReleaseBreakDelay);
            comboStepSize = Mathf.Max(0f, comboStepSize);
            maxComboMultiplier = Mathf.Max(1f, maxComboMultiplier);
            forwardInputRequiredThreshold = Mathf.Max(0f, forwardInputRequiredThreshold);
            airtimeScorePerSecond = Mathf.Max(0f, airtimeScorePerSecond);
            airtimeRewardStartDelay = Mathf.Max(0f, airtimeRewardStartDelay);
            airtimeStatusStartDelay = Mathf.Max(airtimeRewardStartDelay, airtimeStatusStartDelay);
            airtimeComboMultiplierStartDelay = Mathf.Max(airtimeStatusStartDelay, airtimeComboMultiplierStartDelay);

            if (!Application.isPlaying)
            {
                ResolveReferences();
            }
        }

        private void Update()
        {
            if (!ResolveTrackedTarget())
            {
                return;
            }

            ResolveInputHandler();
            ResolveMovementMotor();
            RebindMovementMotor();

            if (!initialized)
            {
                ResetProgress(trackedTarget.position.z);
            }

            if (currentComboHits > 0 && ShouldBreakCombo())
            {
                ClearCombo();
            }

            UpdateAirtimeScore();

            float targetZ = trackedTarget.position.z;
            if (targetZ <= furthestZ)
            {
                return;
            }

            furthestZ = targetZ;

            float deltaZ = furthestZ - lastScoredFurthestZ;
            if (deltaZ <= 0f)
            {
                return;
            }

            scoreAccumulator += deltaZ * scoreScale * ResolveComboMultiplier();
            lastScoredFurthestZ = furthestZ;
            SetScore(Mathf.FloorToInt(scoreAccumulator), false);
        }

        public void SetTarget(Transform target)
        {
            trackedTarget = target;
            initialized = false;

            if (autoFindMovementMotor)
            {
                movementMotor = null;
            }

            if (autoFindInputHandler)
            {
                inputHandler = null;
            }

            ResolveReferences();
            ClearAirtimeTracking();
            RebindMovementMotor();
        }

        public void ResetProgress(float startZ)
        {
            furthestZ = startZ;
            lastScoredFurthestZ = startZ;
            scoreAccumulator = 0f;
            initialized = true;
            ClearCombo();
            ClearAirtimeTracking();
            SetScore(0, true);
        }

        private void HandleBounced(BounceEventData bounceEvent)
        {
            Collider surfaceCollider = bounceEvent.SurfaceCollider;
            if (surfaceCollider == null)
            {
                return;
            }

            Mushroom mushroom = surfaceCollider.GetComponentInParent<Mushroom>();
            if (mushroom == null)
            {
                return;
            }

            RestartAirtimeTracking();

            if (ResolveInputHandler() && IsBrakeIntentActive(inputHandler.CurrentFrame))
            {
                ClearCombo();
                return;
            }

            currentComboHits = currentComboHits <= 0 ? 1 : currentComboHits + 1;

            if (ResolveInputHandler() && HasForwardHoldIntent(inputHandler.CurrentFrame))
            {
                forwardIntentReleasedAt = float.NegativeInfinity;
            }
        }

        private bool ResolveReferences()
        {
            bool hasTarget = ResolveTrackedTarget();
            ResolveInputHandler();
            ResolveMovementMotor();
            return hasTarget;
        }

        private bool ResolveTrackedTarget()
        {
            if (trackedTarget != null)
            {
                return true;
            }

            if (!autoFindPlayer)
            {
                return false;
            }

            PlayerController playerController = FindFirstObjectByType<PlayerController>();
            trackedTarget = playerController != null ? playerController.transform : null;
            return trackedTarget != null;
        }

        private bool ResolveMovementMotor()
        {
            if (movementMotor != null)
            {
                return true;
            }

            if (trackedTarget != null)
            {
                movementMotor = trackedTarget.GetComponent<MovementMotor>();
                if (movementMotor == null)
                {
                    movementMotor = trackedTarget.GetComponentInParent<MovementMotor>();
                }
            }

            if (movementMotor != null || !autoFindMovementMotor)
            {
                return movementMotor != null;
            }

            movementMotor = FindFirstObjectByType<MovementMotor>();
            return movementMotor != null;
        }

        private bool ResolveInputHandler()
        {
            if (inputHandler != null)
            {
                return true;
            }

            if (trackedTarget != null)
            {
                inputHandler = trackedTarget.GetComponent<InputHandler>();
                if (inputHandler == null)
                {
                    inputHandler = trackedTarget.GetComponentInParent<InputHandler>();
                }
            }

            if (inputHandler != null || !autoFindInputHandler)
            {
                return inputHandler != null;
            }

            inputHandler = FindFirstObjectByType<InputHandler>();
            return inputHandler != null;
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

        private bool ShouldBreakCombo()
        {
            if (!ResolveInputHandler())
            {
                forwardIntentReleasedAt = float.NegativeInfinity;
                return false;
            }

            MovementInputFrame inputFrame = inputHandler.CurrentFrame;
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
                forwardIntentReleasedAt = Time.time;
                return false;
            }

            return Time.time >= forwardIntentReleasedAt + forwardReleaseBreakDelay;
        }

        private bool HasForwardHoldIntent(MovementInputFrame inputFrame)
        {
            return inputFrame.Move.y > forwardInputRequiredThreshold;
        }

        private bool IsBrakeIntentActive(MovementInputFrame inputFrame)
        {
            return inputFrame.BrakeAmount > forwardInputRequiredThreshold;
        }

        private void UpdateAirtimeScore()
        {
            if (!ResolveMovementMotor())
            {
                ClearAirtimeTracking();
                return;
            }

            if (movementMotor.IsGrounded)
            {
                ClearAirtimeTracking();
                return;
            }

            if (wasGroundedLastFrame || airborneStartTime <= float.NegativeInfinity)
            {
                wasGroundedLastFrame = false;
                airborneStartTime = Time.time;
                lastRewardedAirtimeDuration = 0f;
                return;
            }

            float rewardableDuration = (Time.time - airborneStartTime) - airtimeRewardStartDelay;
            if (rewardableDuration <= lastRewardedAirtimeDuration)
            {
                return;
            }

            float currentAirtimeSeconds = Mathf.Max(0f, Time.time - airborneStartTime);
            float previousRewardedAirtimeEnd = airtimeRewardStartDelay + lastRewardedAirtimeDuration;
            float baseRewardEnd = Mathf.Min(currentAirtimeSeconds, airtimeComboMultiplierStartDelay);
            float baseRewardStart = Mathf.Min(previousRewardedAirtimeEnd, airtimeComboMultiplierStartDelay);
            float baseRewardDuration = Mathf.Max(0f, baseRewardEnd - baseRewardStart);
            float multipliedRewardStart = Mathf.Max(previousRewardedAirtimeEnd, airtimeComboMultiplierStartDelay);
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
                scoreAccumulator += multipliedRewardDuration * airtimeScorePerSecond * ResolveComboMultiplier();
            }

            SetScore(Mathf.FloorToInt(scoreAccumulator), false);
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

        private void ClearAirtimeTracking()
        {
            wasGroundedLastFrame = movementMotor == null || movementMotor.IsGrounded;
            airborneStartTime = float.NegativeInfinity;
            lastRewardedAirtimeDuration = 0f;
        }

        private void RestartAirtimeTracking()
        {
            wasGroundedLastFrame = false;
            airborneStartTime = Time.time;
            lastRewardedAirtimeDuration = 0f;
        }

        private void SetScore(int nextScore, bool forceNotify)
        {
            if (!forceNotify && nextScore == currentScore)
            {
                return;
            }

            currentScore = nextScore;
            ScoreChanged?.Invoke(currentScore);
        }
    }
}
