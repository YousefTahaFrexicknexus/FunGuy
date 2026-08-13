using UnityEngine;
using UnityEngine.UI;

public class AppUpdate_Popup : MonoBehaviour
{
    [Header("Main Components")]
    public HorizontalLayoutGroup horizontalLayoutGroup;
    [SerializeField] UIButtonAnimator closeButton;
    [SerializeField] UIButtonAnimator laterButton;
    [SerializeField] UIButtonAnimator updateButton;

    void OnEnable()
    {
        closeButton.OnClickAction += OnClick_CloseBtn;
        laterButton.OnClickAction += OnClick_LaterBtn;
        updateButton.OnClickAction += OnClick_UpdateBtn;
    }

    void OnDisable()
    {
        closeButton.OnClickAction -= OnClick_CloseBtn;
        laterButton.OnClickAction -= OnClick_LaterBtn;
        updateButton.OnClickAction -= OnClick_UpdateBtn;
    }

    public void Init()
    {
        if (AppUpdateManager.Instance.appUpdateCheckResult == AppUpdateCheckResult.SoftUpdateAvailable)
        {
            laterButton.gameObject.SetActive(true);
            closeButton.gameObject.SetActive(true);
        }
        else
        {
            laterButton.gameObject.SetActive(false);
            closeButton.gameObject.SetActive(false);
        }
    }

    public void OnClick_UpdateBtn()
    {
        AppUpdateManager.Instance.PerformUpdate();
    }

    public void OnClick_CloseBtn()
    {
        UIManager.Instance.Close_PopupsAndPanels(UIType.appUpdate_Popup, true);
    }

    public void OnClick_LaterBtn()
    {
        UIManager.Instance.Close_PopupsAndPanels(UIType.appUpdate_Popup, true);
    }
}