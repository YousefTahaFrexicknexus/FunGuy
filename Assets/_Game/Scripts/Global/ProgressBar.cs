using System;

using UnityEngine;
using UnityEngine.UI;

using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;

[ExecuteInEditMode]
public class ProgressBar : MonoBehaviour
{
    [Header("Main Components")]
    [SerializeField] Image mask;
    [SerializeField] TMP_Text loadingStatusRTL;
    [SerializeField] CounterAnimator loadingProgress_CounterAnimator;

    [Header("Properties")]
    [SerializeField, Range(0, 100f)]
    [OnValueChanged(nameof(OnProgressChanged))] // Only called if changed via Inspector
    float currentProgress;

    void OnEnable()
    {
        SetLoadingProgress(0);
    }

    public void SetLoadingProgress(float _progress)
    {
        currentProgress = _progress;
        loadingProgress_CounterAnimator.Set_CounterText($"{(int)currentProgress * 100}%");
        mask.fillAmount = currentProgress;
    }

    public void AnimateLoadingProgress(float _progress)
    {
        currentProgress = _progress;
        loadingProgress_CounterAnimator.AnimateProgress((int)(currentProgress * 100) , 0.25f);
        mask.DOFillAmount(currentProgress, 0.25f);
    }
    
    public void BarReset()
    {
        currentProgress = 0;
        mask.fillAmount = 0;
    }

    #region For debugging
        // This method will be called whenever currentProgress is changed in the Inspector
        private void OnProgressChanged()
        {
            // Call your custom function here
            loadingProgress_CounterAnimator.Set_CounterText($"{(int)currentProgress}%");
            mask.fillAmount = currentProgress * 0.01f;
        }
    #endregion ---  For debugging ---
}