using System;
using UnityEngine;

namespace Funguy.IdkPlatformer
{
    public sealed class ForwardProgressScoreTracker : MonoBehaviour
    {
        [SerializeField] private Transform trackedTarget;
        [SerializeField] private bool autoFindPlayer = true;
        [SerializeField] private float scoreScale = 1f;

        private bool initialized;
        private float originZ;
        private float furthestZ;
        private int currentScore;

        public event Action<int> ScoreChanged;

        public Transform TrackedTarget => trackedTarget;

        public int CurrentScore => currentScore;

        public float FurthestForwardZ => furthestZ;

        public void SetTarget(Transform target)
        {
            trackedTarget = target;
            initialized = false;
        }

        public void ResetProgress(float startZ)
        {
            originZ = startZ;
            furthestZ = startZ;
            initialized = true;
            SetScore(0, true);
        }

        private void Awake()
        {
            ResolveTrackedTarget();
        }

        private void Update()
        {
            if (!ResolveTrackedTarget())
            {
                return;
            }

            if (!initialized)
            {
                ResetProgress(trackedTarget.position.z);
            }

            if (trackedTarget.position.z <= furthestZ)
            {
                return;
            }

            furthestZ = trackedTarget.position.z;
            int nextScore = Mathf.Max(0, Mathf.FloorToInt((furthestZ - originZ) * Mathf.Max(0.0001f, scoreScale)));
            SetScore(nextScore, false);
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

        private void SetScore(int score, bool forceNotify)
        {
            if (!forceNotify && score == currentScore)
            {
                return;
            }

            currentScore = Mathf.Max(0, score);
            ScoreChanged?.Invoke(currentScore);
        }
    }
}
