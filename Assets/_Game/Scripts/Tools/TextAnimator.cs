using UnityEngine;
using UnityEditor;

using TMPro;
using DG.Tweening;
using Sirenix.OdinInspector;

public class TextAnimator : MonoBehaviour
{
    [Header("Main components")]
    public TextMeshProUGUI textTMP; // Reference to the TMP text component

    [Header("Animation Parameters"), Space]
    [Header("Counter")]
    [SerializeField] string counterPrefix = "x"; // Prefix for the counter text
    [SerializeField] float counterDuration = 0.25f; // Duration of the value change animation
    [SerializeField] Ease counter_EaseType = Ease.InOutCubic;

    [Header("Scale")]
    [SerializeField] Ease easeInType = Ease.InOutCubic;
    [SerializeField] Ease easeOutType = Ease.InOutCubic;
    [SerializeField] float scaleDuration = 0.2f; // Duration of the scaling animation
    [SerializeField] Vector3 increaseScale = new Vector3(1.2f, 1.2f, 1.2f); // Scale for increase animation
    [SerializeField] Vector3 decreaseScale = new Vector3(0.8f, 0.8f, 0.8f); // Scale for decrease animation

    [Header("Fade")]
    Tween fadeTween; // Tween for fading animation
    [SerializeField] float fadeInDuration = 1f; // Duration of the fading animation
    [SerializeField] float fadeOutDuration = 0.25f; // Duration of the fading animation

    int currentValue = 1;

    void Reset()
    {
        textTMP = GetComponent<TextMeshProUGUI>();
    }

    public void SetText(string _value)
    {
        textTMP.text = _value;
    }

    public void SetColor(Color _color)
    {
        textTMP.color = _color;
    }
    
    [Button("Animate Counter")]
    public void SetAndAnimateLandingQuality(string _targetValue, bool _isScaleUp)
    {
        textTMP.text = $"{_targetValue}";

        AnimateScale(_isScaleUp);
        AnimateFadeIn();
    }

    [Button("Animate Multiplier")]
    public void SetAndAnimateMultiplier(int _targetValue)
    {
        textTMP.text = $"{counterPrefix}{_targetValue}";

        // Determine if the target value is greater or less than the current value
        bool isIncreasing = _targetValue > currentValue;

        AnimateScale(isIncreasing);

        // Animate the counter value
        DOTween.To(() => currentValue, x => currentValue = x, _targetValue, counterDuration).SetEase(counter_EaseType).
        OnComplete(() =>
        {
            textTMP.transform.DOScale(Vector3.one, scaleDuration);
        });
    }

    // Plain HUD counters use the same scale animation without a multiplier prefix.
    public void SetAndAnimateCounter(int targetValue)
    {
        bool isIncreasing = targetValue > currentValue;
        SetCounterValue(targetValue);
        AnimateScale(isIncreasing);
    }

    public void SetCounterValue(int value)
    {
        currentValue = value;
        textTMP.text = value.ToString();
    }

    void AnimateScale(bool _isIncreasing = true, float _scaleDuration = 0.1f)
    {
        Vector3 targetScale = _isIncreasing ? increaseScale : decreaseScale;

        textTMP.transform.DOScale(targetScale, _scaleDuration).SetEase(easeInType).OnComplete(() =>
        {
            textTMP.transform.DOScale(Vector3.one, _scaleDuration).SetEase(easeOutType);
        });
    }

    void AnimateFadeIn()
    {
        if(fadeTween != null && fadeTween.IsActive())
        {
            fadeTween.Kill();
        }

        fadeTween = textTMP.DOFade(1, fadeInDuration).SetEase(easeInType).OnComplete(() =>
        {
            AnimateFadeOut();
        });
    }

    void AnimateFadeOut()
    {
        if(fadeTween != null && fadeTween.IsActive())
        {
            fadeTween.Kill();
        }

        fadeTween = textTMP.DOFade(0, fadeOutDuration).SetEase(easeOutType);
    }

    public int Get_CurrentValue()
    {
        return currentValue;
    }

#region Debugging
    [Header("Debug")]
    [SerializeField] string targetValue_Debugging = "Text";

    #if (UNITY_EDITOR)   
        [CustomEditor(typeof(CounterAnimator))]
        public class CustomInspector : Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
        
                CounterAnimator counterAnimator = (CounterAnimator) target;
        
                if(GUILayout.Button("Animate value"))
                {
                    counterAnimator.AnimateCounter_debugging();
                }
            }
        }
    #endif
#endregion --- Debugging ---
}