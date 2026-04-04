using System;
using DG.Tweening;
using UnityEngine;

namespace FunGuy.Runner
{
    public sealed class GridJumpMotor : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;

        private RunnerPlayerConfig config;
        private Sequence activeSequence;
        private Vector3 baseScale = Vector3.one;
        private bool hasCapturedBaseScale;

        private Vector3 flightStart;
        private Vector3 flightSegmentStart;
        private Vector3 flightTarget;
        private float flightProgressNormalized;
        private float flightSegmentStartProgress;
        private bool isInFlight;
        private bool prioritizeVerticalMovement;
        private int flightForwardCells = 1;
        private int flightUpwardCells;

        public bool IsInFlight => isInFlight;
        public float FlightProgressNormalized => flightProgressNormalized;

        private void Awake()
        {
            ResolveVisualRoot();
        }

        private void OnDestroy()
        {
            KillActiveSequence();
        }

        public void Configure(RunnerPlayerConfig playerConfig)
        {
            config = playerConfig;
            ResolveVisualRoot();
        }

        public void SnapTo(Vector3 worldPosition)
        {
            KillActiveSequence();
            if (visualRoot != null)
            {
                visualRoot.DOKill();
            }

            isInFlight = false;
            flightProgressNormalized = 0f;
            flightSegmentStartProgress = 0f;
            prioritizeVerticalMovement = false;
            flightForwardCells = 1;
            flightUpwardCells = 0;
            transform.position = worldPosition;

            if (visualRoot != null)
            {
                visualRoot.localScale = baseScale;
                visualRoot.localRotation = Quaternion.identity;
            }
        }

        public void BeginJump(
            Vector3 from,
            Vector3 to,
            float jumpDuration,
            int forwardCells,
            int upwardCells,
            Action onFlightComplete)
        {
            KillActiveSequence();
            ResolveVisualRoot();
            if (visualRoot != null)
            {
                visualRoot.DOKill();
            }

            transform.position = from;
            visualRoot.localScale = baseScale;
            isInFlight = false;
            flightProgressNormalized = 0f;
            flightSegmentStartProgress = 0f;
            flightForwardCells = Mathf.Max(1, forwardCells);
            flightUpwardCells = Mathf.Max(0, upwardCells);
            prioritizeVerticalMovement = to.y > from.y + 0.01f;

            activeSequence = DOTween.Sequence()
                .SetRecyclable(true)
                .OnKill(() =>
                {
                    activeSequence = null;
                    isInFlight = false;
                });

            AppendAnticipation(activeSequence, from);
            activeSequence.AppendCallback(() =>
            {
                flightStart = transform.position;
                flightSegmentStart = flightStart;
                flightTarget = to;
                flightProgressNormalized = 0f;
                flightSegmentStartProgress = 0f;
                isInFlight = true;
                PlayLaunchStretch();
            });
            activeSequence.Append(
                DOVirtual.Float(0f, 1f, jumpDuration, value =>
                {
                    flightProgressNormalized = value;
                    UpdateFlightTransform();
                }).SetEase(config != null ? config.JumpEase : Ease.Linear));
            activeSequence.AppendCallback(() =>
            {
                isInFlight = false;
                flightProgressNormalized = 1f;
                transform.position = flightTarget;
                onFlightComplete?.Invoke();
            });
        }

        public void RetargetLanding(Vector3 newTarget, int forwardCells, int upwardCells, bool verticalPriority = false)
        {
            if (isInFlight)
            {
                flightSegmentStart = transform.position;
                flightSegmentStartProgress = flightProgressNormalized;
            }

            flightTarget = newTarget;
            flightForwardCells = Mathf.Max(1, forwardCells);
            flightUpwardCells = Mathf.Max(0, upwardCells);
            prioritizeVerticalMovement = verticalPriority || flightTarget.y > flightSegmentStart.y + 0.01f;

            if (isInFlight)
            {
                UpdateFlightTransform();
            }
        }

        public void PlayLandingImpact()
        {
            if (visualRoot == null || config == null)
            {
                return;
            }

            visualRoot.DOKill();
            visualRoot.localScale = Vector3.Scale(baseScale, config.LandingScale);
            visualRoot.DOScale(baseScale, config.LandingSquashDuration + config.RecoverDuration)
                .SetEase(config.RecoverEase)
                .SetRecyclable(true);
        }

        public void PlayFallFromCurrent(float deathY, Action onComplete)
        {
            KillActiveSequence();
            isInFlight = false;
            flightProgressNormalized = 0f;
            flightSegmentStartProgress = 0f;
            prioritizeVerticalMovement = false;

            float fallDuration = config != null ? config.FailFallDuration : 0.35f;

            if (visualRoot != null)
            {
                visualRoot.DOKill();
                visualRoot.DOScale(baseScale * 0.78f, fallDuration).SetEase(Ease.InQuad).SetRecyclable(true);
            }

            activeSequence = DOTween.Sequence()
                .SetRecyclable(true)
                .OnKill(() => activeSequence = null);

            activeSequence.Append(transform.DOMoveY(deathY, fallDuration).SetEase(Ease.InQuad));
            activeSequence.OnComplete(() => onComplete?.Invoke());
        }

        private void UpdateFlightTransform()
        {
            float remainingProgress = Mathf.Max(0.0001f, 1f - flightSegmentStartProgress);
            float segmentProgress = Mathf.Clamp01((flightProgressNormalized - flightSegmentStartProgress) / remainingProgress);

            float horizontalProgress = segmentProgress;
            float verticalProgress = segmentProgress;

            if (prioritizeVerticalMovement && flightTarget.y > flightSegmentStart.y + 0.01f)
            {
                verticalProgress = 1f - Mathf.Pow(1f - segmentProgress, 2f);
                horizontalProgress = segmentProgress * segmentProgress;
            }

            Vector3 position = new(
                Mathf.LerpUnclamped(flightSegmentStart.x, flightTarget.x, horizontalProgress),
                Mathf.LerpUnclamped(flightSegmentStart.y, flightTarget.y, verticalProgress),
                Mathf.LerpUnclamped(flightSegmentStart.z, flightTarget.z, horizontalProgress));

            float segmentArcMultiplier = Mathf.Lerp(0.35f, 1f, remainingProgress);
            float arcHeight = config != null
                ? config.GetJumpArcHeight(flightForwardCells, flightUpwardCells)
                : 1f;
            float arc = 4f * (arcHeight * segmentArcMultiplier) * segmentProgress * (1f - segmentProgress);
            transform.position = position + Vector3.up * arc;
        }

        private void AppendAnticipation(Sequence sequence, Vector3 from)
        {
            if (config == null || visualRoot == null || config.AnticipationDuration <= 0f)
            {
                return;
            }

            sequence.Append(visualRoot.DOScale(Vector3.Scale(baseScale, config.AnticipationScale), config.AnticipationDuration).SetEase(Ease.OutQuad));
            sequence.Join(transform.DOMoveY(from.y - config.AnticipationDip, config.AnticipationDuration).SetEase(Ease.OutQuad));
        }

        private void PlayLaunchStretch()
        {
            if (visualRoot != null && config != null)
            {
                visualRoot.localScale = Vector3.Scale(baseScale, config.LaunchScale);
            }
        }

        private void ResolveVisualRoot()
        {
            if (visualRoot == null)
            {
                visualRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
            }

            if (!hasCapturedBaseScale)
            {
                baseScale = visualRoot.localScale;
                hasCapturedBaseScale = true;
            }
        }

        private void KillActiveSequence()
        {
            if (activeSequence != null && activeSequence.IsActive())
            {
                activeSequence.Kill();
            }
        }
    }
}
