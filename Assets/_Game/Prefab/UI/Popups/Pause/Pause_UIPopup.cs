using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class Pause_UIPopup : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Button characterSelectButton;

    void OnEnable()
    {
       GameplayEvents.OnPauseGameplay?.Invoke(true); 

       SetupButtons();
    }

    void SetupButtons()
    {
        if (characterSelectButton != null)
        {
            characterSelectButton.interactable = (GameplayManager.Instance.currentGameplayState != GameplayState.gameplay);
        }
    }

    public void OnClick_Resume()
    {
        GameplayManager.Instance.OnPauseGameplay(false);
        UIManager.Instance.Close_PopupsAndPanels(UIType.Pause);
    }

    public void OnClick_CharacterSelect()
    {
        GameplayEvents.OnPauseGameplay?.Invoke(false);
        UIManager.Instance.Close_PopupsAndPanels(UIType.Pause);
        UIManager.Instance.Open_PopupsAndPanels(UIType.CharacterSelect);
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
        GameplayManager.Instance.OnPauseGameplay(false);
        GameplayManager.Instance.LoadMainMenu();
    }
}
