#if FUNG_PHASED_TEST
// Diagnostics only. All production scenes and source materials remain unchanged.
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Temporary, profile-gated A/B harness. Does not execute in regular builds.
/// All visual options are applied to duplicate URP assets or runtime instances.
/// </summary>
public sealed class FunGuyPhaseDVisualAB : MonoBehaviour
{
    private const int ComparisonSeed = 20261106;
    private const float RescanInterval = 4f;
    private const float FpsWarmupSeconds = 8f;
    private const float FpsSampleSeconds = 5f;
    private float _nextScanTime;
    private float _fpsWarmupUntil;
    private float _fpsElapsed;
    private float _fpsLongestFrame;
    private int _fpsFrames;
    private int _fpsWindow;
    private bool _loggedFirstScan;

#if FUNG_PHASED_SHADOW120 && FUNG_PHASED_OCCLUSION_ON
    private const string PipelineResource = "FunGuy_PhaseD_Combined";
#elif FUNG_PHASED_SHADOW120
    private const string PipelineResource = "FunGuy_PhaseD_Shadow120";
#elif FUNG_PHASED_OCCLUSION_ON
    private const string PipelineResource = "FunGuy_PhaseD_OcclusionOn";
#elif FUNG_PHASED_SMALL_SHADOWS_OFF
    private const string PipelineResource = "FunGuy_PhaseD_GrassFlowerShadowOff";
#else
    private const string PipelineResource = "FunGuy_PhaseD_Baseline";
#endif

#if FUNG_PHASED_SMALL_SHADOWS_OFF
    private const bool RemoveSmallFoliageShadows = true;
#else
    private const bool RemoveSmallFoliageShadows = false;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SelectPipeline()
    {
        // Diagnostic-only seed: helps comparisons but cannot guarantee identical
        // procedural worlds or timings if another random source is used.
        UnityEngine.Random.InitState(ComparisonSeed);

        UniversalRenderPipelineAsset pipeline =
            Resources.Load<UniversalRenderPipelineAsset>(PipelineResource);
        if (pipeline == null)
        {
            Debug.LogError("[FunGuy PhaseD] Missing diagnostic URP: " + PipelineResource);
            return;
        }

        QualitySettings.renderPipeline = pipeline;
        Debug.Log("[FunGuy PhaseD] Selected " + PipelineResource +
                  "; seed=" + ComparisonSeed +
                  "; small-foliage-shadow-off=" + RemoveSmallFoliageShadows +
                  "; shadowDistance=" + pipeline.shadowDistance);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartRuntimeScanner()
    {
        // Scanner runs in every variant so its modest overhead is comparable.
        // Never persists to scene or source prefab assets.
        if (FindFirstObjectByType<FunGuyPhaseDVisualAB>() != null)
            return;
        GameObject go = new GameObject("__FunGuy_PhaseD_Visual_AB");
        DontDestroyOnLoad(go);
        go.AddComponent<FunGuyPhaseDVisualAB>();
    }

    private void OnEnable()
    {
        _nextScanTime = 0f;
        _fpsWarmupUntil = Time.realtimeSinceStartup + FpsWarmupSeconds;
        _fpsElapsed = 0f;
        _fpsLongestFrame = 0f;
        _fpsFrames = 0;
        _fpsWindow = 0;
    }

    private void Update()
    {
        SampleGameFps();
        if (Time.unscaledTime < _nextScanTime)
            return;
        _nextScanTime = Time.unscaledTime + RescanInterval;
        ScanSmallFoliage();
    }

    private void SampleGameFps()
    {
        // Match the in-game FPSCounter's Update-frame and unscaled time sample.
        // Avoid ProfilerRecorder and intrusive Editor profiler recording.
        if (Time.realtimeSinceStartup < _fpsWarmupUntil)
            return;
        float delta = Time.unscaledDeltaTime;
        if (delta <= 0f)
            return;
        _fpsElapsed += delta;
        _fpsFrames++;
        if (delta > _fpsLongestFrame)
            _fpsLongestFrame = delta;
        if (_fpsElapsed < FpsSampleSeconds)
            return;

        Debug.Log("[FunGuy PhaseD FPS] mode=" + PipelineResource +
                  "; window=" + (++_fpsWindow) +
                  "; frames=" + _fpsFrames +
                  "; seconds=" + _fpsElapsed.ToString("F3") +
                  "; avgFps=" + (_fpsFrames / _fpsElapsed).ToString("F2") +
                  "; worstFrameMs=" + (1000f * _fpsLongestFrame).ToString("F2"));
        _fpsElapsed = 0f;
        _fpsLongestFrame = 0f;
        _fpsFrames = 0;
    }

    private void ScanSmallFoliage()
    {
        // Streamers generate/recycle blocks. Rechecks handle new grass instances.
        MeshRenderer[] renderers =
            FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int matched = 0;
        int changed = 0;
        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer == null || !renderer.gameObject.scene.IsValid())
                continue;
            string name = renderer.gameObject.name;
            bool isSmallFoliage =
                name.StartsWith("TFF_Grass_Patch_", StringComparison.Ordinal) ||
                name.StartsWith("TFF_Flower_Patch_", StringComparison.Ordinal);
            if (!isSmallFoliage)
                continue;
            matched++;
            if (RemoveSmallFoliageShadows &&
                renderer.shadowCastingMode != ShadowCastingMode.Off)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                changed++;
            }
        }

        if (!_loggedFirstScan || changed > 0)
        {
            Debug.Log("[FunGuy PhaseD] Foliage scan: matching MeshRenderers=" +
                      matched + ", shadows disabled in scan=" + changed +
                      ", mode=" + PipelineResource);
            _loggedFirstScan = true;
        }
    }
}
#endif
