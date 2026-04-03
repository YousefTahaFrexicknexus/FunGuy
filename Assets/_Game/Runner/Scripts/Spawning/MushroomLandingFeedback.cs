using DG.Tweening;
using UnityEngine;

namespace FunGuy.Runner
{
    [DisallowMultipleComponent]
    public sealed class MushroomLandingFeedback : MonoBehaviour, IRunnerSurfaceLandingFeedback
    {
        [SerializeField] private Transform cap;
        [SerializeField] private Vector3 landingScale = new(1.025f, 0.975f, 1.025f);
        [SerializeField] private float squashDuration = 0.04f;
        [SerializeField] private float recoverDuration = 0.07f;

        private Vector3 baseScale = Vector3.one;
        private bool hasCapturedBaseScale;

        private void Awake()
        {
            ResolveCap();
            CaptureBaseScale();
        }

        private void OnDisable()
        {
            if (cap != null)
            {
                cap.DOKill();
                cap.localScale = baseScale;
            }
        }

        public void PlayLandingFeedback()
        {
            ResolveCap();
            CaptureBaseScale();

            if (cap == null)
            {
                return;
            }

            cap.DOKill();
            cap.localScale = Vector3.Scale(baseScale, landingScale);
            cap.DOScale(baseScale, squashDuration + recoverDuration)
                .SetEase(Ease.OutQuad)
                .SetRecyclable(true);
        }

        private void ResolveCap()
        {
            if (cap != null)
            {
                return;
            }

            Transform foundCap = transform.Find("Cap");
            if (foundCap != null)
            {
                cap = foundCap;
            }
        }

        private void CaptureBaseScale()
        {
            if (hasCapturedBaseScale || cap == null)
            {
                return;
            }

            baseScale = cap.localScale;
            hasCapturedBaseScale = true;
        }
    }
}
