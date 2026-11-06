using UnityEngine;
using TMPro;

/// <summary>
/// Shows the current frame rate in a TextMeshPro label (UI or 3D text).
/// Averages over a short interval so the number is readable, uses unscaled time so
/// it keeps working when Time.timeScale is 0, and doesn't allocate garbage per update.
/// Put it on the same object as the TMP text, or drag the text into the Label field.
/// </summary>
[DisallowMultipleComponent]
public class FPSCounter : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("TextMeshProUGUI (Canvas) or TextMeshPro (3D). Found automatically on this object or its children if empty.")]
    [SerializeField] private TMP_Text label;

    [Header("Update")]
    [Tooltip("Seconds over which frames are averaged before the text is refreshed.")]
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.5f;

    [Header("Display")]
    [SerializeField] private bool showMilliseconds = true;
    [Tooltip("Adds a second line with the lowest and highest FPS seen in the last interval.")]
    [SerializeField] private bool showMinMax = false;

    [Header("Colour by performance")]
    [SerializeField] private bool colorByPerformance = true;
    [Tooltip("At or above this FPS the text uses the Good colour.")]
    [SerializeField] private int goodFps = 55;
    [Tooltip("At or above this FPS (and below Good) the text uses the Ok colour. Below it, the Bad colour.")]
    [SerializeField] private int okFps = 30;
    [SerializeField] private Color goodColor = new Color(0.3f, 1f, 0.4f);
    [SerializeField] private Color okColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] private Color badColor = new Color(1f, 0.3f, 0.3f);

    private int frames;
    private float elapsed;
    private float shortestFrame = float.MaxValue;
    private float longestFrame;

    /// <summary>Average FPS over the last completed interval.</summary>
    public float Fps { get; private set; }

    private void Reset()
    {
        label = GetComponentInChildren<TMP_Text>();
    }

    private void Awake()
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();

        if (label == null)
        {
            Debug.LogWarning("FPSCounter: no TMP_Text found. Assign one in the inspector.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        ResetSamples();
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        frames++;
        elapsed += dt;
        if (dt < shortestFrame) shortestFrame = dt;
        if (dt > longestFrame) longestFrame = dt;

        if (elapsed < refreshInterval) return;

        Fps = frames / elapsed;
        float frameMs = 1000f * elapsed / frames;

        // SetText with numeric arguments formats without creating strings (no GC).
        if (showMinMax)
        {
            float minFps = 1f / longestFrame;     // slowest frame = lowest FPS
            float maxFps = 1f / shortestFrame;    // fastest frame = highest FPS

            if (showMilliseconds)
                label.SetText("{0:0} FPS ({1:1} ms)\nmin {2:0}  max {3:0}", Fps, frameMs, minFps, maxFps);
            else
                label.SetText("{0:0} FPS\nmin {1:0}  max {2:0}", Fps, minFps, maxFps);
        }
        else
        {
            if (showMilliseconds)
                label.SetText("{0:0} FPS ({1:1} ms)", Fps, frameMs);
            else
                label.SetText("{0:0} FPS", Fps);
        }

        if (colorByPerformance)
            label.color = Fps >= goodFps ? goodColor : (Fps >= okFps ? okColor : badColor);

        ResetSamples();
    }

    /// <summary>Show or hide the counter (e.g. from a debug menu button).</summary>
    public void SetVisible(bool visible)
    {
        if (label != null) label.enabled = visible;
        enabled = visible;
    }

    /// <summary>Flip visibility. Hook this to a UI button or a debug key.</summary>
    public void Toggle()
    {
        SetVisible(!enabled);
    }

    private void ResetSamples()
    {
        frames = 0;
        elapsed = 0f;
        shortestFrame = float.MaxValue;
        longestFrame = 0f;
    }
}
