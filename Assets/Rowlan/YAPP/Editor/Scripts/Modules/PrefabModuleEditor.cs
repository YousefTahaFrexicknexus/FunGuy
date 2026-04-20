using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Rowlan.Yapp
{
    public class PrefabModuleEditor : ModuleEditorI
    {

#pragma warning disable 0414
        public PrefabPainterEditor editor;
        public PrefabPainter editorTarget;
#pragma warning restore 0414


        public List<PrefabTemplateCollection> templateCollections;
        private PrefabSettingsTemplate defaultTemplate = ScriptableObject.CreateInstance<PrefabSettingsTemplate>();

        private PrefabView prefabView;

        public PrefabModuleEditor(PrefabPainterEditor editor)
        {
            this.editor = editor;
            this.editorTarget = editor.GetPainter();

            this.prefabView = new PrefabView(this);

            LoadTemplateCollection();
        }

        private void LoadTemplateCollection()
        {
            // load the available prefab settings templates
            string[] templateCollectionGuids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(PrefabTemplateCollection)));

            // check if we have a template collection at all
            if (templateCollectionGuids.Length == 0)
            {
                Debug.LogError(string.Format("No asset of type {0} found", typeof(PrefabTemplateCollection)));
            }

            // create list of valid collections
            templateCollections = new List<PrefabTemplateCollection>();

            foreach (string guid in templateCollectionGuids)
            {
                // use the first one found
                string templateCollectionGuidsFilePath = AssetDatabase.GUIDToAssetPath(guid);

                PrefabTemplateCollection templateCollection = AssetDatabase.LoadAssetAtPath(templateCollectionGuidsFilePath, typeof(PrefabTemplateCollection)) as PrefabTemplateCollection;

                if (!templateCollection)
                {
                    Debug.LogError(string.Format("Template collection not found: {0}", templateCollectionGuidsFilePath));
                    continue;
                }

                if (!templateCollection.active)
                    continue;

                templateCollections.Add(templateCollection);
            }

        }

        public void OnInspectorGUI()
        {

            GUILayout.BeginVertical("box");
            {
                // change background color in case there are no prefabs yet
                if (editorTarget.prefabSettingsList.Count == 0)
                {
                    EditorGUILayout.HelpBox("Drop prefabs on the prefab template boxes in order to use them.", MessageType.Info);

                    editor.SetErrorBackgroundColor();
                }

                foreach (PrefabTemplateCollection templateCollection in templateCollections)
                {
                    // check for microverse and handle it
                    if( templateCollection.collectionType == CollectionType.MicroVerse)
                    {
                        if (!MicroVerseIntegration.IsInstalled())
                            continue;
                    }

                    DrawCollection(templateCollection);
                }

                editor.SetDefaultBackgroundColor();

                //
                // prefab list
                //

                if (editorTarget.prefabSettingsList.Count > 0)
                {
                    EditorGUILayout.Space();
                }

                // draw the list or grid
                prefabView.DrawGUI();
            }

            GUILayout.EndVertical();

        }

        private void DrawCollection(PrefabTemplateCollection templateCollection)
        {
            GUILayout.BeginVertical();
            {
                EditorGUILayout.LabelField(templateCollection.displayName, GUIStyles.BoxTitleStyle);

                GUILayout.BeginHorizontal();
                {
                    GUILayout.BeginVertical();
                    {
                        int gridRows = Mathf.CeilToInt((float)templateCollection.templates.Count / Constants.PrefabTemplateGridColumnCount);

                        for (int row = 0; row < gridRows; row++)
                        {
                            GUILayout.BeginHorizontal();
                            {
                                for (int column = 0; column < Constants.PrefabTemplateGridColumnCount; column++)
                                {
                                    int index = column + row * Constants.PrefabTemplateGridColumnCount;

                                    PrefabSettingsTemplate template = index < templateCollection.templates.Count ? templateCollection.templates[index] : defaultTemplate;

                                    // drop area
                                    Rect prefabDropArea = GUILayoutUtility.GetRect(0.0f, 34.0f, GUIStyles.DropAreaStyle, GUILayout.ExpandWidth(true));

                                    bool hasDropArea = index < templateCollection.templates.Count;
                                    if (hasDropArea)
                                    {
                                        // drop area box with background color and info text
                                        GUI.color = GUIStyles.DropAreaBackgroundColor;
                                        GUI.Box(prefabDropArea, template.templateName, GUIStyles.DropAreaStyle);
                                        GUI.color = GUIStyles.DefaultBackgroundColor;

                                        Event evt = Event.current;
                                        switch (evt.type)
                                        {
                                            case EventType.DragUpdated:
                                            case EventType.DragPerform:

                                                if (prefabDropArea.Contains(evt.mousePosition))
                                                {
                                                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                                                    if (evt.type == EventType.DragPerform)
                                                    {
                                                        DragAndDrop.AcceptDrag();

                                                        // analyze dropped content and add dropped prefabs of it
                                                        AddPrefabs( template);
                                                    }
                                                }
                                                break;
                                        }
                                    }

                                }
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                    GUILayout.EndVertical();
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

        }

        private void AddPrefabs(PrefabSettingsTemplate template)
        {
            // list of new prefabs that should be created via drag/drop
            // we can't do it in the drag/drop code itself, we'd get exceptions like
            //   ArgumentException: Getting control 12's position in a group with only 12 controls when doing dragPerform. Aborting
            // followed by
            //   Unexpected top level layout group! Missing GUILayout.EndScrollView/EndVertical/EndHorizontal? UnityEngine.GUIUtility:ProcessEvent(Int32, IntPtr)
            // they must be added when everything is done (currently at the end of this method)
            editor.newDraggedPrefabs = new List<PrefabSettings>();

            List<GameObject> prefabs;

            switch (template.templateType)
            {
                case TemplateType.Prefab:
                default:
                    prefabs = GetDroppedPrefabs();
                    break;

                case TemplateType.MicroVerseTreeStamp:
                    prefabs = MicroVerseIntegration.GetDroppedPrefabs();
                    break;
            }

            foreach (GameObject prefab in prefabs)
            {
                // add the prefab to the list using the template
                AddPrefab(prefab as GameObject, template);
            }

        }

        private List<GameObject> GetDroppedPrefabs()
        {
            List<GameObject> list = new List<GameObject>();

            foreach (Object droppedObject in DragAndDrop.objectReferences)
            {
                // check if the object is an actual asset from the hierarchy (not from the scene)
                bool isPrefabAsset = PrefabUtility.IsPartOfPrefabAsset(droppedObject);

                if (!isPrefabAsset)
                {
                    Debug.Log("Not a prefab asset: " + droppedObject);
                    continue;
                }

                // allow only prefabs
                if (PrefabUtility.GetPrefabAssetType(droppedObject) == PrefabAssetType.NotAPrefab)
                {
                    Debug.Log("Not a prefab: " + droppedObject);
                    continue;
                }

                list.Add(droppedObject as GameObject);
            }

            return list;
        }


        public void AddPrefab(GameObject prefab, PrefabSettingsTemplate template)
        {
            // new settings
            PrefabSettings prefabSettings = new PrefabSettings();

            prefabSettings.ApplyTemplate(template);

            // initialize with dropped prefab
            prefabSettings.prefab = prefab;

            editor.newDraggedPrefabs.Add(prefabSettings);

        }

        public void OnSceneGUI()
        {
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
        }

        public void ModeChanged(PrefabPainter.Mode mode)
        {
        }
        public void OnEnteredPlayMode()
        {
        }

    }
}
