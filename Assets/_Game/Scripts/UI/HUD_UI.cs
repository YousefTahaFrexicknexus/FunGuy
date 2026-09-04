using System.Collections.Generic;
using Sirenix.OdinInspector;

using UnityEngine;
using UnityEngine.UI;

using TMPro;

public class HUD_UI : MonoBehaviour
{
    [TabGroup("Score UI")]
    [TabGroup("Score UI"),SerializeField] TextMeshProUGUI scoreText;

    [TabGroup("Momentum UI")]
    [TabGroup("Momentum UI"),SerializeField] TextMeshProUGUI momentumMultiplierText;
    [TabGroup("Momentum UI"),SerializeField] ProgressBar momentumBar_S1;
    [TabGroup("Momentum UI"),SerializeField] ProgressBar momentumBar_S2;
    [TabGroup("Momentum UI"),SerializeField] ProgressBar momentumBar_S3;
    [TabGroup("Momentum UI"),SerializeField] ProgressBar momentumBar_S4;

    [TabGroup("Jump UI")]
    [TabGroup("Jump UI"),SerializeField] Sprite jumpActive_Sprite;
    [TabGroup("Jump UI"),SerializeField] Sprite jumpInactive_Sprite;
    [TabGroup("Jump UI"),SerializeField] List<Image> jumpImages;

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
        GameplayEvents.OnSetActiveTuningProfile += Init;
        GameplayEvents.GameplayReset += OnReset;

        GameplayEvents.OnAirJump += OnAirJump;
        GameplayEvents.OnMushroomJump += OnMushroomJump;

        GameplayEvents.OnMomentumChanged += OnMomentumChanged;
        GameplayEvents.OnMultiplierChanged += OnMultiplierChanged;
        GameplayEvents.OnScoreChanged += OnScoreChanged;
    }

    void UnregisterGameplayEvents()
    {
        GameplayEvents.OnSetActiveTuningProfile -= Init;
        GameplayEvents.GameplayReset -= OnReset;

        GameplayEvents.OnAirJump -= OnAirJump;
        GameplayEvents.OnMushroomJump -= OnMushroomJump;
        GameplayEvents.OnMomentumChanged -= OnMomentumChanged;
        GameplayEvents.OnMultiplierChanged -= OnMultiplierChanged;
        GameplayEvents.OnScoreChanged -= OnScoreChanged;
    }

    public void Init(MovementTuningProfile _movementTuningProfile)
    {
        // Set the number of jump images based on the number of dash charges per bounce
        for(int i = 0; i < jumpImages.Count; i++)
        {
            jumpImages[i].gameObject.SetActive(i < _movementTuningProfile.DashChargesPerBounce);
        } 
    }

    void OnScoreChanged(int _score)
    {
        scoreText.text = _score.ToString();
    }

    void OnMomentumChanged(float _normalizedMomentum)
    {
        momentumBar_S1.ChangeProgressValue(_normalizedMomentum);
        momentumBar_S2.ChangeProgressValue(_normalizedMomentum);
        momentumBar_S3.ChangeProgressValue(_normalizedMomentum);
        momentumBar_S4.ChangeProgressValue(_normalizedMomentum);
    }

    void OnMultiplierChanged(float _multiplier)
    {
        momentumMultiplierText.text = $"X{_multiplier}";
    }

    void OnAirJump(int _jumpsLeft)
    {
        for(int i = 0; i < jumpImages.Count; i++)
        {
            jumpImages[i].sprite = i < _jumpsLeft ? jumpActive_Sprite : jumpInactive_Sprite;
        }
    }

    void OnMushroomJump()
    {
        foreach(Image jumpImage in jumpImages)
        {
            jumpImage.sprite = jumpActive_Sprite;
        }
    }

    void OnReset()
    {
        momentumBar_S1.BarReset();
        momentumBar_S2.BarReset();
        momentumBar_S3.BarReset();
        momentumBar_S4.BarReset();

        foreach(Image jumpImage in jumpImages)
        {
            jumpImage.sprite = jumpActive_Sprite;
        }
    }

}