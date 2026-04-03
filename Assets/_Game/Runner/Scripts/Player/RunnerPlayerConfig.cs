using DG.Tweening;
using UnityEngine;

namespace FunGuy.Runner
{
    [CreateAssetMenu(fileName = "RunnerPlayerConfig", menuName = "FunGuy/Runner/Player Config")]
    public sealed class RunnerPlayerConfig : ScriptableObject
    {
        [Header("Start Cell")]
        [SerializeField] private int startLane = 1;
        [SerializeField] private int startLayer;

        [Header("Bounce Rhythm")]
        [SerializeField] private float initialBounceDelay = 0.55f;
        [SerializeField] private float timeBetweenBounces = 0.22f;
        [SerializeField] private float minimumTimeBetweenBounces = 0.12f;
        [SerializeField] private float anticipationDuration = 0.08f;
        [SerializeField] private float anticipationDip = 0.1f;
        [SerializeField] private float jumpDuration = 0.6f;
        [SerializeField] private float minimumJumpDuration = 0.38f;
        [SerializeField] private float jumpArcHeight = 1.55f;
        [SerializeField] private float landingPause = 0.12f;
        [SerializeField] private float minimumLandingPause = 0.05f;
        [SerializeField] private int speedRampDistanceCells = 320;

        [Header("Mid-Air Steering")]
        [SerializeField, Range(0.1f, 1f)] private float retargetLockProgress = 0.92f;

        [Header("Extra Jumps")]
        [SerializeField] private int maxExtraJumps = 2;
        [SerializeField] private int extraForwardCells = 1;
        [SerializeField] private int extraJumpUpCells = 1;

        [Header("Scoring")]
        [SerializeField] private int scorePerForwardCell = 10;
        [SerializeField] private int scorePerCollectible = 25;

        [Header("Fail State")]
        [SerializeField] private float failFallDuration = 0.4f;
        [SerializeField] private float deathDepthCells = 2.5f;

        [Header("Feel")]
        [SerializeField] private Vector3 anticipationScale = new(1.1f, 0.84f, 1.1f);
        [SerializeField] private Vector3 launchScale = new(0.9f, 1.16f, 0.9f);
        [SerializeField] private Vector3 landingScale = new(1.14f, 0.82f, 1.14f);
        [SerializeField] private float landingSquashDuration = 0.06f;
        [SerializeField] private float recoverDuration = 0.08f;
        [SerializeField] private Ease jumpEase = Ease.Linear;
        [SerializeField] private Ease recoverEase = Ease.OutBack;

        public int StartLane => startLane;
        public int StartLayer => startLayer;
        public float InitialBounceDelay => initialBounceDelay;
        public float TimeBetweenBounces => timeBetweenBounces;
        public float MinimumTimeBetweenBounces => minimumTimeBetweenBounces;
        public float AnticipationDuration => anticipationDuration;
        public float AnticipationDip => anticipationDip;
        public float JumpDuration => jumpDuration;
        public float MinimumJumpDuration => minimumJumpDuration;
        public float JumpArcHeight => jumpArcHeight;
        public float LandingPause => landingPause;
        public float MinimumLandingPause => minimumLandingPause;
        public int SpeedRampDistanceCells => speedRampDistanceCells;
        public float RetargetLockProgress => retargetLockProgress;
        public int MaxExtraJumps => maxExtraJumps;
        public int ExtraForwardCells => extraForwardCells > 0 ? extraForwardCells : 1;
        public int ExtraJumpUpCells => extraJumpUpCells > 0 ? extraJumpUpCells : 1;
        public int ScorePerForwardCell => scorePerForwardCell > 0 ? scorePerForwardCell : 10;
        public int ScorePerCollectible => scorePerCollectible > 0 ? scorePerCollectible : 25;
        public float FailFallDuration => failFallDuration;
        public float DeathDepthCells => deathDepthCells;
        public Vector3 AnticipationScale => anticipationScale;
        public Vector3 LaunchScale => launchScale;
        public Vector3 LandingScale => landingScale;
        public float LandingSquashDuration => landingSquashDuration;
        public float RecoverDuration => recoverDuration;
        public Ease JumpEase => jumpEase;
        public Ease RecoverEase => recoverEase;

        public float EvaluateSpeedRamp(int traveledCells)
        {
            if (speedRampDistanceCells <= 0)
            {
                return 1f;
            }

            return Mathf.Clamp01(traveledCells / (float)speedRampDistanceCells);
        }

        public float GetJumpDuration(int traveledCells)
        {
            return Mathf.Lerp(jumpDuration, minimumJumpDuration, EvaluateSpeedRamp(traveledCells));
        }

        public float GetTimeBetweenBounces(int traveledCells)
        {
            return Mathf.Lerp(timeBetweenBounces, minimumTimeBetweenBounces, EvaluateSpeedRamp(traveledCells));
        }

        public float GetLandingPause(int traveledCells)
        {
            return Mathf.Lerp(landingPause, minimumLandingPause, EvaluateSpeedRamp(traveledCells));
        }

        private void OnValidate()
        {
            initialBounceDelay = Mathf.Max(0f, initialBounceDelay);
            timeBetweenBounces = Mathf.Max(0f, timeBetweenBounces);
            minimumTimeBetweenBounces = Mathf.Max(0f, minimumTimeBetweenBounces);
            anticipationDuration = Mathf.Max(0f, anticipationDuration);
            anticipationDip = Mathf.Max(0f, anticipationDip);
            jumpDuration = Mathf.Max(0.05f, jumpDuration);
            minimumJumpDuration = Mathf.Max(0.05f, minimumJumpDuration);
            jumpArcHeight = Mathf.Max(0.1f, jumpArcHeight);
            landingPause = Mathf.Max(0f, landingPause);
            minimumLandingPause = Mathf.Max(0f, minimumLandingPause);
            speedRampDistanceCells = Mathf.Max(1, speedRampDistanceCells);
            retargetLockProgress = Mathf.Clamp(retargetLockProgress, 0.1f, 1f);
            maxExtraJumps = Mathf.Max(0, maxExtraJumps);
            extraForwardCells = Mathf.Max(1, extraForwardCells);
            extraJumpUpCells = Mathf.Max(1, extraJumpUpCells);
            scorePerForwardCell = Mathf.Max(0, scorePerForwardCell);
            scorePerCollectible = Mathf.Max(0, scorePerCollectible);
            failFallDuration = Mathf.Max(0.1f, failFallDuration);
            deathDepthCells = Mathf.Max(0.5f, deathDepthCells);
            landingSquashDuration = Mathf.Max(0.01f, landingSquashDuration);
            recoverDuration = Mathf.Max(0.01f, recoverDuration);

            minimumTimeBetweenBounces = Mathf.Min(minimumTimeBetweenBounces, timeBetweenBounces);
            minimumJumpDuration = Mathf.Min(minimumJumpDuration, jumpDuration);
            minimumLandingPause = Mathf.Min(minimumLandingPause, landingPause);
        }
    }
}
