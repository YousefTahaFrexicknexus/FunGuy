using System.Collections.Generic;
using Sirenix.OdinInspector;

using UnityEngine;
using UnityEngine.UI;

using TMPro;

using DG.Tweening;

public class HUD_UI : MonoBehaviour
{    
    [TabGroup("Score UI")]
    [TabGroup("Score UI"),SerializeField] TextMeshProUGUI scoreText;

    [TabGroup("Landing UI")]
    [TabGroup("Landing UI"),SerializeField] TextMeshProUGUI landingQualityText;
    [TabGroup("Scale Parameters"), Header("Scale Parameters"), Space]
    [SerializeField, LabelText("Initial Scale")]  Vector3 initScale = Vector3.one * 0.75f;
    [TabGroup("Scale Parameters"), SerializeField, LabelText("Final Scale")]  Vector3 finalScale = Vector3.one;

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
        // --- Game preperation ---
        GameplayEvents.OnSetActiveTuningProfile += Init;

        // --- Game state changes ---
        GameplayEvents.GameplayReset += OnReset;

        // --- Gameplay changes --- 
        GameplayEvents.OnAirJump += OnAirJump;
        GameplayEvents.OnMushroomLanding += OnMushroomLanding;

        // --- Momentum --- 
        GameplayEvents.OnMomentumChanged += OnMomentumChanged;
        GameplayEvents.OnMultiplierChanged += OnMultiplierChanged;

        // --- Score changes ---
        GameplayEvents.OnScoreChanged += OnScoreChanged;
    }

    void UnregisterGameplayEvents()
    {
        // --- Game preperation ---
        GameplayEvents.OnSetActiveTuningProfile -= Init;

        // --- Game state changes ---
        GameplayEvents.GameplayReset -= OnReset;

        // --- Gameplay changes --- 
        GameplayEvents.OnAirJump -= OnAirJump;
        GameplayEvents.OnMushroomLanding -= OnMushroomLanding;

        // --- Momentum --- 
        GameplayEvents.OnMomentumChanged -= OnMomentumChanged;
        GameplayEvents.OnMultiplierChanged -= OnMultiplierChanged;

        // --- Score changes ---
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

    void OnMushroomLanding(LandingQuality _landingQuality)
    {
        OnLandingQuality(_landingQuality);

        foreach(Image jumpImage in jumpImages)
        {
            jumpImage.sprite = jumpActive_Sprite;
        }
    }

    void OnLandingQuality(LandingQuality _landingQuality)
    {
        landingQualityText.text = $"{_landingQuality}";

        switch(_landingQuality)
        {
            case LandingQuality.Perfect:
                landingQualityText.color = Color.purple;
                break;
            case LandingQuality.Good:
                landingQualityText.color = Color.green;
                break;
            case LandingQuality.Bad:
                landingQualityText.color = Color.red;
                break;
            default:
                landingQualityText.color = Color.white;
                break;
        }

        // landingQualityText.DOScale(finalScale * 1.1f, duration / 2).SetEase(easeIn_Type).OnComplete(() =>
        // {
        //     landingQualityText.DOScale(initScale, duration / 2).SetEase(easeOut_Type);
        // });

        // landingQualityText.DOFade(0, duration).OnComplete(() =>
        // {
        //     isAnimating = false;
        //     this.gameObject.SetActive(false);
        // });
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

    public void OnClick_Pause()
    {
        UIManager.Instance.Open_PopupsAndPanels(UIType.Pause);
    }
}