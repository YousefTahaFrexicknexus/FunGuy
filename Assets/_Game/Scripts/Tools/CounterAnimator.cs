using UnityEngine;
using UnityEditor;

using TMPro;
using DG.Tweening;

public class CounterAnimator : MonoBehaviour
{
    [Header("Main components")]
    public TextMeshProUGUI counterText; // Reference to the TMP text component

    [Header("Animation Parameters"), Space]
    [Header("Counter")]
    [SerializeField] Ease counter_EaseType = Ease.InOutCubic;
    [SerializeField] float counterDuration = 1f; // Duration of the value change animation

    [Header("Scale")]
    [SerializeField] Ease scale_EaseType = Ease.InOutCubic;
    [SerializeField] float scaleDuration = 0.2f; // Duration of the scaling animation
    [SerializeField] Vector3 increaseScale = new Vector3(1.2f, 1.2f, 1.2f); // Scale for increase animation
    [SerializeField] Vector3 decreaseScale = new Vector3(0.8f, 0.8f, 0.8f); // Scale for decrease animation

    int currentValue = 0;

    public void Set_CounterText(string _value)
    {
        int valueTemp = 0;

        if(int.TryParse(_value, out valueTemp))
        {
            currentValue = valueTemp;
        }

        counterText.text = _value;
    }

    // Update the counter text with the current value
    void UpdateCounterText()
    {
        counterText.text = currentValue.ToString();
    }

    // Update the counter text with the current value
    void UpdateProgressText()
    {
        counterText.text = $"{currentValue.ToString()}%";
    }

    public void AnimateCounter(int _targetValue)
    {
        // Determine if the target value is greater or less than the current value
        bool isIncreasing = _targetValue > currentValue;

        // Animate the counter value
        DOTween.To(() => currentValue, x => currentValue = x, _targetValue, counterDuration)
               .SetEase(counter_EaseType)
               .OnUpdate(UpdateCounterText)
               .OnStart(() => AnimateScale(isIncreasing))
               .OnComplete(() => counterText.transform.DOScale(Vector3.one, scaleDuration));
    }

    public void AnimateCounter(int _targetValue, float _duration)
    {
        DOTween.To(() => currentValue, x => currentValue = x, _targetValue, _duration)
                .SetEase(counter_EaseType)
                .OnUpdate(UpdateCounterText);
    }

    public void AnimateProgress(int _targetValue, float Duration)
    {
        // Animate the counter value
        DOTween.To(() => currentValue, x => currentValue = x, _targetValue, Duration)
               .SetEase(counter_EaseType)
               .OnUpdate(() =>
                {
                    UpdateProgressText();

                    if(_targetValue > 0)
                    {
                        AnimateScale(true, 0.025f);
                    }
                });
    }

    // Animate the scale of the text based on whether the value is increasing or decreasing
    void AnimateScale(bool _isIncreasing = true)
    {
        Vector3 targetScale = _isIncreasing ? increaseScale : decreaseScale;

        counterText.transform.DOScale(targetScale, scaleDuration)
                             .SetEase(scale_EaseType)
                             .SetLoops(2, LoopType.Yoyo);
    }

    void AnimateScale(bool _isIncreasing = true, float _scaleDuration = 0.1f)
    {
        Vector3 targetScale = _isIncreasing ? increaseScale : decreaseScale;

        counterText.transform.DOScale(targetScale, _scaleDuration)
                             .SetEase(scale_EaseType)
                             .OnComplete(() => counterText.transform.DOScale(Vector3.one, _scaleDuration));
    }

    public void AnimateCounter_debugging()
    {
        AnimateCounter(targetValue_Debugging);
    }

    public int Get_CurrentValue()
    {
        return currentValue;
    }

#region Debugging
    [Header("Debug")]
    [SerializeField] int targetValue_Debugging = 0;

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