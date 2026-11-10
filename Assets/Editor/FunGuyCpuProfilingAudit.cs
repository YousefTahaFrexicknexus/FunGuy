using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UProfiler = UnityEngine.Profiling.Profiler;

/// <summary>
/// One-shot, editor-only CPU profiling harness. Request via
/// work/windows-profiling-2026-11-09/cpu-audit.request.
/// Does not save scenes, change project settings or build a player.
/// The stream phase moves only a temporary generation target, never the player.
/// </summary>
[InitializeOnLoad]
internal static class FunGuyCpuProfilingAudit
{
    private const double WarmupSeconds = 2.5;
    private const double BaselineSeconds = 6.0;
    private const double StreamSeconds = 13.0;
    private const double RecoverySeconds = 5.0;
    private const float StreamSpeed = 28f;

    private static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static readonly string AuditDir = Path.Combine(Root, "work", "windows-profiling-2026-11-09", "cpu-profile");
    private static readonly string RequestPath = Path.Combine(Root, "work", "windows-profiling-2026-11-09", "cpu-audit.request");
    private static readonly string ActiveFlag = Path.Combine(AuditDir, "active.flag");

    private static double nextPoll;
    private static double startedAt;
    private static double lastSampleAt;
    private static bool capturing;
    private static bool enteredPlayForAudit;
    private static bool savedPreviousProfiler;
    private static bool previousEnabled;
    private static bool previousBinaryLog;
    private static bool previousProfileEditor;
    private static bool previousCallstacks;
    private static bool previousRunInBackground;
    private static bool previousPaused;
    private static bool previousProfilerDriverEnabled;
    private static bool resetIssued;
    private static int lastSampleFrame = -1;
    private static string capturePrefix;
    private static string rawPath;
    private static GameObject anchorObject;
    private static Transform originalTarget;
    private static GridCourseStreamer grid;
    private static BlockSpawner blocks;
    private static RunFlowCoordinator coordinator;
    private static readonly List<Counter> Counters = new();
    private static readonly List<Sample> Samples = new();
    private static readonly List<string> Events = new();

    private sealed class Counter
    {
        public string Key;
        public string Unit;
        public ProfilerRecorder Recorder;
    }

    private sealed class Sample
    {
        public int Frame;
        public string Phase;
        public double Elapsed;
        public double WallMs;
        public float PlayerZ;
        public float AnchorZ;
        public int ActiveBlocks;
        public int PooledBlocks;
        public readonly Dictionary<string, long> Counters = new();
    }

    static FunGuyCpuProfilingAudit()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void Poll()
    {
        try
        {
            if (!EditorApplication.isPlaying && !capturing &&
                EditorApplication.timeSinceStartup >= nextPoll)
            {
                nextPoll = EditorApplication.timeSinceStartup + 2;
                if (File.Exists(RequestPath) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    if (SceneManager.GetActiveScene().isDirty)
                    {
                        Debug.LogWarning("[FunGuy CPU Audit] Refusing auto-Play with unsaved scene changes.");
                        return;
                    }

                    Directory.CreateDirectory(AuditDir);
                    File.Delete(RequestPath);
                    File.WriteAllText(ActiveFlag, DateTime.UtcNow.ToString("o"));
                    enteredPlayForAudit = true;
                    EditorApplication.EnterPlaymode();
                }
            }

            if (!EditorApplication.isPlaying || !enteredPlayForAudit || !File.Exists(ActiveFlag))
                return;

            if (!capturing)
            {
                if (startedAt == 0)
                    startedAt = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - startedAt >= WarmupSeconds)
                    StartCapture();
                return;
            }

            TickCapture();
        }
        catch (Exception ex)
        {
            Events.Add("EXCEPTION " + ex);
            Debug.LogError("[FunGuy CPU Audit] " + ex);
            FinishCapture("exception");
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(ActiveFlag))
        {
            enteredPlayForAudit = true;
            startedAt = EditorApplication.timeSinceStartup;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (capturing) FinishCapture("play-exited");
            enteredPlayForAudit = false;
            startedAt = 0;
        }
    }

    private static void StartCapture()
    {
        Directory.CreateDirectory(AuditDir);
        capturePrefix = Path.Combine(AuditDir, "funguy-cpu-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        rawPath = capturePrefix + ".raw";
        Samples.Clear();
        Events.Clear();
        resetIssued = false;
        lastSampleFrame = -1;

        grid = UnityEngine.Object.FindFirstObjectByType<GridCourseStreamer>();
        blocks = UnityEngine.Object.FindFirstObjectByType<BlockSpawner>();
        coordinator = UnityEngine.Object.FindFirstObjectByType<RunFlowCoordinator>();
        originalTarget = UnityEngine.Object.FindFirstObjectByType<MushroomRunnerPlayer>()?.transform;
        Events.Add("scene=" + SceneManager.GetActiveScene().name);
        Events.Add("streamer=" + (grid != null) + " blockSpawner=" + (blocks != null) +
                   " coordinator=" + (coordinator != null) + " player=" + (originalTarget != null));

        DiscoverCounters();
        previousEnabled = UProfiler.enabled;
        previousBinaryLog = UProfiler.enableBinaryLog;
        previousCallstacks = UProfiler.enableAllocationCallstacks;
        previousProfileEditor = ProfilerDriver.profileEditor;
        previousProfilerDriverEnabled = ProfilerDriver.enabled;
        previousRunInBackground = Application.runInBackground;
        previousPaused = EditorApplication.isPaused;
        savedPreviousProfiler = true;
        Events.Add("previousProfileEditor=" + previousProfileEditor +
                   " previousDriverEnabled=" + previousProfilerDriverEnabled +
                   " previousRunInBackground=" + previousRunInBackground +
                   " previousPaused=" + previousPaused +
                   " initialFrame=" + Time.frameCount);
        // The editor can be out of focus while the external audit tool is running.
        // These are temporary runtime/editor flags and are restored at the end.
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        ProfilerDriver.profileEditor = false;
        SetProfilerDriverEnabled(true);
        UProfiler.enabled = true;
        // The previous binary logging run captured only two game frames and produced
        // a 338 MB file, so prefer the live ProfilerDriver ring buffer for this test.
        UProfiler.enableBinaryLog = false;

        capturing = true;
        lastSampleAt = EditorApplication.timeSinceStartup;
        Events.Add("START " + DateTime.UtcNow.ToString("o"));
        Debug.Log("[FunGuy CPU Audit] Recording startup / stream / reset: " + rawPath);
    }

    private static void DiscoverCounters()
    {
        foreach (var c in Counters)
            c.Recorder.Dispose();
        Counters.Clear();

        var available = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(available);
        var lines = new List<string>();
        var selected = new HashSet<string>(StringComparer.Ordinal);
        var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Main Thread", "Render Thread", "GC Allocated In Frame",
            "GC Used Memory", "GC Reserved Memory", "System Used Memory",
            "Total Used Memory", "Draw Calls Count", "Batches Count",
            "SetPass Calls Count", "Triangles Count", "Vertices Count",
            "Physics.Simulate", "Physics.Processing", "BehaviourUpdate",
            "BehaviourFixedUpdate", "Scripts", "CPU Total Frame Time",
            "CPU Main Thread Frame Time", "CPU Render Thread Frame Time"
        };

        foreach (var handle in available)
        {
            var desc = ProfilerRecorderHandle.GetDescription(handle);
            string key = desc.Category.Name + "|" + desc.Name;
            lines.Add(key + "|" + desc.UnitType);
            if (!wanted.Contains(desc.Name) || !selected.Add(key))
                continue;

            var recorder = ProfilerRecorder.StartNew(desc.Category, desc.Name, 1);
            if (recorder.Valid)
                Counters.Add(new Counter { Key = key, Unit = desc.UnitType.ToString(), Recorder = recorder });
            else
                recorder.Dispose();
        }
        Directory.CreateDirectory(AuditDir);
        File.WriteAllLines(Path.Combine(AuditDir, "available-counters.txt"), lines.OrderBy(s => s));
    }

    private static void TickCapture()
    {
        double elapsed = EditorApplication.timeSinceStartup - lastSampleAt;
        if (elapsed > BaselineSeconds + StreamSeconds + RecoverySeconds + 15)
        {
            Events.Add("TIMEOUT frame=" + Time.frameCount + " paused=" + EditorApplication.isPaused +
                       " runInBackground=" + Application.runInBackground);
            FinishCapture("timeout");
            return;
        }

        // The editor may update more than once per game frame.
        if (Time.frameCount == lastSampleFrame)
            return;
        lastSampleFrame = Time.frameCount;
        string phase;
        if (elapsed < BaselineSeconds)
            phase = "baseline";
        else if (elapsed < BaselineSeconds + StreamSeconds)
            phase = "stream";
        else if (elapsed < BaselineSeconds + StreamSeconds + RecoverySeconds)
            phase = "recovery";
        else
        {
            FinishCapture("complete");
            return;
        }

        if (phase == "stream")
        {
            if (anchorObject == null && originalTarget != null && grid != null && blocks != null)
            {
                anchorObject = new GameObject("__FunGuyCPUProfileStreamAnchor");
                anchorObject.hideFlags = HideFlags.HideAndDontSave;
                anchorObject.transform.position = originalTarget.position;
                grid.SetStreamingTarget(anchorObject.transform);
                blocks.SetStreamingTarget(anchorObject.transform);
                Events.Add("STREAM_START z=" + anchorObject.transform.position.z.ToString("F1", CultureInfo.InvariantCulture));
            }

            if (anchorObject != null)
                anchorObject.transform.position += Vector3.forward * (StreamSpeed * Time.unscaledDeltaTime);
        }
        else if (phase == "recovery" && !resetIssued)
        {
            resetIssued = true;
            // Reset restores both spawners' original player target via RunFlowCoordinator.
            Events.Add("RESET_START z=" + (anchorObject != null ? anchorObject.transform.position.z.ToString("F1") : "none"));
            grid?.SetStreamingTarget(originalTarget);
            blocks?.SetStreamingTarget(originalTarget);
            if (anchorObject != null)
                UnityEngine.Object.Destroy(anchorObject);
            anchorObject = null;
            coordinator?.ResetRun();
            Events.Add("RESET_DONE");
        }

        var sample = new Sample
        {
            Frame = Time.frameCount,
            Phase = phase,
            Elapsed = elapsed,
            WallMs = Math.Max(0, Time.unscaledDeltaTime * 1000.0),
            PlayerZ = originalTarget != null ? originalTarget.position.z : float.NaN,
            AnchorZ = anchorObject != null ? anchorObject.transform.position.z : float.NaN,
            ActiveBlocks = blocks != null ? blocks.ActiveBlockCount : -1,
            PooledBlocks = blocks != null ? blocks.PooledBlockCount : -1
        };
        foreach (var c in Counters)
        {
            try
            {
                if (c.Recorder.Valid && c.Recorder.Count > 0)
                    sample.Counters[c.Key] = c.Recorder.GetSample(c.Recorder.Count - 1).Value;
            }
            catch (Exception ex)
            {
                Events.Add("counter error " + c.Key + ": " + ex.Message);
            }
        }
        Samples.Add(sample);
    }

    private static void FinishCapture(string reason)
    {
        if (!capturing && !File.Exists(ActiveFlag))
            return;

        capturing = false;
        Events.Add("FINISH " + reason + " " + DateTime.UtcNow.ToString("o"));
        try
        {
            if (anchorObject != null)
            {
                grid?.SetStreamingTarget(originalTarget);
                blocks?.SetStreamingTarget(originalTarget);
                UnityEngine.Object.Destroy(anchorObject);
                anchorObject = null;
            }

            if (savedPreviousProfiler)
                UProfiler.enableBinaryLog = false;

            if (!string.IsNullOrEmpty(capturePrefix))
            {
                WriteCsv();
                WriteTimeline();
                bool savedProfile = false;
                try { savedProfile = ProfilerDriver.SaveProfile(capturePrefix + ".data"); }
                catch (Exception ex) { Events.Add("SaveProfile error: " + ex.Message); }
                Events.Add("SaveProfile success=" + savedProfile);
                File.WriteAllLines(capturePrefix + ".events.txt", Events);
                Debug.Log("[FunGuy CPU Audit] Finished " + reason + " samples=" + Samples.Count +
                          " file=" + capturePrefix);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[FunGuy CPU Audit] Failed to save: " + ex);
        }
        finally
        {
            foreach (var c in Counters)
                c.Recorder.Dispose();
            Counters.Clear();
            if (savedPreviousProfiler)
            {
                UProfiler.enableBinaryLog = previousBinaryLog;
                UProfiler.enableAllocationCallstacks = previousCallstacks;
                UProfiler.enabled = previousEnabled;
                ProfilerDriver.profileEditor = previousProfileEditor;
                SetProfilerDriverEnabled(previousProfilerDriverEnabled);
                Application.runInBackground = previousRunInBackground;
                EditorApplication.isPaused = previousPaused;
            }
            savedPreviousProfiler = false;
            if (File.Exists(ActiveFlag)) File.Delete(ActiveFlag);
            if (enteredPlayForAudit && EditorApplication.isPlaying)
                EditorApplication.delayCall += () => { if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); };
            enteredPlayForAudit = false;
            startedAt = 0;
        }
    }

    private static void WriteCsv()
    {
        var names = Counters.Select(c => c.Key).ToList();
        var sb = new StringBuilder(1024 * 1024);
        sb.AppendLine("frame,phase,seconds,wall_frame_ms,player_z,stream_z,active_blocks,pooled_blocks," + string.Join(",", names.Select(CsvEscape)));
        foreach (var sample in Samples)
        {
            sb.Append(sample.Frame).Append(',').Append(sample.Phase).Append(',')
                .Append(sample.Elapsed.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.WallMs.ToString("F4", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.PlayerZ.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.AnchorZ.ToString("F2", CultureInfo.InvariantCulture)).Append(',')
                .Append(sample.ActiveBlocks).Append(',').Append(sample.PooledBlocks);
            foreach (var name in names)
            {
                sb.Append(',');
                if (sample.Counters.TryGetValue(name, out long value))
                    sb.Append(value);
            }
            sb.AppendLine();
        }
        File.WriteAllText(capturePrefix + ".frames.csv", sb.ToString());

        var countersInfo = new StringBuilder();
        foreach (var c in Counters)
            countersInfo.AppendLine(c.Key + " unit=" + c.Unit);
        File.WriteAllText(capturePrefix + ".counters.txt", countersInfo.ToString());
    }

    private static string CsvEscape(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";

    private static void SetProfilerDriverEnabled(bool enabled)
    {
        // Public getter, but Unity 6000.3 exposes the matching setter as internal.
        var method = typeof(ProfilerDriver).GetMethod("SetProfilingEnabled",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        if (method != null)
            method.Invoke(null, new object[] { enabled });
    }

    private static void WriteTimeline()
    {
        var slow = new List<(int frame, float ms)>();
        var perMarker = new Dictionary<string, (int calls, double ms, float maxMs)>(StringComparer.Ordinal);
        int first = ProfilerDriver.firstFrameIndex;
        int last = ProfilerDriver.lastFrameIndex;
        int validFrames = 0;
        int totalRawSamples = 0;
        var frameLines = new List<string> { "frame,frame_ms,samples" };

        if (first < 0 || last < first)
        {
            Events.Add("No ProfilerDriver frames available first=" + first + " last=" + last);
            return;
        }

        for (int frame = Math.Max(first, last - 220); frame <= last; frame++)
        {
            try
            {
                using (var view = ProfilerDriver.GetRawFrameDataView(frame, 0))
                {
                    if (view == null || !view.valid) continue;
                    float frameMs = view.frameTimeMs;
                    slow.Add((frame, frameMs));
                    validFrames++;
                    int count = Math.Min(view.sampleCount, 7000);
                    totalRawSamples += count;
                    frameLines.Add(frame + "," + frameMs.ToString("F4", CultureInfo.InvariantCulture) + "," + count);
                    for (int i = 0; i < count; i++)
                    {
                        float ms = view.GetSampleTimeMs(i);
                        if (ms < 0.005f) continue;
                        string name = view.GetSampleName(i);
                        if (string.IsNullOrEmpty(name)) continue;
                        var stat = perMarker.TryGetValue(name, out var v) ? v : (calls: 0, ms: 0d, maxMs: 0f);
                        perMarker[name] = (stat.calls + 1, stat.ms + ms, Mathf.Max(stat.maxMs, ms));
                    }
                }
            }
            catch (Exception ex)
            {
                Events.Add("RawFrameDataView frame " + frame + " error " + ex.Message);
            }
        }
        File.WriteAllLines(capturePrefix + ".profiler-frames.csv", frameLines);

        var markerLines = new List<string> { "marker,calls,inclusive_total_ms,max_sample_ms" };
        foreach (var kv in perMarker.OrderByDescending(k => k.Value.ms).Take(400))
            markerLines.Add(CsvEscape(kv.Key) + "," + kv.Value.calls + "," +
                            kv.Value.ms.ToString("F4", CultureInfo.InvariantCulture) + "," +
                            kv.Value.maxMs.ToString("F4", CultureInfo.InvariantCulture));
        File.WriteAllLines(capturePrefix + ".markers.csv", markerLines);

        var topFrames = new List<string> { "frame,frame_ms,marker,inclusive_ms" };
        foreach (var frame in slow.OrderByDescending(f => f.ms).Take(18))
        {
            using (var view = ProfilerDriver.GetRawFrameDataView(frame.frame, 0))
            {
                if (!view.valid) continue;
                var frameMarkers = new List<(string name, float ms)>();
                for (int i = 0; i < Math.Min(view.sampleCount, 7000); i++)
                {
                    float ms = view.GetSampleTimeMs(i);
                    if (ms >= 0.08f)
                        frameMarkers.Add((view.GetSampleName(i), ms));
                }
                foreach (var mark in frameMarkers.OrderByDescending(f => f.ms).Take(22))
                    topFrames.Add(frame.frame + "," + frame.ms.ToString("F4", CultureInfo.InvariantCulture) +
                                  "," + CsvEscape(mark.name) + "," + mark.ms.ToString("F4", CultureInfo.InvariantCulture));
            }
        }
        File.WriteAllLines(capturePrefix + ".slow-frame-markers.csv", topFrames);
        Events.Add("ProfilerDriver frames " + first + ".." + last +
                   " valid=" + validFrames + " raw_samples=" + totalRawSamples);
    }
}
