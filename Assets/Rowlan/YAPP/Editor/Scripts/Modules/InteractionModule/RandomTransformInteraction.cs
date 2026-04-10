using UnityEditor;
using UnityEngine;
using static Rowlan.Yapp.BrushComponent;
using static Rowlan.Yapp.InteractionSettings;

namespace Rowlan.Yapp
{
    public class RandomTransformInteraction : InteractionModuleI
    {
        private RandomTransform randomTransform;

#pragma warning disable 0414
        PrefabPainterEditor editor;
        PrefabPainter editorTarget;
#pragma warning restore 0414

        UnityTerrainTreeManager terrainTreeManager;

        /// <summary>
        /// For this feature we want to change the transform only on mouse click on drag, not when the mouse is continuously pressed.
        /// This flag contains information whether the mouse event was applied or not.
        /// </summary>
        private bool mouseEventApplied = true;

        public RandomTransformInteraction(PrefabPainterEditor editor)
        {
            this.editor = editor;
            this.editorTarget = editor.GetPainter();

            terrainTreeManager = new UnityTerrainTreeManager(editor);

            randomTransform = editorTarget.interactionSettings.randomTransform;
        }

        public void OnInspectorGUI()
        {
            GUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Random Transform", GUIStyles.BoxTitleStyle);

            // scale
            randomTransform.changeScale = EditorGUILayout.Toggle("Change Scale", randomTransform.changeScale);

            if (randomTransform.changeScale)
            {
                randomTransform.scaleMin = EditorGUILayout.FloatField("Scale Min", randomTransform.scaleMin);
                randomTransform.scaleMax = EditorGUILayout.FloatField("Scale Max", randomTransform.scaleMax);
            }

            // rotation
            GUILayout.BeginHorizontal();
            {
                randomTransform.randomRotation = EditorGUILayout.Toggle("Random Rotation", randomTransform.randomRotation);

                // right align the buttons
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("X", EditorStyles.miniButton))
                {
                    QuickRotationSetting(randomTransform, 1f, 0f, 0f);
                }
                if (GUILayout.Button("Y", EditorStyles.miniButton))
                {
                    QuickRotationSetting(randomTransform, 0f, 1f, 0f);
                }
                if (GUILayout.Button("Z", EditorStyles.miniButton))
                {
                    QuickRotationSetting(randomTransform, 0f, 0f, 1f);
                }
                if (GUILayout.Button("XYZ", EditorStyles.miniButton))
                {
                    QuickRotationSetting(randomTransform, 1f, 1f, 1f);
                }

                if (GUILayout.Button(randomTransform.rotationRange.GetDisplayName(), EditorStyles.miniButton))
                {
                    randomTransform.rotationRange = randomTransform.rotationRange.GetNext();
                }


            }
            GUILayout.EndHorizontal();

            // rotation limits
            if (randomTransform.randomRotation)
            {

                float min = randomTransform.rotationRange.GetMinimum();
                float max = randomTransform.rotationRange.GetMaximum();

                EditorGuiUtilities.MinMaxEditor("  Rotation Limit X", ref randomTransform.rotationMinX, ref randomTransform.rotationMaxX, min, max);
                EditorGuiUtilities.MinMaxEditor("  Rotation Limit Y", ref randomTransform.rotationMinY, ref randomTransform.rotationMaxY, min, max);
                EditorGuiUtilities.MinMaxEditor("  Rotation Limit Z", ref randomTransform.rotationMinZ, ref randomTransform.rotationMaxZ, min, max);
            }

            GUILayout.EndVertical();
        }

        private void QuickRotationSetting(RandomTransform prefabSettings, float x, float y, float z)
        {
            float min = prefabSettings.rotationRange.GetMinimum();
            float max = prefabSettings.rotationRange.GetMaximum();

            prefabSettings.randomRotation = true;

            prefabSettings.rotationMinX = min * x;
            prefabSettings.rotationMaxX = max * x;
            prefabSettings.rotationMinY = min * y;
            prefabSettings.rotationMaxY = max * y;
            prefabSettings.rotationMinZ = min * z;
            prefabSettings.rotationMaxZ = max * z;

        }

        public bool OnSceneGUI(BrushMode brushMode, RaycastHit raycastHit, out bool applyPhysics)
        {
            applyPhysics = false;

            // check if there's a click or drag event and if we should apply the mouse event
            bool mouseEventReceived = Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag;

            if (mouseEventReceived && mouseEventApplied)
            {
                mouseEventApplied = false;
            }

            // skip if the click event has been applied; this prevents multiple invocations when the user keeps the mouse pressed
            if ( mouseEventApplied)
                return false;

            switch (brushMode)
            {
                case BrushMode.ShiftPressed:

                    SetScale(raycastHit);

                    applyPhysics = true;

                    // consume event, otherwise brush won't be drawn
                    if (Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
                        Event.current.Use();

                    mouseEventApplied = true;

                    return true;

            }

            return false;
        }

        private float GetRandomScale()
        {
            return Random.Range(randomTransform.scaleMin, randomTransform.scaleMax);
        }

        private Quaternion GetRandomRotation()
        {
            float rotationX = Random.Range(randomTransform.rotationMinX, randomTransform.rotationMaxX);
            float rotationY = Random.Range(randomTransform.rotationMinY, randomTransform.rotationMaxY);
            float rotationZ = Random.Range(randomTransform.rotationMinZ, randomTransform.rotationMaxZ);

            Quaternion rotation = Quaternion.Euler(rotationX, rotationY, rotationZ);

            return rotation;
        }

        private void SetScale(RaycastHit hit)
        {
            switch (editor.GetPainter().brushSettings.spawnTarget)
            {
                case BrushSettings.SpawnTarget.PrefabContainer:

                    SetScalePrefabs(hit);

                    break;

                case BrushSettings.SpawnTarget.TerrainTrees:

                    float brushSize = editorTarget.brushSettings.brushSize;
                    float scale = GetRandomScale();

                    float scaleValueX = scale;
                    float scaleValueY = scale;

                    terrainTreeManager.SetScale(hit.point, brushSize, scaleValueX, scaleValueY);

                    break;

                /*
                case BrushSettings.SpawnTarget.TerrainDetails:
                    Debug.LogError("Not implemented");
                    break;
                */
                case BrushSettings.SpawnTarget.VegetationStudioPro:
                    Debug.LogError("Not implemented");
                    break;
            }
        }

        // TODO: check performance; currently invoked multiple times in the editor loop
        private void SetScalePrefabs(RaycastHit hit)
        {

            Transform[] containerChildren = PrefabUtils.GetContainerChildren(editorTarget.container, hit, editorTarget.brushSettings.brushSize);

            foreach (Transform transform in containerChildren)
            {
                Undo.RegisterCompleteObjectUndo(transform, "Random transform");

                if (randomTransform.changeScale)
                {
                    float scaleValue = GetRandomScale();
                    Vector3 scaleVector = new Vector3(scaleValue, scaleValue, scaleValue);

                    transform.localScale = scaleVector;
                }

                if (randomTransform.randomRotation)
                {
                    Quaternion rotationValue = GetRandomRotation();

                    transform.localRotation = rotationValue;
                }
            }
        }
    }
}
