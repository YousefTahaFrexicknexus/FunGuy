using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FunGuy.Runner
{
    public sealed class RunnerExtraJumpCounter : MonoBehaviour
    {
        [SerializeField] private List<Image> jumpIcons = new();
        [SerializeField] private Color activeColor = Color.white;
        [SerializeField] private Color inactiveColor = new(1f, 1f, 1f, 0.22f);
        [SerializeField] private Vector3 activeScale = Vector3.one;
        [SerializeField] private Vector3 inactiveScale = new(0.82f, 0.82f, 1f);
        [SerializeField] private float popDuration = 0.16f;

        private int previousCount = -1;

        private void OnEnable()
        {
            RunnerGameEvents.ExtraJumpsChanged += Apply;
        }

        private void OnDisable()
        {
            RunnerGameEvents.ExtraJumpsChanged -= Apply;
        }

        public void Apply(int current, int max)
        {
            for (int i = 0; i < jumpIcons.Count; i++)
            {
                bool inRange = i < max;
                jumpIcons[i].gameObject.SetActive(inRange);

                if (!inRange)
                {
                    continue;
                }

                bool active = i < current;
                jumpIcons[i].color = active ? activeColor : inactiveColor;
                jumpIcons[i].rectTransform.DOKill();
                jumpIcons[i].rectTransform.localScale = active ? activeScale : inactiveScale;

                if (active && current != previousCount && i == current - 1)
                {
                    jumpIcons[i].rectTransform.localScale = activeScale * 1.24f;
                    jumpIcons[i].rectTransform.DOScale(activeScale, popDuration).SetEase(Ease.OutBack).SetRecyclable(true);
                }
            }

            previousCount = current;
        }
    }
}
