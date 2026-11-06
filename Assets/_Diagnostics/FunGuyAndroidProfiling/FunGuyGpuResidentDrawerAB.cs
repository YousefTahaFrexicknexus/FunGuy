#if FUNG_GPU_RESIDENT_DRAWER_AB
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Temporary, build-profile-gated Android profiling experiment. Both diagnostic
/// profiles include this same bootstrap and both URP assets. Only the chosen
/// URP asset's GPU Resident Drawer setting differs.
/// </summary>
internal static class FunGuyGpuResidentDrawerAB
{
    private const int ComparisonSeed = 20261106;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyProfilePipeline()
    {
#if FUNG_GPU_RESIDENT_DRAWER_OFF
        const string resourceName = "FunGuy_GRD_Off";
        const string mode = "OFF";
#else
        const string resourceName = "FunGuy_GRD_On";
        const string mode = "ON";
#endif

        // Both builds generate the same UnityEngine.Random-based starting world.
        // This affects diagnostics only, not ordinary profiles or production gameplay.
        Random.InitState(ComparisonSeed);

        UniversalRenderPipelineAsset pipeline = Resources.Load<UniversalRenderPipelineAsset>(resourceName);
        if (pipeline == null)
        {
            Debug.LogError("[FunGuy Profiling] GPU Resident Drawer A/B failed: missing Resources pipeline " + resourceName);
            return;
        }

        QualitySettings.renderPipeline = pipeline;
        Debug.Log("[FunGuy Profiling] GPU Resident Drawer " + mode +
                  " diagnostic URP selected: " + pipeline.name +
                  "; fixed Unity random seed=" + ComparisonSeed);
    }

}
#endif
