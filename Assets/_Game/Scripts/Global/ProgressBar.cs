using System;

using UnityEngine;
using UnityEngine.UI;

using DG.Tweening;
using Sirenix.OdinInspector;

[ExecuteInEditMode]
public class ProgressBar : MonoBehaviour
{
    [Header("Main Components")]
    [SerializeField] Image mask;

    [Header("Properties")]
    [SerializeField] float rangeMin = 0f;
    [SerializeField] float rangeMax = 1f;
    
    [SerializeField, Range(0, 1)]
    [OnValueChanged(nameof(OnProgressChanged))] // Only called if changed via Inspector
    float currentProgress;

    void OnEnable()
    {
        SetProgressValue(0);
    }

    public void ChangeProgressValue(float _progress)
    {
        currentProgress = Mathf.InverseLerp(rangeMin, rangeMax, _progress);
        mask.fillAmount = currentProgress;
    }

    public void SetProgressValue(float _progress)
    {
        currentProgress = _progress;
        mask.fillAmount = currentProgress;
    }

    public void AnimateProgress(float _progress)
    {
        currentProgress = _progress;
        mask.DOFillAmount(currentProgress, 0.25f);
    }
    
    public void BarReset()
    {
        currentProgress = rangeMin;
        mask.fillAmount = 0;
    }

    #region For debugging
    void OnProgressChanged()
    {
        mask.fillAmount = currentProgress;
    }
    #endregion ---  For debugging ---
}