using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// No-build, one-shot Editor Play Mode experiment: unmodified GPU Resident Drawer,
/// explicit prefab render-asset preload, GPU Resident Drawer OFF, back ON.
/// The active scene, player, and persistent URP asset are not modified.
/// Request by creating work/windows-profiling-2026-11-09/cpu-profile/compare-renderers.request.
/// </summary>
[InitializeOnLoad]
internal static class FunGuyRendererExperiment
{
    private const float TravelSpeed = 28f;
    private const double PhaseWarmup = 1.5;
    private const double PhaseTravel = 12.0;
    private const int PhaseCount = 4;

    private static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static readonly string Dir = Path.Combine(Root, "work/windows-profiling-2026-11-09/cpu-profile");
    private static readonly string Request = Path.Combine(Dir, "compare-renderers.request");
    private static readonly string Active = Path.Combine(Dir, "compare-renderers.active");
    private static readonly string[] PhaseNames = { "ON_cold", "ON_preloaded", "OFF_preloaded", "ON_repeat" };

    private static readonly string[] WantedCounters =
    {
        "Internal|Main Thread", "Memory|GC Allocated In Frame",
        "Other|GPUResidentDrawer", "Render|GPUDrivenProcessor.EnableGPUDrivenRenderingAndDispatchRendererData",
        "Loading|Loading.ReadObject", "Loading|AsyncReadManager.SyncRequest",
        "Loading|Instantiate", "Render|CPU Main Thread Frame Time",
        "Render|CPU Render Thread Frame Time", "Render|Batches Count",
        "Render|Draw Calls Count", "Render|SetPass Calls Count",
        "Physics|Physics.Simulate", "PlayerLoop|BehaviourUpdate"
    };

    private sealed class Counter
    {
        public string Name;
        public string Unit;
        public ProfilerRecorder Recorder;
    }
    private sealed class Row
    {
        public string Phase;
        public int Frame;
        public double Elapsed;
        public double WallMs;
        public float StreamZ;
        public int ActiveBlocks, PooledBlocks;
        public long[] Values;
    }

    private static readonly List<Counter> Counters = new();
    private static readonly List<Row> Data = new();
    private static readonly List<string> Logs = new();

    private static double nextPoll, startTime, phaseStart;
    private static int phaseIndex = -1, lastFrame = -1;
    private static bool capturing, originalRunInBackground, originalPause;
    private static Transform player;
    private static BlockSpawner blocks;
    private static GridCourseStreamer grid;
    private static RunFlowCoordinator flow;
    private static GameObject cursor;
    private static RenderPipelineAsset originalQualityPipeline;
    private static UniversalRenderPipelineAsset temporaryOffAsset;
    private static string prefix;

    static FunGuyRendererExperiment()
    {
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private static void Update()
    {
        try
        {
            if (!capturing && !EditorApplication.isPlaying &&
                EditorApplication.timeSinceStartup >= nextPoll)
            {
                nextPoll = EditorApplication.timeSinceStartup + 2;
                if (File.Exists(Request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    if (SceneManager.GetActiveScene().isDirty)
                    {
                        Debug.LogWarning("[FunGuy Renderer Experiment] Scene has unsaved changes; refusing automatic Play Mode.");
                        return;
                    }
                    Directory.CreateDirectory(Dir);
                    File.Delete(Request);
                    File.WriteAllText(Active, "one-shot");
                    EditorApplication.EnterPlaymode();
                }
            }

            if (!EditorApplication.isPlaying || !File.Exists(Active))
                return;

            if (phaseIndex == -1)
            {
                if (startTime == 0) startTime = EditorApplication.timeSinceStartup;
                if (EditorApplication.timeSinceStartup - startTime > 2.5)
                    Begin();
                return;
            }

            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            double seconds = EditorApplication.timeSinceStartup - phaseStart;
            if (seconds >= PhaseWarmup + PhaseTravel)
            {
                EndPhase();
                if (++phaseIndex >= PhaseCount)
                {
                    Finish("completed");
                    return;
                }
                StartPhase();
                return;
            }

            if (cursor == null) return;
            if (seconds >= PhaseWarmup)
                cursor.transform.position += Vector3.forward * (TravelSpeed * Mathf.Min(Time.unscaledDeltaTime, 0.05f));

            // Only capture the same 12-second stream interval in each phase.
            if (seconds < PhaseWarmup) return;
            var values = new long[Counters.Count];
            for (int i = 0; i < Counters.Count; i++)
            {
                var rec = Counters[i].Recorder;
                if (rec.Valid && rec.Count > 0)
                    values[i] = rec.GetSample(rec.Count - 1).Value;
            }

            Data.Add(new Row
            {
                Phase = PhaseNames[phaseIndex],
                Frame = Time.frameCount,
                Elapsed = seconds - PhaseWarmup,
                WallMs = Time.unscaledDeltaTime * 1000.0,
                StreamZ = cursor.transform.position.z,
                ActiveBlocks = blocks.ActiveBlockCount,
                PooledBlocks = blocks.PooledBlockCount,
                Values = values
            });
        }
        catch (Exception ex)
        {
            Logs.Add("EXCEPTION " + ex);
            Debug.LogError("[FunGuy Renderer Experiment] " + ex);
            Finish("exception");
        }
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Active))
            startTime = EditorApplication.timeSinceStartup;
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (capturing) Finish("exiting-play");
            startTime = 0;
            phaseIndex = -1;
        }
    }

    private static void Begin()
    {
        player = UnityEngine.Object.FindFirstObjectByType<MushroomRunnerPlayer>()?.transform;
        blocks = UnityEngine.Object.FindFirstObjectByType<BlockSpawner>();
        grid = UnityEngine.Object.FindFirstObjectByType<GridCourseStreamer>();
        flow = UnityEngine.Object.FindFirstObjectByType<RunFlowCoordinator>();

        if (player == null || blocks == null || grid == null || flow == null)
            throw new InvalidOperationException("Missing player, environment, grid or coordinator.");

        originalQualityPipeline = QualitySettings.renderPipeline;
        var originalUrp = (QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline :
            GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
        if (originalUrp == null)
            throw new InvalidOperationException("Active quality tier is not URP.");

        if (originalUrp.gpuResidentDrawerMode == GPUResidentDrawerMode.Disabled)
            throw new InvalidOperationException("Expected starting renderer mode ON; found Disabled.");

        temporaryOffAsset = UnityEngine.Object.Instantiate(originalUrp);
        temporaryOffAsset.name = "__FunGuyTemporaryGPUResidentDrawerOff";
        temporaryOffAsset.hideFlags = HideFlags.HideAndDontSave;
        temporaryOffAsset.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;

        originalRunInBackground = Application.runInBackground;
        originalPause = EditorApplication.isPaused;
        Application.runInBackground = true;
        EditorApplication.isPaused = false;
        capturing = true;
        prefix = Path.Combine(Dir, "renderer-ab-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        Logs.Clear();
        Data.Clear();
        Logs.Add("quality=" + QualitySettings.GetQualityLevel() +
                 " originalURP=" + originalUrp.name +
                 " renderScale=" + originalUrp.renderScale.ToString(CultureInfo.InvariantCulture) +
                 " drawer=" + originalUrp.gpuResidentDrawerMode);
        Logs.Add("originalQualityPipeline=" + (originalQualityPipeline != null ? originalQualityPipeline.name : "(null)"));
        Logs.Add("scene=" + SceneManager.GetActiveScene().name);
        Logs.Add("runtimePreload prefabs=" + blocks.PreloadedFuturePrefabCount +
                 " materials=" + blocks.PreloadedFutureMaterialCount +
                 " meshes=" + blocks.PreloadedFutureMeshCount +
                 " elapsedMs=" + blocks.PreloadDurationMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        Logs.Add("gridPreload prefabs=" + grid.PreloadedGridPrefabCount +
                 " materials=" + grid.PreloadedGridMaterialCount +
                 " meshes=" + grid.PreloadedGridMeshCount +
                 " elapsedMs=" + grid.GridPreloadDurationMilliseconds.ToString("F2", CultureInfo.InvariantCulture));
        SetupCounters();
        phaseIndex = 0;
        StartPhase();
        Debug.Log("[FunGuy Renderer Experiment] Started 4 phases: ON cold, ON preloaded, OFF preloaded, ON repeat.");
    }

    private static void SetupCounters()
    {
        foreach (var c in Counters) c.Recorder.Dispose();
        Counters.Clear();
        var handles = new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        var names = new HashSet<string>(WantedCounters);
        foreach (var h in handles)
        {
            var desc = ProfilerRecorderHandle.GetDescription(h);
            string name = desc.Category.Name + "|" + desc.Name;
            if (!names.Remove(name)) continue;
            var rec = ProfilerRecorder.StartNew(desc.Category, desc.Name, 1);
            if (rec.Valid)
                Counters.Add(new Counter { Name = name, Recorder = rec, Unit = desc.UnitType.ToString() });
            else rec.Dispose();
        }
        Logs.Add("missingCounters=" + string.Join(";", names));
    }

    private static void StartPhase()
    {
        if (phaseIndex == 1)
            PreloadReferencedAssets();
        if (phaseIndex == 2)
        {
            QualitySettings.renderPipeline = temporaryOffAsset;
            Logs.Add("Temporary drawer OFF pipeline assigned");
        }
        if (phaseIndex == 3)
        {
            QualitySettings.renderPipeline = originalQualityPipeline;
            Logs.Add("Original pipeline restored for ON-repeat");
        }

        flow.ResetRun();
        cursor = new GameObject("__FunGuyRendererABStreamAnchor");
        cursor.hideFlags = HideFlags.HideAndDontSave;
        cursor.transform.position = player.position;
        blocks.SetStreamingTarget(cursor.transform);
        grid.SetStreamingTarget(cursor.transform);
        phaseStart = EditorApplication.timeSinceStartup;
        lastFrame = -1;
        Logs.Add("START " + PhaseNames[phaseIndex] + " UTC " + DateTime.UtcNow.ToString("o") +
                 " z=" + cursor.transform.position.z.ToString("F2", CultureInfo.InvariantCulture));
    }

    private static void EndPhase()
    {
        Logs.Add("END " + PhaseNames[phaseIndex] +
                 " z=" + (cursor != null ? cursor.transform.position.z.ToString("F2", CultureInfo.InvariantCulture) : "?") +
                 " active=" + blocks.ActiveBlockCount + " pooled=" + blocks.PooledBlockCount);
        blocks.SetStreamingTarget(player);
        grid.SetStreamingTarget(player);
        if (cursor != null) UnityEngine.Object.Destroy(cursor);
        cursor = null;
    }

    private static void PreloadReferencedAssets()
    {
        var serialized = new SerializedObject(blocks);
        var entries = serialized.FindProperty("blockSequence");
        var prefabs = new HashSet<GameObject>();
        var meshes = new HashSet<Mesh>();
        var materials = new HashSet<Material>();
        int renderers = 0, colliders = 0;
        double before = EditorApplication.timeSinceStartup;
        for (int i = 0; i < entries.arraySize; i++)
        {
            var data = entries.GetArrayElementAtIndex(i).objectReferenceValue as BlockData;
            if (data == null || data.prefab == null || !prefabs.Add(data.prefab)) continue;
            foreach (var renderer in data.prefab.GetComponentsInChildren<Renderer>(true))
            {
                renderers++;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || !materials.Add(material)) continue;
                    // Touch shader and material metadata, not renderer.material (which would clone).
                    _ = material.shader;
                }
                if (renderer is SkinnedMeshRenderer skin && skin.sharedMesh != null)
                    meshes.Add(skin.sharedMesh);
            }
            foreach (var filter in data.prefab.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null) meshes.Add(filter.sharedMesh);
            foreach (var collider in data.prefab.GetComponentsInChildren<MeshCollider>(true))
            {
                colliders++;
                if (collider.sharedMesh != null) meshes.Add(collider.sharedMesh);
            }
        }
        long triangles = 0;
        foreach (var mesh in meshes)
            triangles += mesh.vertexCount; // Read mesh metadata, never mesh.vertices.
        Logs.Add("PRELOAD distinctPrefabs=" + prefabs.Count + " renderers=" + renderers +
                 " materials=" + materials.Count + " meshes=" + meshes.Count +
                 " meshVertices=" + triangles + " meshColliders=" + colliders +
                 " elapsedMs=" + ((EditorApplication.timeSinceStartup - before) * 1000).ToString("F2", CultureInfo.InvariantCulture));
    }

    private static void Finish(string reason)
    {
        if (!capturing && !File.Exists(Active)) return;
        capturing = false;
        try
        {
            if (cursor != null)
            {
                blocks?.SetStreamingTarget(player);
                grid?.SetStreamingTarget(player);
                UnityEngine.Object.Destroy(cursor);
                cursor = null;
            }
            if (QualitySettings.renderPipeline != originalQualityPipeline)
                QualitySettings.renderPipeline = originalQualityPipeline;

            if (!string.IsNullOrEmpty(prefix))
            {
                var lines = new List<string>();
                var headers = new List<string> { "phase", "frame", "elapsed_s", "wall_ms", "stream_z", "active_blocks", "pooled_blocks" };
                headers.AddRange(Counters.Select(c => c.Name));
                lines.Add(string.Join(",", headers.Select(Escape)));
                foreach (var row in Data)
                {
                    var values = new List<string>
                    {
                        row.Phase,row.Frame.ToString(),row.Elapsed.ToString("F4",CultureInfo.InvariantCulture),
                        row.WallMs.ToString("F4",CultureInfo.InvariantCulture),
                        row.StreamZ.ToString("F3",CultureInfo.InvariantCulture),
                        row.ActiveBlocks.ToString(),row.PooledBlocks.ToString()
                    };
                    values.AddRange(row.Values.Select(v => v.ToString(CultureInfo.InvariantCulture)));
                    lines.Add(string.Join(",",values.Select(Escape)));
                }
                File.WriteAllLines(prefix + ".csv", lines);
                Logs.Add("FINISH " + reason + " rows=" + Data.Count);
                Logs.AddRange(Counters.Select(c => "COUNTER " + c.Name + " unit=" + c.Unit));
                File.WriteAllLines(prefix + ".log.txt", Logs);
            }
        }
        catch (Exception ex) { Debug.LogError("[FunGuy Renderer Experiment] Save failed: " + ex); }
        finally
        {
            foreach (var c in Counters) c.Recorder.Dispose();
            Counters.Clear();
            if (QualitySettings.renderPipeline != originalQualityPipeline)
                QualitySettings.renderPipeline = originalQualityPipeline;
            if (temporaryOffAsset != null) UnityEngine.Object.DestroyImmediate(temporaryOffAsset);
            temporaryOffAsset = null;
            if (capturing || phaseIndex >= 0)
            {
                Application.runInBackground = originalRunInBackground;
                EditorApplication.isPaused = originalPause;
            }
            phaseIndex = -1;
            startTime = 0;
            if (File.Exists(Active)) File.Delete(Active);
            if (EditorApplication.isPlaying)
                EditorApplication.delayCall += () => { if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode(); };
            Debug.Log("[FunGuy Renderer Experiment] Finished " + reason + " samples=" + Data.Count);
        }
    }

    private static string Escape(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
