using UnityEngine;
using UnityEngine.UI;

namespace Funguy.IdkPlatformer
{
    [DisallowMultipleComponent]
    public sealed class ForwardProgressScoreView : MonoBehaviour
    {
        [SerializeField] private ForwardProgressScoreTracker scoreTracker;
        [SerializeField] private Text scoreText;
        [SerializeField] private string scoreFormat = "SCORE {0:0000}";

        private void Reset()
        {
            scoreText = GetComponent<Text>();
            scoreTracker = FindFirstObjectByType<ForwardProgressScoreTracker>();
        }

        private void OnEnable()
        {
            if (scoreText == null)
            {
                scoreText = GetComponent<Text>();
            }

            if (scoreTracker == null)
            {
                scoreTracker = FindFirstObjectByType<ForwardProgressScoreTracker>();
            }

            if (scoreTracker != null)
            {
                scoreTracker.ScoreChanged += HandleScoreChanged;
                HandleScoreChanged(scoreTracker.CurrentScore);
            }
        }

        private void OnDisable()
        {
            if (scoreTracker != null)
            {
                scoreTracker.ScoreChanged -= HandleScoreChanged;
            }
        }

        public void SetScoreTracker(ForwardProgressScoreTracker tracker)
        {
            if (scoreTracker == tracker)
            {
                return;
            }

            if (scoreTracker != null)
            {
                scoreTracker.ScoreChanged -= HandleScoreChanged;
            }

            scoreTracker = tracker;

            if (isActiveAndEnabled && scoreTracker != null)
            {
                scoreTracker.ScoreChanged += HandleScoreChanged;
                HandleScoreChanged(scoreTracker.CurrentScore);
            }
        }

        private void HandleScoreChanged(int score)
        {
            if (scoreText == null)
            {
                return;
            }

            scoreText.text = string.Format(scoreFormat, Mathf.Max(0, score));
        }
    }
}
