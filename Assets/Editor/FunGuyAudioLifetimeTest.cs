using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One-shot EditMode->PlayMode lifetime verification for AudioManager; never saves or builds.</summary>
[InitializeOnLoad]
internal static class FunGuyAudioLifetimeTest
{
    static readonly string Root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static readonly string Dir = Path.Combine(Root, "work", "windows-profiling-2026-11-09", "cpu-profile");
    static readonly string Request = Path.Combine(Dir, "test-audio-lifetime.request");
    static readonly string Active = Path.Combine(Dir, "test-audio-lifetime.active");
    static bool started, previousRunInBackground;
    static double nextPoll, enteredAt;

    static FunGuyAudioLifetimeTest()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayState;
    }

    static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && File.Exists(Active))
            enteredAt = EditorApplication.timeSinceStartup;
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying && EditorApplication.timeSinceStartup >= nextPoll)
        {
            nextPoll = EditorApplication.timeSinceStartup + 2;
            if (File.Exists(Request) && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                if (SceneManager.GetActiveScene().isDirty)
                {
                    Debug.LogWarning("[FunGuy Audio QA] Scene has unsaved edits. Skipping automatic Play Mode.");
                    return;
                }
                File.Delete(Request);
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Active, "one-shot-audio-lifetime-test");
                EditorApplication.EnterPlaymode();
            }
        }

        if (!EditorApplication.isPlaying || !File.Exists(Active))
            return;

        if (enteredAt == 0)
            enteredAt = EditorApplication.timeSinceStartup;

        if (!started)
        {
            started = true;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
        }

        if (EditorApplication.timeSinceStartup - enteredAt < 4.0)
            return;

        try
        {
            var managers = UnityEngine.Object.FindObjectsByType<AudioManager>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            AudioManager singleton = AudioManager.Instance;
            bool hasOne = managers.Length == 1;
            bool root = singleton != null && singleton.transform.parent == null;
            string summary = "Local=" + DateTime.Now.ToString("o") +
                "\nScene=" + SceneManager.GetActiveScene().name +
                "\nAudioManagerCount=" + managers.Length +
                "\nSingletonExists=" + (singleton != null) +
                "\nSingletonIsRoot=" + root +
                "\nSingletonScene=" + (singleton != null ? singleton.gameObject.scene.name : "(none)") +
                "\nPASS=" + (hasOne && root);
            File.WriteAllText(Path.Combine(Dir, "audio-lifetime-verification.txt"), summary);
            Debug.Log("[FunGuy Audio QA] " + summary.Replace("\n", " | "));
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(Dir, "audio-lifetime-verification.txt"), ex.ToString());
            Debug.LogError("[FunGuy Audio QA] " + ex);
        }
        finally
        {
            if (started) Application.runInBackground = previousRunInBackground;
            started = false;
            enteredAt = 0;
            File.Delete(Active);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            };
        }
    }
}
