using UnityEngine;

public class Pause_UIPopup : MonoBehaviour
{
    public void OnClick_Resume()
    {
        UIManager.Instance.Close_PopupsAndPanels(UIType.Pause);

        // TODO: Tell UI Manager to close all gameplay popups and resume gameplay
    }

    public void OnClick_Restart()
    {
        UIManager.Instance.Close_PopupsAndPanels(UIType.Pause);

        // TODO: Tell UI Manager to close all gameplay popups and restart game
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
