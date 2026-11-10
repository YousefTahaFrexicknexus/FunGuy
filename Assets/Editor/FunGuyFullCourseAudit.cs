using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-only full-course CPU/pooling stress test. Never builds players, never saves scenes,
/// never changes graphics settings. The player and camera stay in place; a temporary
/// invisible target exercises streaming and is destroyed after profiling.
/// </summary>
[InitializeOnLoad]
internal static class FunGuyFullCourseAudit
{
    const float TravelSpeed = 112f;
    const int PassCount = 2;
    const double StartupDelay = 2.5;
    const double WarmupSeconds = 1.5;
    const double CooldownSeconds = 2.0;

    static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static readonly string Dir = Path.Combine(Root, "work", "windows-profiling-2026-11-09", "cpu-profile");
    static readonly string Request = Path.Combine(Dir, "full-course-audit.request");
    static readonly string Active = Path.Combine(Dir, "full-course-audit.active");
    static readonly string[] Markers =
    {
        "Other|GPUResidentDrawer",
        "Render|GPUDrivenProcessor.EnableGPUDrivenRenderingAndDispatchRendererData",
        "Loading|Loading.ReadObject",
        "Loading|AsyncReadManager.SyncRequest",
        "Loading|Instantiate",
        "Render|CPU Main Thread Frame Time",
        "Render|CPU Render Thread Frame Time",
        "Internal|Main Thread",
        "Memory|GC Allocated In Frame",
        "Memory|GC Used Memory",
        "Render|Draw Calls Count",
        "Render|Batches Count",
        "Render|SetPass Calls Count",
        "Physics|Physics.Simulate",
        "Scripts|FunGuy.Grid.GenerateNextSlice",
        "Scripts|FunGuy.Grid.InstantiatePrefab",
        "Scripts|FunGuy.Grid.ActivatePrefab",
        "Scripts|FunGuy.BlockSpawner.InstantiatePrefab",
        "Scripts|FunGuy.BlockSpawner.ReactivatePrefab"
    };

    class CapturedCounter
    {
        public string Key;
        public ProfilerRecorder Recorder;
        public string Unit;
    }

    static readonly List<CapturedCounter> counters = new();
    static readonly List<string> log = new();
    static readonly List<string> lines = new();
    static double nextPoll, enteredAt, passStart;
    static string prefix;
    static int pass = -1, lastFrame;
    static bool inProgress, originalRunInBackground, originalPause;
    static Transform player;
    static GameObject cursor;
    static BlockSpawner blocks;
    static GridCourseStreamer grid;
    static RunFlowCoordinator flow;
    static float endZ;

    static FunGuyFullCourseAudit()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += PlayStateChanged;
    }

    static void Update()
    {
        try
        {
            if (!EditorApplication.isPlaying && !inProgress && EditorApplication.timeSinceStartup >= nextPoll)
            {
                nextPoll = EditorApplication.timeSinceStartup + 2;
                if (File.Exists(Request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    if (SceneManager.GetActiveScene().isDirty)
                    {
                        Debug.LogWarning("[FunGuy Full Course] Scene has unsaved edits; refusing automated Play Mode.");
                        return;
                    }

                    Directory.CreateDirectory(Dir);
                    File.Delete(Request);
                    File.WriteAllText(Active, "no-build-full-course-audit");
                    EditorApplication.EnterPlaymode();
                }
            }

            if (!EditorApplication.isPlaying || !File.Exists(Active))
                return;
            if (!inProgress)
            {
                if (enteredAt == 0) enteredAt = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - enteredAt >= StartupDelay)
                    Begin();
                return;
            }

            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            double elapsed = EditorApplication.timeSinceStartup - passStart;
            float z = (float)Math.Min(endZ, elapsed > WarmupSeconds
                ? (elapsed - WarmupSeconds) * TravelSpeed : 0);
            if (cursor != null)
            {
                Vector3 position = cursor.transform.position;
                position.z = player.position.z + z;
                cursor.transform.position = position;
            }

            Capture(elapsed, z);

            double travelSeconds = endZ / TravelSpeed + WarmupSeconds;
            if (elapsed >= travelSeconds + CooldownSeconds)
            {
                log.Add($"PASS_END {pass} elapsed={elapsed:F3} z={z:F1} blockCreated={blocks.TotalInstancesCreated} blockReused={blocks.TotalPoolHits} blockDestroyed={blocks.TotalInstancesDestroyed} blockPool={blocks.PooledBlockCount} gridCreated={grid.TotalInstancesCreated} gridReused={grid.TotalPoolHits} gridDestroyed={grid.TotalInstancesDestroyed} gridPool={grid.PooledInstanceCount} nativeAllocated={Profiler.GetTotalAllocatedMemoryLong()} nativeReserved={Profiler.GetTotalReservedMemoryLong()} monoUsed={Profiler.GetMonoUsedSizeLong()}");
                EndPass();
                if (++pass >= PassCount)
                    Finish("completed");
                else
                    BeginPass();
            }
        }
        catch (Exception ex)
        {
            log.Add("EXCEPTION " + ex);
            Debug.LogError("[FunGuy Full Course] " + ex);
            Finish("exception");
        }
    }

    static void PlayStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Active))
            enteredAt = EditorApplication.timeSinceStartup;
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (inProgress) Finish("play-mode-exited");
            enteredAt = 0;
        }
    }

    static void Begin()
    {
        blocks = UnityEngine.Object.FindFirstObjectByType<BlockSpawner>();
        grid = UnityEngine.Object.FindFirstObjectByType<GridCourseStreamer>();
        flow = UnityEngine.Object.FindFirstObjectByType<RunFlowCoordinator>();
        player = UnityEngine.Object.FindFirstObjectByType<MushroomRunnerPlayer>()?.transform;
        if (blocks == null || grid == null || flow == null || player == null)
            throw new InvalidOperationException("Missing required gameplay references");

        endZ = Mathf.Min(blocks.MaximumDistance, grid.GenerationEndZ);
        if (endZ <= 0f || float.IsInfinity(endZ))
            throw new InvalidOperationException("The course needs a finite maximum Z");

        if (UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset != null)
        {
            float renderScale = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset.renderScale;
            if (Mathf.Abs(renderScale - 1f) > 0.0001f)
                throw new InvalidOperationException("Render scale must remain exactly 1.0; found " + renderScale);
        }

        originalRunInBackground = Application.runInBackground;
        originalPause = EditorApplication.isPaused;
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        inProgress = true;
        prefix = Path.Combine(Dir, "full-course-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        log.Clear();
        lines.Clear();
        log.Add("EffectiveBlockPoolCapacity=" + blocks.EffectivePoolCapacity);
        log.Add("Scene=" + SceneManager.GetActiveScene().name +
            " courseEndZ=" + endZ + " travelSpeed=" + TravelSpeed +
            " runs=" + PassCount + " Unity=" + Application.unityVersion);
        log.Add("BlockPreload=" + blocks.PreloadedFuturePrefabCount +
                " meshes=" + blocks.PreloadedFutureMeshCount +
                " preloadMs=" + blocks.PreloadDurationMilliseconds.ToString("F2",CultureInfo.InvariantCulture));
        log.Add("GridPreload=" + grid.PreloadedGridPrefabCount +
                " meshes=" + grid.PreloadedGridMeshCount +
                " preloadMs=" + grid.GridPreloadDurationMilliseconds.ToString("F2",CultureInfo.InvariantCulture));
        ConfigureCounters();
        lines.Add("pass,frame,seconds,z,frame_ms,active_blocks,pooled_blocks,block_created,block_reused,block_destroyed,active_slices,grid_cached,grid_pooled,grid_created,grid_reused,grid_destroyed,managed_heap_bytes," +
            string.Join(",", counters.Select(c => c.Key)));
        pass = 0;
        BeginPass();
        Debug.Log("[FunGuy Full Course] Profiling full course from Z=0 to " + endZ + " twice.");
    }

    static void ConfigureCounters()
    {
        foreach (var c in counters) c.Recorder.Dispose();
        counters.Clear();
        var wanted = new HashSet<string>(Markers);
        var available = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(available);
        foreach (var handle in available)
        {
            var desc = ProfilerRecorderHandle.GetDescription(handle);
            string name = desc.Category.Name + "|" + desc.Name;
            if (!wanted.Remove(name)) continue;
            var rec = ProfilerRecorder.StartNew(desc.Category, desc.Name, 1);
            if (rec.Valid)
                counters.Add(new CapturedCounter { Key = name, Recorder = rec, Unit = desc.UnitType.ToString() });
            else rec.Dispose();
        }

        // The Script markers may have been registered after this lookup. Bind them
        // by their exact category/name as well, if not visible in the handle table yet.
        foreach (string name in Markers.Where(k => k.StartsWith("Scripts|", StringComparison.Ordinal)))
        {
            if (counters.Exists(c => c.Key == name)) continue;
            var rec = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, name.Substring(8), 1);
            if (rec.Valid)
                counters.Add(new CapturedCounter { Key = name, Recorder = rec, Unit = "TimeNanoseconds" });
            else rec.Dispose();
        }

        log.Add("CapturedCounters=" + counters.Count);
        log.Add("MissingCounters=" + string.Join(";", Markers.Where(w => counters.All(c => c.Key != w))));
    }

    static void BeginPass()
    {
        // Reset run flow to a known start for each pass, then drive just the
        // streaming cursor while the player/camera remain in their authored pose.
        flow.ResetRun();
        cursor = new GameObject("__FunGuyFullCourseAuditCursor");
        cursor.hideFlags = HideFlags.HideAndDontSave;
        cursor.transform.position = player.position;
        blocks.SetStreamingTarget(cursor.transform);
        grid.SetStreamingTarget(cursor.transform);
        passStart = EditorApplication.timeSinceStartup;
        lastFrame = -1;
        log.Add("PASS_START " + pass + " UTC=" + DateTime.UtcNow.ToString("o") +
                " blocksCreated=" + blocks.TotalInstancesCreated +
                " gridCreated=" + grid.TotalInstancesCreated +
                " blockReused=" + blocks.TotalPoolHits +
                " gridReused=" + grid.TotalPoolHits);
    }

    static void EndPass()
    {
        blocks?.SetStreamingTarget(player);
        grid?.SetStreamingTarget(player);
        if (cursor != null) UnityEngine.Object.Destroy(cursor);
        cursor = null;
    }

    static void Capture(double elapsed, float z)
    {
        var col = new List<string>(counters.Count + 18)
        {
            pass.ToString(), Time.frameCount.ToString(),
            elapsed.ToString("F4", CultureInfo.InvariantCulture),
            z.ToString("F2", CultureInfo.InvariantCulture),
            (Time.unscaledDeltaTime * 1000f).ToString("F4", CultureInfo.InvariantCulture),
            blocks.ActiveBlockCount.ToString(), blocks.PooledBlockCount.ToString(),
            blocks.TotalInstancesCreated.ToString(), blocks.TotalPoolHits.ToString(),
            blocks.TotalInstancesDestroyed.ToString(), grid.ActiveSliceCount.ToString(),
            grid.CachedInstanceCount.ToString(), grid.PooledInstanceCount.ToString(),
            grid.TotalInstancesCreated.ToString(), grid.TotalPoolHits.ToString(),
            grid.TotalInstancesDestroyed.ToString(),
            (Time.frameCount % 20 == 0 ? GC.GetTotalMemory(false) : 0).ToString()
        };
        foreach (var c in counters)
        {
            ProfilerRecorder recorder = c.Recorder;
            long value = recorder.Valid && recorder.Count > 0
                ? recorder.GetSample(recorder.Count - 1).Value : 0;
            col.Add(value.ToString(CultureInfo.InvariantCulture));
        }
        lines.Add(string.Join(",", col));
    }

    static void Finish(string reason)
    {
        if (!inProgress && !File.Exists(Active)) return;
        bool shouldExit = EditorApplication.isPlaying && File.Exists(Active);
        try
        {
            EndPass();
            if (!string.IsNullOrEmpty(prefix))
            {
                log.Add("FINISH=" + reason + " totalSamples=" + Math.Max(0,lines.Count-1));
                foreach (var c in counters) log.Add("COUNTER " + c.Key + " unit=" + c.Unit);
                File.WriteAllLines(prefix + ".csv", lines);
                File.WriteAllLines(prefix + ".log.txt", log);
            }
        }
        catch (Exception ex) { Debug.LogError("[FunGuy Full Course] Failed writing capture: " + ex); }
        finally
        {
            foreach (var c in counters) c.Recorder.Dispose();
            counters.Clear();
            if (inProgress)
            {
                Application.runInBackground = originalRunInBackground;
                EditorApplication.isPaused = originalPause;
            }
            inProgress = false;
            pass = -1;
            enteredAt = 0;
            if (File.Exists(Active)) File.Delete(Active);
            if (shouldExit)
                EditorApplication.delayCall += () =>
                {
                    if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                };
            Debug.Log("[FunGuy Full Course] Finished " + reason);
        }
    }
}
