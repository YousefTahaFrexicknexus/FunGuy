#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LevelDesignerTools
{
    public sealed partial class LevelDesignerWindow
    {
        // Optional diagnostics. Creates only a temporary, unsaved additive scene.
        [MenuItem("Tools/Level Designer/Diagnostics/Run Scene Checks")]
        static void RunSceneChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Exit Play Mode before running Level Designer checks."); return; }
            Scene previous = SceneManager.GetActiveScene();
            UnityEngine.Object[] selected = Selection.objects;
            bool hadPrefs = EditorPrefs.HasKey(PrefKey);
            string oldPrefs = EditorPrefs.GetString(PrefKey, "");
            Scene testScene = default;
            LevelDesignerWindow window = null;
            GameObject a = null, b = null, parent = null;
            int passed = 0;
            Action<bool, string> check = (ok, name) => { if (!ok) throw new Exception("FAILED: " + name); passed++; };
            try
            {
                testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(testScene);
                parent = new GameObject("LD Check Root");
                a = GameObject.CreatePrimitive(PrimitiveType.Cube); a.name = "Rock"; a.transform.SetParent(parent.transform);
                b = GameObject.CreatePrimitive(PrimitiveType.Cube); b.name = "Rock (1)"; b.transform.SetParent(parent.transform);
                window = CreateInstance<LevelDesignerWindow>();
                window.settings = new Settings { targetMode = TargetMode.CurrentSelection, activeTab = 1, scaleMode = ScaleMode.Add, scaleAxes = new Axes { x = true, y = false, z = false }, scaleValue = Vector3.one, simpleScale = false };
                window.targets = new List<GameObject> { a, b };
                var objects = window.GetTargets();
                a.transform.localScale = new Vector3(2, 3, 4); b.transform.localScale = new Vector3(-1, 0, 2);
                var values = window.Calculate(1, objects, 1);
                check(values[objects.IndexOf(a)].Equals(new Vector3(3, 3, 4)) && values[objects.IndexOf(b)].Equals(new Vector3(0, 0, 2)), "Add uses each current scale; preserves unchecked axes");
                window.settings.scaleMode = ScaleMode.Multiply; window.settings.scaleValue = Vector3.one * 2;
                values = window.Calculate(1, objects, 1);
                check(values[objects.IndexOf(a)].x == 4 && values[objects.IndexOf(b)].x == -2, "Multiply handles negative scales");
                window.settings.scaleMode = ScaleMode.RandomMultiply; window.settings.uniformScale = true;
                window.settings.uniformMin = window.settings.uniformMax = 2; window.settings.scaleAxes = new Axes { x = true, y = true, z = true };
                values = window.Calculate(1, objects, 1);
                check(values[objects.IndexOf(a)].Equals(new Vector3(4, 6, 8)), "Uniform multiplier preserves proportions");
                window.Preview(false);
                check(window.HasPreview && a.transform.localScale.Equals(new Vector3(2,3,4)), "Preview leaves transforms unchanged");
                var shown = new List<Vector3>(window.previewValues);
                window.Preview(true);
                check(window.previewValues.SequenceEqual(shown) && a.transform.localScale.x == 2, "Fixed-range shuffle does not accumulate");
                window.CommitPreview();
                check(a.transform.localScale.Equals(new Vector3(4,6,8)), "Apply commits displayed values");
                Undo.PerformUndo(); check(a.transform.localScale.Equals(new Vector3(2,3,4)), "Undo restores original");
                Undo.PerformRedo(); check(a.transform.localScale.Equals(new Vector3(4,6,8)), "Redo restores result");
                window.settings.uniformMin = 3; window.settings.uniformMax = 2;
                bool rejected = false;
                try { window.Calculate(1, objects, 1); } catch (ArgumentException) { rejected = true; }
                check(rejected && a.transform.localScale.x == 4, "Invalid range rejects without mutation");
                var rng = new System.Random(42);
                bool stepped = true;
                for (int i = 0; i < 100; i++) { float angle = SampleAngle(rng, -90, 180, 45); stepped &= angle >= -90 && angle <= 180 && angle % 45 == 0; }
                check(stepped, "Rotation steps stay on grid and within bounds");
                window.settings.normalizeNames = true;
                check(window.MatchName(a.name) == window.MatchName(b.name), "Copy suffix matching");
                window.settings.targetMode = TargetMode.FindSimilar; window.settings.rules = MatchRule.Name;
                window.reference = a; window.hierarchyRoot = parent.transform; window.FindSimilar();
                check(window.targets.Count == 2, "Hierarchy-limited name matching");
                window.excluded.Add(b); check(window.GetTargets().Count == 1, "Target exclusion"); window.excluded.Clear();
                window.settings.targetMode = TargetMode.CurrentSelection; window.targets.Add(parent);
                check(window.GetTargets().Count == 1 && window.GetTargets()[0] == parent, "Skip descendants when parent targeted");
                window.targets = new List<GameObject> { a }; window.settings.uniformMin = window.settings.uniformMax = 1.2f;
                window.Preview(false); check(window.HasPreview, "Preview prepared");
                a.transform.localPosition += Vector3.right;
                check(!window.PreviewValid(), "External transform edit invalidates preview"); window.ClearPreview();
                check(!window.HasPreview && window.ghosts.Count == 0, "Cancel releases preview resources");
                Debug.Log($"Level Designer: {passed} scene checks passed. Still test prefab persistence, UI and rendering in your project.");
            }
            catch (Exception e) { Debug.LogError($"Level Designer: {passed} checks passed before failure. {e}"); }
            finally
            {
                if (window) DestroyImmediate(window);
                if (a) Undo.ClearUndo(a.transform); if (b) Undo.ClearUndo(b.transform); if (parent) Undo.ClearUndo(parent.transform);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (testScene.IsValid() && testScene.isLoaded) EditorSceneManager.CloseScene(testScene, true);
                Selection.objects = selected;
                if (hadPrefs) EditorPrefs.SetString(PrefKey, oldPrefs); else EditorPrefs.DeleteKey(PrefKey);
            }
        }
    }
}
#endif
