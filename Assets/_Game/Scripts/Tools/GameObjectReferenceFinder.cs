#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class GameObjectReferenceFinder : EditorWindow
{
    GameObject target;

    Vector2 scroll;

    readonly List<ReferenceResult> results = new();


    [MenuItem("Tools/Reference Finder")]
    static void OpenWindow()
    {
        GetWindow<GameObjectReferenceFinder>("Reference Finder");
    }


    void OnGUI()
    {
        GUILayout.Label("Scene Reference Finder", EditorStyles.boldLabel);

        target = (GameObject)EditorGUILayout.ObjectField(
            "Target",
            target,
            typeof(GameObject),
            true
        );

        GUILayout.Space(5);

        if (GUILayout.Button("Use Selected GameObject"))
        {
            target = Selection.activeGameObject;
        }

        if (GUILayout.Button("Find References"))
        {
            FindReferences();
        }

        GUILayout.Space(10);

        GUILayout.Label($"References Found: {results.Count}", EditorStyles.boldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll);

        foreach (ReferenceResult result in results)
        {
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.ObjectField(
                "Object",
                result.gameObject,
                typeof(GameObject),
                true
            );

            EditorGUILayout.ObjectField(
                "Component",
                result.component,
                typeof(Component),
                true
            );

            EditorGUILayout.LabelField("Field", result.propertyPath);

            if (GUILayout.Button("Select"))
            {
                Selection.activeGameObject = result.gameObject;
                EditorGUIUtility.PingObject(result.gameObject);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }


    void FindReferences()
    {
        results.Clear();

        if (target == null)
        {
            Debug.LogWarning("No target GameObject selected.");
            return;
        }

        Scene scene = target.scene;

        if (!scene.IsValid())
        {
            Debug.LogWarning("Target must be inside a scene.");
            return;
        }

        Component[] targetComponents = target.GetComponents<Component>();

        HashSet<Object> targetObjects = new()
        {
            target
        };

        foreach (Component component in targetComponents)
        {
            if (component != null)
            {
                targetObjects.Add(component);
            }
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            MonoBehaviour[] behaviours =
                root.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == null)
                    continue;

                SerializedObject serializedObject =
                    new SerializedObject(behaviour);

                SerializedProperty property =
                    serializedObject.GetIterator();

                bool enterChildren = true;

                while (property.NextVisible(enterChildren))
                {
                    enterChildren = false;

                    if (property.propertyType !=
                        SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    Object reference =
                        property.objectReferenceValue;

                    if (reference == null)
                        continue;

                    if (targetObjects.Contains(reference))
                    {
                        results.Add(new ReferenceResult
                        {
                            gameObject = behaviour.gameObject,
                            component = behaviour,
                            propertyPath = property.propertyPath
                        });
                    }
                }
            }
        }

        Debug.Log(
            $"Found {results.Count} references to {target.name}."
        );
    }


    class ReferenceResult
    {
        public GameObject gameObject;
        public Component component;
        public string propertyPath;
    }
}
#endif