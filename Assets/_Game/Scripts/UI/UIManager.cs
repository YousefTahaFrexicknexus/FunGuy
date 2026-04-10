using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
public class UIManager : MonoBehaviour
{
    #region Instance | Singleton
    private static UIManager _instance;
    public static UIManager Instance
    {
        get
        {
            if (!_instance)
                _instance = GameObject.FindObjectOfType<UIManager>();
            return _instance;
        }
    }
    private void DontDestroyThis()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }
    #endregion --- Instance | Singleton ---
#region Vars
    [TabGroup("General"), Header("Main Components"), Space]
    [TabGroup("General"), SerializeField] List<PopupsOrPanel> popupsAndPanels;
#endregion --- Vars ---
    void Awake()
    {
        // DontDestroyThis();
    }
    public void Open_PopupsAndPanels(UIType _type, bool _forceOpen = false)
    {
        UIPanelAnimator uiPanelAnimator = GetPopupOrPanel_ByType(_type);
        if (uiPanelAnimator == null)
            return;
        uiPanelAnimator.gameObject.SetActive(true);
        if(_forceOpen)
            uiPanelAnimator.OnClick_ForceOpen();
        else
            uiPanelAnimator.OnClick_Open();
    }
    public void Close_PopupsAndPanels(UIType _type, bool _forceClose = false)
    {
        UIPanelAnimator uiPanelAnimator = GetPopupOrPanel_ByType(_type);
        if (uiPanelAnimator == null)
            return;
        if(_forceClose)
            uiPanelAnimator.OnClick_ForceClose();
        else
            uiPanelAnimator.OnClick_Close();
    }
    public bool IsPopupOpen(UIType _type)
    {
        UIPanelAnimator uiPanelAnimator = GetPopupOrPanel_ByType(_type);
        if (uiPanelAnimator == null)
            return false;
        return uiPanelAnimator.gameObject.activeInHierarchy;
    }
    public UIPanelAnimator GetPopupOrPanel_ByType(UIType _Type)
    {
        var found = popupsAndPanels.Find(UIPanel => UIPanel.type == _Type);
        return found != null ? found.uiPanelAnimator : null;
    }
    //check if any popup is open
    public bool IsAnyPopupOpen()
    {
        foreach (var popup in popupsAndPanels)
        {
            if (popup.uiPanelAnimator.gameObject.activeInHierarchy)
            {
                return true;
            }
        }
        return false;
    }
}
[Serializable]
public class PopupsOrPanel
{
    public UIType type;
    public UIPanelAnimator uiPanelAnimator;
}
public enum UIType
{
    language,
    login,
    noInternet,
    profile,
    editProfile,
    noCoins,
    linkProfile,
    MarbleIdentification,
    CardinfoPopup,
    discardAllPopup,
    multipleChoicePanel,//// Panel for multiple choices (Ace, Jack, King)
    stepSelectionPanel, // Panel for selecting specific step numbers (Seven)
    gameEndPopup, ///simple popup for game end
    exitPopup, // exit screen for gameplay
    showHambergurMenu,
    showRulesPopup,
    showSoundsPopup,
    discardAny,
    rejoinPopup,
    CheatMenu_Popup,
    loadingPanel,
    connectionLostPanel,
    matchmakingPanel,
    appUpdate_Popup,
    opponentProfile,
    purchaseSuccessfull_Popup,
    serverUnreachable,
    ErrorPopup,
}
