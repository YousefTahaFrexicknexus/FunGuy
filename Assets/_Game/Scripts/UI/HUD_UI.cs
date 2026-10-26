using System.Collections.Generic;
using Sirenix.OdinInspector;

using UnityEngine;
using UnityEngine.UI;

using TMPro;

using DG.Tweening;

public class HUD_UI : MonoBehaviour
{
    [TabGroup("Score UI")]
    [TabGroup("Score UI"), SerializeField] TextMeshProUGUI scoreText;
    [TabGroup("Coin UI"), SerializeField] TextAnimator coinCounter_TextAnimator;
    int collectedCoins;

    [TabGroup("Landing UI")]
    [TabGroup("Landing UI"), SerializeField] TextAnimator landingQuality_TextAnimator;
    [TabGroup("Scale Parameters"), Header("Scale Parameters"), Space]
    [SerializeField, LabelText("Initial Scale")] Vector3 initScale = Vector3.one * 0.75f;
    [TabGroup("Scale Parameters"), SerializeField, LabelText("Final Scale")] Vector3 finalScale = Vector3.one;

    [TabGroup("Momentum UI")]
    [TabGroup("Momentum UI"), SerializeField] TextAnimator momentumMultiplier_TextAnimator;
    [TabGroup("Momentum UI"), SerializeField] ProgressBar momentumBar_S1;
    [TabGroup("Momentum UI"), SerializeField] ProgressBar momentumBar_S2;
    [TabGroup("Momentum UI"), SerializeField] ProgressBar momentumBar_S3;
    [TabGroup("Momentum UI"), SerializeField] ProgressBar momentumBar_S4;

    [TabGroup("Jump UI")]
    [TabGroup("Jump UI"), SerializeField] Sprite jumpActive_Sprite;
    [TabGroup("Jump UI"), SerializeField] Sprite jumpInactive_Sprite;
    [TabGroup("Jump UI"), SerializeField] List<Image> jumpImages;

    void Awake()
    {
        coinCounter_TextAnimator?.SetCounterValue(0);
    }

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
        GameplayEvents.OnCoinCollected += OnCoinCollected;
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
        GameplayEvents.OnCoinCollected -= OnCoinCollected;
    }

    public void Init(MovementTuningProfile _movementTuningProfile)
    {
        // Set the number of jump images based on the number of dash charges per bounce
        for (int i = 0; i < jumpImages.Count; i++)
        {
            jumpImages[i].gameObject.SetActive(i < _movementTuningProfile.DashChargesPerBounce);
        }
    }

    void OnScoreChanged(int _score)
    {
        scoreText.text = _score.ToString();
    }

    void OnCoinCollected(int value)
    {
        if (value <= 0) return;
        collectedCoins += value;
        coinCounter_TextAnimator?.SetAndAnimateCounter(collectedCoins);
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
        momentumMultiplier_TextAnimator.SetAndAnimateMultiplier((int)_multiplier);
    }

    void OnAirJump(int _jumpsLeft)
    {
        for (int i = 0; i < jumpImages.Count; i++)
        {
            jumpImages[i].sprite = i < _jumpsLeft ? jumpActive_Sprite : jumpInactive_Sprite;
        }
    }

    void OnMushroomLanding(LandingQuality _landingQuality)
    {
        OnLandingQuality(_landingQuality);

        foreach (Image jumpImage in jumpImages)
        {
            jumpImage.sprite = jumpActive_Sprite;
        }
    }

    void OnLandingQuality(LandingQuality _landingQuality)
    {
        bool isGoodOrPerfectLanding = _landingQuality == LandingQuality.Good || _landingQuality == LandingQuality.Perfect;
        landingQuality_TextAnimator.SetAndAnimateLandingQuality(_landingQuality.ToString(), isGoodOrPerfectLanding);

        switch (_landingQuality)
        {
            case LandingQuality.Perfect:
                landingQuality_TextAnimator.SetColor(Color.orange);
                break;
            case LandingQuality.Good:
                landingQuality_TextAnimator.SetColor(Color.green);
                break;
            case LandingQuality.Bad:
                landingQuality_TextAnimator.SetColor(Color.red);
                break;
            default:
                landingQuality_TextAnimator.SetColor(Color.white);
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
        collectedCoins = 0;
        coinCounter_TextAnimator?.SetCounterValue(0);

        momentumBar_S1.BarReset();
        momentumBar_S2.BarReset();
        momentumBar_S3.BarReset();
        momentumBar_S4.BarReset();

        foreach (Image jumpImage in jumpImages)
        {
            jumpImage.sprite = jumpActive_Sprite;
        }
    }

    public void OnClick_Pause()
    {
        UIManager.Instance.Open_PopupsAndPanels(UIType.Pause);
    }
}