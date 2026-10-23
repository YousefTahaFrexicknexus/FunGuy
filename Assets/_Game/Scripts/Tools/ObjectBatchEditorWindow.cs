#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ObjectBatchEditor
{
    public enum MatchMode
    {
        PrefabAsset,
        Mesh,
        GameObjectName,
        Tag,
        Component,
        Material
    }

    public enum ScopeMode
    {
        CurrentParent,
        CurrentHierarchy,
        SelectedHierarchies,
        EntireScene
    }

    public enum ValueMode
    {
        Add,
        Multiply,
        Absolute,
        Relative
    }

    [Serializable]
    public struct AxisFloat
    {
        public bool enabled;
        public float value;
    }

    [Serializable]
    public struct AxisRange
    {
        public bool enabled;
        public float min;
        public float max;
    }

    [CreateAssetMenu(
        fileName = "ObjectBatchPreset",
        menuName = "Tools/Object Batch Editor/Preset")]
    public class ObjectBatchPreset : ScriptableObject
    {
        public ValueMode positionMode = ValueMode.Add;
        public AxisFloat positionX;
        public AxisFloat positionY;
        public AxisFloat positionZ;

        public bool randomRotation;
        public ValueMode rotationMode = ValueMode.Relative;
        public AxisRange rotationX;
        public AxisRange rotationY;
        public AxisRange rotationZ;

        public bool randomScale;
        public ValueMode scaleMode = ValueMode.Multiply;
        public AxisFloat scaleX;
        public AxisFloat scaleY;
        public AxisFloat scaleZ;
        public AxisRange scaleXRandom;
        public AxisRange scaleYRandom;
        public AxisRange scaleZRandom;

        public bool useSeed;
        public int seed = 12345;
    }

    public class ObjectBatchEditorWindow : EditorWindow
    {
        private const string MenuPath = "Tools/Object Batch Editor/Object Batch Editor";

        private GameObject sourceObject;

        private MatchMode matchMode = MatchMode.PrefabAsset;
        private ScopeMode scopeMode = ScopeMode.CurrentHierarchy;

        private bool combineCriteria;
        private bool includeInactive;

        private readonly List<MatchMode> additionalCriteria = new();

        private List<GameObject> foundObjects = new();

        // Position
        private ValueMode positionMode = ValueMode.Add;
        private AxisFloat positionX;
        private AxisFloat positionY;
        private AxisFloat positionZ;

        // Rotation
        private bool randomRotation;
        private ValueMode rotationMode = ValueMode.Relative;
        private AxisRange rotationX;
        private AxisRange rotationY;
        private AxisRange rotationZ;

        // Scale
        private bool randomScale;
        private ValueMode scaleMode = ValueMode.Multiply;
        private AxisFloat scaleX;
        private AxisFloat scaleY;
        private AxisFloat scaleZ;
        private AxisRange scaleXRandom;
        private AxisRange scaleYRandom;
        private AxisRange scaleZRandom;

        // Random
        private bool useSeed;
        private int seed = 12345;
        private int previewSeed = 12345;

        // Preview
        private bool previewChanges;
        private readonly Dictionary<int, TransformState> previewStates = new();

        // Presets
        private ObjectBatchPreset preset;

        private Vector2 scroll;

        private GUIStyle headerStyle;
        private GUIStyle boxHeaderStyle;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<ObjectBatchEditorWindow>("Object Batch Editor");
            window.minSize = new Vector2(390, 650);
            window.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            Undo.undoRedoPerformed -= Repaint;

            if (previewChanges)
                ClearPreview();
        }

        private void OnSelectionChanged()
        {
            if (Selection.activeGameObject != null)
            {
                sourceObject = Selection.activeGameObject;
                Repaint();
            }
        }

        private void OnGUI()
        {
            EnsureStyles();

            scroll = EditorGUILayout.BeginScrollView(scroll);

            DrawSourceSection();
            DrawMatchingSection();
            DrawPositionSection();
            DrawRotationSection();
            DrawScaleSection();
            DrawRandomSection();
            DrawPresetSection();
            DrawApplySection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSourceSection()
        {
            BeginSection("Source");

            EditorGUILayout.HelpBox(
                "Select one scene object, configure how similar objects are identified, then click Find Similar.",
                MessageType.Info);

            sourceObject = EditorGUILayout.ObjectField(
                "Source Object",
                sourceObject,
                typeof(GameObject),
                true) as GameObject;

            if (sourceObject == null && Selection.activeGameObject != null)
                sourceObject = Selection.activeGameObject;

            if (sourceObject != null)
            {
                EditorGUILayout.LabelField(
                    "Selected:",
                    sourceObject.name,
                    EditorStyles.miniLabel);
            }

            EndSection();
        }

        private void DrawMatchingSection()
        {
            BeginSection("Find Similar");

            matchMode = (MatchMode)EditorGUILayout.EnumPopup("Match By", matchMode);
            scopeMode = (ScopeMode)EditorGUILayout.EnumPopup("Scope", scopeMode);

            includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);
            combineCriteria = EditorGUILayout.Toggle(
                new GUIContent("Additional Criteria"),
                combineCriteria);

            if (combineCriteria)
            {
                EditorGUILayout.HelpBox(
                    "All additional criteria must also match the source object.",
                    MessageType.None);

                for (int i = 0; i < additionalCriteria.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    additionalCriteria[i] =
                        (MatchMode)EditorGUILayout.EnumPopup(additionalCriteria[i]);

                    if (GUILayout.Button("−", GUILayout.Width(28)))
                    {
                        additionalCriteria.RemoveAt(i);
                        i--;
                    }

                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("+ Add Criteria"))
                {
                    additionalCriteria.Add(MatchMode.Mesh);
                }
            }

            GUI.enabled = sourceObject != null;

            if (GUILayout.Button("Find Similar", GUILayout.Height(32)))
                FindSimilar();

            GUI.enabled = true;

            if (foundObjects.Count > 0)
            {
                EditorGUILayout.LabelField(
                    $"Found: {foundObjects.Count} object(s)",
                    EditorStyles.boldLabel);

                if (GUILayout.Button("Select Found Objects"))
                    SelectFoundObjects();

                if (GUILayout.Button("Clear Results"))
                {
                    foundObjects.Clear();
                    ClearPreview();
                }
            }

            EndSection();
        }

        private void DrawPositionSection()
        {
            BeginSection("Position");

            positionMode =
                (ValueMode)EditorGUILayout.EnumPopup(
                    new GUIContent("Mode"),
                    positionMode);

            DrawAxisFloat("X", ref positionX);
            DrawAxisFloat("Y", ref positionY);
            DrawAxisFloat("Z", ref positionZ);

            EndSection();
        }

        private void DrawRotationSection()
        {
            BeginSection("Rotation");

            randomRotation =
                EditorGUILayout.Toggle("Randomize", randomRotation);

            rotationMode =
                (ValueMode)EditorGUILayout.EnumPopup(
                    new GUIContent("Mode"),
                    rotationMode);

            if (randomRotation)
            {
                DrawAxisRange("X", ref rotationX);
                DrawAxisRange("Y", ref rotationY);
                DrawAxisRange("Z", ref rotationZ);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Rotation uses random ranges when Randomize is enabled.",
                    MessageType.None);
            }

            EndSection();
        }

        private void DrawScaleSection()
        {
            BeginSection("Scale");

            randomScale =
                EditorGUILayout.Toggle("Randomize", randomScale);

            scaleMode =
                (ValueMode)EditorGUILayout.EnumPopup(
                    new GUIContent("Mode"),
                    scaleMode);

            if (randomScale)
            {
                DrawAxisRange("X", ref scaleXRandom);
                DrawAxisRange("Y", ref scaleYRandom);
                DrawAxisRange("Z", ref scaleZRandom);
            }
            else
            {
                DrawAxisFloat("X", ref scaleX);
                DrawAxisFloat("Y", ref scaleY);
                DrawAxisFloat("Z", ref scaleZ);
            }

            EndSection();
        }

        private void DrawRandomSection()
        {
            BeginSection("Randomization");

            useSeed = EditorGUILayout.Toggle("Use Seed", useSeed);

            if (useSeed)
                seed = EditorGUILayout.IntField("Seed", seed);

            if (GUILayout.Button("Randomize Seed"))
                seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);

            EndSection();
        }

        private void DrawPresetSection()
        {
            BeginSection("Presets");

            preset = (ObjectBatchPreset)EditorGUILayout.ObjectField(
                "Preset",
                preset,
                typeof(ObjectBatchPreset),
                false);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Save New Preset"))
                SaveNewPreset();

            GUI.enabled = preset != null;

            if (GUILayout.Button("Save To Preset"))
                SaveToPreset();

            if (GUILayout.Button("Load Preset"))
                LoadPreset();

            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            EndSection();
        }

        private void DrawApplySection()
        {
            BeginSection("Apply");

            GUI.enabled = foundObjects.Count > 0;

            if (!previewChanges)
            {
                if (GUILayout.Button("Preview Changes", GUILayout.Height(28)))
                    CreatePreview();
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Preview is active. Scene objects have temporarily changed. Apply to keep the changes or Cancel to restore them.",
                    MessageType.Warning);

                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("Apply", GUILayout.Height(32)))
                    ApplyPreview();

                if (GUILayout.Button("Cancel", GUILayout.Height(32)))
                    ClearPreview();

                EditorGUILayout.EndHorizontal();
            }

            GUI.enabled = true;

            if (GUILayout.Button("Reset Tool Settings"))
                ResetSettings();

            EndSection();
        }

        private void FindSimilar()
        {
            ClearPreview();

            if (sourceObject == null)
                return;

            List<GameObject> candidates = GetCandidates(sourceObject);

            foundObjects.Clear();

            foreach (GameObject candidate in candidates)
            {
                if (candidate == null)
                    continue;

                if (!includeInactive && !candidate.activeInHierarchy)
                    continue;

                if (!Matches(candidate, matchMode))
                    continue;

                if (combineCriteria)
                {
                    bool allMatch = true;

                    foreach (MatchMode criterion in additionalCriteria)
                    {
                        if (!Matches(candidate, criterion))
                        {
                            allMatch = false;
                            break;
                        }
                    }

                    if (!allMatch)
                        continue;
                }

                foundObjects.Add(candidate);
            }

            SelectFoundObjects();
            Repaint();
        }

        private List<GameObject> GetCandidates(GameObject source)
        {
            var result = new List<GameObject>();

            switch (scopeMode)
            {
                case ScopeMode.CurrentParent:
                {
                    Transform parent = source.transform.parent;

                    if (parent == null)
                        result.Add(source);
                    else
                    {
                        foreach (Transform child in parent)
                            result.Add(child.gameObject);
                    }

                    break;
                }

                case ScopeMode.CurrentHierarchy:
                {
                    Transform root = source.transform;

                    while (root.parent != null)
                        root = root.parent;

                    AddHierarchy(root, result);
                    break;
                }

                case ScopeMode.SelectedHierarchies:
                {
                    foreach (GameObject selected in Selection.gameObjects)
                    {
                        if (selected != null)
                            AddHierarchy(selected.transform, result);
                    }

                    break;
                }

                case ScopeMode.EntireScene:
                {
                    result.AddRange(
                        UnityEngine.SceneManagement.SceneManager
                            .GetActiveScene()
                            .GetRootGameObjects());

                    var all = new List<GameObject>();

                    foreach (GameObject root in result)
                        AddHierarchy(root.transform, all);

                    return all;
                }
            }

            return result;
        }

        private static void AddHierarchy(
            Transform root,
            List<GameObject> result)
        {
            result.Add(root.gameObject);

            foreach (Transform child in root)
                AddHierarchy(child, result);
        }

        private bool Matches(GameObject candidate, MatchMode mode)
        {
            switch (mode)
            {
                case MatchMode.PrefabAsset:
                {
                    GameObject sourcePrefab =
                        PrefabUtility.GetCorrespondingObjectFromSource(sourceObject);

                    GameObject candidatePrefab =
                        PrefabUtility.GetCorrespondingObjectFromSource(candidate);

                    if (sourcePrefab != null && candidatePrefab != null)
                        return sourcePrefab == candidatePrefab;

                    return sourceObject == candidate;
                }

                case MatchMode.Mesh:
                {
                    Mesh sourceMesh = GetMesh(sourceObject);
                    Mesh candidateMesh = GetMesh(candidate);

                    return sourceMesh != null &&
                           sourceMesh == candidateMesh;
                }

                case MatchMode.GameObjectName:
                    return sourceObject.name == candidate.name;

                case MatchMode.Tag:
                    return sourceObject.CompareTag(candidate.tag);

                case MatchMode.Component:
                    return HasSameComponentType(sourceObject, candidate);

                case MatchMode.Material:
                {
                    Material[] sourceMaterials =
                        GetMaterials(sourceObject);

                    Material[] candidateMaterials =
                        GetMaterials(candidate);

                    if (sourceMaterials.Length != candidateMaterials.Length)
                        return false;

                    for (int i = 0; i < sourceMaterials.Length; i++)
                    {
                        if (sourceMaterials[i] != candidateMaterials[i])
                            return false;
                    }

                    return true;
                }
            }

            return false;
        }

        private static Mesh GetMesh(GameObject go)
        {
            MeshFilter filter = go.GetComponent<MeshFilter>();

            if (filter != null)
                return filter.sharedMesh;

            SkinnedMeshRenderer skinned =
                go.GetComponent<SkinnedMeshRenderer>();

            return skinned != null ? skinned.sharedMesh : null;
        }

        private static Material[] GetMaterials(GameObject go)
        {
            Renderer renderer = go.GetComponent<Renderer>();

            return renderer != null
                ? renderer.sharedMaterials
                : Array.Empty<Material>();
        }

        private static bool HasSameComponentType(
            GameObject source,
            GameObject candidate)
        {
            Component[] sourceComponents = source.GetComponents<Component>();
            Component[] candidateComponents = candidate.GetComponents<Component>();

            foreach (Component sourceComponent in sourceComponents)
            {
                if (sourceComponent == null)
                    continue;

                Type sourceType = sourceComponent.GetType();

                foreach (Component candidateComponent in candidateComponents)
                {
                    if (candidateComponent != null &&
                        candidateComponent.GetType() == sourceType)
                        return true;
                }
            }

            return false;
        }

        private void SelectFoundObjects()
        {
            if (foundObjects.Count == 0)
                return;

            Selection.objects = foundObjects.ToArray();
        }

        private void CreatePreview()
        {
            if (foundObjects.Count == 0)
                return;

            ClearPreview();

            previewStates.Clear();

            foreach (GameObject go in foundObjects)
            {
                if (go == null)
                    continue;

                previewStates[go.GetInstanceID()] =
                    new TransformState(go.transform);
            }

            previewSeed = useSeed ? seed : Environment.TickCount;
            var random = new System.Random(previewSeed);

            foreach (GameObject go in foundObjects)
                ApplyToObject(go, random);

            previewChanges = true;
            SceneView.RepaintAll();
        }

        private void ApplyPreview()
        {
            if (!previewChanges)
                return;

            // Restore the original values first so Unity's Undo snapshot records
            // the true pre-operation state, not the previewed state.
            foreach (GameObject go in foundObjects)
            {
                if (go != null &&
                    previewStates.TryGetValue(go.GetInstanceID(), out TransformState state))
                {
                    state.Restore(go.transform);
                }
            }

            Transform[] transforms = GetValidTransforms();
            Undo.RecordObjects(transforms, "Apply Object Batch Changes");

            var random = new System.Random(previewSeed);

            foreach (GameObject go in foundObjects)
                ApplyToObject(go, random);

            foreach (Transform transform in transforms)
            {
                if (transform != null)
                    EditorUtility.SetDirty(transform);
            }

            previewChanges = false;
            previewStates.Clear();

            SceneView.RepaintAll();
        }

        private void ClearPreview()
        {
            if (!previewChanges)
                return;

            foreach (GameObject go in foundObjects)
            {
                if (go == null)
                    continue;

                if (previewStates.TryGetValue(
                        go.GetInstanceID(),
                        out TransformState state))
                {
                    state.Restore(go.transform);
                }
            }

            previewStates.Clear();
            previewChanges = false;

            SceneView.RepaintAll();
        }

        private Transform[] GetValidTransforms()
        {
            var transforms = new List<Transform>();

            foreach (GameObject go in foundObjects)
            {
                if (go != null)
                    transforms.Add(go.transform);
            }

            return transforms.ToArray();
        }

        private void ApplyToObject(
            GameObject go,
            System.Random random)
        {
            Transform transform = go.transform;

            // Position
            Vector3 position = transform.localPosition;

            ApplyAxis(
                ref position.x,
                positionX,
                positionMode);

            ApplyAxis(
                ref position.y,
                positionY,
                positionMode);

            ApplyAxis(
                ref position.z,
                positionZ,
                positionMode);

            transform.localPosition = position;

            // Rotation
            Vector3 rotation = transform.localEulerAngles;

            if (randomRotation)
            {
                ApplyRange(
                    ref rotation.x,
                    rotationX,
                    rotationMode,
                    random);

                ApplyRange(
                    ref rotation.y,
                    rotationY,
                    rotationMode,
                    random);

                ApplyRange(
                    ref rotation.z,
                    rotationZ,
                    rotationMode,
                    random);
            }

            transform.localEulerAngles = rotation;

            // Scale
            Vector3 scale = transform.localScale;

            if (randomScale)
            {
                ApplyRange(
                    ref scale.x,
                    scaleXRandom,
                    scaleMode,
                    random);

                ApplyRange(
                    ref scale.y,
                    scaleYRandom,
                    scaleMode,
                    random);

                ApplyRange(
                    ref scale.z,
                    scaleZRandom,
                    scaleMode,
                    random);
            }
            else
            {
                ApplyAxis(
                    ref scale.x,
                    scaleX,
                    scaleMode);

                ApplyAxis(
                    ref scale.y,
                    scaleY,
                    scaleMode);

                ApplyAxis(
                    ref scale.z,
                    scaleZ,
                    scaleMode);
            }

            transform.localScale = scale;
        }

        private static void ApplyAxis(
            ref float current,
            AxisFloat axis,
            ValueMode mode)
        {
            if (!axis.enabled)
                return;

            switch (mode)
            {
                case ValueMode.Add:
                case ValueMode.Relative:
                    current += axis.value;
                    break;

                case ValueMode.Multiply:
                    current *= axis.value;
                    break;

                case ValueMode.Absolute:
                    current = axis.value;
                    break;
            }
        }

        private static void ApplyRange(
            ref float current,
            AxisRange axis,
            ValueMode mode,
            System.Random random)
        {
            if (!axis.enabled)
                return;

            float randomValue = Mathf.Lerp(
                axis.min,
                axis.max,
                (float)random.NextDouble());

            switch (mode)
            {
                case ValueMode.Add:
                case ValueMode.Relative:
                    current += randomValue;
                    break;

                case ValueMode.Multiply:
                    current *= randomValue;
                    break;

                case ValueMode.Absolute:
                    current = randomValue;
                    break;
            }
        }

        private void SaveNewPreset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Object Batch Preset",
                "ObjectBatchPreset",
                "asset",
                "Choose where to save the preset.");

            if (string.IsNullOrEmpty(path))
                return;

            ObjectBatchPreset newPreset =
                CreateInstance<ObjectBatchPreset>();

            CopySettingsToPreset(newPreset);

            AssetDatabase.CreateAsset(newPreset, path);
            AssetDatabase.SaveAssets();

            preset = newPreset;
            EditorGUIUtility.PingObject(preset);
        }

        private void SaveToPreset()
        {
            if (preset == null)
                return;

            Undo.RecordObject(preset, "Save Object Batch Preset");

            CopySettingsToPreset(preset);

            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssets();
        }

        private void LoadPreset()
        {
            if (preset == null)
                return;

            ClearPreview();

            positionMode = preset.positionMode;
            positionX = preset.positionX;
            positionY = preset.positionY;
            positionZ = preset.positionZ;

            randomRotation = preset.randomRotation;
            rotationMode = preset.rotationMode;
            rotationX = preset.rotationX;
            rotationY = preset.rotationY;
            rotationZ = preset.rotationZ;

            randomScale = preset.randomScale;
            scaleMode = preset.scaleMode;
            scaleX = preset.scaleX;
            scaleY = preset.scaleY;
            scaleZ = preset.scaleZ;

            scaleXRandom = preset.scaleXRandom;
            scaleYRandom = preset.scaleYRandom;
            scaleZRandom = preset.scaleZRandom;

            useSeed = preset.useSeed;
            seed = preset.seed;

            Repaint();
        }

        private void CopySettingsToPreset(
            ObjectBatchPreset target)
        {
            target.positionMode = positionMode;

            // Position uses the three axis values.
            // The preset stores them separately through the struct fields.
            target.positionX = positionX;
            target.positionY = positionY;
            target.positionZ = positionZ;

            target.randomRotation = randomRotation;
            target.rotationMode = rotationMode;
            target.rotationX = rotationX;
            target.rotationY = rotationY;
            target.rotationZ = rotationZ;

            target.randomScale = randomScale;
            target.scaleMode = scaleMode;

            target.scaleX = scaleX;
            target.scaleY = scaleY;
            target.scaleZ = scaleZ;

            target.scaleXRandom = scaleXRandom;
            target.scaleYRandom = scaleYRandom;
            target.scaleZRandom = scaleZRandom;

            target.useSeed = useSeed;
            target.seed = seed;
        }

        private void ResetSettings()
        {
            ClearPreview();

            positionMode = ValueMode.Add;
            positionX = default;
            positionY = default;
            positionZ = default;

            randomRotation = false;
            rotationMode = ValueMode.Relative;
            rotationX = default;
            rotationY = default;
            rotationZ = default;

            randomScale = false;
            scaleMode = ValueMode.Multiply;
            scaleX = default;
            scaleY = default;
            scaleZ = default;

            scaleXRandom = default;
            scaleYRandom = default;
            scaleZRandom = default;

            useSeed = false;
            seed = 12345;

            Repaint();
        }

        private void DrawAxisFloat(
            string label,
            ref AxisFloat axis)
        {
            EditorGUILayout.BeginHorizontal();

            axis.enabled = EditorGUILayout.Toggle(
                axis.enabled,
                GUILayout.Width(18));

            EditorGUILayout.LabelField(
                label,
                GUILayout.Width(18));

            if (axis.enabled)
            {
                axis.value = EditorGUILayout.FloatField(axis.value);
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.FloatField(0f);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAxisRange(
            string label,
            ref AxisRange axis)
        {
            EditorGUILayout.BeginHorizontal();

            axis.enabled = EditorGUILayout.Toggle(
                axis.enabled,
                GUILayout.Width(18));

            EditorGUILayout.LabelField(
                label,
                GUILayout.Width(18));

            using (new EditorGUI.DisabledScope(!axis.enabled))
            {
                axis.min = EditorGUILayout.FloatField(
                    new GUIContent("Min"),
                    axis.min);

                axis.max = EditorGUILayout.FloatField(
                    new GUIContent("Max"),
                    axis.max);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void BeginSection(string title)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, headerStyle);
        }

        private void EndSection()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        private void EnsureStyles()
        {
            headerStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 13
            };

            boxHeaderStyle ??= new GUIStyle(EditorStyles.boldLabel);
        }

        [Serializable]
        private struct TransformState
        {
            private Vector3 position;
            private Vector3 rotation;
            private Vector3 scale;

            public TransformState(Transform transform)
            {
                position = transform.localPosition;
                rotation = transform.localEulerAngles;
                scale = transform.localScale;
            }

            public void Restore(Transform transform)
            {
                transform.localPosition = position;
                transform.localEulerAngles = rotation;
                transform.localScale = scale;
            }
        }
    }
}
#endif
