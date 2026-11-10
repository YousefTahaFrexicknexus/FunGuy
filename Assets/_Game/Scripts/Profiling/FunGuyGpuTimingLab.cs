// Entire implementation is development-player only. Runtime changes do not alter
// imported URP assets or ship in the Windows release build.
#if UNITY_EDITOR || (DEVELOPMENT_BUILD && UNITY_STANDALONE_WIN)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Windows DX12 GPU/CPU timing lab. F8 toggles the overlay. Runtime switches
/// restore the authored PC baseline on exit and after an A/B suite.
/// FrameTimingManager values are delayed; the suite excludes transition frames.
/// </summary>
[DisallowMultipleComponent]
internal sealed class FunGuyGpuTimingLab : MonoBehaviour
{
    private readonly FrameTiming[] latest = new FrameTiming[1];
    private readonly List<float> cpuSamples = new List<float>(2400);
    private readonly List<float> gpuSamples = new List<float>(2400);
    private readonly string[] caseNames =
    {
        "PC baseline",
        "Shadow distance 80",
        "Shadow distance 40",
        "2 cascades",
        "Shadow atlas 2048",
        "Shadow atlas 1024",
        "SSAO off",
        "MSAA 2x",
        "MSAA off",
        "HDR off",
        "Main sun shadows off",
        "LOD bias 1.5"
    };

    private UniversalRenderPipelineAsset pipeline;
    private ScriptableRendererFeature ssaoFeature;
    private Light sun;
    private Baseline saved;
    private bool initialized;
    private bool showPanel = true;
    private bool recording = true;
    private bool suiteRunning;
    private bool suiteRequested;
    private bool timingsAvailable;
    private int suiteIndex = -1;
    private int countedFrames;
    private int validGpuFrames;
    private int sampleFrameCount;
    private ulong lastTimingTimestamp;
    private double caseStart;
    private double lastFlush;
    private double lastLog;
    private double sessionStart;
    private float liveCpu;
    private float liveGpu;
    private bool liveGpuValid;
    private string resultSummary = "Start an A/B suite in gameplay, or change one setting manually.";
    private string outputDirectory;
    private StreamWriter csv;
    private StreamWriter report;
    private RunFlowCoordinator flow;
    private Transform player;
    private int nextRefreshFrame;
    private Vector2 scroll;
    private Rect panelRect = new Rect(0, 70, 415, 680);
    private const double SecondsPerCase = 7.0;
    private const double DiscardAfterChangeSeconds = 1.75;
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private bool inError;

    private struct Baseline
    {
        public float shadowDistance;
        public int atlas;
        public int cascades;
        public int msaa;
        public float lodBias;
        public bool hdr;
        public bool ssaoEnabled;
        public LightShadows sunShadows;
    }

    private void Awake()
    {
        var probe = GetComponent<FunGuyWindowsDevelopmentProbe>();
        outputDirectory = probe != null ? probe.CaptureDirectory : null;
        if (string.IsNullOrEmpty(outputDirectory))
            outputDirectory = Path.Combine(Application.persistentDataPath, "FunGuyProfiling",
                DateTime.Now.ToString("yyyyMMdd-HHmmss", Inv));
        Directory.CreateDirectory(outputDirectory);
        csv = new StreamWriter(Path.Combine(outputDirectory, "frame-timing-gpu.csv"),
            false, new UTF8Encoding(false), 128 * 1024);
        csv.WriteLine("elapsed_s,frame,frame_start_timestamp,scene,player_z,cpu_frame_ms,cpu_main_ms,cpu_render_ms,gpu_ms,gpu_valid,unity_delta_ms,shadow_distance,shadow_atlas,shadow_cascades,msaa,ssao,hdr,sun_shadows,lod_bias,render_scale,ab_case,ab_phase");
        report = new StreamWriter(Path.Combine(outputDirectory, "graphics-ab-results.csv"),
            false, new UTF8Encoding(false));
        report.WriteLine("case_index,case_name,seconds,samples_cpu,samples_gpu_valid,cpu_mean_ms,cpu_p50_ms,cpu_p95_ms,cpu_p99_ms,gpu_mean_ms,gpu_p50_ms,gpu_p95_ms,gpu_p99_ms,scene,player_z,note");
        sessionStart = Time.realtimeSinceStartupAsDouble;
        lastFlush = sessionStart;
        timingsAvailable = FrameTimingManager.IsFeatureEnabled();
        suiteRequested = HasArgument("-funguy-run-ab");
        SceneManager.sceneLoaded += OnSceneLoaded;
        Application.quitting += FlushAndClose;
        Debug.Log("[FunGuy GPU Timing] FrameTimingManager enabled=" + timingsAvailable +
            " GPU file=" + outputDirectory +
            " autoSuite=" + suiteRequested);
    }

    private static bool HasArgument(string value)
    {
        foreach (string a in Environment.GetCommandLineArgs())
            if (string.Equals(a, value, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (initialized) SetSettings(saved);
        initialized = false;
        pipeline = null;
        ssaoFeature = null;
        sun = null;
        flow = null;
        player = null;
        nextRefreshFrame = 0;
        if (suiteRunning)
        {
            FinishCase("scene changed; measurements stopped");
            CancelSuite();
        }
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
            showPanel = !showPanel;
        if (Time.frameCount >= nextRefreshFrame)
        {
            nextRefreshFrame = Time.frameCount + 45;
            if (flow == null) flow = FindFirstObjectByType<RunFlowCoordinator>();
            if (player == null)
            {
                var runner = FindFirstObjectByType<MushroomRunnerPlayer>();
                player = runner != null ? runner.transform : null;
            }
        }

        if (!initialized && flow != null && flow.IsStartupReady &&
            SceneManager.GetActiveScene().name == "MushroomRunnerGameplay")
            InitializeSettings();

        if (!suiteRunning && suiteRequested && SceneManager.GetActiveScene().name == "MushroomRunnerGameplay" &&
            flow != null && flow.IsStartupReady && initialized)
        {
            suiteRequested = false;
            BeginSuite();
        }

        if (suiteRunning)
        {
            if (flow == null || !flow.IsStartupReady)
                return;
            if (Time.realtimeSinceStartupAsDouble - caseStart >= SecondsPerCase)
            {
                FinishCase("automated test; same view recommended");
                if (suiteIndex + 1 < caseNames.Length)
                    BeginCase(suiteIndex + 1);
                else
                    CompleteSuite();
            }
        }
    }

    private void LateUpdate()
    {
        if (!timingsAvailable || inError) return;
        try
        {
            FrameTimingManager.CaptureFrameTimings();
            uint count = FrameTimingManager.GetLatestTimings(1, latest);
            if (count == 0) return;
            FrameTiming t = latest[0];
            ulong timestamp = t.frameStartTimestamp;
            if (timestamp != 0 && timestamp == lastTimingTimestamp) return;
            lastTimingTimestamp = timestamp;

            double elapsed = Time.realtimeSinceStartupAsDouble;
            float cpu = (float)t.cpuFrameTime;
            float gpu = (float)t.gpuFrameTime;
            bool cpuOk = cpu > 0f && cpu < 2000f && !float.IsNaN(cpu);
            bool gpuOk = gpu > 0f && gpu < 2000f && !float.IsNaN(gpu);
            if (!cpuOk) return;

            countedFrames++;
            if (gpuOk) validGpuFrames++;
            liveCpu = cpu;
            liveGpu = gpu;
            liveGpuValid = gpuOk;

            string scene = SceneManager.GetActiveScene().name;
            double age = elapsed - caseStart;
            bool isGame = scene == "MushroomRunnerGameplay" && flow != null && flow.IsStartupReady;
            bool isMeasured = suiteRunning && isGame && age >= DiscardAfterChangeSeconds;
            if (isMeasured)
            {
                cpuSamples.Add(cpu);
                sampleFrameCount++;
                if (gpuOk) gpuSamples.Add(gpu);
            }

            if (recording && csv != null)
            {
                Baseline v = CurrentSettings();
                var sb = new StringBuilder(330);
                sb.Append((elapsed - sessionStart).ToString("F4", Inv)).Append(',');
                sb.Append(Time.frameCount).Append(',').Append(timestamp).Append(',');
                sb.Append('"').Append(scene).Append('"').Append(',');
                sb.Append((player != null ? player.position.z : 0f).ToString("F2", Inv)).Append(',');
                sb.Append(cpu.ToString("F4", Inv)).Append(',');
                sb.Append(t.cpuMainThreadFrameTime.ToString("F4", Inv)).Append(',');
                sb.Append(t.cpuRenderThreadFrameTime.ToString("F4", Inv)).Append(',');
                sb.Append(gpuOk ? gpu.ToString("F4", Inv) : "").Append(',');
                sb.Append(gpuOk ? '1' : '0').Append(',');
                sb.Append((Time.unscaledDeltaTime * 1000f).ToString("F4", Inv)).Append(',');
                sb.Append(v.shadowDistance.ToString("F0", Inv)).Append(',');
                sb.Append(v.atlas).Append(',').Append(v.cascades).Append(',');
                sb.Append(v.msaa).Append(',').Append(v.ssaoEnabled ? '1' : '0').Append(',');
                sb.Append(v.hdr ? '1' : '0').Append(',').Append((int)v.sunShadows).Append(',');
                sb.Append(v.lodBias.ToString("F2", Inv)).Append(',');
                sb.Append(pipeline != null ? pipeline.renderScale.ToString("F3", Inv) : "").Append(',');
                sb.Append(suiteRunning ? suiteIndex.ToString(Inv) : "-1").Append(',');
                sb.Append(suiteRunning ? (isMeasured ? "measure" : "settle") : "manual");
                csv.WriteLine(sb.ToString());
            }

            if (elapsed - lastFlush >= 1.0)
            {
                csv?.Flush();
                report?.Flush();
                lastFlush = elapsed;
            }
        }
        catch (Exception ex)
        {
            inError = true;
            Debug.LogError("[FunGuy GPU Timing] FrameTimingManager failure: " + ex);
        }
    }

    private void InitializeSettings()
    {
        pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null) return;
        if (pipeline.rendererDataList.Length > 0)
        {
            foreach (var data in pipeline.rendererDataList)
            {
                if (data == null) continue;
                foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                {
                    if (feature != null &&
                        feature.name.IndexOf("AmbientOcclusion", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ssaoFeature = feature;
                        break;
                    }
                }
                if (ssaoFeature != null) break;
            }
        }
        sun = RenderSettings.sun;
        if (sun == null)
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (Light light in lights)
                if (light != null && light.type == LightType.Directional) { sun = light; break; }
        }
        // The authored Windows PC asset is the source of truth; modifications
        // take place only in this Development Player process and are restored.
        saved = CurrentSettings();
        initialized = true;
        Debug.Log("[FunGuy GPU Timing] PC baseline: distance=" + saved.shadowDistance +
            " cascades=" + saved.cascades + " atlas=" + saved.atlas +
            " msaa=" + saved.msaa + " ssao=" + saved.ssaoEnabled +
            " hdr=" + saved.hdr + " sunShadow=" + saved.sunShadows +
            " renderScale=" + pipeline.renderScale);
    }

    private Baseline CurrentSettings()
    {
        return new Baseline
        {
            shadowDistance = pipeline != null ? pipeline.shadowDistance : 0f,
            atlas = pipeline != null ? pipeline.mainLightShadowmapResolution : 0,
            cascades = pipeline != null ? pipeline.shadowCascadeCount : 0,
            msaa = pipeline != null ? pipeline.msaaSampleCount : 0,
            hdr = pipeline != null && pipeline.supportsHDR,
            ssaoEnabled = ssaoFeature != null && ssaoFeature.isActive,
            lodBias = QualitySettings.lodBias,
            sunShadows = sun != null ? sun.shadows : LightShadows.None
        };
    }

    private void SetSettings(Baseline b)
    {
        if (pipeline == null) return;
        pipeline.shadowDistance = b.shadowDistance;
        pipeline.mainLightShadowmapResolution = b.atlas;
        pipeline.shadowCascadeCount = b.cascades;
        pipeline.msaaSampleCount = b.msaa;
        pipeline.supportsHDR = b.hdr;
        if (ssaoFeature != null) ssaoFeature.SetActive(b.ssaoEnabled);
        if (sun != null) sun.shadows = b.sunShadows;
        QualitySettings.lodBias = b.lodBias;
        // Render scale is deliberately never adjustable in this experiment.
        pipeline.renderScale = 1f;
    }

    private void Change(Action<BaselineSetter> apply)
    {
        if (!initialized || suiteRunning) return;
        var mutable = new BaselineSetter(CurrentSettings(), saved.sunShadows);
        apply(mutable);
        SetSettings(mutable.Value);
        resultSummary = "Manual change recorded. Hold camera still, then use Run A/B suite for comparisons.";
    }

    private sealed class BaselineSetter
    {
        public Baseline Value;
        private readonly LightShadows initialSunShadows;
        public BaselineSetter(Baseline initial, LightShadows originalSunShadows)
        {
            Value = initial;
            initialSunShadows = originalSunShadows;
        }
        public void Distance(float value) { var b=Value; b.shadowDistance=value; Value=b; }
        public void Atlas(int value) { var b=Value; b.atlas=value; Value=b; }
        public void Cascades(int value) { var b=Value; b.cascades=value; Value=b; }
        public void Msaa(int value) { var b=Value; b.msaa=value; Value=b; }
        public void AO(bool value) { var b=Value; b.ssaoEnabled=value; Value=b; }
        public void HDR(bool value) { var b=Value; b.hdr=value; Value=b; }
        public void SunShadows(bool value) { var b=Value; b.sunShadows=value ? initialSunShadows : LightShadows.None; Value=b; }
        public void LOD(float value) { var b=Value; b.lodBias=value; Value=b; }
    }

    private void BeginSuite()
    {
        if (!initialized || suiteRunning) return;
        if (flow == null || !flow.IsStartupReady ||
            SceneManager.GetActiveScene().name != "MushroomRunnerGameplay")
        {
            resultSummary = "Enter regular gameplay and finish loading before starting.";
            return;
        }
        suiteRunning = true;
        recording = true;
        report.WriteLine("# A/B suite UTC " + DateTime.UtcNow.ToString("o") +
            " screen=" + Screen.width + "x" + Screen.height +
            " renderScale=1.0; settings change one at a time; settle=" +
            DiscardAfterChangeSeconds.ToString(Inv) + "s; sample=" +
            (SecondsPerCase - DiscardAfterChangeSeconds).ToString(Inv) +
            "s. Static camera recommended; GPU timestamps delayed by several frames.");
        BeginCase(0);
        resultSummary = "Running automatic A/B. Keep the camera stationary for a fair comparison.";
    }

    private void BeginCase(int index)
    {
        suiteIndex = index;
        SetSettings(saved);
        var b = saved;
        switch (index)
        {
            case 1: b.shadowDistance = 80; break;
            case 2: b.shadowDistance = 40; break;
            case 3: b.cascades = 2; break;
            case 4: b.atlas = 2048; break;
            case 5: b.atlas = 1024; break;
            case 6: b.ssaoEnabled = false; break;
            case 7: b.msaa = 2; break;
            case 8: b.msaa = 1; break;
            case 9: b.hdr = false; break;
            case 10: b.sunShadows = LightShadows.None; break;
            case 11: b.lodBias = 1.5f; break;
        }
        SetSettings(b);
        cpuSamples.Clear();
        gpuSamples.Clear();
        sampleFrameCount = 0;
        caseStart = Time.realtimeSinceStartupAsDouble;
        Debug.Log("[FunGuy GPU A/B] Case " + (index + 1) + "/" + caseNames.Length +
            " " + caseNames[index]);
    }

    private static float Percentile(List<float> data, float pct)
    {
        if (data.Count == 0) return 0f;
        var copy = new List<float>(data);
        copy.Sort();
        return copy[Mathf.Clamp(Mathf.RoundToInt((copy.Count - 1) * pct), 0, copy.Count - 1)];
    }

    private static float Mean(List<float> data)
    {
        if (data.Count == 0) return 0f;
        double sum = 0;
        for (int i = 0; i < data.Count; i++) sum += data[i];
        return (float)(sum / data.Count);
    }

    private void FinishCase(string note)
    {
        if (suiteIndex < 0 || report == null) return;
        string s = string.Join(",", new string[]
        {
            suiteIndex.ToString(Inv),
            "\"" + caseNames[suiteIndex] + "\"",
            SecondsPerCase.ToString("F2", Inv),
            cpuSamples.Count.ToString(Inv),
            gpuSamples.Count.ToString(Inv),
            Mean(cpuSamples).ToString("F4", Inv),
            Percentile(cpuSamples, 0.50f).ToString("F4", Inv),
            Percentile(cpuSamples, 0.95f).ToString("F4", Inv),
            Percentile(cpuSamples, 0.99f).ToString("F4", Inv),
            gpuSamples.Count > 0 ? Mean(gpuSamples).ToString("F4", Inv) : "",
            gpuSamples.Count > 0 ? Percentile(gpuSamples, 0.50f).ToString("F4", Inv) : "",
            gpuSamples.Count > 0 ? Percentile(gpuSamples, 0.95f).ToString("F4", Inv) : "",
            gpuSamples.Count > 0 ? Percentile(gpuSamples, 0.99f).ToString("F4", Inv) : "",
            "\"" + SceneManager.GetActiveScene().name + "\"",
            (player != null ? player.position.z : 0f).ToString("F2", Inv),
            "\"" + note.Replace("\"", "'") + "\""
        });
        report.WriteLine(s);
        report.Flush();
        resultSummary = caseNames[suiteIndex] + ": CPU " +
            Percentile(cpuSamples, 0.5f).ToString("F2", Inv) + " ms / GPU " +
            (gpuSamples.Count > 0 ? Percentile(gpuSamples, 0.5f).ToString("F2", Inv) + " ms" : "NO VALID TIMINGS") +
            " (" + gpuSamples.Count + "/" + cpuSamples.Count + " valid GPU)";
    }

    private void CompleteSuite()
    {
        suiteRunning = false;
        suiteIndex = -1;
        SetSettings(saved);
        report.WriteLine("# Finished; original PC baseline restored. Raw frame settings and positions in frame-timing-gpu.csv");
        report.Flush();
        resultSummary = "A/B complete. Original PC graphics restored. CSV saved: " + outputDirectory;
        Debug.Log("[FunGuy GPU A/B] Complete. Results " + outputDirectory);
    }

    private void CancelSuite()
    {
        suiteRunning = false;
        suiteIndex = -1;
        SetSettings(saved);
        resultSummary = "A/B cancelled; PC baseline restored. Partial results saved.";
    }

    private void OnGUI()
    {
        if (!showPanel || !initialized) return;
        float width = Mathf.Min(430f, Mathf.Max(300f, Screen.width * 0.38f));
        float height = Mathf.Min(Screen.height - 25f, 780f);
        if (height < 250f) return;
        panelRect = new Rect(Screen.width - width - 15f, 14f, width, height);
        GUI.depth = -1000;
        GUI.backgroundColor = new Color(0.16f, 0.23f, 0.21f, 0.97f);
        GUI.Window(61427, panelRect, DrawPanel, "FUNG UY  |  DX12 GPU TIMING LAB  [F8]");
    }

    private void DrawPanel(int id)
    {
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("PC graphics • Native render scale 1.0 (locked)");
        GUILayout.Label("GPU timings: " + (timingsAvailable ? (liveGpuValid ? liveGpu.ToString("F2", Inv) + " ms" : "unavailable") : "FEATURE DISABLED") +
            " | CPU: " + liveCpu.ToString("F2", Inv) + " ms");
        GUILayout.Label("Valid GPU samples: " + validGpuFrames + " / " + countedFrames +
            "   |   " + Screen.width + "x" + Screen.height);
        GUILayout.Label("Scene: " + SceneManager.GetActiveScene().name +
            " | player Z: " + (player != null ? player.position.z.ToString("F1", Inv) : "N/A"));
        if (!timingsAvailable)
            GUILayout.Label("GPU timing not supported; CPU results only. No invented GPU values.");

        recording = GUILayout.Toggle(recording, "Record per-frame CPU/GPU CSV");
        GUI.enabled = !suiteRunning;
        if (GUILayout.Button("RESTORE ORIGINAL PC GRAPHICS (all settings)"))
        {
            SetSettings(saved);
            resultSummary = "Original PC settings restored.";
        }

        var b = CurrentSettings();
        GUILayout.Space(8);
        GUILayout.Label("SHADOWS  |  current " + b.shadowDistance.ToString("F0", Inv) + " units");
        float nextDist = GUILayout.HorizontalSlider(b.shadowDistance, 20f, 200f);
        int roundedDist = Mathf.RoundToInt(nextDist / 10f) * 10;
        if (roundedDist != Mathf.RoundToInt(b.shadowDistance))
            Change(s => s.Distance(roundedDist));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("40")) Change(s => s.Distance(40));
        if (GUILayout.Button("80")) Change(s => s.Distance(80));
        if (GUILayout.Button("120")) Change(s => s.Distance(120));
        if (GUILayout.Button("160")) Change(s => s.Distance(160));
        GUILayout.EndHorizontal();

        GUILayout.Label("Main shadow atlas: " + b.atlas);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("1024")) Change(s => s.Atlas(1024));
        if (GUILayout.Button("2048")) Change(s => s.Atlas(2048));
        if (GUILayout.Button("4096")) Change(s => s.Atlas(4096));
        GUILayout.EndHorizontal();

        GUILayout.Label("Cascades: " + b.cascades);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("1")) Change(s => s.Cascades(1));
        if (GUILayout.Button("2")) Change(s => s.Cascades(2));
        if (GUILayout.Button("4")) Change(s => s.Cascades(4));
        GUILayout.EndHorizontal();

        bool nextShadows = GUILayout.Toggle(b.sunShadows != LightShadows.None, "Main directional shadows ON");
        if (nextShadows != (b.sunShadows != LightShadows.None))
            Change(s => s.SunShadows(nextShadows));

        GUILayout.Space(5);
        GUILayout.Label("EFFECTS  |  one variable at a time");
        if (ssaoFeature != null)
        {
            bool nextAO = GUILayout.Toggle(b.ssaoEnabled, "Screen-space ambient occlusion (SSAO)");
            if (nextAO != b.ssaoEnabled) Change(s => s.AO(nextAO));
        }
        else GUILayout.Label("SSAO renderer feature not found: unavailable");

        bool nextHDR = GUILayout.Toggle(b.hdr, "HDR rendering");
        if (nextHDR != b.hdr) Change(s => s.HDR(nextHDR));
        GUILayout.Label("MSAA: " + b.msaa + "x");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Off")) Change(s => s.Msaa(1));
        if (GUILayout.Button("2x")) Change(s => s.Msaa(2));
        if (GUILayout.Button("4x")) Change(s => s.Msaa(4));
        GUILayout.EndHorizontal();
        GUILayout.Label("LOD bias: " + b.lodBias.ToString("F1", Inv));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("1.5")) Change(s => s.LOD(1.5f));
        if (GUILayout.Button("2.0")) Change(s => s.LOD(2f));
        if (GUILayout.Button("2.5")) Change(s => s.LOD(2.5f));
        GUILayout.EndHorizontal();
        GUI.enabled = true;

        GUILayout.Space(7);
        if (suiteRunning)
        {
            double sec = Time.realtimeSinceStartupAsDouble - caseStart;
            GUILayout.Label("A/B " + (suiteIndex + 1) + "/" + caseNames.Length + "  " +
                caseNames[suiteIndex] + "  " + sec.ToString("F1", Inv) + "/" +
                SecondsPerCase.ToString("F0", Inv) + "s");
            if (GUILayout.Button("CANCEL A/B (restore original)")) CancelSuite();
        }
        else if (GUILayout.Button("RUN ONE-AT-A-TIME A/B SUITE (~84 sec)"))
            BeginSuite();
        GUILayout.Label(resultSummary);
        GUILayout.Label("F8 hide/show  •  compare SAME CAMERA POSITION  •  fullscreen works");
        GUILayout.Label("Logs: Application.persistentDataPath/FunGuyProfiling/<timestamp>");
        GUILayout.EndScrollView();
    }

    private void FlushAndClose()
    {
        if (suiteRunning)
        {
            FinishCase("application exited; incomplete test");
            CancelSuite();
        }
        try
        {
            report?.Flush();
            report?.Dispose();
            csv?.Flush();
            csv?.Dispose();
        }
        catch (Exception ex) { Debug.LogWarning("[FunGuy GPU Timing] Could not close CSV: " + ex); }
        report = null;
        csv = null;
    }

    private void OnDestroy()
    {
        if (initialized) SetSettings(saved);
        FlushAndClose();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Application.quitting -= FlushAndClose;
    }
}
#endif
