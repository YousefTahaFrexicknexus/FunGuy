using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RunnerLandingAssistTools
{
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    static void DrawAssist(RunnerLandingAssist assist, GizmoType gizmoType) => assist.DrawAssistGizmos();

    [MenuItem("Tools/Funguy/Landing Assist/Apply Strong Preset (Scene and Player Prefab)")]
    static void ApplyStrongPreset()
    {
        if (!EditorApplication.isPlaying)
        {
            const string path = "Assets/_Game/Funguy.MushroomRunner/Prefabs/MushroomRunnerPlayer.prefab";
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var assist = prefab.GetComponent<RunnerLandingAssist>();
                if (assist == null) assist = prefab.AddComponent<RunnerLandingAssist>();
                assist.ApplyStrongPreset();
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        int count = 0;
        foreach (var motor in Object.FindObjectsByType<RunnerMovementMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (EditorUtility.IsPersistent(motor) || !motor.gameObject.scene.IsValid()) continue;
            var assist = motor.GetComponent<RunnerLandingAssist>();
            if (assist == null) assist = Undo.AddComponent<RunnerLandingAssist>(motor.gameObject);
            Undo.RecordObject(assist, "Strong landing assist preset");
            assist.ApplyStrongPreset();
            PrefabUtility.RecordPrefabInstancePropertyModifications(assist);
            EditorUtility.SetDirty(assist);
            if (!EditorApplication.isPlaying) EditorSceneManager.MarkSceneDirty(motor.gameObject.scene);
            Selection.activeGameObject = motor.gameObject;
            count++;
        }
        foreach (SceneView view in SceneView.sceneViews) view.drawGizmos = true;
        SceneView.RepaintAll();
        Debug.Log($"Applied strong landing assist to {count} scene player(s). Reduce Assist Strength to tune it down. Cyan: prediction; green: target; yellow: rescue offset.");
    }
}
