using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(SurfaceTagAttribute))]
public sealed class SurfaceTagDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var field = new TagField(property.displayName, property.stringValue)
        {
            bindingPath = property.propertyPath,
            tooltip = property.tooltip
        };
        field.AddToClassList(BaseField<string>.alignedFieldUssClassName);
        return field;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        using (new EditorGUI.PropertyScope(position, label, property))
        {
            bool previousMixedValue = EditorGUI.showMixedValue;
            try
            {
                EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                using (var change = new EditorGUI.ChangeCheckScope())
                {
                    string selectedTag = EditorGUI.TagField(position, label, property.stringValue);
                    if (change.changed) property.stringValue = selectedTag;
                }
            }
            finally
            {
                EditorGUI.showMixedValue = previousMixedValue;
            }
        }
    }
}
