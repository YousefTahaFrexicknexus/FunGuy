using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

internal static class VolumetricSunShaftsMenu
{
    private const string MaterialPath = "Assets/_Game/Lighting/SunShafts/VolumetricSunShafts.mat";

    [MenuItem("GameObject/FunGuy/Volumetric Sun Shafts", false, 30)]
    private static void AddToScene()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null || !material.shader.isSupported)
        {
            EditorUtility.DisplayDialog("Volumetric Sun Shafts",
                "Unity could not load the sun shafts material. Check the Console for shader import errors.", "OK");
            return;
        }

        Vector3 anchor = Selection.activeTransform != null
            ? Selection.activeTransform.position
            : SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.pivot : Vector3.zero;

        GameObject volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
        volume.name = "Volumetric Sun Shafts";
        Undo.RegisterCreatedObjectUndo(volume, "Add volumetric sun shafts");
        Object.DestroyImmediate(volume.GetComponent<Collider>());
        volume.transform.SetPositionAndRotation(anchor + Vector3.up * 4f, Quaternion.identity);
        volume.transform.localScale = new Vector3(14f, 9f, 18f);

        var renderer = volume.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        Selection.activeGameObject = volume;
    }
}
