using UnityEditor;

[CustomEditor(typeof(MushroomLauncher)), CanEditMultipleObjects]
public sealed class MushroomLauncherEditor : Editor
{
    public override void OnInspectorGUI()
    {
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
