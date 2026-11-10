using System;
using System.IO;
using Unity.Profiling.Memory;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

/// <summary>
/// One-shot Unity Memory Profiler captures for the Windows performance audit.
/// Can perform an explicitly requested, one-shot Play Mode capture without making a player build.
/// </summary>
[InitializeOnLoad]
internal static class FunGuyMemoryAuditCapture
{
    private static readonly string ProjectRoot =
        Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    private static readonly string CaptureDirectory = Path.Combine(ProjectRoot, "MemoryCaptures");
    private static readonly string RequestDirectory = Path.Combine(ProjectRoot, "work", "windows-profiling-2026-11-09");
    private static readonly string EditorRequest = Path.Combine(RequestDirectory, "capture-editor-after-fixes.request");
    private static readonly string PlayRequest = Path.Combine(RequestDirectory, "capture-play-after-fixes.request");
    private static readonly string AutoPlayRequest = Path.Combine(RequestDirectory, "enter-play-for-memory-audit.request");
    private static readonly string AutoPlayActive = Path.Combine(RequestDirectory, "memory-audit-autoplay-active.flag");

    private static double playCaptureAfter;
    private static double nextRequestPoll;
    private static bool snapshotInProgress;

    static FunGuyMemoryAuditCapture()
    {
        EditorApplication.delayCall += CaptureEditorIfRequested;
        EditorApplication.delayCall += SchedulePlayCaptureIfAlreadyPlaying;
        EditorApplication.delayCall += EnterPlayForAuditIfRequested;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update += PollCaptureRequests;
    }

    private static void PollCaptureRequests()
    {
        if (EditorApplication.timeSinceStartup < nextRequestPoll)
            return;

        nextRequestPoll = EditorApplication.timeSinceStartup + 2.0;
        if (snapshotInProgress)
            return;

        if (File.Exists(EditorRequest))
            CaptureEditorIfRequested();
        else if (File.Exists(AutoPlayRequest))
            EnterPlayForAuditIfRequested();
    }

    private static void EnterPlayForAuditIfRequested()
    {
        if (!File.Exists(AutoPlayRequest))
            return;
        if (snapshotInProgress || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnterPlayForAuditIfRequested;
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (SceneManager.GetActiveScene().isDirty)
        {
            Debug.LogWarning("[FunGuy Memory Audit] Auto-play skipped: the current scene has unsaved edits. " +
                             "Save or discard them before running the one-shot capture.");
            return;
        }

        File.Delete(AutoPlayRequest);
        File.WriteAllText(AutoPlayActive, DateTime.UtcNow.ToString("o"));
        EditorApplication.EnterPlaymode();
    }

    [MenuItem("Tools/FunGuy/Memory Audit/Capture Snapshot Now")]
    private static void CaptureSnapshotFromMenu()
    {
        Capture(EditorApplication.isPlaying ? "play-manual" : "editor-manual");
    }

    private static void CaptureEditorIfRequested()
    {
        if (!File.Exists(EditorRequest))
            return;

        if (snapshotInProgress || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CaptureEditorIfRequested;
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        File.Delete(EditorRequest);
        Capture("editor-after-fixes");
    }

    private static void SchedulePlayCaptureIfAlreadyPlaying()
    {
        if (EditorApplication.isPlaying)
            SchedulePlayCapture();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
            SchedulePlayCapture();
        else if (state == PlayModeStateChange.ExitingPlayMode)
            EditorApplication.update -= CapturePlayIfRequested;
    }

    private static void SchedulePlayCapture()
    {
        if (!File.Exists(PlayRequest))
            return;

        playCaptureAfter = EditorApplication.timeSinceStartup + 5.0;
        EditorApplication.update -= CapturePlayIfRequested;
        EditorApplication.update += CapturePlayIfRequested;
    }

    private static void CapturePlayIfRequested()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < playCaptureAfter)
            return;

        EditorApplication.update -= CapturePlayIfRequested;
        if (!File.Exists(PlayRequest))
            return;

        File.Delete(PlayRequest);
        Capture("play-after-fixes");
    }

    private static void Capture(string label)
    {
        if (snapshotInProgress)
            return;
        snapshotInProgress = true;
        Directory.CreateDirectory(CaptureDirectory);
        string fullPath = Path.Combine(
            CaptureDirectory,
            $"funguy-{label}-{DateTime.Now:yyyyMMdd-HHmmss}.snap");

        int activeBlocks = 0;
        int pooledBlocks = 0;
        if (EditorApplication.isPlaying)
        {
            foreach (BlockSpawner spawner in UnityEngine.Object.FindObjectsByType<BlockSpawner>(FindObjectsSortMode.None))
            {
                activeBlocks += spawner.ActiveBlockCount;
                pooledBlocks += spawner.PooledBlockCount;
            }
        }

        // Record equivalent Editor runtime counters alongside the detailed .snap file.
        var counters = new MemoryCounters
        {
            label = label,
            timeUtc = DateTime.UtcNow.ToString("o"),
            scene = SceneManager.GetActiveScene().name,
            editorPlayMode = EditorApplication.isPlaying,
            totalAllocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
            totalReservedBytes = Profiler.GetTotalReservedMemoryLong(),
            totalUnusedReservedBytes = Profiler.GetTotalUnusedReservedMemoryLong(),
            monoUsedBytes = Profiler.GetMonoUsedSizeLong(),
            monoHeapBytes = Profiler.GetMonoHeapSizeLong(),
            activeEnvironmentBlocks = activeBlocks,
            pooledEnvironmentBlocks = pooledBlocks
        };
        File.WriteAllText(fullPath + ".metrics.json", JsonUtility.ToJson(counters, true));

        MemoryProfiler.TakeSnapshot(fullPath, (path, succeeded) =>
        {
            snapshotInProgress = false;
            if (succeeded)
                Debug.Log($"[FunGuy Memory Audit] Snapshot saved: {path}");
            else
                Debug.LogError($"[FunGuy Memory Audit] Snapshot failed: {path}");

            if (label == "play-after-fixes" && File.Exists(AutoPlayActive))
            {
                File.Delete(AutoPlayActive);
                EditorApplication.delayCall += () =>
                {
                    if (EditorApplication.isPlaying)
                        EditorApplication.ExitPlaymode();
                };
            }
        }, CaptureFlags.ManagedObjects | CaptureFlags.NativeObjects | CaptureFlags.NativeAllocations);
    }

    [Serializable]
    private class MemoryCounters
    {
        public string label;
        public string timeUtc;
        public string scene;
        public bool editorPlayMode;
        public long totalAllocatedBytes;
        public long totalReservedBytes;
        public long totalUnusedReservedBytes;
        public long monoUsedBytes;
        public long monoHeapBytes;
        public int activeEnvironmentBlocks;
        public int pooledEnvironmentBlocks;
    }
}
