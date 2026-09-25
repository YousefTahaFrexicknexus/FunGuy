using DG.Tweening;
using UnityEngine;

public class Pause_UIPopup : MonoBehaviour
{
    void OnEnable()
    {
       GameplayEvents.OnPauseGameplay?.Invoke(true); 
    }

    public void OnClick_Resume()
    {
        GameplayManager.Instance.OnPauseGameplay(false);
        UIManager.Instance.Close_PopupsAndPanels(UIType.Pause);
    }

    public void OnClick_Restart()
    {
        UIManager.Instance.Close_PopupsAndPanels(UIType.Pause);

        GameplayManager.Instance.OnPauseGameplay(false);
        GameplayManager.Instance.RestartGame();
    }

    public void OnClick_Settings()
    {
        UIManager.Instance.Close_PopupsAndPanels(UIType.Pause);
        UIManager.Instance.Open_PopupsAndPanels(UIType.Settings);
    }

    public void OnClick_MainMenu()
    {
        
    }
}
