#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class FunGuySimulatorProbe
{
    static readonly string Folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".utmp/profiling/simulator"));
    static double nextCheck;

    static FunGuySimulatorProbe()
    {
        EditorApplication.update += Update;
    }

    static void Update()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1d;
        string req = Path.Combine(Folder, "probe.request");
        if (!File.Exists(req)) return;
        try
        {
            File.Delete(req);
            Directory.CreateDirectory(Folder);
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .Where(w => w.GetType().Name.Contains("Simulator") || w.GetType().Name.Contains("GameView"))
                .Select(w => new
                {
                    type = w.GetType().FullName,
                    title = w.titleContent.text,
                    focused = EditorWindow.focusedWindow == w,
                    size = w.position.size.ToString(),
                    fields = w.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        .Select(f => new
                        {
                            name = f.Name,
                            type = f.FieldType.FullName,
                            value = GetSafeValue(f, w)
                        }).ToArray()
                }).ToArray();
            File.WriteAllText(Path.Combine(Folder, "probe.json"), JsonConvert.SerializeObject(new
            {
                editorIsPlaying = EditorApplication.isPlaying,
                unity = Application.unityVersion,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                deviceProfiles = AssetDatabase.FindAssets("RedmiNote12Pro5G_22101316G")
                    .Select(g => AssetDatabase.GUIDToAssetPath(g)).ToArray(),
                windows
            }, Formatting.Indented));
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(Folder, "probe-error.txt"), ex.ToString());
        }
    }

    static string GetSafeValue(FieldInfo f, object target)
    {
        try
        {
            var t = f.FieldType;
            if (t.IsPrimitive || t.IsEnum || t == typeof(string))
                return Convert.ToString(f.GetValue(target));
            var value = f.GetValue(target);
            return value == null ? "<null>" : "<" + value.GetType().Name + ">";
        }
        catch { return "<unavailable>"; }
    }
}
#endif
