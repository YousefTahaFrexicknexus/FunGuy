using UnityEngine;
using UnityEngine.UI;

using TMPro;
using DG.Tweening;

public class SpeedMeter_HUDUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] ProgressBar speedMeter;
    [SerializeField] Scrollbar speedMeterScrollbar;
    [SerializeField] TextMeshProUGUI speedText;
    [SerializeField] RunnerMovementMotor movementMotor;

    [Header("Animation")]
    [SerializeField, Min(0f)] float tierSlideDuration = 0.3f;

    Tween handleTween;
    float handleTarget = -1f;
    float displayedProgress;

    void OnEnable()
    {
        if (movementMotor == null)
        {
            movementMotor = FindFirstObjectByType<RunnerMovementMotor>();
        }

        handleTarget = -1f;
        UpdateTierHandle();
    }

    void LateUpdate()
    {
        // Read the player's current profile and bound tier source, including after resets/profile changes.
        UpdateTierHandle();
        float speed = movementMotor != null ? movementMotor.Velocity.z : 0f;
        UpdateSpeedMeter(speed, movementMotor != null ? movementMotor.CurrentMaxSpeed : 0f);
    }

    void OnDisable()
    {
        handleTween?.Kill();
        handleTween = null;
    }

    public void UpdateSpeedMeter(float _speed, float _maxSpeed)
    {
        // The full bar always represents x4, rather than the active tier's maximum.
        MovementTuningProfile profile = movementMotor != null ? movementMotor.TuningProfile : null;
        float fullBarSpeed = profile != null ? profile.GetMaxSpeed(MomentumTier.Maximum) : 0f;
        float normalizedSpeed = fullBarSpeed > 0f ? Mathf.Clamp01(_speed / fullBarSpeed) : 0f;

        if (speedMeter != null)
        {
            // Smooth the fill without starting overlapping tweens every movement update.
            displayedProgress = Mathf.Lerp(displayedProgress, normalizedSpeed, 1f - Mathf.Exp(-12f * Time.deltaTime));
            speedMeter.SetProgressValue(displayedProgress);
        }

        if (speedText != null)
        {
            speedText.text = Mathf.Round(_speed).ToString();
        }

    }

    void UpdateTierHandle()
    {
        if (speedMeterScrollbar == null)
        {
            return;
        }

        MomentumSystem momentum = movementMotor != null ? movementMotor.MomentumSource : null;
        MomentumTier tier = momentum != null ? momentum.CurrentTier : MomentumTier.Low;
        float target = tier switch
        {
            MomentumTier.Medium => 0.5f,
            MomentumTier.High => 0.75f,
            MomentumTier.Maximum => 1f,
            _ => 0.25f
        };

        if (Mathf.Approximately(handleTarget, target))
        {
            return;
        }

        bool initialize = handleTarget < 0f;
        handleTarget = target;
        handleTween?.Kill();
        handleTween = null;

        if (initialize || tierSlideDuration <= 0f)
        {
            speedMeterScrollbar.SetValueWithoutNotify(target);
        }
        else
        {
            handleTween = DOTween.To(() => speedMeterScrollbar.value,
                value => speedMeterScrollbar.SetValueWithoutNotify(value), target, tierSlideDuration)
                .SetEase(Ease.InOutSine);
        }
    }
}
