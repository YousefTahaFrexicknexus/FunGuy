using UnityEngine;
using DG.Tweening;

using System.Collections;
using System.Collections.Generic;

using Unity.Cinemachine;

using UnityEngine.SceneManagement;

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

    [Header("Main References")]
    [SerializeField] MomentumSystem momentumSystem;
    [SerializeField] DistanceScoreManager distanceScoreManager;

    [Header("Player References")]
    [SerializeField] MovementTuningProfile defaultMovementTuningProfile;
    [SerializeField] List<MovementTuningProfile> characterTuningProfiles;
    [SerializeField] HammockSlingshot hammockSlingshot;
    [SerializeField] RunnerMovementMotor runnerMovementMotor;

    [Header("Camera references")]
    [SerializeField] Camera slingShotCamera;
    [SerializeField] Camera playerCamera;
    [SerializeField] Camera characterSelectCamera;
    [SerializeField] CinemachineCamera playerCinemachineCamera;

    [Header("Player References")]
    Rigidbody rigidbody;

    [Header("PlayerGameplayState")]
    float isGroundedDuration = 0.5f;
    [SerializeField] float inGroundedCounter = 0;
    float inWaterDuration = 0.5f;
    [SerializeField] float inWaterCounter = 0;

    public bool isPlayerGrounded = false;
    public bool isPlayerInWater = false;
    [SerializeField] PlayerGameplayState prevPlayerGameplayState = PlayerGameplayState.idle;
    [SerializeField] PlayerGameplayState currentPlayerGameplayState = PlayerGameplayState.idle;

    [Header("Gameplay State")]
    public GameplayState currentGameplayState = GameplayState.slingshot;

    void Awake()
    {
        Init();
    }

    void Init()
    {
        rigidbody = runnerMovementMotor.rigidBody;
        Set_FrameRate(240);
    }

    void OnEnable()
    {
        SubscribeToGameplayEvents();
        BindMovementMomentum();
    }

    void OnDisable()
    {
        UnsubscribeFromGameplayEvents();
        if (runnerMovementMotor != null)
            runnerMovementMotor.SetMomentumSystem(null);
    }

    void Set_FrameRate(int _frameValue)
	{
		Application.targetFrameRate = _frameValue;
	}

    void FixedUpdate()
    {
        UpdatePlayerGameplayState();

        // Legacy configurations retain their original update path. Opted-in balance ticks run in the motor.
        if (momentumSystem != null && runnerMovementMotor != null && !momentumSystem.UsesTierSpeedDecay)
        {
            momentumSystem.Tick(runnerMovementMotor.Velocity.z, Time.fixedDeltaTime);
        }
    }

    void SubscribeToGameplayEvents()
    {
        GameplayEvents.GameplayReset += OnGameplayReset;
        GameplayEvents.OnPauseGameplay += OnPauseGameplay;

        GameplayEvents.OnCharacterSelectActivated += OnCharacterSelectActivated;
        GameplayEvents.OnCharacterSelectDeactivated += OnCharacterSelectDeactivated;

        GameplayEvents.OnSlingShotLaunched += OnSlingShotLaunched;

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

        OnGameplayReset();
    }

    public void RestartGame()
    {
        GameplayEvents.GameplayReset?.Invoke();
    }

    void OnGameplayReset()
    {
        ResetSlingShot();

        ResetPlayerGameplayState();
        ResetPlayerGameplayStateParams();

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

        if (runnerMovementMotor != null)
            runnerMovementMotor.SetMomentumSystem(momentumSystem);
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

    void OnSlingShotLaunched()
    {
        currentGameplayState = GameplayState.gameplay;
        
        slingShotCamera.gameObject.SetActive(false);
        playerCamera.enabled = true;
        runnerMovementMotor.enabled = true;
    }

    void ResetSlingShot()
    {
        currentGameplayState = GameplayState.slingshot;

        hammockSlingshot.ResetSlingshot();
        runnerMovementMotor.enabled = false;
        runnerMovementMotor.transform.position = Vector3.zero;

        slingShotCamera.gameObject.SetActive(true);
        playerCamera.enabled = false;
    }

    void OnCharacterSelectActivated()
    {
        hammockSlingshot.enabled = false;
        runnerMovementMotor.enabled = false;
        slingShotCamera.enabled = false;
        playerCamera.enabled = false;
        currentGameplayState = GameplayState.characterSelect;
    }

    void OnCharacterSelectDeactivated()
    {
        hammockSlingshot.enabled = true;
        slingShotCamera.enabled = true;
        playerCamera.enabled = false;
        runnerMovementMotor.enabled = true;
        currentGameplayState = GameplayState.slingshot;

        hammockSlingshot.ResetSlingshot();
    }

    public void OnCharacterSelected(int __selectedIndex)
    {
        SetActiveTuningProfile(characterTuningProfiles[__selectedIndex]);
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
        CointPitchValueResetCoroutine = StartCoroutine(ResetCoinPitchValue());

        IEnumerator PlayCoinCollectSFX()
        {
            yield return new WaitForSeconds(0.1f);
            AudioManager.Instance.PlaySFX($"CoinCollect", coinPitchValue, 0.5f);
        }
    }

    IEnumerator ResetCoinPitchValue()
    {
        yield return new WaitForSeconds(0.5f);
        coinPitchValue = 1f;
    }

    void OnApplyModifier(MushroomModifierType _modifierType)
    {
        AudioManager.Instance.PlaySFX($"Modifier_{_modifierType}");
    }

#region PlayerGameplayState
    void UpdatePlayerGameplayState()
    {
        CheckPlayerGameplayState();

        switch(currentPlayerGameplayState)
        {
            case PlayerGameplayState.grounded:
            {
                UpdateGroundedBehavior();
                break;
            }

            case PlayerGameplayState.underwater:
            {
                UpdateUnderwaterBehavior();
                break;
            }
        }
    }

    void CheckPlayerGameplayState()
    {
        if(Mathf.Approximately(rigidbody.linearVelocity.y, 0) && (isPlayerGrounded || isPlayerInWater))
        {
            if(isPlayerGrounded)
            {
                ChangePlayerGameplayState(PlayerGameplayState.grounded);
            }
            else
            {
                ChangePlayerGameplayState(PlayerGameplayState.underwater);
            }
        }
        else if(rigidbody.linearVelocity.y <= 0)
        {
            if(Mathf.FloorToInt(rigidbody.linearVelocity.y) == -1 && (isPlayerGrounded || isPlayerInWater))
            {
                if(isPlayerGrounded)
                {
                    ChangePlayerGameplayState(PlayerGameplayState.grounded);
                }
                else
                {
                    ChangePlayerGameplayState(PlayerGameplayState.underwater);
                }
            }
            else
            {
                ChangePlayerGameplayState(PlayerGameplayState.falling);
            }
        }
        else if(rigidbody.linearVelocity.y > 0)
        {
            ChangePlayerGameplayState(PlayerGameplayState.flying);
        }
    }

    public void ChangePlayerGameplayState(PlayerGameplayState _playerGameplayState)
    {
        prevPlayerGameplayState = currentPlayerGameplayState;
        currentPlayerGameplayState = _playerGameplayState;

        GameplayEvents.OnPlayerGameplayStateChange?.Invoke(currentPlayerGameplayState);
    }

    public void OnPlayerGameplayStateChange(PlayerGameplayState _playerGameplayState)
    {
        switch(currentPlayerGameplayState)
        {
            case PlayerGameplayState.grounded:
            {
                isPlayerGrounded = true;
                isPlayerInWater = false;
                break;
            }

            case PlayerGameplayState.underwater:
            {
                isPlayerGrounded = false;
                isPlayerInWater = true;
                break;
            }

            case PlayerGameplayState.flying:
            {
                ResetPlayerGameplayStateParams();
                break;
            }

            case PlayerGameplayState.falling:
            {
                ResetPlayerGameplayStateParams();
                break;
            }
        }
    }

    void ResetPlayerGameplayStateParams()
    {
        inWaterCounter = 0;
        inGroundedCounter = 0;
    }

    void ResetPlayerGameplayState()
    {
        isPlayerGrounded = false;
        isPlayerInWater = false;
    }

    void RequestPlayerDeath()
    {
        inWaterCounter = 0;
        isPlayerInWater = false;

        // Route ground/water deaths through the cover-and-respawn transition.
        // Pause-menu restarts still use the existing immediate reset.
        RunFlowCoordinator coordinator = FindAnyObjectByType<RunFlowCoordinator>();
        if (coordinator != null)
            coordinator.ReportFailure(RunFailureReason.EnvironmentalDeath);
        else
            GameplayEvents.GameplayReset?.Invoke();
    }

#endregion --- PlayerGameplayState ---

#region Flying
    void UpdateFlyingBehavior()
    {

    }
#endregion --- Flying ---

#region Falling
    void UpdateFallingBehavior()
    {
        
    }
#endregion --- Falling ---

#region Ground
    public void SetIsPlayerGrounded(bool _State)
    {
        isPlayerGrounded = _State;
    }

    void UpdateGroundedBehavior()
    {
        if(isPlayerGrounded)
        {
            inGroundedCounter += Time.deltaTime;

            if(inGroundedCounter > isGroundedDuration)
            {
                RequestPlayerDeath();
            }
        }
    }
#endregion --- Ground ---

#region Water
    public void SetIsPlayerInWater(bool _State)
    {
        isPlayerInWater = _State;
    }

    void UpdateUnderwaterBehavior()
    {
        if(isPlayerInWater)
        {
            inWaterCounter += Time.deltaTime;

            if(inWaterCounter > inWaterDuration)
            {
                RequestPlayerDeath();
            }
        }
    }
#endregion --- Water ---

    public void LoadMainMenu()
    {
        UIManager.Instance.Close_PopupsAndPanels(UIType.Pause);
        SceneManager.LoadScene("MainMenuPGC");
    }
}

public enum PlayerGameplayState
{
    idle = 0,
    grounded,
    underwater,
    flying,
    falling,
}

public enum GameplayState
{
    slingshot = 0,
    gameplay,
    characterSelect,
}