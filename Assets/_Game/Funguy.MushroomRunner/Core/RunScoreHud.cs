using UnityEngine;
using UnityEngine.UI;

using TMPro;

[DisallowMultipleComponent]
public sealed class RunScoreHud : MonoBehaviour
{
    [SerializeField, Tooltip("Score service this HUD listens to.")]
    RunScoreService scoreTracker;
    [SerializeField, Tooltip("Main label used to display the numeric score.")]
    TextMeshProUGUI scoreTMP;
    [SerializeField, Tooltip("Secondary label used for combo and airtime callouts.")]
    TextMeshProUGUI statusText;
    [SerializeField, Tooltip("String format used for the score text. {0} is the current score.")]
    string scoreFormat = "N0";

    Color defaultScoreColor;
    float statusVisibility;
    RunScoreSnapshot currentSnapshot;
    bool hasSnapshot;

    void Reset()
    {
        CacheDefaultVisuals();
    }

    void OnEnable()
    {
        CacheDefaultVisuals();

        if (scoreTracker != null)
        {
            scoreTracker.SnapshotChanged += HandleSnapshotChanged;
            HandleSnapshotChanged(scoreTracker.CurrentSnapshot);
        }

        RefreshMomentumVisuals(true, currentSnapshot);
    }

    void OnDisable()
    {
        if (scoreTracker != null)
        {
            scoreTracker.SnapshotChanged -= HandleSnapshotChanged;
        }
    }

    void Update()
    {
        if (!hasSnapshot)
        {
            return;
        }

        RefreshMomentumVisuals(false, currentSnapshot);
    }

    public void SetScoreTracker(RunScoreService tracker)
    {
        if (scoreTracker == tracker)
        {
            return;
        }

        if (scoreTracker != null)
        {
            scoreTracker.SnapshotChanged -= HandleSnapshotChanged;
        }

        scoreTracker = tracker;

        if (isActiveAndEnabled && scoreTracker != null)
        {
            scoreTracker.SnapshotChanged += HandleSnapshotChanged;
            HandleSnapshotChanged(scoreTracker.CurrentSnapshot);
        }
    }

    void HandleSnapshotChanged(RunScoreSnapshot snapshot)
    {
        CacheDefaultVisuals();
        currentSnapshot = snapshot;
        hasSnapshot = true;

        if (scoreTMP != null)
        {
            scoreTMP.text = snapshot.Score.ToString(scoreFormat);
        }

        RefreshMomentumVisuals(true, snapshot);
    }

    void CacheDefaultVisuals()
    {
        if (scoreTMP != null)
        {
            defaultScoreColor = scoreTMP.color;
        }
    }

    void RefreshMomentumVisuals(bool instant, RunScoreSnapshot snapshot)
    {
        if (scoreTMP == null)
        {
            return;
        }

        if (statusText == null || !hasSnapshot)
        {
            FadeMomentumVisuals(string.Empty, defaultScoreColor, 0f, instant);
            return;
        }

        string statusLabel = BuildStatusLabel
        (
            snapshot.HasActiveCombo,
            snapshot.IsAirborne,
            snapshot.HasQualifiedAirtime,
            snapshot.HasQualifiedAirtimeMultiplier,
            snapshot.ComboHits,
            snapshot.Multiplier,
            snapshot.CurrentAirtimeSeconds,
            snapshot.RewardedAirtimeSeconds
        );

        float emphasis = ResolveEmphasis
        (
            snapshot.HasActiveCombo,
            snapshot.IsAirborne,
            snapshot.HasQualifiedAirtime,
            snapshot.ComboHits,
            snapshot.Multiplier,
            snapshot.RewardedAirtimeSeconds
        );

        float urgency = snapshot.IsComboBreakPending && snapshot.ComboBreakDelaySeconds > 0f
            ? 1f - Mathf.Clamp01(snapshot.ComboBreakTimeRemainingSeconds / snapshot.ComboBreakDelaySeconds)
            : 0f;

        Color accentColor = ResolveAccentColor
        (
            snapshot.HasActiveCombo,
            snapshot.IsAirborne,
            snapshot.HasQualifiedAirtime,
            snapshot.ComboHits,
            snapshot.RewardedAirtimeSeconds
        );

        float pulseSpeed = 3.5f + (emphasis * 4.5f) + (urgency * 2f);
        float pulse = 0.5f + (0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed));
        float animatedEmphasis = emphasis * Mathf.Lerp(0.7f, 1f, pulse);

        FadeMomentumVisuals(statusLabel, accentColor, animatedEmphasis, instant);
    }

    void FadeMomentumVisuals(string statusLabel, Color accentColor, float emphasis, bool instant)
    {
        if (scoreTMP == null)
        {
            return;
        }

        float targetVisibility = string.IsNullOrEmpty(statusLabel) ? 0f : 1f;
        float deltaTime = instant ? 1f : Time.unscaledDeltaTime * 5f;

        statusVisibility = Mathf.MoveTowards(statusVisibility, targetVisibility, deltaTime);

        scoreTMP.color = Color.Lerp(defaultScoreColor, accentColor, Mathf.Clamp01((0.14f + (emphasis * 0.4f)) * statusVisibility));

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

    static string BuildStatusLabel(bool hasCombo, bool isAirborne, bool hasQualifiedAirtime, bool hasQualifiedAirtimeMultiplier, int comboHits,
                                float comboMultiplier, float airtimeSeconds, float rewardedAirtimeSeconds)
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

    static float ResolveEmphasis(bool hasCombo, bool isAirborne, bool hasQualifiedAirtime, int comboHits, float comboMultiplier, float rewardedAirtimeSeconds)
    {
        float comboEmphasis = hasCombo ? Mathf.Clamp01((comboMultiplier - 1f) + (comboHits * 0.1f)) : 0f;
        float airEmphasis = isAirborne && hasQualifiedAirtime ? Mathf.Clamp01(rewardedAirtimeSeconds / 2f) : 0f;
        return Mathf.Clamp01(Mathf.Max(comboEmphasis, airEmphasis));
    }

    static Color ResolveAccentColor(bool hasCombo, bool isAirborne, bool hasQualifiedAirtime, int comboHits, float rewardedAirtimeSeconds)
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