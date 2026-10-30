#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Unity.Profiling.Memory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

// One-time read-only Editor profiling run. No Android builds, quality edits, or gameplay edits.
// Trigger by creating .utmp/profiling/editor-2026-10-31/run.request.
// The existing FunGuyPerformanceAudit.cs is left untouched.
[InitializeOnLoad]
internal static class FunGuyEditorSnapshotSession
{
    const string SessionKey = "FunGuy.EditorProfiling.20261031.Stage";
    const string WasPlayingKey = "FunGuy.EditorProfiling.20261031.WasPlaying";
    const string RequestRelative = ".utmp/profiling/editor-2026-10-31/run.request";
    const int WarmupFrames = 120;
    const int CaptureFrames = 600;
    const int BetweenSnapshotsFrames = 600;
    static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".utmp/profiling/editor-2026-10-31"));
    static readonly List<Counter> Counters = new List<Counter>();
    static readonly List<double> FrameTimes = new List<double>();
    static string stage;
    static int lastGameFrame = -1, warmup, between, frames;
    static double lastRequestCheck;
    static double started;
    static string sceneAtStart;

    sealed class Counter : IDisposable
    {
        public string name, unit;
        public ProfilerRecorder recorder;
        public readonly List<double> values = new List<double>();
        public void Dispose() { recorder.Dispose(); }
    }

    static FunGuyEditorSnapshotSession()
    {
        stage = SessionState.GetString(SessionKey, "idle");
        if (stage == "failed" || stage == "interrupted")
        {
            stage = "idle";
            SessionState.SetString(SessionKey, stage);
        }
        else if (stage == "capturing")
        {
            // The Editor may reload while transitioning to Play mode.
            // Start a fresh sample rather than treating partial data as valid.
            stage = "waiting_for_play";
            SessionState.SetString(SessionKey, stage);
        }
        EditorApplication.update += Update;
        AssemblyReloadEvents.beforeAssemblyReload += OnReload;
        // Do not treat a delayed Editor callback as an interrupted capture.
        // Entering Play mode can reload assemblies and queue a delayCall after Begin().
        // AssemblyReloadEvents handles genuine reloads and the session can restart safely.
    }

    static void OnReload()
    {
        DisposeCounters();
        // Entering Play mode can reload the scripting domain after the capture starts.
        // Discard that partial CPU capture and restart cleanly once the reload finishes.
        if (stage == "capturing")
            SessionState.SetString(SessionKey, "waiting_for_play");
        else if (stage == "snapshot_early" || stage == "snapshot_late" || stage == "between")
            SessionState.SetString(SessionKey, "interrupted");
    }

    static void SetStage(string next)
    {
        stage = next;
        SessionState.SetString(SessionKey, next);
        WriteStatus(null);
    }

    static void WriteStatus(string error)
    {
        try
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Path.Combine(Root, "status.json"), JsonConvert.SerializeObject(new
            {
                stage, error, utc = DateTime.UtcNow.ToString("o"),
                scene = SceneManager.GetActiveScene().path,
                playing = EditorApplication.isPlaying,
                capturedFrames = frames, targetFrames = CaptureFrames,
                unityVersion = Application.unityVersion
            }, Formatting.Indented));
        }
        catch (Exception ex) { Debug.LogWarning("FunGuy audit status write failed: " + ex.Message); }
    }

    static void Update()
    {
        if (stage == "idle" && EditorApplication.timeSinceStartup - lastRequestCheck > 1)
        {
            lastRequestCheck = EditorApplication.timeSinceStartup;
            string request = Path.GetFullPath(Path.Combine(Application.dataPath, "..", RequestRelative));
            if (File.Exists(request))
            {
                File.Delete(request);
                SessionState.SetBool(WasPlayingKey, EditorApplication.isPlaying);
                SetStage("entering");
            }
        }

        if (stage == "entering")
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying)
                return;
            if (!EditorApplication.isPlaying)
            {
                var scene = SceneManager.GetActiveScene();
                if (scene.path.IndexOf("MushroomRunnerGameplay", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    Fail("Active scene is not MushroomRunnerGameplay. Open the gameplay scene before capturing.");
                    return;
                }
                if (scene.isDirty)
                {
                    Fail("Active scene contains unsaved changes. Refusing to enter Play mode to avoid disturbing edits.");
                    return;
                }
                SetStage("waiting_for_play");
                EditorApplication.isPlaying = true;
            }
            else
            {
                Begin();
            }
            return;
        }

        if (stage == "waiting_for_play")
        {
            if (EditorApplication.isPlaying && !EditorApplication.isCompiling)
                Begin();
            return;
        }

        if (!EditorApplication.isPlaying && (stage == "capturing" || stage == "between" || stage.StartsWith("snapshot_", StringComparison.Ordinal)))
        {
            Fail("Play mode ended during profiling.");
            return;
        }

        if (stage == "capturing")
        {
            if (Time.frameCount == lastGameFrame) return;
            lastGameFrame = Time.frameCount;
            if (warmup-- > 0) return;
            FrameTimes.Add(Time.unscaledDeltaTime * 1000.0);
            foreach (var c in Counters)
                c.values.Add(c.recorder.LastValue);
            frames++;
            if (frames >= CaptureFrames)
                FinishCpuAndCaptureEarlyMemory();
            return;
        }

        if (stage == "between")
        {
            if (Time.frameCount == lastGameFrame) return;
            lastGameFrame = Time.frameCount;
            if (++between >= BetweenSnapshotsFrames)
                CaptureMemory("late");
        }
    }

    static void Begin()
    {
        sceneAtStart = SceneManager.GetActiveScene().path;
        if (sceneAtStart.IndexOf("MushroomRunnerGameplay", StringComparison.OrdinalIgnoreCase) < 0)
        {
            Fail("Play mode scene is not MushroomRunnerGameplay.");
            return;
        }
        Directory.CreateDirectory(Root);
        countersSetup();
        frames = 0;
        warmup = WarmupFrames;
        between = 0;
        lastGameFrame = -1;
        FrameTimes.Clear();
        started = EditorApplication.timeSinceStartup;

        ProfilerDriver.ClearAllFrames();
        Profiler.logFile = Path.Combine(Root, "editor-cpu-binary.raw");
        Profiler.enableBinaryLog = true;
        Profiler.enabled = true;
        SetStage("capturing");
        WriteJson("environment.json", new
        {
            unity = Application.unityVersion,
            scene = sceneAtStart,
            startedUtc = DateTime.UtcNow.ToString("o"),
            source = "Unity Editor Play mode; includes Editor overhead",
            isDevelopmentBuild = Debug.isDebugBuild,
            graphicsDevice = SystemInfo.graphicsDeviceName,
            graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
            graphicsMemoryTotalReportedMB = SystemInfo.graphicsMemorySize,
            systemMemoryTotalReportedMB = SystemInfo.systemMemorySize,
            processor = SystemInfo.processorType,
            processorCount = SystemInfo.processorCount,
            resolution = new { Screen.width, Screen.height },
            quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
            vSyncCount = QualitySettings.vSyncCount,
            targetFrameRate = Application.targetFrameRate,
            warmupFrames = WarmupFrames, measuredFrames = CaptureFrames,
            betweenSnapshotsFrames = BetweenSnapshotsFrames,
            memorySnapshotScope = "Whole Unity Editor process, NOT isolated game"
        });
    }

    static void countersSetup()
    {
        DisposeCounters();
        string[] wanted =
        {
            "Main Thread", "Render Thread", "GPU Frame Time",
            "GC Allocated In Frame", "GC Used Memory", "Total Used Memory",
            "Draw Calls Count", "Batches Count", "SetPass Calls Count",
            "Triangles Count", "Vertices Count", "Shadow Casters Count",
            "Instanced Batches Count", "Physics.Simulate",
            "Funguy.LandingAssist.Search", "Funguy.LandingAssist.Correction"
        };
        var available = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(available);
        var nameSet = new HashSet<string>(wanted);
        foreach (var h in available)
        {
            var desc = ProfilerRecorderHandle.GetDescription(h);
            if (!nameSet.Contains(desc.Name)) continue;
            var recorder = ProfilerRecorder.StartNew(desc.Category, desc.Name, 1);
            if (recorder.Valid) Counters.Add(new Counter { name = desc.Name, unit = desc.UnitType.ToString(), recorder = recorder });
            else recorder.Dispose();
        }
    }

    static void FinishCpuAndCaptureEarlyMemory()
    {
        try
        {
            Profiler.enableBinaryLog = false;
            Profiler.enabled = false;
            ProfilerDriver.SaveProfile(Path.Combine(Root, "editor-cpu-profile.raw"));
            WriteJson("cpu-aggregate.json", new
            {
                measuredFrames = frames,
                frameTimeMs = Stats(FrameTimes),
                gpuFrameMs = GpuFramesFromRaw(120),
                recorderStats = Counters.Select(c => new { c.name, c.unit, stats = Stats(c.values) }).ToArray(),
                topCpuMarkers = TopMarkers(120),
                allMeasurementsAreEditorProcess = true
            });
            DisposeCounters();
        }
        catch (Exception ex)
        {
            Fail("CPU capture failed: " + ex);
            return;
        }
        CaptureMemory("early");
    }

    static object GpuFramesFromRaw(int maxFrames)
    {
        var values = new List<double>();
        int last = ProfilerDriver.lastFrameIndex;
        for (int i = Math.Max(ProfilerDriver.firstFrameIndex, last - maxFrames + 1); i <= last; i++)
        {
            using (var view = ProfilerDriver.GetRawFrameDataView(i, 0))
            {
                if (view.valid && view.frameGpuTimeMs > 0)
                    values.Add(view.frameGpuTimeMs);
            }
        }
        return Stats(values);
    }

    static object TopMarkers(int maxFrames)
    {
        var summary = new Dictionary<string, double>();
        var calls = new Dictionary<string, int>();
        int last = ProfilerDriver.lastFrameIndex;
        int count = 0;
        for (int f = Math.Max(ProfilerDriver.firstFrameIndex, last - maxFrames + 1); f <= last; f++)
        {
            using (var view = ProfilerDriver.GetRawFrameDataView(f, 0))
            {
                if (!view.valid) continue;
                count++;
                for (int i = 0; i < view.sampleCount; i++)
                {
                    string n = view.GetSampleName(i);
                    if (string.IsNullOrEmpty(n)) continue;
                    double old;
                    summary.TryGetValue(n, out old);
                    summary[n] = old + view.GetSampleTimeMs(i);
                    int c;
                    calls.TryGetValue(n, out c);
                    calls[n] = c + 1;
                }
            }
        }
        return summary.OrderByDescending(x => x.Value).Take(80)
            .Select(x => new { name = x.Key, inclusiveMsPerFrame = x.Value / Math.Max(1, count), sampleCalls = calls[x.Key] }).ToArray();
    }

    static object Stats(List<double> vals)
    {
        if (vals.Count == 0) return new { count = 0, mean = 0.0, p95 = 0.0, max = 0.0, min = 0.0 };
        var sorted = vals.OrderBy(x => x).ToArray();
        return new
        {
            count = vals.Count,
            mean = vals.Average(),
            p95 = sorted[(int)((vals.Count - 1) * 0.95)],
            max = sorted[vals.Count - 1],
            min = sorted[0]
        };
    }

    static void CaptureMemory(string label)
    {
        SetStage("snapshot_" + label);
        string path = Path.Combine(Root, "memory-" + label + ".snap");
        if (File.Exists(path)) File.Delete(path);
        WriteJson("memory-counters-" + label + ".json", new
        {
            capturedUtc = DateTime.UtcNow.ToString("o"),
            captureScope = "Entire Unity Editor process",
            unityTotalAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
            unityTotalReservedBytes = Profiler.GetTotalReservedMemoryLong(),
            unityMonoUsedBytes = Profiler.GetMonoUsedSizeLong(),
            unityMonoHeapBytes = Profiler.GetMonoHeapSizeLong(),
            graphicsDriverAllocatedBytes = Profiler.GetAllocatedMemoryForGraphicsDriver(),
            graphicsReportedTotalMbNotUsage = SystemInfo.graphicsMemorySize,
            gameFrame = Time.frameCount,
            scene = SceneManager.GetActiveScene().path
        });
        try
        {
            MemoryProfiler.TakeSnapshot(path, (string resultPath, bool success) =>
            {
                WriteJson("memory-result-" + label + ".json", new
                {
                    success, path = resultPath, exists = File.Exists(resultPath),
                    bytes = File.Exists(resultPath) ? new FileInfo(resultPath).Length : 0
                });
                if (!success)
                {
                    Fail("Memory Profiler snapshot failed: " + label);
                    return;
                }
                if (label == "early")
                {
                    between = 0;
                    SetStage("between");
                }
                else
                {
                    Complete();
                }
            }, CaptureFlags.ManagedObjects | CaptureFlags.NativeObjects);
        }
        catch (Exception ex) { Fail("TakeSnapshot " + label + " failed: " + ex); }
    }

    static void Complete()
    {
        SetStage("complete");
        WriteJson("result.json", new
        {
            completedUtc = DateTime.UtcNow.ToString("o"), result = "complete",
            cpuCapture = "editor-cpu-profile.raw",
            earlyMemorySnapshot = "memory-early.snap",
            lateMemorySnapshot = "memory-late.snap",
            profileAnalyzerMenu = "Window/Analysis/Profile Analyzer",
            measuredIn = "Editor Play mode; not representative of exact device CPU/GPU utilization",
            noBuildPerformed = true
        });
        Debug.Log("[FunGuy Profile] CPU profile and both memory snapshots saved to " + Root);
        if (!SessionState.GetBool(WasPlayingKey, false))
            EditorApplication.isPlaying = false;
        EditorApplication.delayCall += () => EditorApplication.ExecuteMenuItem("Window/Analysis/Profile Analyzer");
    }

    static void Fail(string error)
    {
        DisposeCounters();
        if (Profiler.enabled) Profiler.enabled = false;
        if (Profiler.enableBinaryLog) Profiler.enableBinaryLog = false;
        SetStage("failed");
        WriteStatus(error);
        Debug.LogError("[FunGuy Profile] " + error);
        if (EditorApplication.isPlaying && !SessionState.GetBool(WasPlayingKey, false))
            EditorApplication.isPlaying = false;
    }

    static void DisposeCounters()
    {
        foreach (var c in Counters) c.Dispose();
        Counters.Clear();
    }

    static void WriteJson(string name, object payload)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(Path.Combine(Root, name), JsonConvert.SerializeObject(payload, Formatting.Indented));
    }
}
#endif
