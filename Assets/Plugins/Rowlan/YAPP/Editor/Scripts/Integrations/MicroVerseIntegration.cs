using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Yapp
{
#if __MICROVERSE_VEGETATION__

    using JBooth.MicroVerseCore;
    using JBooth.MicroVerseCore.Browser;

    /// <summary>
    /// MicroVerse integration. Class is based on the unity terrain tree version. 
    /// </summary>
    public class MicroVerseIntegration
    {
        // internal properties, maybe we'll make them public
        private bool randomTreeColor = false;
        private float treeColorAdjustment = 0.8f;

        SerializedProperty targetTerrain;
        SerializedProperty copyPasteStamp;
        SerializedProperty autoAddPrototype;

        PrefabPainterEditor editor;

        UnityTerrainTreeManager terrainTreeManager;

        public MicroVerseIntegration(PrefabPainterEditor editor)
        {
            this.editor = editor;

            terrainTreeManager = new UnityTerrainTreeManager(editor);

            targetTerrain = editor.FindProperty(x => x.brushSettings.targetTerrain);
            copyPasteStamp = editor.FindProperty(x => x.brushSettings.microVerse.copyPasteStamp);
            autoAddPrototype = editor.FindProperty(x => x.brushSettings.autoAddPrototype);
        }

        public void OnInspectorGUI()
        {
            GUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Terrain Trees", GUIStyles.BoxTitleStyle);

                if (this.editor.GetPainter().brushSettings.targetTerrain == null)
                {
                    editor.SetErrorBackgroundColor();
                }

                EditorGUILayout.PropertyField(targetTerrain, new GUIContent("Target Terrain", "The terrain to work with"));

                if (this.editor.GetPainter().brushSettings.microVerse.copyPasteStamp == null)
                {
                    editor.SetErrorBackgroundColor();
                }

                EditorGUILayout.BeginHorizontal();
                {
                    bool prevGUIEnabled = GUI.enabled;
                    GUI.enabled = this.editor.GetPainter().brushSettings.targetTerrain != null;
                    {
                        EditorGUILayout.PropertyField(copyPasteStamp, new GUIContent("Copy Paste Stamp", "MicroVerse Copy/Paste Stamp"));

                        GUI.enabled = this.editor.GetPainter().brushSettings.microVerse.copyPasteStamp == null;
                        {
                            if (GUILayout.Button("Create", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                            {
                                CreateCopyPasteStamp();
                            }
                        }
                    }
                    GUI.enabled = prevGUIEnabled;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(autoAddPrototype, new GUIContent("Auto Add Prototype", "Add prototype to terrain in case it's missing"));

                editor.SetDefaultBackgroundColor();


                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(new GUIContent( "Extract Prefabs", "Replace the prefabs with the ones from the Unity terrain"), GUILayout.Width(100)))
                    {
                        CreatePrefabSettingsFromUnityTerrain();
                    }

                    if (GUILayout.Button(new GUIContent("Clear Prefabs", "Remove all prefab settings"), GUILayout.Width(100)))
                    {
                        ClearPrefabs();
                    }

                    /*
                    if (GUILayout.Button(new GUIContent("Log Info", "Log terrain info to the console"), GUILayout.Width(100)))
                    {
                        LogInfo();
                    }
                    */

                    if (GUILayout.Button(new GUIContent( "Clear Terrain", "Remove all trees from the terrain"), GUILayout.Width(120)))
                    {
                        RemoveAll();
                    }

                    if (GUILayout.Button(new GUIContent("Clear Prototypes", "Remove all trees from the terrain prototype settings"), GUILayout.Width(120)))
                    {
                        ClearPrototypes();
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox("Terrain Trees is highly experimental and not fully implemented yet! Backup your project!", MessageType.Warning);

            }
            GUILayout.EndVertical();
        }


        private void LogInfo()
        {
            terrainTreeManager.LogPrototypes();
        }

        private void RemoveAll()
        {
            terrainTreeManager.RemoveAllInstances();

            // apply data to stamp
            ApplyMicroVerse();

            // invalidate so that regular stamps get reapplied
            InvalidateMicroVerse();
        }

        private void ClearPrototypes()
        {
            terrainTreeManager.RemoveAllInstancesAndPrototypes();

            // apply data to stamp
            ApplyMicroVerse();

            // invalidate so that regular stamps get reapplied
            InvalidateMicroVerse();
        }

        public void AddNewPrefab(PrefabSettings prefabSettings, Vector3 newPosition, Quaternion newRotation, Vector3 newLocalScale, float overlapOffset)
        {
            // brush mode
            float brushSize = editor.GetPainter().brushSettings.brushSize;

            // poisson mode: use the discs as brush size
            if (editor.GetPainter().brushSettings.distribution == BrushSettings.Distribution.Poisson_Any || editor.GetPainter().brushSettings.distribution == BrushSettings.Distribution.Poisson_Terrain)
            {
                brushSize = editor.GetPainter().brushSettings.poissonDiscSize;
            }

            GameObject prefab = prefabSettings.prefab;
            bool allowOverlap = editor.GetPainter().brushSettings.allowOverlap;
            bool autoAddPrototype = editor.GetPainter().brushSettings.autoAddPrototype;

            terrainTreeManager.PlacePrefab( prefab, autoAddPrototype, newPosition, newLocalScale, newRotation, brushSize, randomTreeColor, treeColorAdjustment, allowOverlap, overlapOffset);

            // apply data to stamp
            ApplyMicroVerse();

        }

        public void RemovePrefabs( RaycastHit raycastHit)
        {
            Vector3 position = raycastHit.point;
            float brushSize = editor.GetPainter().brushSettings.brushSize;

            terrainTreeManager.RemoveOverlapping( position, brushSize);

            // apply data to stamp
            ApplyMicroVerse();
        }

        /// <summary>
        /// Read from terrain and feed the tree data into the copy/paste stamp
        /// </summary>
        public void ApplyMicroVerse()
        {
            CopyPasteStamp stamp = editor.GetPainter().brushSettings.microVerse.copyPasteStamp;

            if (stamp.stamp == null)
            {
                Debug.LogError("No stamp data in copy/paste stamp");
                return;
            }

            var terrains = MicroVerse.instance.terrains;

            CopyStamp.TreeCopyData tcd = CopyPasteStampEditorAccess.CaptureTrees(terrains, stamp.GetBounds(), stamp.transform);

            stamp.stamp.treeData = tcd;

            // don't invalidate here, it makes it all a bit slower
            // MicroVerse.instance.Invalidate();
        }

        private void InvalidateMicroVerse()
        {
            // invalidate so everything gets recreated
            MicroVerse.instance.Invalidate();
        }

        /// <summary>
        /// Extract the prefabs of the unity terrain and create yapp settings from them.
        /// </summary>
        private void CreatePrefabSettingsFromUnityTerrain()
        {
            // get the prefabs
            List<GameObject> prefabs = terrainTreeManager.ExtractPrefabs();

            // create new settings list
            editor.AddPrefabs( Constants.TEMPLATE_NAME_TREE, prefabs, true);
        }

        private void ClearPrefabs()
        {
            // create new settings list
            editor.ClearPrefabs();
        }

        private void CreateCopyPasteStamp()
        {
            //
            // note: this is a modified version of jasons's code from MenuItem.cs, method CreateMicroVerseForExisting
            //

            // create stamp
            MicroVerse.instance.SyncTerrainList();

            MicroVerse mv = MicroVerse.instance;

            Terrain terrain = this.editor.GetPainter().brushSettings.targetTerrain;

            var oldPos = terrain.transform.position;
            terrain.transform.position = new Vector3(terrain.transform.position.x, 0, terrain.transform.position.z);
            // mod: uniqueness
            var go = CreateGO("CopyPaste Stamp " + terrain.name);
            GameObjectUtility.EnsureUniqueNameForSibling(go);
            var cp = go.AddComponent<CopyPasteStamp>();
            cp.transform.parent = mv.transform;
            cp.pixelQuantization = true;
            cp.transform.localScale = terrain.terrainData.size;
            cp.transform.position = terrain.transform.position;
            cp.transform.localPosition += new Vector3(terrain.terrainData.size.x / 2, terrain.transform.localPosition.y, terrain.terrainData.size.z / 2); ;
            var path = AssetDatabase.GetAssetPath(terrain.terrainData);
            path = path.Replace("\\", "/");
            if (string.IsNullOrEmpty(path))
                path = "Assets/";
            path = path.Substring(0, path.LastIndexOf("/") + 1);
            path = path + terrain.name + "_cpstamp";

            // mod: add _yapp to file prefix
            {
                path = path + "_yapp";

                // create unique asset path. for that we need an extension
                path = path + ".asset";
                path = AssetDatabase.GenerateUniqueAssetPath(path);

            }

            cp.applyTrees = true;
            cp.applyDetails = true;
            cp.applyHoles = true;
            cp.heightStamp.mode = HeightStamp.CombineMode.Override;

            // mod: don't copy anything, we create a new stamp
            {
                cp.copyHeights = false;
                cp.copyTexturing = false;
                cp.copyHoles = false;
                cp.copyTrees = false;
                cp.copyDetails = false;

                cp.applyHeights = false;
                cp.applyTexturing = false;
                cp.applyHoles = false;
                cp.applyTrees = true; // only apply trees
                cp.applyDetails = false;
            }

            CopyPasteStampEditorAccess.Capture(cp, path);

            terrain.transform.position = oldPos;

            // mod: assign stamp
            this.editor.GetPainter().brushSettings.microVerse.copyPasteStamp = cp;
        }

        public static GameObject CreateGO(string name)
        {
            //
            // note: this is a modified version of jasons's code from MenuItem.cs, method CreateMicroVerseForExisting
            //

            GameObject go = new GameObject(name);


            if (Selection.activeObject != null)
            {
                if (Selection.activeObject as GameObject)
                {
                    go.transform.SetParent(((GameObject)Selection.activeObject).transform);
                }
            }

            if (Selection.activeObject is GameObject)
            {
                GameObject parent = Selection.activeObject as GameObject;
                go.transform.SetParent(parent.transform, false);
            }
            if (go.GetComponentInParent<MicroVerse>() == null && MicroVerse.instance != null)
            {
                go.transform.SetParent(MicroVerse.instance.gameObject.transform, true);
            }
            go.transform.localScale = new Vector3(100, 100, 100);

            // Selection.activeObject = go;
            return go;
        }

        /// <summary>
        /// Extract tree stamp prefabs out of the dropped content
        /// </summary>
        /// <returns></returns>
        public static List<GameObject> GetDroppedPrefabs()
        {
            // they must be added when everything is done (currently at the end of this method)
            List<GameObject> gameObjects = new List<GameObject>();

            // handle content browser presets
            object presets = DragAndDrop.GetGenericData("preset");
            
            if( presets != null)
            {
                if (presets is PresetItem)
                {
                    PresetItem presetItem = (PresetItem)presets;

                    TreeStamp[] stamps = presetItem.content.prefab.gameObject.transform.GetComponentsInChildren<TreeStamp>();

                    foreach (TreeStamp stamp in stamps)
                    {
                        AddTreeStamp(gameObjects, stamp);
                    }
                }
            }

            // handle tree stamps
            object[] objects = DragAndDrop.objectReferences;

            if(objects != null)
            {
                foreach (object obj in objects)
                {
                    if (obj is GameObject)
                    {
                        GameObject go = (GameObject)obj;

                        TreeStamp[] stamps = go.transform.GetComponentsInChildren<TreeStamp>();

                        foreach (TreeStamp stamp in stamps)
                        {
                            AddTreeStamp(gameObjects, stamp);
                        }
                    }

                }
            }

            return gameObjects;
        }

        private static void AddTreeStamp(List<GameObject> gameObjects, TreeStamp stamp)
        {
            foreach (TreePrototypeSerializable tp in stamp.prototypes)
            {
                gameObjects.Add(tp.prefab);
            }
        }

        public static bool IsInstalled()
        {
            return true;
        }
    }
#else
    public class MicroVerseIntegration
    {
        public MicroVerseIntegration(PrefabPainterEditor editor)
        {
        }
		
        public void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("MicroVerse not found", MessageType.Error);
        }		
		
        public void ApplyMicroVerse()
        {
        }

        public void RemovePrefabs(RaycastHit raycastHit)
        {
        }

        public void AddNewPrefab(PrefabSettings prefabSettings, Vector3 newPosition, Quaternion newRotation, Vector3 newLocalScale, float overlapOffset)
        {
        }
		
        public static List<GameObject> GetDroppedPrefabs()
        {
            return new List<GameObject>();
        }
		
        public static bool IsInstalled()
        {
            return false;
        }
	}
#endif
}
