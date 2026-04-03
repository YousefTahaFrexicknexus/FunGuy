using DG.Tweening;
using UnityEngine;

namespace FunGuy.Runner
{
    [DisallowMultipleComponent]
    public sealed class SideApproachPlatformVisual : MonoBehaviour, IRunnerSpawnInitializable
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private float entryDistanceInCells = 1.4f;
        [SerializeField] private float entryDuration = 0.72f;
        [SerializeField] private float activeHoldDuration = 0.48f;
        [SerializeField] private float exitDuration = 0.82f;
        [SerializeField] private float landableLeadTime = 0.08f;
        [SerializeField] private float hoverHeight = 0.05f;
        [SerializeField] private float hoverDuration = 0.55f;
        [SerializeField] private float entryTiltDegrees = 8f;

        private static int alternatingSide = 1;

        private Sequence motionSequence;
        private Vector3 anchoredLocalPosition;
        private Vector3 startLocalPosition;
        private Vector3 exitLocalPosition;
        private GridSurfaceActor boundSurface;
        private int entrySide = 1;

        private void Awake()
        {
            ResolveVisualRoot();
            anchoredLocalPosition = visualRoot != null ? visualRoot.localPosition : Vector3.zero;
        }

        private void OnDisable()
        {
            ResetVisualState();
        }

        private void OnDestroy()
        {
            ResetVisualState();
        }

        public void InitializeOnSpawn(RunnerGridSystem gridSystem, Vector3Int cell)
        {
            ResolveVisualRoot();

            if (visualRoot == null)
            {
                return;
            }

            ResetVisualState();
            anchoredLocalPosition = visualRoot.localPosition;

            float cellWidth = 2f;

            if (gridSystem != null && gridSystem.Config != null)
            {
                cellWidth = Mathf.Max(0.5f, gridSystem.Config.cellSize.x);
            }

            entrySide = alternatingSide;
            alternatingSide *= -1;

            Vector3 sideOffset = Vector3.right * cellWidth * entryDistanceInCells * entrySide;
            startLocalPosition = anchoredLocalPosition + sideOffset;
            exitLocalPosition = anchoredLocalPosition - sideOffset;
            visualRoot.localPosition = startLocalPosition;
            visualRoot.localRotation = Quaternion.Euler(0f, entryTiltDegrees * -entrySide, 0f);
        }

        public void ScheduleArrival(GridSurfaceActor surface, float secondsUntilArrival)
        {
            ResolveVisualRoot();

            if (visualRoot == null || surface == null)
            {
                return;
            }

            boundSurface = surface;
            boundSurface.SetLandingEnabled(false);

            float travelDuration = Mathf.Min(entryDuration, Mathf.Max(0.24f, secondsUntilArrival));
            float waitBeforeEntry = Mathf.Max(0f, secondsUntilArrival - travelDuration);
            float enableOffset = waitBeforeEntry + Mathf.Max(0f, travelDuration - landableLeadTime);
            motionSequence = DOTween.Sequence().SetRecyclable(true);
            motionSequence.AppendInterval(waitBeforeEntry);
            motionSequence.AppendCallback(ResetToEntryState);
            motionSequence.Append(
                visualRoot.DOLocalMove(anchoredLocalPosition, travelDuration)
                    .SetEase(Ease.OutSine));
            motionSequence.Join(
                visualRoot.DOLocalRotate(Vector3.zero, travelDuration)
                    .SetEase(Ease.OutSine));
            motionSequence.InsertCallback(enableOffset, EnableLanding);
            motionSequence.AppendInterval(activeHoldDuration);
            motionSequence.AppendCallback(DisableLanding);
            motionSequence.Append(
                visualRoot.DOLocalMove(exitLocalPosition, exitDuration)
                    .SetEase(Ease.InSine));
            motionSequence.Join(
                visualRoot.DOLocalRotate(new Vector3(0f, entryTiltDegrees * entrySide, 0f), exitDuration)
                    .SetEase(Ease.InSine));
            motionSequence.Insert(
                waitBeforeEntry + travelDuration,
                visualRoot.DOLocalMoveY(anchoredLocalPosition.y + hoverHeight, hoverDuration)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine));
            motionSequence.OnKill(() =>
            {
                motionSequence = null;
                DisableLanding();
            });
            motionSequence.OnComplete(() =>
            {
                motionSequence = null;
                DisableLanding();
                visualRoot.localPosition = exitLocalPosition;
            });
        }

        private void ResetToEntryState()
        {
            if (visualRoot == null)
            {
                return;
            }

            visualRoot.localPosition = startLocalPosition;
            visualRoot.localRotation = Quaternion.Euler(0f, entryTiltDegrees * -entrySide, 0f);
        }

        private void EnableLanding()
        {
            boundSurface?.SetLandingEnabled(true);
        }

        private void DisableLanding()
        {
            boundSurface?.SetLandingEnabled(false);
        }

        private void ResolveVisualRoot()
        {
            if (visualRoot != null)
            {
                return;
            }

            visualRoot = transform.childCount > 0 ? transform.GetChild(0) : transform;
        }

        private void ResetVisualState()
        {
            if (motionSequence != null && motionSequence.IsActive())
            {
                motionSequence.Kill();
            }

            DisableLanding();

            if (visualRoot == null)
            {
                return;
            }

            visualRoot.localPosition = anchoredLocalPosition;
            visualRoot.localRotation = Quaternion.identity;
        }
    }
}
