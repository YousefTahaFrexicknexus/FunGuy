using UnityEditor;
using UnityEngine;

public class FindMissingScripts : MonoBehaviour
{
    #if UNITY_EDITOR
    [MenuItem("Tools/Find Missing Scripts in Scene")]
    public static void FindMissingScriptsInScene()
    {
        GameObject[] allObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        int missingCount = 0;

        foreach (GameObject go in allObjects)
        {
            missingCount += FindInGO(go);
        }

        Debug.Log($"Total GameObjects with missing scripts: {missingCount}");
    }

    private static int FindInGO(GameObject go)
    {
        int missingCount = 0;
        Component[] components = go.GetComponents<Component>();

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                Debug.LogError($"Missing script found in GameObject: {GetFullPath(go)}", go);
                missingCount++;
            }
        }

        foreach (Transform child in go.transform)
        {
            missingCount += FindInGO(child.gameObject);
        }

        return missingCount;
    }

    private static string GetFullPath(GameObject go)
    {
        string path = go.name;
        Transform parent = go.transform.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    #if (UNITY_EDITOR)   
    [CustomEditor(typeof(FindMissingScripts))]
    public class CustomInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
    
            if(GUILayout.Button("Find missing scripts"))
            {
                FindMissingScriptsInScene();
            }
        }
    }
#endif
#endif
}
