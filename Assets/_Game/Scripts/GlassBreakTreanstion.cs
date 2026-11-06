using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class GlassBreakTreanstion : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] string playerTag = "Player";
    [SerializeField] bool pauseGameplay = true;

    [Header("Ending Screen")]
    [SerializeField] TMP_FontAsset font;
    [SerializeField, TextArea] string message = "To be continued";
    [SerializeField, Min(0f)] float textDelay = 0.4f;
    [SerializeField, Min(0.001f)] float secondsPerCharacter = 0.075f;
    [SerializeField, Min(1f)] float fontSize = 90f;
    [SerializeField, Min(0f), Tooltip("Seconds to hold the completed message before restarting the run.")]
    float restartDelay = 2f;

    [Header("Glass Shatter")]
    [SerializeField] AudioClip glassShatterSfx;
    [SerializeField, Range(0f, 1f)] float sfxVolume = 1f;

    GameObject endingScreen;
    TextMeshProUGUI endingText;
    bool hasTriggered;
    bool ownsPause;
    float previousTimeScale;

    void Reset() => AssignDefaultAssets();

    void OnValidate()
    {
        textDelay = Mathf.Max(0f, textDelay);
        secondsPerCharacter = Mathf.Max(0.001f, secondsPerCharacter);
        fontSize = Mathf.Max(1f, fontSize);
        restartDelay = Mathf.Max(0f, restartDelay);
        AssignDefaultAssets();
    }

    void AssignDefaultAssets()
    {
#if UNITY_EDITOR
        if (font == null)
            font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/_Game/Art/Fonts/Cutepunch Personal Use Only SDF.asset");
        if (glassShatterSfx == null)
            glassShatterSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/_Game/Audio/GlassShatter.wav");
#endif
    }

    void OnCollisionEnter(Collision collision) => TryBeginTransition(collision.collider);

    void OnTriggerEnter(Collider other) => TryBeginTransition(other);

    void TryBeginTransition(Collider other)
    {
        if (hasTriggered || !isActiveAndEnabled) return;

        // The player's physics colliders can live below its tagged root.
        for (Transform candidate = other.transform; candidate != null; candidate = candidate.parent)
        {
            if (!candidate.CompareTag(playerTag)) continue;
            BeginTransition();
            return;
        }
    }

    // Also available to a UnityEvent for an authored ending sequence.
    public void BeginTransition()
    {
        if (hasTriggered || !isActiveAndEnabled) return;
        hasTriggered = true;
        CreateEndingScreen();
        PlayGlassShatter();

        if (pauseGameplay)
        {
            previousTimeScale = Time.timeScale;
            ownsPause = true;
            Time.timeScale = 0f;
        }

        StartCoroutine(TypeMessage());
    }

    void CreateEndingScreen()
    {
        endingScreen = new GameObject("ToBeContinuedScreen", typeof(RectTransform), typeof(Canvas),
            typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
        // Keep this runtime screen in the trigger's scene so scene unload cleans it up.
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(endingScreen, gameObject.scene);

        Canvas canvas = endingScreen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var scaler = endingScreen.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var background = new GameObject("BlackBackground", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        background.transform.SetParent(endingScreen.transform, false);
        Stretch(background.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
        var image = background.GetComponent<UnityEngine.UI.Image>();
        image.color = Color.black;
        image.raycastTarget = true;

        var textObject = new GameObject("ToBeContinuedText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(background.transform, false);
        // Leave margins on both wide and portrait screens.
        Stretch(textObject.GetComponent<RectTransform>(), new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.75f));
        endingText = textObject.GetComponent<TextMeshProUGUI>();
        if (font != null) endingText.font = font;
        endingText.fontSize = fontSize;
        endingText.enableAutoSizing = true;
        endingText.fontSizeMin = 24f;
        endingText.fontSizeMax = fontSize;
        endingText.color = Color.white;
        endingText.alignment = TextAlignmentOptions.Center;
        endingText.raycastTarget = false;
        endingText.text = message;
        endingText.maxVisibleCharacters = 0;
        endingText.ForceMeshUpdate();
    }

    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void PlayGlassShatter()
    {
        if (glassShatterSfx == null)
        {
            Debug.LogWarning("[GlassBreakTreanstion] Assign a glass shatter SFX clip.", this);
            return;
        }

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlaySFXClip(glassShatterSfx, sfxVolume);
            return;
        }

        // A standalone scene can still preview the ending without an AudioManager.
        AudioSource source = endingScreen.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.ignoreListenerPause = true;
        source.PlayOneShot(glassShatterSfx, sfxVolume);
    }

    IEnumerator TypeMessage()
    {
        // Realtime waits keep typing while the game is frozen on the ending screen.
        if (textDelay > 0f) yield return new WaitForSecondsRealtime(textDelay);
        var characterDelay = new WaitForSecondsRealtime(secondsPerCharacter);
        int characterCount = endingText.textInfo.characterCount;
        for (int visible = 1; visible <= characterCount; visible++)
        {
            endingText.maxVisibleCharacters = visible;
            if (visible < characterCount) yield return characterDelay;
        }

        if (restartDelay > 0f) yield return new WaitForSecondsRealtime(restartDelay);
        RestartRun();
    }

    void RestartRun()
    {
        RunFlowCoordinator coordinator = FindAnyObjectByType<RunFlowCoordinator>();
        // Clear the overlay and release the pause before reset listeners can remove this trigger.
        ResetTransition();
        Time.timeScale = 1f;
        if (coordinator != null)
            coordinator.ReportFailure(RunFailureReason.Unknown);
        else
            GameplayEvents.GameplayReset?.Invoke();
    }

    void OnDisable() => ResetTransition();

    public void ResetTransition()
    {
        StopAllCoroutines();
        if (endingScreen != null) Destroy(endingScreen);
        endingScreen = null;
        endingText = null;
        // Release our pause when this ending is removed or its scene is unloaded.
        if (ownsPause && Time.timeScale == 0f) Time.timeScale = previousTimeScale;
        ownsPause = false;
        hasTriggered = false;
    }
}
