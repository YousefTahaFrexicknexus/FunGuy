using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent, visible main-menu -> gameplay loading bridge. Uses one cover
/// across async scene activation and RunFlowCoordinator's staged-world startup.
/// This runner survives SceneManager.LoadSceneAsync(Single), unlike menu scripts.
/// </summary>
[DisallowMultipleComponent]
public sealed class FunGuySceneLoadingTransition : MonoBehaviour
{
    private static FunGuySceneLoadingTransition current;
    private FunGuyStartupLoadingOverlay cover;
    private string targetScene;
    private float startedTime;
    private bool activatedScene;

    public static bool IsLoading => current != null;

    public static bool Begin(string sceneName, Sprite logo)
    {
        if (current != null || !Application.CanStreamedLevelBeLoaded(sceneName))
            return false;

        var host = new GameObject("__FunGuySceneLoadingTransition");
        DontDestroyOnLoad(host);
        current = host.AddComponent<FunGuySceneLoadingTransition>();
        current.targetScene = sceneName;
        current.cover = FunGuyStartupLoadingOverlay.ShowDuringSceneLoad(logo);
        current.startedTime = Time.realtimeSinceStartup;
        current.StartCoroutine(current.LoadAsync());
        return true;
    }

    private IEnumerator LoadAsync()
    {
        // Render the persistent cover before Unity begins loading the scene.
        cover?.UpdateSceneLoading(0f);
        yield return null;

        AsyncOperation operation;
        try
        {
            operation = SceneManager.LoadSceneAsync(targetScene, LoadSceneMode.Single);
        }
        catch (Exception ex)
        {
            Debug.LogError("[FunGuy SceneLoad] Could not load " + targetScene + ": " + ex);
            Finish();
            yield break;
        }

        if (operation == null)
        {
            Debug.LogError("[FunGuy SceneLoad] Null async operation for " + targetScene);
            Finish();
            yield break;
        }

        operation.allowSceneActivation = false;
        while (operation.progress < 0.9f)
        {
            cover?.UpdateSceneLoading(operation.progress / 0.9f);
            yield return null;
        }

        cover?.UpdateSceneLoading(1f);
        // Allow one frame of fully loaded progress before activating the new scene.
        yield return null;
        operation.allowSceneActivation = true;
        while (!operation.isDone)
            yield return null;
        activatedScene = true;

        // RunFlowCoordinator takes ownership of the same cover to prepare the
        // world and GPU assets before it allows movement. The driver remains
        // alive across the scene transition to handle scenes without a run flow.
        yield return null;
        var coordinator = FindFirstObjectByType<RunFlowCoordinator>();
        if (coordinator != null)
        {
            const float safetySeconds = 40f;
            float deadline = Time.realtimeSinceStartup + safetySeconds;
            while (coordinator != null && !coordinator.IsStartupReady &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            if (coordinator != null && !coordinator.IsStartupReady)
                Debug.LogWarning("[FunGuy SceneLoad] Waiting for scene preparation exceeded " +
                    safetySeconds + " seconds. Releasing cover to avoid permanently blocking input.");
        }
        else
        {
            // Infinite-run scenes may not use RunFlowCoordinator.
            yield return null;
        }

        Debug.Log("[FunGuy SceneLoad] " + targetScene + " visible after " +
            (Time.realtimeSinceStartup - startedTime).ToString("F2") +
            " seconds; activated=" + activatedScene);
        Finish();
    }

    private void Finish()
    {
        cover?.Close();
        cover = null;
        if (current == this) current = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (current == this)
        {
            cover?.Close();
            current = null;
        }
    }
}
