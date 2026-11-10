using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-shot staging QA in the Editor. No player builds and no scene saves.</summary>
[InitializeOnLoad]
internal static class FunGuyStartupWarmupAudit
{
    static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static readonly string Dir = Path.Combine(Root, "work", "windows-profiling-2026-11-09", "startup-prewarm");
    static readonly string Request = Path.Combine(Dir, "test-startup.request");
    static readonly string Active = Path.Combine(Dir, "test-startup.active");
    static readonly string Live = Path.Combine(Dir, "test-startup.live.txt");
    static readonly string Final = Path.Combine(Dir, "test-startup.final.txt");

    static double nextCheck, entered, lastSample, readyAt;
    static bool sawWarming, sawDisabledDuringWarmup, sawOverlay, didScreenshot;
    static readonly List<string> samples = new();

    static FunGuyStartupWarmupAudit()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayState;
    }

    static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Active))
        {
            entered = EditorApplication.timeSinceStartup;
            lastSample = 0;
            readyAt = 0;
            samples.Clear();
            sawWarming = sawDisabledDuringWarmup = sawOverlay = didScreenshot = false;
        }
    }

    static void Tick()
    {
        try
        {
            if (!EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= nextCheck)
            {
                nextCheck = EditorApplication.timeSinceStartup + 2;
                if (File.Exists(Request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    if (SceneManager.GetActiveScene().isDirty)
                    {
                        Debug.LogWarning("[FunGuy Startup QA] Unsaved scene changes: aborting auto Play Mode.");
                        return;
                    }

                    Directory.CreateDirectory(Dir);
                    File.Delete(Request);
                    File.WriteAllText(Active, "Editor-only");
                    File.Delete(Live);
                    File.Delete(Final);
                    EditorApplication.EnterPlaymode();
                }
            }

            if (!EditorApplication.isPlaying || !File.Exists(Active))
                return;

            if (entered <= 0) entered = EditorApplication.timeSinceStartup;
            double elapsed = EditorApplication.timeSinceStartup - entered;
            if (elapsed < lastSample + 0.3)
                return;
            lastSample = elapsed;

            var coordinator = UnityEngine.Object.FindFirstObjectByType<RunFlowCoordinator>();
            var blocks = UnityEngine.Object.FindFirstObjectByType<BlockSpawner>();
            var grid = UnityEngine.Object.FindFirstObjectByType<GridCourseStreamer>();
            var player = UnityEngine.Object.FindFirstObjectByType<MushroomRunnerPlayer>();
            var overlay = GameObject.Find("FunGuy Gameplay Warm-up (Runtime)");
            if (coordinator != null && coordinator.IsStartupWarming)
            {
                sawWarming = true;
                if (player != null && player.State == MushroomRunnerPlayer.PlayerState.Disabled)
                    sawDisabledDuringWarmup = true;
            }
            if (overlay != null)
                sawOverlay = true;

            if (!didScreenshot && overlay != null && coordinator != null &&
                coordinator.StartupProgress > 0.2f && coordinator.StartupProgress < 0.95f)
            {
                ScreenCapture.CaptureScreenshot(Path.Combine(Dir, "loading-cover-preview.png"));
                didScreenshot = true;
            }

            string line = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "t={0:F2}s frame={1} ready={2} warming={3} player={4} progress={5:F3} overlay={6} phase={7} blockCreated={8} blockPooled={9} blockReuse={10} gridCreated={11} gridPooled={12} gridReuse={13} reservedMiB={14:F1}",
                elapsed, Time.frameCount,
                coordinator != null && coordinator.IsStartupReady,
                coordinator != null && coordinator.IsStartupWarming,
                player != null ? player.State.ToString() : "Missing",
                coordinator != null ? coordinator.StartupProgress : -1,
                overlay != null,
                coordinator != null ? coordinator.StartupStage : "Missing",
                blocks != null ? blocks.TotalInstancesCreated : -1,
                blocks != null ? blocks.PooledBlockCount : -1,
                blocks != null ? blocks.TotalPoolHits : -1,
                grid != null ? grid.TotalInstancesCreated : -1,
                grid != null ? grid.PooledInstanceCount : -1,
                grid != null ? grid.TotalPoolHits : -1,
                UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / 1048576.0);
            samples.Add(line);
            File.WriteAllLines(Live, samples);

            if (coordinator != null && coordinator.IsStartupReady && readyAt <= 0)
                readyAt = elapsed;

            if ((readyAt > 0 && elapsed > readyAt + 1.2) || elapsed > 45)
            {
                bool pass = readyAt > 0 && sawWarming && sawDisabledDuringWarmup &&
                            sawOverlay && overlay == null && player != null &&
                            player.State == MushroomRunnerPlayer.PlayerState.Active;
                samples.Add("RESULT pass=" + pass +
                            " warmupObserved=" + sawWarming +
                            " disabledDuringWarmup=" + sawDisabledDuringWarmup +
                            " coverObserved=" + sawOverlay +
                            " coverRemoved=" + (overlay == null) +
                            " progressScreenshotRequested=" + didScreenshot +
                            " warmupElapsedMs=" + (coordinator != null ? coordinator.StartupDurationMilliseconds : -1) +
                            " memoryBudgetReached=" + (coordinator != null && coordinator.StartupHitMemoryBudget));
                File.WriteAllLines(Final, samples);
                File.Delete(Active);
                EditorApplication.delayCall += () =>
                {
                    if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                };
                Debug.Log("[FunGuy Startup QA] " + samples[samples.Count-1]);
            }
        }
        catch (Exception ex)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(Final, ex.ToString());
            if (File.Exists(Active)) File.Delete(Active);
            if (EditorApplication.isPlaying)
                EditorApplication.delayCall += () =>
                {
                    if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
                };
            Debug.LogError("[FunGuy Startup QA] " + ex);
        }
    }
}
