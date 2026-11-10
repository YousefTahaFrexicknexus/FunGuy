using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Lightweight in-scene startup cover. No prefab, new textures, or saved scene overrides.
/// It only exists while the first-run environment and shader warm-up is in progress.
/// </summary>
public sealed class FunGuyStartupLoadingOverlay
{
    private static FunGuyStartupLoadingOverlay current;
    private readonly GameObject root;
    private readonly bool startedInMenu;
    private bool gameplayPhase;

    public static FunGuyStartupLoadingOverlay ShowDuringSceneLoad(Sprite logo)
    {
        if (current != null && current.root != null)
            return current;

        current = new FunGuyStartupLoadingOverlay(default(Scene), logo, true);
        current.SetDisplayedProgress(0f, "Entering the forest...");
        return current;
    }

    public static FunGuyStartupLoadingOverlay AcquireForGameplay(Scene ownerScene, Sprite logo)
    {
        if (current != null && current.root != null)
        {
            current.gameplayPhase = true;
            return current;
        }

        current = new FunGuyStartupLoadingOverlay(ownerScene, logo, false);
        current.gameplayPhase = true;
        return current;
    }

    public void UpdateSceneLoading(float progress)
    {
        if (root == null || gameplayPhase)
            return;

        SetDisplayedProgress(Mathf.Lerp(0.02f, 0.30f, Mathf.Clamp01(progress)),
            "Loading the forest...");
    }
    private readonly Image progressFill;
    private readonly TMP_Text statusLabel;
    private readonly TMP_Text progressLabel;

    private FunGuyStartupLoadingOverlay(Scene ownerScene, Sprite logo, bool persistAcrossScenes)
    {
        root = new GameObject("FunGuy Gameplay Warm-up (Runtime)", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        startedInMenu = persistAcrossScenes;
        if (persistAcrossScenes && Application.isPlaying)
            Object.DontDestroyOnLoad(root);
        else if (ownerScene.IsValid() && ownerScene.isLoaded)
            SceneManager.MoveGameObjectToScene(root, ownerScene);

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32750;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var background = MakeImage("Forest Color", root.transform, new Color(0.065f, 0.13f, 0.11f, 1f));
        Stretch(background.rectTransform);
        background.raycastTarget = true; // prevent hidden menu buttons receiving clicks

        var center = new GameObject("Centered Loading Content", typeof(RectTransform)).GetComponent<RectTransform>();
        center.SetParent(root.transform, false);
        center.anchorMin = center.anchorMax = new Vector2(0.5f, 0.5f);
        center.sizeDelta = new Vector2(750f, 610f);
        center.anchoredPosition = new Vector2(0f, 10f);

        if (logo != null)
        {
            var logoImage = MakeImage("FunGuy Logo", center, Color.white);
            logoImage.sprite = logo;
            logoImage.preserveAspect = true;
            Place(logoImage.rectTransform, 0f, 76f, 620f, 340f);
            logoImage.raycastTarget = false;
        }
        else
        {
            var title = MakeText("FunGuy Heading", center, "FUNGUY", 92, FontStyles.Bold,
                new Color(1f, 0.91f, 0.73f));
            Place(title.rectTransform, 0f, 90f, 650f, 180f);
        }

        statusLabel = MakeText("Loading Status", center, "Growing the forest...", 33, FontStyles.Bold,
            new Color(0.99f, 0.92f, 0.74f));
        Place(statusLabel.rectTransform, 0f, -145f, 690f, 70f);

        var track = MakeImage("Loading Track", center, new Color(0.18f, 0.28f, 0.22f));
        track.rectTransform.anchorMin = track.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        track.rectTransform.sizeDelta = new Vector2(590f, 18f);
        track.rectTransform.anchoredPosition = new Vector2(0f, -218f);

        progressFill = MakeImage("Loading Progress Fill", track.transform, new Color(1f, 0.58f, 0.28f));
        var fillRect = progressFill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        // A null-sprite Image ignores fillAmount: resize the fill rect instead.
        progressFill.type = Image.Type.Simple;

        progressLabel = MakeText("Loading Percentage", center, "0%", 25, FontStyles.Normal,
            new Color(0.88f, 0.88f, 0.78f));
        Place(progressLabel.rectTransform, 0f, -268f, 400f, 54f);
    }

    public void UpdateProgress(float value, string stage)
    {
        float mapped = startedInMenu && gameplayPhase
            ? Mathf.Lerp(0.30f, 1f, Mathf.Clamp01(value))
            : Mathf.Clamp01(value);
        SetDisplayedProgress(mapped, stage);
    }

    private void SetDisplayedProgress(float clamped, string stage)
    {
        if (root == null)
            return;
        progressFill.rectTransform.anchorMax = new Vector2(clamped, 1f);
        if (!string.IsNullOrEmpty(stage))
            statusLabel.text = stage;
        progressLabel.SetText("{0:0}%", Mathf.RoundToInt(clamped * 100f));
    }

    public void Close()
    {
        if (current == this)
            current = null;
        if (root != null)
            Object.Destroy(root);
    }

    private static Image MakeImage(string name, Transform parent, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text MakeText(string name, Transform parent, string value, float size,
        FontStyles style, Color color)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI))
            .GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false);
        label.text = value;
        label.fontSize = size;
        label.fontStyle = style;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }

    private static void Place(RectTransform transform, float x, float y, float width, float height)
    {
        transform.anchorMin = transform.anchorMax = new Vector2(0.5f, 0.5f);
        transform.sizeDelta = new Vector2(width, height);
        transform.anchoredPosition = new Vector2(x, y);
    }

    private static void Stretch(RectTransform transform)
    {
        transform.anchorMin = Vector2.zero;
        transform.anchorMax = Vector2.one;
        transform.offsetMin = transform.offsetMax = Vector2.zero;
    }
}
