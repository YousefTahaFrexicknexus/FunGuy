using UnityEditor;

[CustomEditor(typeof(MushroomLauncher)), CanEditMultipleObjects]
public sealed class MushroomLauncherEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Each quality runs its speed rules from top to bottom. Effectiveness scales incoming speed once, then Add / Multiply / Set run in list order. Only the final result clamps to zero. An empty list keeps the effective incoming speed.", MessageType.Info);
        DrawDefaultInspector();
        foreach (var inspected in targets)
        {
            string warning = ((MushroomLauncher)inspected).ConfigurationWarning;
            if (!string.IsNullOrEmpty(warning))
            {
                EditorGUILayout.HelpBox(warning, MessageType.Warning);
                break;
            }
        }
    }
}
