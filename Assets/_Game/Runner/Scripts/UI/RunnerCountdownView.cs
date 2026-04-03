using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FunGuy.Runner
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class RunnerCountdownView : MonoBehaviour
    {
        [SerializeField] private Text countdownText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float popDuration = 0.24f;
        [SerializeField] private float fadeDuration = 0.18f;
        [SerializeField] private Vector3 hiddenScale = new(0.7f, 0.7f, 0.7f);
        [SerializeField] private Vector3 shownScale = Vector3.one;

        private RectTransform rectTransform;

        private void Awake()
        {
            ResolveReferences();
            HideImmediate();
        }

        private void OnEnable()
        {
            RunnerGameEvents.CountdownTick += ShowTick;
            RunnerGameEvents.CountdownFinished += HideAnimated;
        }

        private void OnDisable()
        {
            RunnerGameEvents.CountdownTick -= ShowTick;
            RunnerGameEvents.CountdownFinished -= HideAnimated;

            if (rectTransform != null)
            {
                rectTransform.DOKill();
            }

            if (canvasGroup != null)
            {
                canvasGroup.DOKill();
            }
        }

        private void ShowTick(int secondsRemaining)
        {
            ResolveReferences();

            if (countdownText == null || canvasGroup == null || rectTransform == null)
            {
                return;
            }

            countdownText.text = secondsRemaining.ToString();
            canvasGroup.DOKill();
            rectTransform.DOKill();

            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = false;
            rectTransform.localScale = shownScale * 1.28f;
            rectTransform.DOScale(shownScale, popDuration).SetEase(Ease.OutBack).SetRecyclable(true);
        }

        private void HideAnimated()
        {
            ResolveReferences();

            if (canvasGroup == null || rectTransform == null)
            {
                return;
            }

            canvasGroup.DOKill();
            rectTransform.DOKill();

            rectTransform.DOScale(hiddenScale, fadeDuration).SetEase(Ease.InBack).SetRecyclable(true);
            canvasGroup.DOFade(0f, fadeDuration).SetEase(Ease.OutQuad).SetRecyclable(true).OnComplete(() =>
            {
                if (countdownText != null)
                {
                    countdownText.text = string.Empty;
                }
            });
        }

        private void HideImmediate()
        {
            ResolveReferences();

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
            }

            if (rectTransform != null)
            {
                rectTransform.localScale = hiddenScale;
            }
        }

        private void ResolveReferences()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
            }

            if (countdownText == null)
            {
                countdownText = GetComponent<Text>();
            }

            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }
        }
    }
}
