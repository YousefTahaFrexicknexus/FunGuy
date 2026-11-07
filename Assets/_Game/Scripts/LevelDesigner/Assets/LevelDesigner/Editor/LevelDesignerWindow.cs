#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LevelDesignerTools
{
    public sealed partial class LevelDesignerWindow : EditorWindow
    {
        [Flags] public enum MatchRule { None = 0, PrefabSource = 1, Name = 2, SharedMesh = 4, ComponentType = 8 }
        public enum TargetMode { FindSimilar, CurrentSelection }
        public enum Combine { All, Any }
        public enum RotationMode { Set, Offset }
        public enum ScaleMode { Add, Multiply, RandomSet, RandomMultiply }
        public enum PositionMode { Set, AddOffset, RandomOffset }
        [Serializable] public class Axes
        {
            public bool x, y = true, z;
            public bool Any => x || y || z;
            public bool Has(int i) => i == 0 ? x : i == 1 ? y : z;
        }
        [Serializable] public class Settings
        {
            public int version = 1;
            public TargetMode targetMode;
            public MatchRule rules = MatchRule.PrefabSource;
            public Combine combine;
            public bool includeRoot, includeInactive = true, normalizeNames = true;
            public bool topLevelOnly = true;
            public string componentType = "";
            public RotationMode rotationMode;
            public Space rotationSpace = Space.Self;
            public Axes rotationAxes = new Axes();
            public Vector3 rotationMin = Vector3.zero, rotationMax = Vector3.one * 360f;
            public ScaleMode scaleMode = ScaleMode.Multiply;
            public Axes scaleAxes = new Axes { x = true, y = true, z = true };
            public Vector3 scaleValue = Vector3.one;
            public Vector3 scaleMin = Vector3.one * 0.8f, scaleMax = Vector3.one * 1.2f;
            public bool uniformScale = true;
            public float uniformMin = 0.8f, uniformMax = 1.2f;
            public PositionMode positionMode = PositionMode.AddOffset;
            public Space positionSpace = Space.World;
            public Axes positionAxes = new Axes { x = true, y = false, z = true };
            public Vector3 positionValue, positionMin = -Vector3.one, positionMax = Vector3.one;
            public bool fixedSeed;
            public int seed = 12345;
            public float rotationStep;
            public bool simpleScale = true;
            public float scaleAmount = 1.2f;
            public int activeTab;
        }
        [SerializeField] Settings settings = new Settings();
        [SerializeField] GameObject reference;
        [SerializeField] Transform hierarchyRoot;
        [SerializeField] List<GameObject> targets = new List<GameObject>();
        [SerializeField] string searchSignature = "";
        Vector2 scroll;
        string message = "Choose a reference and hierarchy root, then click Find Similar.";
        bool showTargets, advancedMatching, precision, showPresets, highlight = true;
        int targetPage, presetIndex;
        string presetName = "My variation";
        [SerializeField] List<GameObject> excluded = new List<GameObject>();
        [Serializable] class SavedPreset { public string name, json; }
        [Serializable] class PresetCollection { public List<SavedPreset> items = new List<SavedPreset>(); }
        PresetCollection customPresets = new PresetCollection();
        string PresetPath => Path.Combine("ProjectSettings", "LevelDesignerPresets.json");
        sealed class Pose
        {
            public Transform t, parent;
            public Vector3 position, scale;
            public Quaternion rotation;
            public Pose(Transform value) { t = value; parent = t.parent; position = t.localPosition; scale = t.localScale; rotation = t.localRotation; }
            public bool Unchanged => t && t.parent == parent && t.localPosition.Equals(position) && t.localScale.Equals(scale) && t.localRotation.Equals(rotation);
        }
        sealed class Ghost { public Mesh mesh; public Matrix4x4 matrix; public Bounds bounds; public bool ownsMesh; }
        List<Ghost> ghosts = new List<Ghost>();
        List<Pose> originalPoses = new List<Pose>();
        List<GameObject> previewObjects;
        List<Vector3> previewValues;
        int previewOperation;
        Material previewMaterial;
        string previewSettings, previewSearchSignature;
        int shuffleIndex;
        bool HasPreview => previewObjects != null;

        const string PrefKey = "LevelDesignerTools.Settings.v2";

        [MenuItem("Tools/Level Designer/Batch Transform")]
        static void Open() => GetWindow<LevelDesignerWindow>("Level Designer");
        void OnEnable()
        {
            minSize = new Vector2(390, 540);
            try { if (EditorPrefs.HasKey(PrefKey)) settings = JsonUtility.FromJson<Settings>(EditorPrefs.GetString(PrefKey)) ?? new Settings(); }
            catch { settings = new Settings(); }
            try { ValidateSettings(settings); settings.activeTab = Mathf.Clamp(settings.activeTab, 0, 2); }
            catch { settings = new Settings(); }
            Selection.selectionChanged += Repaint;
            EditorApplication.hierarchyChanged += HierarchyChanged;
            Undo.undoRedoPerformed += OnUndo;
            SceneView.duringSceneGui += DrawScene;
            EditorApplication.playModeStateChanged += OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload += ClearPreview;
            try { if (File.Exists(PresetPath)) customPresets = JsonUtility.FromJson<PresetCollection>(File.ReadAllText(PresetPath)) ?? new PresetCollection(); }
            catch (Exception e) { message = "Could not read saved presets: " + e.Message; }
            if (customPresets.items == null) customPresets.items = new List<SavedPreset>();
        }
        void OnDisable()
        {
            EditorPrefs.SetString(PrefKey, JsonUtility.ToJson(settings));
            Selection.selectionChanged -= Repaint;
            EditorApplication.hierarchyChanged -= HierarchyChanged;
            Undo.undoRedoPerformed -= OnUndo;
            SceneView.duringSceneGui -= DrawScene;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            AssemblyReloadEvents.beforeAssemblyReload -= ClearPreview;
            ClearPreview();
            if (previewMaterial) DestroyImmediate(previewMaterial);
        }
        void HierarchyChanged() { searchSignature = ""; if (HasPreview) message = "Hierarchy changed. Refresh your targets and preview again."; ClearPreview(); Repaint(); }
        void OnUndo() { ClearPreview(); Repaint(); }
        void OnPlayMode(PlayModeStateChange state) { ClearPreview(); Repaint(); }
        static bool SceneObject(GameObject go)
        {
            return go && !EditorUtility.IsPersistent(go) && go.scene.IsValid() && go.scene.isLoaded
                && !EditorSceneManager.IsPreviewScene(go.scene) && PrefabStageUtility.GetPrefabStage(go) == null
                && (go.hideFlags & HideFlags.NotEditable) == 0
                && (go.transform.hideFlags & HideFlags.NotEditable) == 0;
        }
        string Signature() => $"{reference?.GetInstanceID()}|{hierarchyRoot?.GetInstanceID()}|{settings.rules}|{settings.combine}|{settings.includeRoot}|{settings.includeInactive}|{settings.normalizeNames}|{settings.componentType}";
        static void Heading(string title, string detail)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(detail)) EditorGUILayout.LabelField(detail, EditorStyles.wordWrappedMiniLabel);
        }
        static Space SpacePicker(string label, Space value) => EditorGUILayout.Popup(label, value == Space.World ? 0 : 1, new[] { "World", "Relative to Parent" }) == 0 ? Space.World : Space.Self;
        void OnGUI()
        {
            if (HasPreview && (!PreviewValid() || previewSettings != JsonUtility.ToJson(settings))) { ClearPreview(); message = "Objects or settings changed. Preview again to see the new result."; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            Heading("LEVEL DESIGNER", "Choose objects. Try a variation. Make it yours.");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Heading("1  Choose objects", "Your working set stays captured while you inspect the scene.");
            EditorGUI.BeginChangeCheck();
            settings.targetMode = (TargetMode)GUILayout.Toolbar((int)settings.targetMode, new[] { "Find Similar", "Use Selection" });
            if (EditorGUI.EndChangeCheck()) { ClearPreview(); targets.Clear(); excluded.Clear(); }
            if (settings.targetMode == TargetMode.FindSimilar) DrawMatching();
            else
            {
                EditorGUILayout.HelpBox("Select objects in the Hierarchy or Scene, then capture them here. Only those objects will be edited.", MessageType.None);
                if (GUILayout.Button("Capture Selected Objects", GUILayout.Height(28)))
                {
                    ClearPreview(); targets = Selection.gameObjects.Where(SceneObject).Distinct().ToList(); excluded.Clear(); targetPage = 0;
                    message = $"Captured {targets.Count} objects. You can now change the Unity selection freely.";
                }
            }
            settings.topLevelOnly = EditorGUILayout.Toggle(new GUIContent("Avoid editing children twice", "If parent and child are in the list, edit only the parent. Children still inherit its changes."), settings.topLevelOnly);
            var usable = GetTargets();
            EditorGUILayout.LabelField($"{usable.Count} objects ready to edit", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(usable.Count == 0))
                {
                    if (GUILayout.Button("Frame Objects")) FrameTargets(usable);
                    if (GUILayout.Button("Select in Scene")) Selection.objects = usable.Cast<Object>().ToArray();
                }
                bool newHighlight = GUILayout.Toggle(highlight, "Highlight", "Button");
                if (newHighlight != highlight) { highlight = newHighlight; SceneView.RepaintAll(); }
            }
            showTargets = EditorGUILayout.Foldout(showTargets, $"Review objects / exclude ({excluded.Count} excluded)", true);
            if (showTargets) DrawTargetList();
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Heading("2  Adjust", "Only the active tab is applied. Unchecked axes stay unchanged.");
            DrawPresets();
            EditorGUI.BeginChangeCheck();
            settings.activeTab = GUILayout.Toolbar(Mathf.Clamp(settings.activeTab, 0, 2), new[] { "Rotate", "Scale", "Move" });
            if (settings.activeTab == 0) DrawRotation();
            else if (settings.activeTab == 1) DrawScale();
            else DrawPosition();
            precision = EditorGUILayout.Foldout(precision, "Randomness options", true);
            if (precision)
            {
                settings.fixedSeed = EditorGUILayout.Toggle("Repeatable variations", settings.fixedSeed);
                if (settings.fixedSeed) settings.seed = EditorGUILayout.IntField("Variation seed", settings.seed);
                EditorGUILayout.LabelField("Shuffle tries the next variation; edits never stack during preview.", EditorStyles.wordWrappedMiniLabel);
            }
            if (EditorGUI.EndChangeCheck()) { ClearPreview(); shuffleIndex = 0; message = "Settings updated. Preview Changes to try them."; }
            EditorGUILayout.EndVertical();
            Heading("3  Preview & apply", Summary(usable.Count));
            bool stale = settings.targetMode == TargetMode.FindSimilar && searchSignature != Signature();
            string block = EditorApplication.isPlayingOrWillChangePlaymode ? "Exit Play Mode to edit your level." : stale ? "Click Find Similar Objects to refresh your working set." : usable.Count == 0 ? "Choose some objects above to get started." : !ActiveAxes().Any ? "Choose at least one axis to adjust." : "";
            if (block.Length > 0) EditorGUILayout.HelpBox(block, MessageType.Info);
            using (new EditorGUI.DisabledScope(block.Length > 0))
            {
                if (!HasPreview)
                {
                    if (GUILayout.Button("Preview Changes", GUILayout.Height(34))) Preview(false);
                }
                else
                {
                    EditorGUILayout.HelpBox("Cyan shapes show the proposed result in Scene view. Original objects are untouched until Apply. Non-mesh objects use bounds.", MessageType.Info);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Shuffle", GUILayout.Height(30))) Preview(true);
                        if (GUILayout.Button("Apply Changes", GUILayout.Height(30))) CommitPreview();
                        if (GUILayout.Button("Cancel", GUILayout.Height(30))) { ClearPreview(); message = "Preview cancelled. Nothing was changed."; }
                    }
                }
            }
            EditorGUILayout.HelpBox(message, MessageType.None);
            EditorGUILayout.LabelField("Scene instances only • One Undo step per Apply", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.EndScrollView();
        }
        Axes ActiveAxes() => settings.activeTab == 0 ? settings.rotationAxes : settings.activeTab == 1 ? settings.scaleAxes : settings.positionAxes;
        string Summary(int count)
        {
            string axes = string.Join(" / ", new[] { settings.activeTab == 0 ? settings.rotationAxes.x : settings.activeTab == 1 ? settings.scaleAxes.x : settings.positionAxes.x,
                settings.activeTab == 0 ? settings.rotationAxes.y : settings.activeTab == 1 ? settings.scaleAxes.y : settings.positionAxes.y,
                settings.activeTab == 0 ? settings.rotationAxes.z : settings.activeTab == 1 ? settings.scaleAxes.z : settings.positionAxes.z }.Select((v,i) => v ? new[]{"X","Y","Z"}[i] : null).Where(v => v != null));
            string action = settings.activeTab == 0 ? (settings.rotationMode == RotationMode.Set ? "Random rotation" : "Random tilt") : settings.activeTab == 1 ? new[]{"Increase size", "Multiply size", "Random size", "Random size multiplier"}[(int)settings.scaleMode] : new[]{"Set position", "Move by offset", "Random position offset"}[(int)settings.positionMode];
            return $"{action} · {axes} · {count} objects";
        }
        void DrawMatching()
        {
            reference = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Example Object", "Choose an object you want to find more of."), reference, typeof(GameObject), true);
            hierarchyRoot = (Transform)EditorGUILayout.ObjectField(new GUIContent("Search Inside", "Only this parent's hierarchy is searched."), hierarchyRoot, typeof(Transform), true);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Active as Example")) reference = Selection.activeGameObject;
                if (GUILayout.Button("Use Active as Parent") && Selection.activeGameObject) hierarchyRoot = Selection.activeGameObject.transform;
            }
            if (hierarchyRoot) EditorGUILayout.LabelField("Searching inside: " + hierarchyRoot.name, EditorStyles.miniLabel);
            advancedMatching = EditorGUILayout.Foldout(advancedMatching, "Advanced matching", true);
            if (!advancedMatching)
            {
                var choices = new[] { MatchRule.PrefabSource, MatchRule.Name, MatchRule.SharedMesh };
                int current = Array.IndexOf(choices, settings.rules);
                if (current < 0) EditorGUILayout.LabelField("Custom combination enabled — expand Advanced matching.", EditorStyles.wordWrappedMiniLabel);
                else settings.rules = choices[EditorGUILayout.Popup("Find objects with", current, new[] { "Same Prefab", "Same Name", "Same 3D Model" })];
            }
            else
            {
                RuleToggle("Same Prefab", MatchRule.PrefabSource);
                RuleToggle("Same Name", MatchRule.Name);
                RuleToggle("Same 3D Model", MatchRule.SharedMesh);
                RuleToggle("Same Component", MatchRule.ComponentType);
                settings.combine = (Combine)EditorGUILayout.Popup("Require", (int)settings.combine, new[]{"All checked rules", "Any checked rule"});
                if ((settings.rules & MatchRule.ComponentType) != 0) DrawComponentPicker();
            }
            settings.includeRoot = EditorGUILayout.Toggle("Include the parent itself", settings.includeRoot);
            settings.includeInactive = EditorGUILayout.Toggle("Include inactive objects", settings.includeInactive);
            if ((settings.rules & MatchRule.Name) != 0) settings.normalizeNames = EditorGUILayout.Toggle("Ignore copy numbers / (Clone)", settings.normalizeNames);
            using (new EditorGUI.DisabledScope(!reference || !hierarchyRoot || settings.rules == MatchRule.None))
                if (GUILayout.Button("Find Similar Objects", GUILayout.Height(28))) { ClearPreview(); excluded.Clear(); targetPage = 0; FindSimilar(); }
            if (!reference || !hierarchyRoot) EditorGUILayout.LabelField("Choose an example and a parent to search inside.", EditorStyles.wordWrappedMiniLabel);
        }
        void RuleToggle(string label, MatchRule rule)
        {
            bool value = EditorGUILayout.Toggle(label, (settings.rules & rule) != 0);
            if (value) settings.rules |= rule; else settings.rules &= ~rule;
        }
        void DrawComponentPicker()
        {
            var types = reference ? reference.GetComponents<Component>().Where(c => c && !(c is Transform)).Select(c => c.GetType()).Distinct().ToArray() : Array.Empty<Type>();
            if (types.Length == 0) { EditorGUILayout.HelpBox("Choose an example with a component other than Transform.", MessageType.Info); return; }
            int index = Array.FindIndex(types, t => t.AssemblyQualifiedName == settings.componentType);
            index = EditorGUILayout.Popup("Component", Mathf.Max(0, index), types.Select(t => t.Name).ToArray());
            settings.componentType = types[index].AssemblyQualifiedName;
        }
        void DrawTargetList()
        {
            var list = targets.Where(SceneObject).ToList();
            int pages = Mathf.Max(1, Mathf.CeilToInt(list.Count / 20f)); targetPage = Mathf.Clamp(targetPage, 0, pages - 1);
            foreach (var go in list.Skip(targetPage * 20).Take(20))
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool include = !excluded.Contains(go);
                    bool next = GUILayout.Toggle(include, "", GUILayout.Width(20));
                    if (next != include) { ClearPreview(); if (next) excluded.Remove(go); else excluded.Add(go); }
                    EditorGUILayout.ObjectField(go, typeof(GameObject), true);
                }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(targetPage == 0)) if (GUILayout.Button("Previous")) targetPage--;
                GUILayout.Label($"{targetPage + 1} / {pages}", EditorStyles.centeredGreyMiniLabel);
                using (new EditorGUI.DisabledScope(targetPage >= pages - 1)) if (GUILayout.Button("Next")) targetPage++;
                if (GUILayout.Button("Include All")) { excluded.Clear(); ClearPreview(); }
            }
            EditorGUILayout.LabelField("Excluded children still inherit changes made to their parent.", EditorStyles.wordWrappedMiniLabel);
        }
        static void DrawAxes(Axes axes)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel("Change axes");
                axes.x = GUILayout.Toggle(axes.x, "X", "Button");
                axes.y = GUILayout.Toggle(axes.y, "Y", "Button");
                axes.z = GUILayout.Toggle(axes.z, "Z", "Button");
            }
        }
        void DrawRotation()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Random Facing")) { settings.rotationAxes = new Axes(); settings.rotationMin = Vector3.zero; settings.rotationMax = Vector3.one * 360; settings.rotationMode = RotationMode.Set; settings.rotationStep = 0; }
                if (GUILayout.Button("Slight Tilt")) { settings.rotationAxes = new Axes { x = true, y = false, z = true }; settings.rotationMin = Vector3.one * -8; settings.rotationMax = Vector3.one * 8; settings.rotationMode = RotationMode.Offset; settings.rotationStep = 0; }
            }
            settings.rotationMode = (RotationMode)EditorGUILayout.Popup("How to rotate", (int)settings.rotationMode, new[]{"Choose a new angle", "Add to current angle"});
            DrawAxes(settings.rotationAxes);
            settings.rotationMin = EditorGUILayout.Vector3Field("Smallest angle (degrees)", settings.rotationMin);
            settings.rotationMax = EditorGUILayout.Vector3Field("Largest angle (degrees)", settings.rotationMax);
            settings.rotationStep = EditorGUILayout.FloatField(new GUIContent("Angle steps (0 = free)", "Use 15, 45 or 90 for modular pieces. Samples multiples of this angle inside your range."), settings.rotationStep);
            settings.rotationSpace = SpacePicker("Rotate relative to", settings.rotationSpace);
        }
        void DrawScale()
        {
            settings.scaleMode = (ScaleMode)EditorGUILayout.Popup("How to resize", (int)settings.scaleMode, new[]{"Increase by", "Multiply by", "Random size (replace)", "Random size (multiply)"});
            bool random = settings.scaleMode == ScaleMode.RandomSet || settings.scaleMode == ScaleMode.RandomMultiply;
            settings.simpleScale = EditorGUILayout.Toggle(new GUIContent("Use one value for all axes", "Multiplying all axes by the same value keeps the object's proportions."), settings.simpleScale);
            if (settings.simpleScale)
            {
                settings.scaleAxes = new Axes { x = true, y = true, z = true };
                settings.uniformScale = true;
                if (random)
                {
                    settings.uniformMin = EditorGUILayout.FloatField("Smallest value", settings.uniformMin);
                    settings.uniformMax = EditorGUILayout.FloatField("Largest value", settings.uniformMax);
                }
                else
                {
                    settings.scaleAmount = EditorGUILayout.FloatField(settings.scaleMode == ScaleMode.Add ? "Increase each axis by" : "Size multiplier", settings.scaleAmount);
                    settings.scaleValue = Vector3.one * settings.scaleAmount;
                }
            }
            else
            {
                DrawAxes(settings.scaleAxes);
                if (random)
                {
                    settings.uniformScale = EditorGUILayout.Toggle("Same random value on checked axes", settings.uniformScale);
                    if (settings.uniformScale) { settings.uniformMin = EditorGUILayout.FloatField("Smallest", settings.uniformMin); settings.uniformMax = EditorGUILayout.FloatField("Largest", settings.uniformMax); }
                    else { settings.scaleMin = EditorGUILayout.Vector3Field("Smallest values", settings.scaleMin); settings.scaleMax = EditorGUILayout.Vector3Field("Largest values", settings.scaleMax); }
                }
                else settings.scaleValue = EditorGUILayout.Vector3Field("Values", settings.scaleValue);
            }
            EditorGUILayout.LabelField("Multiply: 1.2 = 20% larger · 0.8 = 20% smaller. Add and Replace can change proportions.", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Natural Size Variation")) { settings.scaleMode = ScaleMode.RandomMultiply; settings.uniformScale = true; settings.uniformMin = .8f; settings.uniformMax = 1.2f; }
                if (GUILayout.Button("Set up +1")) { settings.scaleMode = ScaleMode.Add; settings.scaleAmount = 1; settings.scaleValue = Vector3.one; }
            }
        }
        void DrawPosition()
        {
            settings.positionMode = (PositionMode)EditorGUILayout.Popup("How to move", (int)settings.positionMode, new[]{"Set position", "Move by offset", "Random offset"});
            DrawAxes(settings.positionAxes);
            if (settings.positionMode == PositionMode.RandomOffset)
            {
                settings.positionMin = EditorGUILayout.Vector3Field("Smallest offset", settings.positionMin);
                settings.positionMax = EditorGUILayout.Vector3Field("Largest offset", settings.positionMax);
            }
            else settings.positionValue = EditorGUILayout.Vector3Field("Distance / position", settings.positionValue);
            settings.positionSpace = SpacePicker("Move relative to", settings.positionSpace);
            EditorGUILayout.LabelField("Relative to Parent uses the same coordinates as the Transform Inspector.", EditorStyles.wordWrappedMiniLabel);
        }
        static Object Source(GameObject go) => EditorUtility.IsPersistent(go) ? go : PrefabUtility.GetCorrespondingObjectFromSource(go);
        static HashSet<Mesh> Meshes(GameObject go)
        {
            var result = new HashSet<Mesh>();
            foreach (var f in go.GetComponents<MeshFilter>()) if (f.sharedMesh) result.Add(f.sharedMesh);
            foreach (var s in go.GetComponents<SkinnedMeshRenderer>()) if (s.sharedMesh) result.Add(s.sharedMesh);
            return result;
        }
        string MatchName(string value) => settings.normalizeNames ? Regex.Replace(value, @"(?:\s*\((?:Clone|\d+)\))+$", "").TrimEnd() : value;
        void FindSimilar()
        {
            if (!reference || !hierarchyRoot || !SceneObject(hierarchyRoot.gameObject)) { message = "Assign a reference and a normal scene hierarchy root."; return; }
            if (settings.rules == MatchRule.None) { message = "Enable at least one matching rule."; return; }
            Type type = Type.GetType(settings.componentType ?? "", false);
            if ((settings.rules & MatchRule.ComponentType) != 0 && (type == null || !typeof(Component).IsAssignableFrom(type) || !reference.GetComponent(type)))
            { message = "Choose a valid component type from the reference."; return; }
            Object source = Source(reference);
            var meshes = Meshes(reference);
            string nameKey = MatchName(reference.name);
            var result = new List<GameObject>();
            foreach (var t in hierarchyRoot.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if ((!settings.includeRoot && t == hierarchyRoot) || (!settings.includeInactive && !go.activeInHierarchy) || !SceneObject(go)) continue;
                var tests = new List<bool>();
                if ((settings.rules & MatchRule.PrefabSource) != 0) tests.Add(source && Source(go) == source);
                if ((settings.rules & MatchRule.Name) != 0) tests.Add(MatchName(go.name) == nameKey);
                if ((settings.rules & MatchRule.SharedMesh) != 0) tests.Add(meshes.Count > 0 && meshes.Overlaps(Meshes(go)));
                if ((settings.rules & MatchRule.ComponentType) != 0) tests.Add(go.GetComponent(type) != null);
                if (tests.Count > 0 && (settings.combine == Combine.All ? tests.All(v => v) : tests.Any(v => v))) result.Add(go);
            }
            targets = result;
            searchSignature = Signature();
            Selection.objects = result.Cast<Object>().ToArray();
            message = result.Count == 0 ? "No matches found. Try Same Name, check your search parent, or include inactive objects." : $"Found {result.Count} matching objects. Choose a tab below to try a variation.";
        }
        List<GameObject> GetTargets()
        {
            IEnumerable<GameObject> source = targets;
            var result = source.Where(SceneObject).Where(g => !excluded.Contains(g)).Distinct().ToList();
            if (settings.targetMode == TargetMode.FindSimilar)
                result = result.Where(g => hierarchyRoot && (g.transform == hierarchyRoot ? settings.includeRoot : g.transform.IsChildOf(hierarchyRoot)) && (settings.includeInactive || g.activeInHierarchy)).ToList();
            if (settings.topLevelOnly)
            {
                var set = new HashSet<Transform>(result.Select(g => g.transform));
                result = result.Where(g => { for (var p = g.transform.parent; p; p = p.parent) if (set.Contains(p)) return false; return true; }).ToList();
            }
            // Parent-first applies world-space results correctly even when both parent and child are edited.
            return result.OrderBy(g => Depth(g.transform)).ThenBy(g => GlobalObjectId.GetGlobalObjectIdSlow(g).ToString(), StringComparer.Ordinal).ToList();
        }
        static int Depth(Transform t) { int d = 0; while (t.parent) { d++; t = t.parent; } return d; }
        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static float Sample(System.Random rng, float a, float b)
        {
            if (!Finite(a) || !Finite(b)) throw new ArgumentException("All values must be finite numbers.");
            if (a > b) throw new ArgumentException("Minimum must not exceed maximum on an affected axis.");
            return (float)((double)a + ((double)b - a) * rng.NextDouble());
        }
        List<Vector3> Calculate(int operation, List<GameObject> objects, int randomSeed)
        {
            var rng = new System.Random(randomSeed);
            var values = new List<Vector3>();
            try
            {
                // Snapshot and validate the complete batch before modifying any object.
                foreach (var go in objects)
                {
                    var t = go.transform;
                    Vector3 v;
                    Axes axes;
                    if (operation == 0)
                    {
                        axes = settings.rotationAxes;
                        v = settings.rotationSpace == Space.World ? t.eulerAngles : t.localEulerAngles;
                        for (int i = 0; i < 3; i++) if (axes.Has(i))
                        {
                            float r = SampleAngle(rng, settings.rotationMin[i], settings.rotationMax[i], settings.rotationStep);
                            v[i] = settings.rotationMode == RotationMode.Set ? r : v[i] + r;
                        }
                    }
                    else if (operation == 2)
                    {
                        axes = settings.positionAxes;
                        v = settings.positionSpace == Space.World ? t.position : t.localPosition;
                        for (int i = 0; i < 3; i++) if (axes.Has(i))
                        {
                            float amount = settings.positionMode == PositionMode.RandomOffset ? Sample(rng, settings.positionMin[i], settings.positionMax[i]) : settings.positionValue[i];
                            v[i] = settings.positionMode == PositionMode.Set ? amount : v[i] + amount;
                        }
                    }
                    else
                    {
                        axes = settings.scaleAxes; v = t.localScale;
                        bool random = operation != 3 && (settings.scaleMode == ScaleMode.RandomSet || settings.scaleMode == ScaleMode.RandomMultiply);
                        float uniform = random && settings.uniformScale ? Sample(rng, settings.uniformMin, settings.uniformMax) : 1;
                        for (int i = 0; i < 3; i++) if (axes.Has(i))
                        {
                            float amount = operation == 3 ? 1 : random ? (settings.uniformScale ? uniform : Sample(rng, settings.scaleMin[i], settings.scaleMax[i])) : settings.scaleValue[i];
                            if (operation == 3 || settings.scaleMode == ScaleMode.Add) v[i] += amount;
                            else if (settings.scaleMode == ScaleMode.RandomSet) v[i] = amount;
                            else v[i] *= amount;
                        }
                    }
                    if (!axes.Any) throw new ArgumentException("Choose at least one axis.");
                    if (!Finite(v)) throw new ArgumentException("The operation would produce a non-finite transform. Reduce the values.");
                    values.Add(v);
                }
            }
            catch { throw; }
            return values;
        }
        void Commit(int operation, List<GameObject> objects, List<Vector3> values)
        {
            string label = operation == 0 ? "Randomize Rotation" : operation == 2 ? "Change Position" : "Change Scale";
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Level Designer: " + label);
            Undo.RecordObjects(objects.Select(g => (Object)g.transform).ToArray(), label);
            try
            {
                for (int i = 0; i < objects.Count; i++)
                {
                    var t = objects[i].transform;
                    if (operation == 0)
                    {
                        if (settings.rotationSpace == Space.World) t.rotation = Quaternion.Euler(values[i]);
                        else t.localRotation = Quaternion.Euler(values[i]);
                    }
                    else if (operation == 2)
                    {
                        if (settings.positionSpace == Space.World) t.position = values[i];
                        else t.localPosition = values[i];
                    }
                    else t.localScale = values[i];
                    if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
                    EditorSceneManager.MarkSceneDirty(t.gameObject.scene);
                }
                Undo.FlushUndoRecordObjects();
                Undo.CollapseUndoOperations(group);
                message = $"{label}: changed {objects.Count} objects. Ctrl/Cmd+Z to undo.";
                SceneView.RepaintAll();
            }
            catch (Exception e)
            {
                Undo.FlushUndoRecordObjects();
                Undo.RevertAllDownToGroup(group);
                message = "Operation rolled back: " + e.Message;
                Debug.LogException(e);
            }
        }
        static float SampleAngle(System.Random rng, float min, float max, float step)
        {
            if (!Finite(step) || step < 0) throw new ArgumentException("Angle steps must be zero or positive.");
            if (step == 0) return Sample(rng, min, max);
            if (!Finite(min) || !Finite(max) || min > max) throw new ArgumentException("Smallest angle must not exceed largest angle.");
            double first = Math.Ceiling((double)min / step), last = Math.Floor((double)max / step);
            if (last < first) throw new ArgumentException("No angle step fits this range. Widen the range or reduce the step size.");
            return (float)((first + Math.Floor(rng.NextDouble() * (last - first + 1))) * step);
        }
        void DrawPresets()
        {
            showPresets = EditorGUILayout.Foldout(showPresets, "Quick starts & my presets", true);
            if (!showPresets) return;
            var builtins = new[]{"Trees — upright, natural sizes", "Rocks — varied angles and sizes", "Ground clutter — subtle variation", "Buildings — quarter-turn facing"};
            var names = builtins.Concat(customPresets.items.Select(p => p.name)).ToArray();
            presetIndex = EditorGUILayout.Popup("Start from", Mathf.Clamp(presetIndex, 0, names.Length - 1), names);
            if (GUILayout.Button("Load These Settings"))
            {
                ClearPreview();
                if (presetIndex < 4) BuiltinPreset(presetIndex);
                else
                {
                    try
                    {
                        var loaded = JsonUtility.FromJson<Settings>(customPresets.items[presetIndex - 4].json);
                        ValidateSettings(loaded);
                        // Presets adjust transforms; preserve the designer's current working set and search.
                        CopyAdjustments(loaded);
                    }
                    catch (Exception e) { message = "Could not load that preset: " + e.Message; }
                }
                shuffleIndex = 0;
            }
            presetName = EditorGUILayout.TextField("Save as", presetName);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(presetName)))
                if (GUILayout.Button("Save My Preset"))
                {
                    string name = presetName.Trim();
                    // Never silently overwrite another named preset.
                    string unique = name; int suffix = 2;
                    while (customPresets.items.Any(p => p.name == unique)) unique = name + " (" + suffix++ + ")";
                    var item = new SavedPreset { name = unique, json = JsonUtility.ToJson(settings) };
                    customPresets.items.Add(item);
                    try { Directory.CreateDirectory("ProjectSettings"); File.WriteAllText(PresetPath, JsonUtility.ToJson(customPresets, true)); presetIndex = customPresets.items.Count + 3; message = "Saved “" + unique + "” for this project."; }
                    catch (Exception e) { customPresets.items.Remove(item); message = "Preset could not be saved: " + e.Message; }
                }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Export File…")) SavePreset();
                if (GUILayout.Button("Import File…")) LoadPreset();
            }
            EditorGUILayout.LabelField("Presets fill Rotate, Scale and Move settings. Preview and apply one tab at a time.", EditorStyles.wordWrappedMiniLabel);
        }
        static void ValidateSettings(Settings loaded)
        {
            if (loaded == null || loaded.version != 1 || loaded.rotationAxes == null || loaded.scaleAxes == null || loaded.positionAxes == null)
                throw new ArgumentException("This is not a valid Level Designer preset.");
            if (!Enum.IsDefined(typeof(RotationMode), loaded.rotationMode) || !Enum.IsDefined(typeof(ScaleMode), loaded.scaleMode)
                || !Enum.IsDefined(typeof(PositionMode), loaded.positionMode) || !Enum.IsDefined(typeof(Space), loaded.rotationSpace)
                || !Enum.IsDefined(typeof(Space), loaded.positionSpace)) throw new ArgumentException("Unsupported preset options.");
        }
        void CopyAdjustments(Settings from)
        {
            from.targetMode = settings.targetMode; from.rules = settings.rules; from.combine = settings.combine;
            from.includeRoot = settings.includeRoot; from.includeInactive = settings.includeInactive; from.normalizeNames = settings.normalizeNames;
            from.componentType = settings.componentType; from.topLevelOnly = settings.topLevelOnly;
            settings = from; settings.activeTab = Mathf.Clamp(settings.activeTab, 0, 2);
            message = "Preset loaded. Choose a tab and preview the result.";
        }
        void BuiltinPreset(int index)
        {
            var next = new Settings { scaleMode = ScaleMode.RandomMultiply, positionMode = PositionMode.RandomOffset };
            if (index == 0) { next.uniformMin = .85f; next.uniformMax = 1.15f; }
            if (index == 1) { next.rotationAxes = new Axes { x = true, y = true, z = true }; next.uniformMin = .75f; next.uniformMax = 1.25f; }
            if (index == 2) { next.uniformMin = .9f; next.uniformMax = 1.1f; next.positionMin = new Vector3(-.2f, 0, -.2f); next.positionMax = new Vector3(.2f, 0, .2f); }
            if (index == 3) { next.rotationStep = 90; next.rotationMax = Vector3.one * 270; next.uniformMin = next.uniformMax = 1; }
            CopyAdjustments(next);
        }
        bool PreviewValid()
        {
            return HasPreview && !EditorApplication.isPlayingOrWillChangePlaymode
                && previewObjects.All(SceneObject) && originalPoses.All(p => p.Unchanged)
                && previewObjects.SequenceEqual(GetTargets())
                && (settings.targetMode != TargetMode.FindSimilar || previewSearchSignature == Signature());
        }
        void ClearPreview()
        {
            foreach (var ghost in ghosts) if (ghost.ownsMesh && ghost.mesh) DestroyImmediate(ghost.mesh);
            ghosts.Clear(); originalPoses.Clear(); previewObjects = null; previewValues = null;
            SceneView.RepaintAll();
        }
        void Preview(bool shuffle)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (settings.targetMode == TargetMode.FindSimilar && searchSignature != Signature()) return;
            ClearPreview();
            if (shuffle) shuffleIndex++;
            var objects = GetTargets();
            if (objects.Count == 0) return;
            try
            {
                int seed = settings.fixedSeed ? unchecked(settings.seed + shuffleIndex) : Guid.NewGuid().GetHashCode();
                var values = Calculate(settings.activeTab, objects, seed);
                previewOperation = settings.activeTab;
                BuildGhosts(objects, values);
                previewObjects = objects; previewValues = values;
                previewSettings = JsonUtility.ToJson(settings); previewSearchSignature = Signature();
                message = "Preview ready. Cyan shows the result; the original objects have not changed.";
                SceneView.RepaintAll();
            }
            catch (Exception e) { ClearPreview(); message = "Check your settings: " + e.Message; }
        }
        void CommitPreview()
        {
            if (!PreviewValid() || previewSettings != JsonUtility.ToJson(settings)) { ClearPreview(); message = "Objects or settings changed. Preview again before applying."; return; }
            var objects = previewObjects; var values = previewValues; int operation = previewOperation;
            ClearPreview();
            Commit(operation, objects, values);
        }
        void BuildGhosts(List<GameObject> objects, List<Vector3> values)
        {
            var proposals = new Dictionary<Transform, Vector3>();
            for (int i = 0; i < objects.Count; i++) proposals[objects[i].transform] = values[i];
            var matrices = new Dictionary<Transform, Matrix4x4>();
            var rotations = new Dictionary<Transform, Quaternion>();
            var watched = new HashSet<Transform>();
            Action<Transform> watch = t => { for (var p = t; p; p = p.parent) watched.Add(p); };
            Func<Transform, Matrix4x4> world = null;
            world = t =>
            {
                if (!t) return Matrix4x4.identity;
                if (matrices.TryGetValue(t, out var cached)) return cached;
                Matrix4x4 parentMatrix = world(t.parent);
                Quaternion parentRotation = t.parent ? rotations[t.parent] : Quaternion.identity;
                Vector3 pos = t.localPosition, scale = t.localScale; Quaternion rot = t.localRotation;
                if (proposals.TryGetValue(t, out var value))
                {
                    if (previewOperation == 0) rot = settings.rotationSpace == Space.World ? Quaternion.Inverse(parentRotation) * Quaternion.Euler(value) : Quaternion.Euler(value);
                    else if (previewOperation == 1) scale = value;
                    else if (settings.positionSpace == Space.World)
                    {
                        if (Mathf.Abs(parentMatrix.determinant) < 1e-12f) throw new ArgumentException("A parent has zero or near-zero scale. Use Relative to Parent or fix the parent's scale first.");
                        pos = parentMatrix.inverse.MultiplyPoint3x4(value);
                    }
                    else pos = value;
                }
                var matrix = parentMatrix * Matrix4x4.TRS(pos, rot, scale);
                for (int component = 0; component < 16; component++)
                    if (!Finite(matrix[component])) throw new ArgumentException("The proposed transform is too large. Reduce your values or the parent scale.");
                matrices[t] = matrix; rotations[t] = parentRotation * rot;
                return matrix;
            };
            var seen = new HashSet<Renderer>();
            foreach (var go in objects)
            {
                watch(go.transform);
                bool added = false;
                foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (!seen.Add(renderer)) continue;
                    watch(renderer.transform);
                    var ghost = new Ghost { matrix = world(renderer.transform), bounds = renderer.localBounds };
                    if (renderer is SkinnedMeshRenderer skinned && skinned.sharedMesh)
                    {
                        ghost.mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave }; ghost.ownsMesh = true;
                        ghosts.Add(ghost); // Track before BakeMesh so failed preparation still cleans up.
                        skinned.BakeMesh(ghost.mesh);
                        ghost.bounds = ghost.mesh.bounds;
                    }
                    else
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        if (renderer is MeshRenderer && filter && filter.sharedMesh) { ghost.mesh = filter.sharedMesh; ghost.bounds = filter.sharedMesh.bounds; }
                        ghosts.Add(ghost);
                    }
                    added = true;
                }
                if (!added) ghosts.Add(new Ghost { matrix = world(go.transform), bounds = new Bounds(Vector3.zero, Vector3.one * .25f) });
            }
            originalPoses = watched.Select(t => new Pose(t)).ToList();
        }
        void DrawScene(SceneView view)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (HasPreview && !PreviewValid()) { ClearPreview(); Repaint(); return; }
            if (Event.current.type != EventType.Repaint) return;
            if (HasPreview)
            {
                if (!previewMaterial)
                {
                    var shader = Shader.Find("Hidden/LevelDesigner/Preview");
                    if (shader) previewMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }
                foreach (var ghost in ghosts)
                {
                    if (ghost.mesh && previewMaterial && previewMaterial.SetPass(0))
                    {
                        for (int sub = 0; sub < ghost.mesh.subMeshCount; sub++) Graphics.DrawMeshNow(ghost.mesh, ghost.matrix, sub);
                    }
                    else using (new Handles.DrawingScope(new Color(.1f, .9f, 1, 1), ghost.matrix)) Handles.DrawWireCube(ghost.bounds.center, ghost.bounds.size);
                }
                Handles.BeginGUI();
                GUILayout.BeginArea(new Rect(12, 12, 280, 45), EditorStyles.helpBox);
                GUILayout.Label("LEVEL DESIGNER · PREVIEW", EditorStyles.boldLabel);
                GUILayout.Label("Cyan = proposed result. Apply in the tool.", EditorStyles.miniLabel);
                GUILayout.EndArea(); Handles.EndGUI();
            }
            else if (highlight)
            {
                // Lightweight bounds highlight; never changes Unity's selection.
                foreach (var go in GetTargets())
                {
                    using (new Handles.DrawingScope(new Color(1, .7f, .2f, .85f)))
                    {
                        Bounds bounds = ObjectBounds(go); Handles.DrawWireCube(bounds.center, bounds.size);
                    }
                }
            }
        }
        static Bounds ObjectBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * .3f);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
        static void FrameTargets(List<GameObject> objects)
        {
            if (objects.Count == 0) return;
            var view = SceneView.lastActiveSceneView;
            if (!view) view = GetWindow<SceneView>();
            Bounds bounds = ObjectBounds(objects[0]);
            foreach (var go in objects.Skip(1)) bounds.Encapsulate(ObjectBounds(go));
            view.Frame(bounds, false); view.Repaint();
        }

        void SavePreset()
        {
            string path = EditorUtility.SaveFilePanel("Save Level Designer Preset", Application.dataPath, "LevelDesignerPreset", "json");
            if (string.IsNullOrEmpty(path)) return;
            try { File.WriteAllText(path, JsonUtility.ToJson(settings, true)); AssetDatabase.Refresh(); message = "Preset saved. Scene references and target lists are not included."; }
            catch (Exception e) { message = "Could not save preset: " + e.Message; }
        }
        void LoadPreset()
        {
            string path = EditorUtility.OpenFilePanel("Load Level Designer Preset", Application.dataPath, "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                string json = File.ReadAllText(path);
                if (!Regex.IsMatch(json, "\"version\"\\s*:\\s*1\\b")) throw new ArgumentException("Not a supported Level Designer preset (version 1 required).");
                var loaded = JsonUtility.FromJson<Settings>(json);
                if (loaded == null || loaded.rotationAxes == null || loaded.scaleAxes == null || loaded.positionAxes == null) throw new ArgumentException("Incomplete preset.");
                if (!Enum.IsDefined(typeof(TargetMode), loaded.targetMode) || !Enum.IsDefined(typeof(Combine), loaded.combine)
                    || !Enum.IsDefined(typeof(RotationMode), loaded.rotationMode) || !Enum.IsDefined(typeof(ScaleMode), loaded.scaleMode)
                    || !Enum.IsDefined(typeof(PositionMode), loaded.positionMode) || !Enum.IsDefined(typeof(Space), loaded.rotationSpace)
                    || !Enum.IsDefined(typeof(Space), loaded.positionSpace) || ((int)loaded.rules & ~15) != 0)
                    throw new ArgumentException("Invalid preset options.");
                if (!json.Contains("\"simpleScale\"")) loaded.simpleScale = false;
                ValidateSettings(loaded); CopyAdjustments(loaded); ClearPreview();
                message = "Preset imported. Your working set is unchanged. Preview the active tab to try it.";
            }
            catch (Exception e) { message = "Could not load preset: " + e.Message; }
        }
    }
}
#endif
