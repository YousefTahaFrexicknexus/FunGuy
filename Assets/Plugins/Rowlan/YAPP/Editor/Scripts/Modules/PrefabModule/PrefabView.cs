using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static Rowlan.Yapp.SelectionGrid;

namespace Rowlan.Yapp
{
    public class PrefabView
    {
#pragma warning disable 0414
        PrefabPainterEditor editor;
        PrefabPainter editorTarget;
#pragma warning restore 0414

        SerializedProperty prefabView;

        GUIContent[] prefabViewButtons;

        int cellSize = 96;

        Selectable[] selectableTrees;
        List<int> selectedTreeIndexes = new List<int>();
        int selectedTreeInstance = -1;

        /// <summary>
        /// Clipboard for grid cut/paste
        /// </summary>
        private Clipboard clipboard = new Clipboard();

        public PrefabView(PrefabModuleEditor prefabModuleEditor)
        {
            this.editor = prefabModuleEditor.editor;
            this.editorTarget = prefabModuleEditor.editorTarget;

            prefabView = editor.FindProperty( x => x.prefabView );

            prefabViewButtons = new GUIContent[]
            {
                new GUIContent( "List", "Show prefabs as list"),
                new GUIContent( "Grid", "Show prefabs as grid"),
            };
        }

        public void DrawGUI()
        {
            ///
            /// prefab view
            /// 

            GUILayout.BeginVertical("box");
            {

                EditorGUILayout.LabelField("View", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();

                EditorGUI.BeginChangeCheck();
                {
                    prefabView.intValue = GUILayout.Toolbar(prefabView.intValue, prefabViewButtons);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    // nothing to do
                }

                EditorGUILayout.EndHorizontal();

            }
            GUILayout.EndVertical();

            
            switch( (PrefabPainter.PrefabView)prefabView.intValue)
            {
                case PrefabPainter.PrefabView.List:
                    DrawList();
                    break;

                case PrefabPainter.PrefabView.Grid:
                    DrawGrid();
                    break;
            }
        }

        private void ApplyClipboardChanges()
        {
            int cutIndex = clipboard.cutIndex;
            int pasteIndex = clipboard.pasteIndex;

            // Debug.Log($"copy/paste {cutIndex} => {pasteIndex}");

            bool valid = cutIndex != pasteIndex && cutIndex >= 0 && cutIndex < editorTarget.prefabSettingsList.Count && pasteIndex >= 0 && pasteIndex < editorTarget.prefabSettingsList.Count;
            if( !valid)
            {
                clipboard.Reset();
                return;
            }

            PrefabSettings moveable = editorTarget.prefabSettingsList[cutIndex];
            PrefabSettings target = editorTarget.prefabSettingsList[pasteIndex];

            editorTarget.prefabSettingsList.Remove(moveable);

            int insertIndex = editorTarget.prefabSettingsList.IndexOf(target);
            editorTarget.prefabSettingsList.Insert(insertIndex, moveable);

            EditorUtility.SetDirty(editorTarget);

            clipboard.Reset();
        }



        private void DrawGrid()
        {

            // check for context click
            if (Event.current.type == EventType.ContextClick)
            {
                GenericMenu menu = SelectionGridMenu.Create(editorTarget.prefabSettingsList);

                menu.ShowAsContext();

                Event.current.Use();
            }

            // convert prefabs to selectables
            CreateSelectables();

            List<PrefabSettings> list = editorTarget.prefabSettingsList;

            // get current active state
            for (int i = 0; i < selectableTrees.Length; i++)
            {
                selectableTrees[i].active = list[i].active;
            }

            // grid selection
            bool changed = SelectionGrid.ShowSelectionGrid(selectedTreeIndexes, selectableTrees, cellSize, clipboard);

            // check if clipboard changed and apply changes
            if (clipboard.IsClipboardValid())
            {
                ApplyClipboardChanges();

                // abort current inspector process and recreate selectables etc
                return;
            }

            // set the attributes of the objects in case anything changed on the selection grid
            if (changed)
            {
                for (int i = 0; i < selectableTrees.Length; i++)
                {
                    var randoms = list[i];

                    randoms.active = selectableTrees[i].active;

                    list[i] = randoms;
                }

                EditorUtility.SetDirty(editorTarget);
            }

            bool multiObjectEditMode = selectedTreeIndexes.Count > 1;

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                selectedTreeInstance = selectedTreeIndexes.Count > 0 ? selectedTreeIndexes[0] : -1;

                if (GUILayout.Button(new GUIContent( "Remove", "Remove the selected items"), EditorStyles.miniButton))
                {
                    Undo.RegisterCompleteObjectUndo(editorTarget, "Prefab list remove");

                    // get top index, we need to select any cell after all selected were removed
                    int topIndex = selectedTreeIndexes.Count > 0 ? selectedTreeIndexes[0] : -1;

                    // iterate backwards for multi-delete
                    selectedTreeIndexes.Reverse();

                    // remove cells
                    foreach (int index in selectedTreeIndexes)
                    {
                        if (index >= 0 && list.Count > index)
                        {
                            list.RemoveAt(index);
                        }
                    }

                    EditorUtility.SetDirty(editorTarget);

                    // pre-select cell: either previous one of the selected cell or the first one
                    int newSelectedIndex = topIndex >= 0 ? topIndex - 1 : -1;
                    if (newSelectedIndex < 0 && selectedTreeIndexes.Count > 0)
                    {
                        newSelectedIndex = 0;
                    }

                    selectedTreeIndexes.Clear();
                    selectedTreeIndexes.Add(newSelectedIndex);

                }

                if (GUILayout.Button(new GUIContent("Clear", "Remove all items"), EditorStyles.miniButton))
                {
                    Undo.RegisterCompleteObjectUndo(editorTarget, "Prefab list clear");

                    list.Clear();
                    EditorUtility.SetDirty(editorTarget);
                }
            }

            // horizontal separator
            editor.AddGUISeparator( 0f, 10f);

            // draw the settings of the selected prefab
            DrawSettings(selectedTreeInstance);

            // mini toolbar: apply settings to all
            EditorGUILayout.BeginHorizontal();
            {
                if (GUILayout.Button( "Reset Selected", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                {
                    Undo.RegisterCompleteObjectUndo(editorTarget, "Reset selected");

                    PrefabSettings resetSettings = new PrefabSettings();

                    foreach (int targetIndex in selectedTreeIndexes)
                    {
                        PrefabSettings target = list[targetIndex];

                        target.Apply(resetSettings);

                        list[targetIndex] = target;
                    }
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Apply to Selected", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                {
                    Undo.RegisterCompleteObjectUndo(editorTarget, "Apply settings to Selected");

                    ApplySettings(selectedTreeInstance, selectedTreeIndexes);
                }

                if (GUILayout.Button("Apply to All", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                {
                    Undo.RegisterCompleteObjectUndo(editorTarget, "Apply settings to All");

                    List<int> allIndexes = new List<int>();

                    for (int i = 0; i < list.Count; i++)
                    {
                        allIndexes.Add(i);
                    }

                    ApplySettings(selectedTreeInstance, allIndexes);
                }

            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// Apply the settings of the object at a source index to all at the given target indexes
        /// </summary>
        /// <param name="sourceIndex"></param>
        /// <param name="targetIndexes"></param>
        private void ApplySettings(int sourceIndex, List<int> targetIndexes)
        {
            List<PrefabSettings> list = editorTarget.prefabSettingsList;

            if (selectedTreeInstance < 0 || selectedTreeInstance >= list.Count)
                return;

            PrefabSettings source = list[sourceIndex];

            foreach (int targetIndex in targetIndexes)
            {
                if (sourceIndex == targetIndex)
                    continue;

                PrefabSettings target = list[targetIndex];

                target.Apply(source);

                list[targetIndex] = target;
            }
        }

        private void CreateSelectables()
        {
            List<PrefabSettings> list = editorTarget.prefabSettingsList;

            if (list == null || list.Count == 0)
            {
                selectableTrees = new Selectable[0];
                //treeIcons[0] = new GUIContent("No Trees");
            }
            else
            {
                // Locate the proto types asset preview textures
                selectableTrees = new Selectable[list.Count];
                for (int i = 0; i < selectableTrees.Length; i++)
                {
                    selectableTrees[i] = new Selectable();

                    Texture tex = AssetPreview.GetAssetPreview(list[i].prefab);
                    selectableTrees[i].image = tex != null ? tex : null;
                    selectableTrees[i].text = selectableTrees[i].tooltip = list[i].prefab != null ? list[i].prefab.name : "Missing";
                    selectableTrees[i].active = selectableTrees[i].active;
                }

                // select focused instance for multi-selection in selection grid
                if (selectedTreeInstance >= 0 && selectedTreeInstance < selectableTrees.Length)
                    selectableTrees[selectedTreeInstance].focused = true;
            }
        }

        private void DrawList()
        {
            //
            // toolbar (clear etc)
            //
            GUILayout.BeginVertical("box");
            {
                GUILayout.BeginHorizontal();
                {
                    // right align the buttons
                    GUILayout.FlexibleSpace();

                    // first prefab has "apply all"
                    if (GUILayout.Button(new GUIContent("Apply first to all", "Apply settings of the prefabs to all prefabs"), EditorStyles.miniButton))
                    {
                        Undo.RegisterCompleteObjectUndo(this.editorTarget, "Apply first to all");

                        if (editorTarget.prefabSettingsList.Count > 1)
                        {
                            PrefabSettings original = this.editorTarget.prefabSettingsList[0];

                            for (int i = 1; i < editorTarget.prefabSettingsList.Count; i++)
                            {
                                PrefabSettings prefabSettings = this.editorTarget.prefabSettingsList[i];
                                prefabSettings.Apply(original);
                            }
                        }
                    }

                    if (GUILayout.Button(new GUIContent("Clear List", "Remove all prefab items"), EditorStyles.miniButton))
                    {
                        Undo.RegisterCompleteObjectUndo(this.editorTarget, "Clear");

                        this.editorTarget.prefabSettingsList.Clear();
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            for (int i = 0; i < editorTarget.prefabSettingsList.Count; i++)
            {
                // horizontal separator
                editor.AddGUISeparator(i == 0 ? 0f : 10f, 10f);

                PrefabSettings prefabSettings = this.editorTarget.prefabSettingsList[i];
                
                DrawSettings(prefabSettings, i);
            }
        }

        private void DrawSettings(int index)
        {
            if (index < 0 || index >= editorTarget.prefabSettingsList.Count)
                return;

            PrefabSettings prefabSettings = editorTarget.prefabSettingsList[index];

            DrawSettings(prefabSettings, index);
        }

        private void DrawSettings(PrefabSettings prefabSettings, int index)
        {
            bool drawToolbar = (PrefabPainter.PrefabView)prefabView.intValue == PrefabPainter.PrefabView.List;

            GUILayout.BeginHorizontal();
            {
                // preview

                // try to get the asset preview
                Texture2D previewTexture = AssetPreview.GetAssetPreview(prefabSettings.prefab);

                // if no asset preview available, try to get the mini thumbnail
                if (!previewTexture)
                {
                    previewTexture = AssetPreview.GetMiniThumbnail(prefabSettings.prefab);
                }

                // if a preview is available, paint it
                if (previewTexture)
                {
                    //GUILayout.Label(previewTexture, EditorStyles.objectFieldThumb, GUILayout.Width(50), GUILayout.Height(50)); // without border, but with size
                    GUILayout.Label(previewTexture, GUILayout.Width(50), GUILayout.Height(50)); // without border, but with size

                    //GUILayout.Box(previewTexture); // with border
                    //GUILayout.Label(previewTexture); // no border
                    //GUILayout.Box(previewTexture, GUILayout.Width(50), GUILayout.Height(50)); // with border and size
                    //EditorGUI.DrawPreviewTexture(new Rect(25, 60, 100, 100), previewTexture); // draws it in absolute coordinates

                }

                if (drawToolbar)
                {
                    // right align the buttons
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Add", EditorStyles.miniButton))
                    {
                        Undo.RegisterCompleteObjectUndo(this.editorTarget, "Add");

                        this.editorTarget.prefabSettingsList.Insert(index + 1, new PrefabSettings());
                    }
                    if (GUILayout.Button("Duplicate", EditorStyles.miniButton))
                    {
                        Undo.RegisterCompleteObjectUndo(this.editorTarget, "Duplicate");

                        PrefabSettings newPrefabSettings = prefabSettings.Clone();
                        this.editorTarget.prefabSettingsList.Insert(index + 1, newPrefabSettings);
                    }
                    if (GUILayout.Button("Reset", EditorStyles.miniButton))
                    {
                        Undo.RegisterCompleteObjectUndo(this.editorTarget, "Reset");

                        // remove existing
                        this.editorTarget.prefabSettingsList.RemoveAt(index);

                        // add new
                        this.editorTarget.prefabSettingsList.Insert(index, new PrefabSettings());

                    }
                    if (GUILayout.Button("Remove", EditorStyles.miniButton))
                    {
                        Undo.RegisterCompleteObjectUndo(this.editorTarget, "Remove");

                        this.editorTarget.prefabSettingsList.Remove(prefabSettings);
                    }
                }
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            prefabSettings.prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefabSettings.prefab, typeof(GameObject), true);

            prefabSettings.active = EditorGUILayout.Toggle("Active", prefabSettings.active);
            prefabSettings.probability = EditorGUILayout.Slider("Probability", prefabSettings.probability, 0, 1);

            // scale
            if (editor.GetPainter().mode == PrefabPainter.Mode.Brush && editorTarget.brushSettings.distribution == BrushSettings.Distribution.Fluent)
            {
                // use the brush scale, hide the change scale option
                EditorGUILayout.LabelField("Change Scale", "Brush scale used in Fluent mode", GUIStyles.ItalicLabelStyle);
            }
            else
            {
                prefabSettings.changeScale = EditorGUILayout.Toggle("Change Scale", prefabSettings.changeScale);

                if (prefabSettings.changeScale)
                {
                    prefabSettings.scaleMin = EditorGUILayout.FloatField("Scale Min", prefabSettings.scaleMin);
                    prefabSettings.scaleMax = EditorGUILayout.FloatField("Scale Max", prefabSettings.scaleMax);
                }
            }
            // position
            prefabSettings.positionOffset = EditorGUILayout.Vector3Field("Position Offset", prefabSettings.positionOffset);

            // rotation
            prefabSettings.rotationOffset = EditorGUILayout.Vector3Field("Rotation Offset", prefabSettings.rotationOffset);
            GUILayout.BeginHorizontal();
            {
                prefabSettings.randomRotation = EditorGUILayout.Toggle("Random Rotation", prefabSettings.randomRotation);

                // right align the buttons
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("X", EditorStyles.miniButton))
                {
                    QuickRotationSetting(prefabSettings, 1f, 0f, 0f);
                }
                if (GUILayout.Button("Y", EditorStyles.miniButton))
                {
                    QuickRotationSetting(prefabSettings, 0f, 1f, 0f);
                }
                if (GUILayout.Button("Z", EditorStyles.miniButton))
                {
                    QuickRotationSetting(prefabSettings, 0f, 0f, 1f);
                }
                if (GUILayout.Button("XYZ", EditorStyles.miniButton))
                {
                    QuickRotationSetting(prefabSettings, 1f, 1f, 1f);
                }

                if (GUILayout.Button(prefabSettings.rotationRange.GetDisplayName(), EditorStyles.miniButton))
                {
                    prefabSettings.rotationRange = prefabSettings.rotationRange.GetNext();
                }


            }
            GUILayout.EndHorizontal();

            // rotation limits
            if (prefabSettings.randomRotation)
            {

                float min = prefabSettings.rotationRange.GetMinimum();
                float max = prefabSettings.rotationRange.GetMaximum();

                EditorGuiUtilities.MinMaxEditor("  Rotation Limit X", ref prefabSettings.rotationMinX, ref prefabSettings.rotationMaxX, min, max);
                EditorGuiUtilities.MinMaxEditor("  Rotation Limit Y", ref prefabSettings.rotationMinY, ref prefabSettings.rotationMaxY, min, max);
                EditorGuiUtilities.MinMaxEditor("  Rotation Limit Z", ref prefabSettings.rotationMinZ, ref prefabSettings.rotationMaxZ, min, max);
            }

            // VS Pro Id
#if VEGETATION_STUDIO_PRO
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("Asset GUID", prefabSettings.assetGUID);
            EditorGUILayout.TextField("VSPro Id", prefabSettings.vspro_VegetationItemID);
            EditorGUI.EndDisabledGroup();
#endif
        }

        private void QuickRotationSetting(PrefabSettings prefabSettings, float x, float y, float z)
        {
            float min = prefabSettings.rotationRange.GetMinimum();
            float max = prefabSettings.rotationRange.GetMaximum();

            prefabSettings.randomRotation = true;

            prefabSettings.rotationMinX = min * x;
            prefabSettings.rotationMaxX = max * x;
            prefabSettings.rotationMinY = min * y;
            prefabSettings.rotationMaxY = max * y;
            prefabSettings.rotationMinZ = min * z;
            prefabSettings.rotationMaxZ = max * z;

        }
    }
}