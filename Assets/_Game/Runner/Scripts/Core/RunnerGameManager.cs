using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FunGuy.Runner
{
    public sealed class RunnerGameManager : MonoBehaviour
    {
        [Header("Configs")]
        [SerializeField] private RunnerGridConfig gridConfig;
        [SerializeField] private RunnerPlayerConfig playerConfig;
        [SerializeField] private RunnerGenerationProfile generationProfile;

        [Header("Scene References")]
        [SerializeField] private RunnerGridSystem gridSystem;
        [SerializeField] private GridWorld gridWorld;
        [SerializeField] private RunnerInputHandler inputHandler;
        [SerializeField] private EndlessGridSpawner spawner;
        [SerializeField] private PlayerRunnerController playerController;
        [SerializeField] private RunnerCameraFollow cameraFollow;

        [Header("Flow")]
        [SerializeField] private bool respawnOnDeath = true;
        [SerializeField] private float respawnDelay = 0.75f;
        [SerializeField] private int startingCountdownSeconds = 3;

        private RunnerGameState state = RunnerGameState.Booting;
        private bool hasBooted;
        private bool isRespawning;
        private Vector3Int startCell;
        private Coroutine countdownRoutine;

        public RunnerGameState State => state;

        private void Reset()
        {
            ResolveDependencies();
        }

        private void Awake()
        {
            ResolveDependencies();
        }

        private void OnEnable()
        {
            RunnerGameEvents.PlayerDied += HandlePlayerDied;
            RunnerGameEvents.LevelCompleted += HandleLevelCompleted;
        }

        private void OnDisable()
        {
            RunnerGameEvents.PlayerDied -= HandlePlayerDied;
            RunnerGameEvents.LevelCompleted -= HandleLevelCompleted;
        }

        private void Start()
        {
            Boot();
        }

        [ContextMenu("Boot Runner")]
        public void Boot()
        {
            if (hasBooted)
            {
                return;
            }

            ResolveDependencies();

            if (gridConfig == null || playerConfig == null || generationProfile == null)
            {
                Debug.LogError("[RunnerGameManager] Missing one or more ScriptableObject configs.");
                return;
            }

            if (gridSystem == null || gridWorld == null || inputHandler == null || spawner == null || playerController == null)
            {
                Debug.LogError("[RunnerGameManager] Missing one or more scene dependencies.");
                return;
            }

            gridSystem.Configure(gridConfig);
            gridWorld.Configure(gridSystem);
            spawner.Configure(gridSystem, gridWorld, generationProfile, playerConfig, playerController);
            playerController.Configure(gridSystem, gridWorld, inputHandler, playerConfig);

            startCell = gridSystem.GetDefaultStartCell(playerConfig.StartLane, playerConfig.StartLayer);
            EnsureHudViewsExist();
            EnsurePlayerTrailExists();
            Transform cameraTarget = EnsureCameraTargetExists();

            if (cameraFollow != null && cameraTarget != null)
            {
                cameraFollow.SetTarget(cameraTarget);
            }

            StartRunFromStartCell();
            hasBooted = true;
        }

        private void ResolveDependencies()
        {
            if (gridSystem == null)
            {
                gridSystem = FindFirstObjectByType<RunnerGridSystem>();
            }

            if (gridWorld == null)
            {
                gridWorld = FindFirstObjectByType<GridWorld>();
            }

            if (inputHandler == null)
            {
                inputHandler = FindFirstObjectByType<RunnerInputHandler>();
            }

            if (spawner == null)
            {
                spawner = FindFirstObjectByType<EndlessGridSpawner>();
            }

            if (playerController == null)
            {
                playerController = FindFirstObjectByType<PlayerRunnerController>();
            }

            if (cameraFollow == null)
            {
                cameraFollow = FindFirstObjectByType<RunnerCameraFollow>();
            }
        }

        private void HandlePlayerDied()
        {
            if (inputHandler != null)
            {
                inputHandler.enabled = false;
            }

            SetState(RunnerGameState.Dead);

            if (respawnOnDeath && !isRespawning)
            {
                StartCoroutine(RespawnAfterDelay());
            }
        }

        private void HandleLevelCompleted()
        {
            SetState(RunnerGameState.Completed);
        }

        private void SetState(RunnerGameState nextState)
        {
            if (state == nextState)
            {
                return;
            }

            state = nextState;
            RunnerGameEvents.RaiseGameStateChanged(state);
        }

        private IEnumerator RespawnAfterDelay()
        {
            isRespawning = true;

            if (spawner != null)
            {
                spawner.enabled = false;
            }

            yield return new WaitForSeconds(respawnDelay);

            StartRunFromStartCell();
            isRespawning = false;
        }

        private void StartRunFromStartCell()
        {
            if (spawner != null)
            {
                spawner.SetStartupDelay(Mathf.Max(0f, startingCountdownSeconds) + playerConfig.InitialBounceDelay);
                spawner.BuildInitialWorld(startCell);
                spawner.enabled = true;
            }

            if (playerController != null)
            {
                playerController.BeginRun(startCell);
                playerController.SetMovementPaused(true);
            }

            if (inputHandler != null)
            {
                inputHandler.enabled = false;
            }

            if (countdownRoutine != null)
            {
                StopCoroutine(countdownRoutine);
            }

            countdownRoutine = StartCoroutine(CountdownThenPlay());
        }

        private IEnumerator CountdownThenPlay()
        {
            SetState(RunnerGameState.Booting);

            int countdownSeconds = Mathf.Max(0, startingCountdownSeconds);

            for (int seconds = countdownSeconds; seconds >= 1; seconds--)
            {
                RunnerGameEvents.RaiseCountdownTick(seconds);
                yield return new WaitForSeconds(1f);
            }

            RunnerGameEvents.RaiseCountdownFinished();

            if (playerController != null)
            {
                playerController.SetMovementPaused(false);
            }

            if (inputHandler != null)
            {
                inputHandler.enabled = true;
            }

            SetState(RunnerGameState.Playing);
            countdownRoutine = null;
        }

        private void EnsureHudViewsExist()
        {
            EnsureCountdownViewExists();
            EnsureScoreViewExists();
        }

        private void EnsurePlayerTrailExists()
        {
            if (playerController == null)
            {
                return;
            }

            RunnerSimpleTrail trail = playerController.GetComponent<RunnerSimpleTrail>();

            if (trail == null)
            {
                trail = playerController.gameObject.AddComponent<RunnerSimpleTrail>();
            }

            trail.ApplyNow();
        }

        private Transform EnsureCameraTargetExists()
        {
            if (playerController == null)
            {
                return null;
            }

            RunnerCameraAnchor anchor = playerController.GetComponentInChildren<RunnerCameraAnchor>(true);

            if (anchor == null)
            {
                GameObject anchorObject = new("CameraAnchor");
                anchorObject.transform.SetParent(playerController.transform, false);
                anchor = anchorObject.AddComponent<RunnerCameraAnchor>();
                anchor.ApplyAnchorOffset();
            }

            return anchor.AnchorTransform;
        }

        private Canvas GetOrCreateHudCanvas()
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();

            if (canvas != null)
            {
                return canvas;
            }

            GameObject canvasObject = new("RunnerHUD");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private void EnsureCountdownViewExists()
        {
            if (FindFirstObjectByType<RunnerCountdownView>() != null)
            {
                return;
            }

            Canvas canvas = GetOrCreateHudCanvas();

            GameObject countdownObject = new("CountdownText", typeof(RectTransform), typeof(CanvasGroup), typeof(Text), typeof(RunnerCountdownView));
            countdownObject.transform.SetParent(canvas.transform, false);

            RectTransform rectTransform = countdownObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(0f, 80f);
            rectTransform.sizeDelta = new Vector2(280f, 280f);

            Text text = countdownObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 180;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(1f, 0.97f, 0.88f, 1f);
            text.raycastTarget = false;
            text.text = string.Empty;
        }

        private void EnsureScoreViewExists()
        {
            if (FindFirstObjectByType<RunnerScoreView>() != null)
            {
                return;
            }

            Canvas canvas = GetOrCreateHudCanvas();
            GameObject scoreObject = new("ScoreText", typeof(RectTransform), typeof(Text), typeof(Outline), typeof(RunnerScoreView));
            scoreObject.transform.SetParent(canvas.transform, false);

            RectTransform rectTransform = scoreObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(1f, 1f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(1f, 1f);
            rectTransform.anchoredPosition = new Vector2(-40f, -36f);
            rectTransform.sizeDelta = new Vector2(320f, 120f);

            Text text = scoreObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 54;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.UpperRight;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = new Color(1f, 0.97f, 0.88f, 1f);
            text.raycastTarget = false;
            text.text = "SCORE\n00000";

            Outline outline = scoreObject.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.32f);
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }
}
