using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(MovementTuningProfile))]
public sealed class MovementTuningProfileEditor : Editor
{
    bool showAdvanced;
    ReorderableList gears;

    void OnEnable()
    {
        gears = new ReorderableList(serializedObject, serializedObject.FindProperty("speedGears"), false, true, true, true);
        gears.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Maximum Speed (world units/second)");
        gears.drawElementCallback = (rect, index, active, focused) =>
        {
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(rect, gears.serializedProperty.GetArrayElementAtIndex(index), new GUIContent($"x{index + 1}"));
        };
        gears.onAddCallback = list =>
        {
            int index = list.serializedProperty.arraySize++;
            list.serializedProperty.GetArrayElementAtIndex(index).floatValue = index > 0
                ? list.serializedProperty.GetArrayElementAtIndex(index - 1).floatValue : 15f;
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        Section("Speed Gears");
        gears.DoLayoutList();
        EditorGUILayout.HelpBox("Entries select x1, x2, x3… Higher multipliers use the last entry. Gears change the ceiling; bounces and steering build speed.", MessageType.Info);
        Section("Steering");
        Property("airAcceleration", "Air Acceleration (units/s²)");
        Property("forwardAirControlMultiplier", "Forward Propulsion");
        Property("strafeSpeed", "Sideways Speed (units/s)");
        Property("steeringResponse", "Steering Response (seconds)");
        Property("airDrag", "Air Resistance (units/s squared)");
        Property("brakeResponse", "Brake Response (seconds)");
        Property("divePull", "Dive Pull (units/s squared)");
        Section("Bounce");
        Property("baseJumpForce", "Launch Speed (units/s)");
        Property("baseBounceSpeedGain", "Speed Gain (units/s)");
        Section("Air Jump");
        Property("dashForce", "Launch Speed (units/s)");
        Property("dashChargesPerBounce", "Charges Per Bounce");
        Property("dashCooldown", "Cooldown (seconds)");
        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "Advanced", true);
        if (showAdvanced)
        {
            EditorGUI.indentLevel++;
            Property("maxControllableSpeed", "Propulsion Speed Limit (units/s)");
            Property("overSpeedDrag", "Overspeed Slowdown (1/s)");
            foreach (string name in new[] {
                "gravityScale", "jumpGravityMultiplier", "fallGravityMultiplier", "useBounceFlightShaper",
                "referencePlanarSpeed", "maximumPlanarSpeed", "slowRiseGravityMultiplier", "fastRiseGravityMultiplier",
                "slowFallGravityMultiplier", "fastFallGravityMultiplier", "apexTransitionVerticalSpeed",
                "apexExtraDownAcceleration", "apexExtraDownDuration", "postBounceLowControlTime",
                "postBounceAirControlMultiplier", "postDashBonusControlTime", "postDashAirControlMultiplier",
                "bounceGraceTime", "dashBufferTime", "minGroundDot" })
            {
                Property(name, ObjectNames.NicifyVariableName(name).Replace("Dash", "Air Jump"));
            }
            EditorGUI.indentLevel--;
        }
        serializedObject.ApplyModifiedProperties();
    }

    static void Section(string label)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
    }

    void Property(string name, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(name);
        EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
    }
}
