using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FunGuy.Runner
{
    [DisallowMultipleComponent]
    public sealed class RunnerScoreView : MonoBehaviour
    {
        [SerializeField] private Text scoreText;
        [SerializeField] private string label = "SCORE";
        [SerializeField] private int minimumDigits = 5;
        [SerializeField] private float popDuration = 0.16f;
        [SerializeField] private float popScaleMultiplier = 1.08f;

        private int displayedScore;

        private void Reset()
        {
            scoreText = GetComponent<Text>();
        }

        private void Awake()
        {
            if (scoreText == null)
            {
                scoreText = GetComponent<Text>();
            }

            RefreshText(displayedScore);
        }

        private void OnEnable()
        {
            RunnerGameEvents.ScoreChanged += Apply;
            RefreshText(displayedScore);
        }

        private void OnDisable()
        {
            RunnerGameEvents.ScoreChanged -= Apply;

            if (scoreText != null)
            {
                scoreText.rectTransform.DOKill();
            }
        }

        public void Apply(int score)
        {
            int clampedScore = Mathf.Max(0, score);
            bool shouldAnimate = clampedScore > displayedScore;
            displayedScore = clampedScore;
            RefreshText(displayedScore);

            if (!shouldAnimate || scoreText == null)
            {
                return;
            }

            RectTransform rectTransform = scoreText.rectTransform;
            rectTransform.DOKill();
            rectTransform.localScale = Vector3.one * popScaleMultiplier;
            rectTransform
                .DOScale(Vector3.one, popDuration)
                .SetEase(Ease.OutBack)
                .SetRecyclable(true);
        }

        private void RefreshText(int score)
        {
            if (scoreText == null)
            {
                return;
            }

            scoreText.text = $"{label}\n{score.ToString().PadLeft(minimumDigits, '0')}";
        }
    }
}
