using UnityEngine;
using UnityEngine.UI;

namespace Funguy.IdkPlatformer
{
    [DisallowMultipleComponent]
    public sealed class ForwardProgressScoreView : MonoBehaviour
    {
        [SerializeField] private ForwardProgressScoreTracker scoreTracker;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text statusText;
        [SerializeField] private string scoreFormat = "SCORE {0:0000}";

        private Color defaultScoreColor;
        private float statusVisibility;

        private void Reset()
        {
            scoreText = GetComponent<Text>();
            scoreTracker = FindFirstObjectByType<ForwardProgressScoreTracker>();
            CacheDefaultVisuals();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (scoreTracker != null)
            {
                scoreTracker.ScoreChanged += HandleScoreChanged;
                HandleScoreChanged(scoreTracker.CurrentScore);
            }

            RefreshMomentumVisuals(true);
        }

        private void OnDisable()
        {
            if (scoreTracker != null)
            {
                scoreTracker.ScoreChanged -= HandleScoreChanged;
            }
        }

        private void Update()
        {
            ResolveReferences();
            RefreshMomentumVisuals(false);
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

            RefreshMomentumVisuals(true);
        }

        private void HandleScoreChanged(int score)
        {
            if (scoreText == null)
            {
                return;
            }

            scoreText.text = string.Format(scoreFormat, Mathf.Max(0, score));
        }

        private void ResolveReferences()
        {
            if (scoreText == null)
            {
                scoreText = GetComponent<Text>();
            }

            if (scoreTracker == null)
            {
                scoreTracker = FindFirstObjectByType<ForwardProgressScoreTracker>();
            }

            if (statusText == null)
            {
                statusText = FindExistingStatusText();
            }

            if (statusText == null && Application.isPlaying)
            {
                statusText = CreateStatusText();
            }

            CacheDefaultVisuals();
        }

        private void CacheDefaultVisuals()
        {
            if (scoreText != null && defaultScoreColor == default)
            {
                defaultScoreColor = scoreText.color;
            }
        }

        private Text FindExistingStatusText()
        {
            Text[] texts = GetComponentsInChildren<Text>(true);
            for (int index = 0; index < texts.Length; index++)
            {
                Text candidate = texts[index];
                if (candidate != null && candidate != scoreText)
                {
                    return candidate;
                }
            }

            return null;
        }

        private Text CreateStatusText()
        {
            GameObject statusObject = new("MomentumText", typeof(RectTransform), typeof(Text), typeof(Outline));
            statusObject.transform.SetParent(transform, false);

            RectTransform statusRect = statusObject.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.5f, 1f);
            statusRect.anchorMax = new Vector2(0.5f, 1f);
            statusRect.pivot = new Vector2(0.5f, 1f);
            statusRect.anchoredPosition = new Vector2(0f, -54f);
            statusRect.sizeDelta = new Vector2(560f, 40f);

            Text newStatusText = statusObject.GetComponent<Text>();
            newStatusText.font = scoreText != null && scoreText.font != null
                ? scoreText.font
                : Resources.GetBuiltinResource<Font>("Arial.ttf");
            newStatusText.fontSize = 24;
            newStatusText.fontStyle = FontStyle.Bold;
            newStatusText.alignment = TextAnchor.MiddleCenter;
            newStatusText.horizontalOverflow = HorizontalWrapMode.Overflow;
            newStatusText.verticalOverflow = VerticalWrapMode.Overflow;
            newStatusText.raycastTarget = false;
            newStatusText.text = string.Empty;
            newStatusText.color = new Color(1f, 1f, 1f, 0f);

            Outline outline = statusObject.GetComponent<Outline>();
            outline.effectColor = new Color(0.04f, 0.05f, 0.08f, 0.55f);
            outline.effectDistance = new Vector2(1.75f, -1.75f);

            return newStatusText;
        }

        private void RefreshMomentumVisuals(bool instant)
        {
            if (scoreText == null)
            {
                return;
            }

            if (statusText == null || scoreTracker == null)
            {
                FadeMomentumVisuals(string.Empty, defaultScoreColor, 0f, instant);
                return;
            }

            bool hasCombo = scoreTracker.HasActiveCombo;
            bool isAirborne = scoreTracker.IsAirborne;
            bool hasQualifiedAirtime = scoreTracker.HasQualifiedAirtime;
            bool hasQualifiedAirtimeMultiplier = scoreTracker.HasQualifiedAirtimeMultiplier;
            int comboHits = scoreTracker.CurrentComboHits;
            float comboMultiplier = scoreTracker.CurrentComboMultiplier;
            float airtimeSeconds = scoreTracker.CurrentAirtimeSeconds;
            float rewardedAirtimeSeconds = scoreTracker.RewardedAirtimeSeconds;
            float comboTimeRemaining = scoreTracker.ComboTimeRemainingSeconds;
            float comboBreakDelay = scoreTracker.ComboBreakDelaySeconds;
            bool isComboBreakPending = scoreTracker.IsComboBreakPending;

            string statusLabel = BuildStatusLabel(
                hasCombo,
                isAirborne,
                hasQualifiedAirtime,
                hasQualifiedAirtimeMultiplier,
                comboHits,
                comboMultiplier,
                airtimeSeconds,
                rewardedAirtimeSeconds);

            float emphasis = ResolveEmphasis(
                hasCombo,
                isAirborne,
                hasQualifiedAirtime,
                comboHits,
                comboMultiplier,
                rewardedAirtimeSeconds);
            float urgency = isComboBreakPending && comboBreakDelay > 0f
                ? 1f - Mathf.Clamp01(comboTimeRemaining / comboBreakDelay)
                : 0f;
            Color accentColor = ResolveAccentColor(
                hasCombo,
                isAirborne,
                hasQualifiedAirtime,
                comboHits,
                rewardedAirtimeSeconds);

            float pulseSpeed = 3.5f + (emphasis * 4.5f) + (urgency * 2f);
            float pulse = 0.5f + (0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed));
            float animatedEmphasis = emphasis * Mathf.Lerp(0.7f, 1f, pulse);

            FadeMomentumVisuals(statusLabel, accentColor, animatedEmphasis, instant);
        }

        private void FadeMomentumVisuals(string statusLabel, Color accentColor, float emphasis, bool instant)
        {
            if (scoreText == null)
            {
                return;
            }

            float targetVisibility = string.IsNullOrEmpty(statusLabel) ? 0f : 1f;
            float deltaTime = instant ? 1f : Time.unscaledDeltaTime * 5f;
            statusVisibility = Mathf.MoveTowards(statusVisibility, targetVisibility, deltaTime);

            scoreText.color = Color.Lerp(defaultScoreColor, accentColor, Mathf.Clamp01((0.14f + (emphasis * 0.4f)) * statusVisibility));

            if (statusText == null)
            {
                return;
            }

            statusText.text = statusLabel;
            Color statusColor = accentColor;
            statusColor.a = statusVisibility * Mathf.Lerp(0.72f, 1f, emphasis);
            statusText.color = statusColor;

            RectTransform statusRect = statusText.rectTransform;
            float scale = 1f + (0.05f * statusVisibility) + (0.11f * emphasis * statusVisibility);
            statusRect.localScale = Vector3.one * scale;

            if (statusText.TryGetComponent(out Outline outline))
            {
                outline.effectColor = new Color(0.04f, 0.05f, 0.08f, Mathf.Lerp(0.2f, 0.62f, statusVisibility));
            }

            if (statusVisibility <= 0f)
            {
                statusText.text = string.Empty;
                statusRect.localScale = Vector3.one;
            }
        }

        private static string BuildStatusLabel(
            bool hasCombo,
            bool isAirborne,
            bool hasQualifiedAirtime,
            bool hasQualifiedAirtimeMultiplier,
            int comboHits,
            float comboMultiplier,
            float airtimeSeconds,
            float rewardedAirtimeSeconds)
        {
            if (isAirborne && hasQualifiedAirtime)
            {
                if (hasCombo && hasQualifiedAirtimeMultiplier && comboMultiplier >= 2f)
                {
                    return $"SKY FEVER x{comboMultiplier:0.##}  AIR {airtimeSeconds:0.0}s";
                }

                if (hasCombo && hasQualifiedAirtimeMultiplier)
                {
                    return $"SKY BONUS x{comboMultiplier:0.##}  AIR {airtimeSeconds:0.0}s";
                }

                if (rewardedAirtimeSeconds >= 2f)
                {
                    return $"MEGA AIR {airtimeSeconds:0.0}s";
                }

                return $"BIG AIR {airtimeSeconds:0.0}s";
            }

            if (hasCombo && comboHits >= 5)
            {
                return $"BLAZE RUN x{comboMultiplier:0.##}";
            }

            if (hasCombo && comboHits >= 3)
            {
                return $"FEVER RUN x{comboMultiplier:0.##}";
            }

            if (hasCombo && comboHits >= 2)
            {
                return $"MUSHROOM STREAK x{comboMultiplier:0.##}";
            }

            if (hasCombo)
            {
                return "STREAK START";
            }

            return string.Empty;
        }

        private static float ResolveEmphasis(
            bool hasCombo,
            bool isAirborne,
            bool hasQualifiedAirtime,
            int comboHits,
            float comboMultiplier,
            float rewardedAirtimeSeconds)
        {
            float comboEmphasis = hasCombo ? Mathf.Clamp01((comboMultiplier - 1f) + (comboHits * 0.1f)) : 0f;
            float airEmphasis = isAirborne && hasQualifiedAirtime ? Mathf.Clamp01(rewardedAirtimeSeconds / 2f) : 0f;
            return Mathf.Clamp01(Mathf.Max(comboEmphasis, airEmphasis));
        }

        private static Color ResolveAccentColor(
            bool hasCombo,
            bool isAirborne,
            bool hasQualifiedAirtime,
            int comboHits,
            float rewardedAirtimeSeconds)
        {
            Color comboColor = new(1f, 0.82f, 0.29f, 1f);
            Color airColor = new(0.39f, 0.93f, 1f, 1f);
            Color frenzyColor = new(1f, 0.48f, 0.32f, 1f);

            if (hasCombo && isAirborne && hasQualifiedAirtime && (comboHits >= 4 || rewardedAirtimeSeconds >= 1.5f))
            {
                return frenzyColor;
            }

            if (hasCombo && isAirborne && hasQualifiedAirtime)
            {
                return Color.Lerp(airColor, comboColor, 0.5f);
            }

            if (hasCombo)
            {
                return comboColor;
            }

            if (isAirborne)
            {
                return airColor;
            }

            return Color.white;
        }
    }
}
