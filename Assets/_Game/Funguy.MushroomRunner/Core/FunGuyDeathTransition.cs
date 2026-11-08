using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Short, unscaled-time, 1960s-cartoon-inspired death wipe. The run rebuild is
/// invoked only once, while a full-screen radial swirl hides the reset.
/// This component is authored on RunFlowCoordinator, not added to player prefabs.
/// </summary>
[DisallowMultipleComponent]
public sealed class FunGuyDeathTransition : MonoBehaviour
{
    [Header("Art")]
    [SerializeField] Sprite funGuyLogo;
    [SerializeField] Color creamRay = new Color(1f, 0.96f, 0.82f, 1f);
    [SerializeField] Color orangeRay = new Color(1f, 0.30f, 0.12f, 1f);
    [SerializeField] Color greenAccent = new Color(0.30f, 0.74f, 0.34f, 1f);
    [SerializeField, Range(8, 40), Tooltip("Broad color ribbons; rounded to a multiple of four so the swirl joins seamlessly.")]
    int rayCount = 8;
    [SerializeField, Range(0f, 1f), Tooltip("How wide the blurred boundaries are. Pure colors remain visible even at 1.")]
    float raySoftness = 0.7f;
    [SerializeField, Range(0f, 16f), Tooltip("How strongly the soft bands curl outward instead of forming straight lines.")]
    float rayCurl = 11f;

    [Header("Timing (unscaled seconds)")]
    [SerializeField, Min(0.05f)] float zoomInSeconds = 0.42f;
    [SerializeField, Min(0f)] float coveredHoldSeconds = 0.08f;
    [SerializeField, Min(0.05f)] float zoomOutSeconds = 0.42f;
    [SerializeField, Range(90f, 1200f)] float backgroundSpinDegreesPerSecond = 220f;
    [SerializeField] bool pauseGameplayDuringTransition = true;

    [Header("Optional SFX")]
    [SerializeField] AudioClip zoomInSound;
    [SerializeField] AudioClip zoomOutSound;
    [SerializeField, Range(0f, 1f)] float sfxVolume = 0.7f;

    GameObject overlay;
    CanvasGroup group;
    RectTransform canvasRect;
    RectTransform burstRect;
    RectTransform logoRect;
    Texture2D burstTexture;
    AudioSource audioSource;
    Coroutine running;
    float previousTimeScale;
    bool ownsTimeScale;

    public bool IsPlaying => running != null;

    void OnValidate()
    {
        zoomInSeconds = Mathf.Max(0.05f, zoomInSeconds);
        coveredHoldSeconds = Mathf.Max(0f, coveredHoldSeconds);
        zoomOutSeconds = Mathf.Max(0.05f, zoomOutSeconds);
        rayCount = Mathf.Clamp(rayCount, 8, 40);
        raySoftness = Mathf.Clamp01(raySoftness);
        rayCurl = Mathf.Clamp(rayCurl, 0f, 16f);
    }

    /// <summary>Returns false if the transition cannot start; caller should reset immediately.</summary>
    public bool TryPlay(Action onFullyCovered)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || IsPlaying)
            return false;

        if (funGuyLogo == null)
        {
            Debug.LogWarning("[FunGuyDeathTransition] Missing FunGuy logo sprite; skipping visual transition.", this);
            return false;
        }

        EnsureOverlay();
        if (overlay == null)
            return false;

        overlay.SetActive(true);
        group.alpha = 0f;
        running = StartCoroutine(Animate(onFullyCovered));
        return true;
    }

    [ContextMenu("Preview Death Wipe (Play Mode)")]
    public void PreviewInPlayMode()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[FunGuyDeathTransition] Enter Play Mode to preview the wipe.", this);
            return;
        }

        TryPlay(null);
    }

    void EnsureOverlay()
    {
        if (overlay != null)
            return;

        overlay = new GameObject("FunGuy Death Wipe (Runtime)", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        SceneManager.MoveGameObjectToScene(overlay, gameObject.scene);

        canvasRect = overlay.GetComponent<RectTransform>();
        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760;

        CanvasScaler scaler = overlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        group = overlay.GetComponent<CanvasGroup>();
        group.blocksRaycasts = true;
        group.interactable = true;

        burstTexture = CreateBurstTexture();
        GameObject burst = new GameObject("Rotating Color Rays", typeof(RectTransform), typeof(RawImage));
        burst.transform.SetParent(overlay.transform, false);
        burstRect = burst.GetComponent<RectTransform>();
        burstRect.anchorMin = burstRect.anchorMax = new Vector2(0.5f, 0.5f);
        burstRect.pivot = new Vector2(0.5f, 0.5f);
        RawImage rays = burst.GetComponent<RawImage>();
        rays.texture = burstTexture;
        rays.color = Color.white;
        rays.raycastTarget = true;

        GameObject logoObject = new GameObject("FunGuy Logo Zoom", typeof(RectTransform));
        logoObject.transform.SetParent(overlay.transform, false);
        logoRect = logoObject.GetComponent<RectTransform>();
        logoRect.anchorMin = logoRect.anchorMax = new Vector2(0.5f, 0.5f);
        logoRect.pivot = new Vector2(0.5f, 0.5f);
        logoRect.sizeDelta = new Vector2(960f, 480f);

        AddLogoImage("Logo Shadow", new Vector2(12f, -14f),
            new Color(0.20f, 0.07f, 0.03f, 0.48f));
        AddLogoImage("FunGuy Logo", Vector2.zero, Color.white);

        if (zoomInSound != null || zoomOutSound != null)
        {
            audioSource = overlay.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerPause = true;
        }

        overlay.SetActive(false);
    }

    void AddLogoImage(string name, Vector2 offset, Color tint)
    {
        GameObject child = new GameObject(name, typeof(RectTransform), typeof(Image));
        child.transform.SetParent(logoRect, false);
        RectTransform rect = child.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = offset;
        Image image = child.GetComponent<Image>();
        image.transform.localScale = new Vector3(0.65f, 0.65f, 0.65f);
        image.sprite = funGuyLogo;
        image.preserveAspect = true;
        image.color = tint;
        image.raycastTarget = false;
    }

    Texture2D CreateBurstTexture()
    {
        const int size = 512;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "FunGuy Cartoon Death Rays (Runtime)";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[size * size];
        float halfSize = (size - 1) * 0.5f;
        const float twoPi = Mathf.PI * 2f;

        // Repeat cream → vivid orange → cream → vivid green.
        // Cream separates orange from green so they never blend into muddy brown.
        // Each ribbon keeps its full color; raySoftness only broadens its blurred edges.
        int repetitions = Mathf.Max(2, Mathf.RoundToInt(rayCount / 4f));
        float transitionHalfWidth = Mathf.Lerp(0.05f, 0.37f, raySoftness);
        for (int y = 0; y < size; y++)
        {
            float dy = y - halfSize;
            for (int x = 0; x < size; x++)
            {
                float dx = x - halfSize;
                float distance = Mathf.Sqrt(dx * dx + dy * dy) / halfSize;
                float angle = Mathf.Atan2(dy, dx);

                // Continuous radius-driven twist makes genuine curled ribbons.
                // The secondary wave is periodic, avoiding a seam where angles wrap.
                float curl = distance * rayCurl +
                    0.65f * Mathf.Sin(angle * 2f - distance * 4.5f);
                float colorPosition = Mathf.Repeat(
                    (angle * repetitions + curl) * (4f / twoPi), 4f);
                int bandIndex = Mathf.FloorToInt(colorPosition);
                float positionInBand = colorPosition - bandIndex;

                Color from = bandIndex == 1 ? orangeRay :
                    bandIndex == 3 ? greenAccent : creamRay;
                int nextIndex = (bandIndex + 1) % 4;
                Color to = nextIndex == 1 ? orangeRay :
                    nextIndex == 3 ? greenAccent : creamRay;

                float blur = Mathf.InverseLerp(
                    0.5f - transitionHalfWidth,
                    0.5f + transitionHalfWidth, positionInBand);
                blur = Mathf.SmoothStep(0f, 1f, blur);
                Color color = Color.Lerp(from, to, blur);

                // Gentle light center; full saturated colors reappear outside it.
                float centerMix = Mathf.SmoothStep(0.03f, 0.19f, distance);
                pixels[y * size + x] = Color.Lerp(creamRay, color, centerMix);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }

    IEnumerator Animate(Action onFullyCovered)
    {
        previousTimeScale = Time.timeScale;
        if (pauseGameplayDuringTransition && previousTimeScale > 0f)
        {
            ownsTimeScale = true;
            Time.timeScale = 0f;
        }

        try
        {
            PlaySound(zoomInSound);

            float elapsed = 0f;
            while (elapsed < zoomInSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / zoomInSeconds);
                UpdateBackground(Time.unscaledDeltaTime);
                group.alpha = Mathf.Clamp01(t * 6f);
                float scale = Mathf.LerpUnclamped(0.16f, 2.55f, EaseOutBack(t));
                SetLogo(scale, (1f - t) * Mathf.Sin(t * 18f) * 9f);
                yield return null;
            }

            // The opaque rotating rays conceal the world rebuild and camera snap.
            group.alpha = 1f;
            SetLogo(2.55f, 0f);
            onFullyCovered?.Invoke();

            if (coveredHoldSeconds > 0f)
            {
                elapsed = 0f;
                while (elapsed < coveredHoldSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    UpdateBackground(Time.unscaledDeltaTime);
                    yield return null;
                }
            }

            PlaySound(zoomOutSound);
            elapsed = 0f;
            while (elapsed < zoomOutSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / zoomOutSeconds);
                UpdateBackground(Time.unscaledDeltaTime);
                SetLogo(Mathf.Lerp(2.55f, 0.05f, t * t * t), Mathf.Sin(t * 11f) * t * 5f);
                // Keep the reset hidden for the beginning of the reveal.
                group.alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.28f, 1f, t));
                yield return null;
            }
        }
        finally
        {
            CleanupPlayback();
        }
    }

    void UpdateBackground(float deltaTime)
    {
        if (canvasRect != null && burstRect != null)
        {
            Vector2 size = canvasRect.rect.size;
            float diagonal = Mathf.Sqrt(size.x * size.x + size.y * size.y);
            burstRect.sizeDelta = Vector2.one * Mathf.Max(200f, diagonal * 1.12f);
            burstRect.localRotation *= Quaternion.Euler(0f, 0f, backgroundSpinDegreesPerSecond * deltaTime);
        }
    }

    void SetLogo(float scale, float zDegrees)
    {
        if (logoRect == null) return;
        logoRect.localScale = Vector3.one * Mathf.Max(0f, scale);
        logoRect.localRotation = Quaternion.Euler(0f, 0f, zDegrees);
    }

    static float EaseOutBack(float t)
    {
        const float overshoot = 1.25f;
        float x = t - 1f;
        return 1f + (overshoot + 1f) * x * x * x + overshoot * x * x;
    }

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip, sfxVolume);
    }

    void CleanupPlayback()
    {
        if (group != null) group.alpha = 0f;
        if (overlay != null) overlay.SetActive(false);
        if (ownsTimeScale)
        {
            Time.timeScale = previousTimeScale;
            ownsTimeScale = false;
        }
        running = null;
    }

    void OnDisable()
    {
        if (running != null) StopCoroutine(running);
        CleanupPlayback();
        if (overlay != null) Destroy(overlay);
        overlay = null;
        if (burstTexture != null) Destroy(burstTexture);
        burstTexture = null;
    }
}