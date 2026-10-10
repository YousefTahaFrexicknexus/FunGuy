using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RunnerModifierSetup
{
    public const string Root = "Assets/_Game/Funguy.MushroomRunner";
    public const string Definitions = Root + "/Modifiers";
    const string PlayerPath = Root + "/Prefabs/MushroomRunnerPlayer.prefab";
    const string HudPath = "Assets/_Game/Prefab/UI/UIManager_Gameplay.prefab";

    [MenuItem("Tools/Mushroom Runner/Set Up Timed Modifiers")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before setup.");
        EnsureFolder(Definitions);
        EnsureFolder(Definitions + "/VFX");
        EnsureFolder(Definitions + "/UI");
        var gold = CreateGoldVfx();
        var chill = Definition("Chilling", MushroomModifierType.Chilling,
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IceDust.prefab"),
            AssetDatabase.LoadAssetAtPath<AudioClip>(Definitions + "/Audio/Chilling.wav"));
        Definition("Perfect Landings", MushroomModifierType.PerfectLandings, gold,
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Game Music Stingers and UI SFX Pack 2/UI_SFX/Misc/PP_Power_Up_1_1.wav"));
        EditPrefab(PlayerPath, root => { foreach (var motor in root.GetComponentsInChildren<RunnerMovementMotor>(true)) EnsureController(motor); });
        EditPrefab(Root + "/Prefabs/Chilling Mushroom.prefab", root =>
        {
            var launcher = root.GetComponentInChildren<MushroomLauncher>(true);
            if (launcher == null) throw new Exception("Chilling Mushroom has no launcher.");
            var so = new SerializedObject(launcher);
            so.FindProperty("modifier").objectReferenceValue = chill;
            so.ApplyModifiedPropertiesWithoutUndo();
        });
        Sprite ring = CreateRing();
        EditPrefab(HudPath, root =>
        {
            foreach (var hud in root.GetComponentsInChildren<HUD_UI>(true)) AddHud(hud, ring);
        });
        // Wire only loaded gameplay objects in the intended scene; preserve all existing user edits.
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path == Root + "/Scenes/MushroomRunnerGameplay.unity")
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var motor in root.GetComponentsInChildren<RunnerMovementMotor>(true))
                    if (motor.GetComponent<RunnerModifierController>() == null) Undo.AddComponent<RunnerModifierController>(motor.gameObject);
                foreach (var hud in root.GetComponentsInChildren<HUD_UI>(true))
                    if (hud.GetComponentInChildren<RunnerModifierHud>(true) == null) AddHud(hud, ring);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Timed modifiers: definitions, Chilling Mushroom, player and HUD configured.");
    }

    static void EnsureController(RunnerMovementMotor motor)
    {
        if (motor.GetComponent<RunnerModifierController>() == null) motor.gameObject.AddComponent<RunnerModifierController>();
    }

    static MushroomModifierDefinition Definition(string name, MushroomModifierType type, GameObject vfx, AudioClip sound)
    {
        string path = Definitions + "/" + name + ".asset";
        var definition = AssetDatabase.LoadAssetAtPath<MushroomModifierDefinition>(path);
        if (definition != null) return definition; // Preserve subsequent Inspector tuning.
        definition = ScriptableObject.CreateInstance<MushroomModifierDefinition>();
        var so = new SerializedObject(definition);
        so.FindProperty("effectType").enumValueIndex = (int)type;
        so.FindProperty("duration").floatValue = 10f;
        so.FindProperty("speedMultiplier").floatValue = 0.7f;
        so.FindProperty("playerVfxPrefab").objectReferenceValue = vfx;
        so.FindProperty("activationSound").objectReferenceValue = sound;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(definition, path);
        return definition;
    }

    static GameObject CreateGoldVfx()
    {
        string path = Definitions + "/VFX/Perfect Glow.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Light/CFXR3 LightGlow A (Loop).prefab");
        if (source == null) throw new Exception("Missing looping light-glow source prefab.");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            instance.name = "Perfect Glow";
            instance.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            instance.transform.localScale = Vector3.one * 0.45f;
            foreach (var particle in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particle.main;
                main.loop = true;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
                main.startColor = new Color(1f, 0.72f, 0.18f, 0.65f);
                main.useUnscaledTime = false;
                var colors = particle.colorOverLifetime;
                colors.enabled = false;
            }
            foreach (var light in instance.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                var so = new SerializedObject(behaviour);
                var clear = so.FindProperty("clearBehavior");
                if (clear != null) { clear.enumValueIndex = 0; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            instance.SetActive(false);
            return PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    static Sprite CreateRing()
    {
        string path = Definitions + "/UI/ModifierRing.png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (existing != null) return existing;
        var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        var pixels = new Color[128 * 128];
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
        {
            float radius = new Vector2(x - 63.5f, y - 63.5f).magnitude;
            float alpha = Mathf.Clamp01(62f - radius) * Mathf.Clamp01(radius - 51f);
            pixels[y * 128 + x] = new Color(1, 1, 1, alpha);
        }
        texture.SetPixels(pixels); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void AddHud(HUD_UI hud, Sprite ring)
    {
        if (hud.GetComponentInChildren<RunnerModifierHud>(true) != null) return;
        RectTransform momentum = null;
        foreach (var child in hud.GetComponentsInChildren<RectTransform>(true))
            if (child.name == "MomentumSect") { momentum = child; break; }
        if (momentum == null) throw new Exception("HUD has no MomentumSect; cannot safely place countdowns.");
        var root = Rect("ModifierCountdowns", momentum, new Vector2(0.5f, 0f), new Vector2(0f, -12f), new Vector2(156f, 68f));
        root.pivot = new Vector2(0.5f, 1f);
        var presenter = root.gameObject.AddComponent<RunnerModifierHud>();
        var chill = CreateSlot(root, "Chilling", -39, new Color(0.3f, 0.9f, 1f), ring);
        var perfect = CreateSlot(root, "Perfect", 39, new Color(1f, 0.75f, 0.2f), ring);
        var so = new SerializedObject(presenter);
        SetSlot(so.FindProperty("chilling"), chill);
        SetSlot(so.FindProperty("perfect"), perfect);
        so.ApplyModifiedPropertiesWithoutUndo();
        chill.root.SetActive(false); perfect.root.SetActive(false);
    }

    static RunnerModifierHud.Slot CreateSlot(RectTransform parent, string name, float x, Color color, Sprite ring)
    {
        var root = Rect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(64, 64));
        var background = Rect("Track", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64)).gameObject.AddComponent<UnityEngine.UI.Image>();
        background.sprite = ring; background.color = new Color(0.05f, 0.08f, 0.12f, 0.65f); background.raycastTarget = false;
        var fill = Rect("CountdownRing", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 64)).gameObject.AddComponent<UnityEngine.UI.Image>();
        fill.sprite = ring; fill.color = color; fill.raycastTarget = false;
        fill.type = UnityEngine.UI.Image.Type.Filled;
        fill.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        fill.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
        fill.fillClockwise = false; // Decreasing counterclockwise fill reveals the empty sector clockwise.
        var icon = Rect("Icon (Assign Later)", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40)).gameObject.AddComponent<UnityEngine.UI.Image>();
        icon.preserveAspect = true; icon.raycastTarget = false; icon.enabled = false;
        return new RunnerModifierHud.Slot { root = root.gameObject, ring = fill, icon = icon };
    }

    static void SetSlot(SerializedProperty property, RunnerModifierHud.Slot slot)
    {
        property.FindPropertyRelative("root").objectReferenceValue = slot.root;
        property.FindPropertyRelative("ring").objectReferenceValue = slot.ring;
        property.FindPropertyRelative("icon").objectReferenceValue = slot.icon;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.gameObject.layer = parent.gameObject.layer;
        rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    static void EditPrefab(string path, Action<GameObject> edit)
    {
        var contents = PrefabUtility.LoadPrefabContents(path);
        try { edit(contents); PrefabUtility.SaveAsPrefabAsset(contents, path); }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
