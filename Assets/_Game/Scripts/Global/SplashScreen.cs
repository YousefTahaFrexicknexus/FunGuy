using UnityEngine;
using System.Collections;

public class SplashScreen : MonoBehaviour
{
    [Header("Main Components")]
    [SerializeField] UIPanelAnimator uiPanelAnimator;

    [Header("Main Properties")]
    public float duration = 2;

    public IEnumerator StartSplashScreenAnimation()
    {
        yield return new WaitForSeconds(uiPanelAnimator.duration * 2);
    }
}
