using DG.Tweening;
using UnityEngine;

public class CharacterSelect_UIPanel : MonoBehaviour
{
    [Header("References")]
    [SerializeField] UIPanelAnimator characterSelectUIPanelAnimator;
    [SerializeField] HUD_UI hudUIPanel;

    void OnEnable()
    {
        GameplayEvents.OnCharacterSelectActivated?.Invoke();
        GameplayEvents.OnCharacterSelected += OnCharacterSelected;

        hudUIPanel.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        GameplayEvents.OnCharacterSelectDeactivated?.Invoke();
        GameplayEvents.OnCharacterSelected -= OnCharacterSelected;

        hudUIPanel.gameObject.SetActive(true);
    }

    public void OnCharacterSelected(int _selectedIndex)
    {
        UIManager.Instance.Close_PopupsAndPanels(UIType.CharacterSelect);
    }
}
