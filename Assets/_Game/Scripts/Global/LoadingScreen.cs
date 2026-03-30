using System;
using System.Collections;
using System.Collections.Generic;

using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreen : MonoBehaviour
{
	[Header("Main Components")]
    [SerializeField] CanvasGroup this_CanvasGroup;
    [SerializeField] TMP_Text loadingText;
    [SerializeField] ProgressBar loading_ProgressBar;

	[Header("Properties")]
    [SerializeField] float fadeDuration = 1;
    [SerializeField] List<LoadingString> loadingStrings;

    public void FadeIn(bool isProgressBarActive = true)
	{
        SetActivity_ProgressBar(isProgressBarActive);

		StopCoroutine(nameof(FadeProcess));
		StartCoroutine(FadeProcess(true));
	}

	public void FadeOut()
	{
		StopCoroutine(nameof(FadeProcess));
		StartCoroutine(FadeProcess(false));
	}

    public void SetActivity_ProgressBar(bool _state)
    {
        if(_state)
            loading_ProgressBar.BarReset();

        loading_ProgressBar.gameObject.SetActive(_state);
    }

    public void SetProgress(float _progress)
    {
        loading_ProgressBar.SetLoadingProgress(_progress);
    }

    public void UpdateProgress(float _progress)
    {
        loading_ProgressBar.AnimateLoadingProgress(_progress);
    }

    public void SetProgressText(string text)
    {
        loadingText.text = text;
    }

    IEnumerator FadeProcess(bool isFadeIn)
	{
		this_CanvasGroup.DOFade(isFadeIn ? 1 : 0f, fadeDuration).OnComplete(()=>
		{
            if(!isFadeIn)
                gameObject.SetActive(false);
		});

		yield return null;
	}

    [Serializable]
    public class LoadingString
    {
        public string id;
        public string text;
    }
}