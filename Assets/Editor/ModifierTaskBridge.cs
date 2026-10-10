using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Temporary, project-local automation mailbox. Only named operations in this task are accepted.
[InitializeOnLoad]
public static class ModifierTaskBridge
{
    const string Request = "Temp/ModifierTask.request";
    const string Result = "Temp/ModifierTask.result";
    static ModifierTaskBridge()
    {
        EditorApplication.update += Poll;
        Application.logMessageReceived += (message, trace, type) =>
        {
            if (EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
                File.AppendAllText("Temp/ModifierRuntimeErrors.txt", message + "\n" + trace + "\n");
        };
    }
    static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
        if (!Application.dataPath.Replace('\\', '/').Equals("D:/Work/FunGuy/Assets", StringComparison.OrdinalIgnoreCase)) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        try
        {
            string result;
            switch (command)
            {
                case "inspect":
                    result = "playing=" + EditorApplication.isPlaying + "\n";
                    for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                    {
                        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                        result += scene.path + " dirty=" + scene.isDirty + "\n";
                    }
                    result += "motors=" + UnityEngine.Object.FindObjectsByType<RunnerMovementMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
                    break;
                case "play":
                    if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != RunnerModifierSetup.Root + "/Scenes/MushroomRunnerGameplay.unity") throw new Exception("Unexpected scene");
                    File.WriteAllText("Temp/ModifierRuntimeErrors.txt", "");
                    EditorApplication.isPlaying = true; result = "Entering Play Mode"; break;
                case "stop": EditorApplication.isPlaying = false; result = "Stopped Play Mode"; break;
                case "feedback": result = Feedback(); break;
                case "feedback-check": result = FeedbackCheck(); break;
                case "expire-feedback": result = ExpireFeedback(); break;
                case "snapshot": ScreenCapture.CaptureScreenshot("Temp/ModifierPreview.png"); result = "Screenshot requested"; break;
                case "verify": RunnerModifierVerification.Run(); result = File.ReadAllText("Temp/ModifierVerification.txt"); break;
                case "setup": RunnerModifierSetup.Run(); result = "Setup complete"; break;
                case "refresh": AssetDatabase.Refresh(); result = "Refreshed"; break;
                default: result = "Unknown command: " + command; break;
            }
            File.WriteAllText(Result, result);
        }
        catch (Exception e) { File.WriteAllText(Result, e.ToString()); }
    }

    static RunnerModifierController LiveEffects() => Object.FindFirstObjectByType<MushroomRunnerPlayer>().MovementMotor.Modifiers;
    static string Feedback()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Requires Play Mode");
        var player = Object.FindFirstObjectByType<MushroomRunnerPlayer>();
        player.MovementMotor.enabled = false;
        player.MovementMotor.rigidBody.linearVelocity = Vector3.zero;
        player.MovementMotor.rigidBody.constraints = RigidbodyConstraints.FreezeAll;
        var effects = player.MovementMotor.Modifiers;
        effects.SetPaused(false);
        effects.Apply(AssetDatabase.LoadAssetAtPath<MushroomModifierDefinition>(RunnerModifierSetup.Definitions + "/Chilling.asset"));
        effects.Apply(AssetDatabase.LoadAssetAtPath<MushroomModifierDefinition>(RunnerModifierSetup.Definitions + "/Perfect Landings.asset"));
        var hud = Object.FindFirstObjectByType<RunnerModifierHud>();
        if (hud == null) throw new Exception("No active modifier HUD");
        hud.Bind(effects);
        Canvas.ForceUpdateCanvases();
        return "Feedback active: " + effects.transform.childCount + " player children; HUD=" + hud.gameObject.activeInHierarchy;
    }

    static string FeedbackCheck()
    {
        var effects = LiveEffects();
        var hud = Object.FindFirstObjectByType<RunnerModifierHud>();
        var text = new System.Text.StringBuilder();
        foreach (var type in new[] { MushroomModifierType.Chilling, MushroomModifierType.PerfectLandings })
        {
            int before = effects.transform.childCount;
            effects.Apply(AssetDatabase.LoadAssetAtPath<MushroomModifierDefinition>(RunnerModifierSetup.Definitions + (type == MushroomModifierType.Chilling ? "/Chilling.asset" : "/Perfect Landings.asset")));
            if (effects.transform.childCount != before) throw new Exception("Refresh duplicated VFX");
            text.AppendLine(type + " remaining=" + effects.GetRemaining(type));
        }
        foreach (var particle in effects.GetComponentsInChildren<ParticleSystem>(true))
            text.AppendLine(particle.name + " active=" + particle.gameObject.activeInHierarchy + " playing=" + particle.isPlaying + " particles=" + particle.particleCount);
        foreach (var image in hud.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            var corners = new Vector3[4]; image.rectTransform.GetWorldCorners(corners);
            text.AppendLine(image.name + " active=" + image.gameObject.activeInHierarchy + " enabled=" + image.enabled + " fill=" + image.fillAmount + " rect=" + image.rectTransform.rect + " corners=" + string.Join(",", corners.Select(c => c.ToString())));
        }
        var manager = AudioManager.Instance;
        var audioSource = (AudioSource)new SerializedObject(manager).FindProperty("SFX_AudioSource").objectReferenceValue;
        text.AppendLine("SFX playing=" + audioSource.isPlaying + " volume=" + audioSource.volume + " mixer=" + (audioSource.outputAudioMixerGroup != null));
        GameplayEvents.OnPauseGameplay?.Invoke(true);
        float remaining = effects.GetRemaining(MushroomModifierType.Chilling);
        effects.AdvanceTimers(2);
        if (effects.GetRemaining(MushroomModifierType.Chilling) != remaining) throw new Exception("Pause advanced timer");
        File.WriteAllText("Temp/ModifierFeedback.txt", text.ToString());
        return text.ToString();
    }

    static string ExpireFeedback()
    {
        var effects = LiveEffects();
        var hud = Object.FindFirstObjectByType<RunnerModifierHud>();
        GameplayEvents.OnPauseGameplay?.Invoke(false);
        effects.AdvanceTimers(20); hud.Refresh();
        foreach (var particle in effects.GetComponentsInChildren<ParticleSystem>(true))
            if (particle.gameObject.activeInHierarchy || particle.particleCount != 0) throw new Exception("VFX survived expiry");
        foreach (var image in hud.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            if (image.gameObject.activeInHierarchy) throw new Exception("HUD survived expiry");
        var chill = AssetDatabase.LoadAssetAtPath<MushroomModifierDefinition>(RunnerModifierSetup.Definitions + "/Chilling.asset");
        effects.Apply(chill);
        var player = effects.GetComponent<MushroomRunnerPlayer>(); player.SetState(MushroomRunnerPlayer.PlayerState.Dead);
        if (effects.IsActive(MushroomModifierType.Chilling)) throw new Exception("Death did not clear effects");
        effects.Apply(chill); effects.enabled = false;
        if (effects.IsActive(MushroomModifierType.Chilling)) throw new Exception("Disable did not clear effects");
        effects.enabled = true;
        return "PASS: VFX reuse, pause, expiry, HUD cleanup, death and disable";
    }
}
