using UnityEngine;
using DG.Tweening;

using System.Collections;

public class GameplayManager : MonoBehaviour
{
    #region Instance
    static GameplayManager _instance;

    public static GameplayManager Instance
    {
        get
        {
            if (!_instance)
                _instance = GameObject.FindAnyObjectByType<GameplayManager>();
            return _instance;
        }
    }
    #endregion --- Instance ---

    [SerializeField] MovementTuningProfile defaultMovementTuningProfile;
    [SerializeField] MomentumSystem momentumSystem;
    [SerializeField] RunnerMovementMotor runnerMovementMotor;
    [SerializeField] DistanceScoreManager distanceScoreManager;

    void OnEnable()
    {
        SubscribeToGameplayEvents();
        BindMovementMomentum();
    }

    void OnDisable()
    {
        UnsubscribeFromGameplayEvents();
        runnerMovementMotor?.SetMomentumSystem(null);
    }

    void SubscribeToGameplayEvents()
    {
        GameplayEvents.GameplayReset += OnGameplayReset;
        GameplayEvents.OnPauseGameplay += OnPauseGameplay;

        GameplayEvents.OnMushroomJump += OnMushroomJump;
        GameplayEvents.OnAirJump += OnAirJump;

        GameplayEvents.OnCoinCollected += OnCoinCollected;
        GameplayEvents.OnApplyModifier += OnApplyModifier;
    }

    void UnsubscribeFromGameplayEvents()
    {
        GameplayEvents.GameplayReset -= OnGameplayReset;
        GameplayEvents.OnPauseGameplay -= OnPauseGameplay;

        GameplayEvents.OnMushroomJump -= OnMushroomJump;
        GameplayEvents.OnAirJump -= OnAirJump;

        GameplayEvents.OnCoinCollected -= OnCoinCollected;
    }

    void Start()
    {
        BindMovementMomentum();
        SetActiveTuningProfile(defaultMovementTuningProfile);
        momentumSystem?.BeginRun();
        distanceScoreManager?.BeginRun();
    }

    public void RestartGame()
    {
        GameplayEvents.GameplayReset?.Invoke();
    }

    void OnGameplayReset()
    {
        momentumSystem?.ResetMomentum();
        distanceScoreManager?.ResetScore();
    }

    public void SetActiveTuningProfile(MovementTuningProfile _movementTuningProfile)
    {
        if (_movementTuningProfile == null)
        {
            return;
        }

        if (runnerMovementMotor != null)
        {
            MushroomRunnerPlayer player = runnerMovementMotor.GetComponentInParent<MushroomRunnerPlayer>();
            if (player != null)
            {
                player.SetTuningProfile(_movementTuningProfile);
            }
            else
            {
                runnerMovementMotor.SetTuningProfile(_movementTuningProfile);
            }
        }

        GameplayEvents.OnSetActiveTuningProfile?.Invoke(_movementTuningProfile);
    }

    void BindMovementMomentum()
    {
        // Scoring and movement must use the same tier source, including the x1 fallback.
        if (distanceScoreManager != null)
        {
            momentumSystem = distanceScoreManager.MomentumSource;
        }

        runnerMovementMotor?.SetMomentumSystem(momentumSystem);
    }
    
    void FixedUpdate()
    {
        // Legacy configurations retain their original update path. Opted-in balance ticks run in the motor.
        if (momentumSystem != null && runnerMovementMotor != null && !momentumSystem.UsesTierSpeedDecay)
        {
            momentumSystem.Tick(runnerMovementMotor.Velocity.z, Time.fixedDeltaTime);
        }
    }

    public void OnPauseGameplay(bool _isPause)
    {
        if(_isPause)
        {
            DOTween.To(() => Time.timeScale, x => Time.timeScale = x, 0, 0.25f).SetEase(Ease.OutQuad).SetUpdate(true);
        }
        else
        {
            DOTween.To(() => Time.timeScale, x => Time.timeScale = x, 1, 0.25f).SetEase(Ease.OutQuad).SetUpdate(true);
        }
    }

    void OnMushroomJump()
    {
        int mushroomJumpVarioation = Random.Range(1, 4);
        float pitchVariation = Random.Range(0.9f, 1.1f);
        AudioManager.Instance.PlaySFX($"MushroomJump_{mushroomJumpVarioation}", pitchVariation);
    }

    void OnAirJump(int _jumpCount)
    {
        int airJumpVarioation = Random.Range(1, 4);
        float pitchVariation = Random.Range(0.9f, 1.1f);
        AudioManager.Instance.PlaySFX($"AirJump_{airJumpVarioation}", pitchVariation, 0.5f);
    }

    void OnMushroomLanding(LandingQuality _landingQuality)
    {
        int mushroomLandingVarioation = Random.Range(1, 4);
        float pitchVariation = Random.Range(0.9f, 1.1f);
        AudioManager.Instance.PlaySFX($"MushroomLanding_{mushroomLandingVarioation}", pitchVariation);
    }

    float coinPitchValue = 1f;
    Coroutine CointPitchValueResetCoroutine;
    void OnCoinCollected(int _coinValue)
    {
        if(CointPitchValueResetCoroutine != null)
        {
            StopCoroutine(CointPitchValueResetCoroutine);
            CointPitchValueResetCoroutine = null;
        }

        coinPitchValue += 0.05f;

        StartCoroutine(PlayCoinCollectSFX());
        CointPitchValueResetCoroutine = StartCoroutine(ResetCointPitchValue());

        IEnumerator PlayCoinCollectSFX()
        {
            yield return new WaitForSeconds(0.1f);
            AudioManager.Instance.PlaySFX($"CoinCollect", coinPitchValue, 0.5f);
        }
    }

    IEnumerator ResetCointPitchValue()
    {
        yield return new WaitForSeconds(0.5f);
        coinPitchValue = 1f;
    }

    void OnApplyModifier(MushroomModifierType _modifierType)
    {
        AudioManager.Instance.PlaySFX($"Modifier_{_modifierType}");
    }
}