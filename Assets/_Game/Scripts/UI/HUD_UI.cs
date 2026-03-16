using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HUD_UI : MonoBehaviour
{
    [Header("Jump bar")]
    [SerializeField] Sprite jumpActive_Sprite;
    [SerializeField] Sprite jumpInactive_Sprite;
    [SerializeField] List<Image> jumpImages;

    void OnEnable()
    {
        RegisterGameplayEvents();
    }

    void OnDisable()
    {
        UnregisterGameplayEvents();
    }

    void RegisterGameplayEvents()
    {
        GameplayEvents.OnAirJump += OnAirJump;
        GameplayEvents.OnMushroomJump += OnMushroomJump;
    }

    void UnregisterGameplayEvents()
    {
        GameplayEvents.OnAirJump -= OnAirJump;
        GameplayEvents.OnMushroomJump -= OnMushroomJump;
    }

    void OnAirJump(int _jumpsLeft)
    {
        for(int i = 0; i < jumpImages.Count; i++)
        {
            jumpImages[i].sprite = i < _jumpsLeft ? jumpActive_Sprite : jumpInactive_Sprite;
        }
    }

    // Reset jump bar
    void OnMushroomJump()
    {
        foreach(Image jumpImage in jumpImages)
        {
            jumpImage.sprite = jumpActive_Sprite;
        }
    }
}