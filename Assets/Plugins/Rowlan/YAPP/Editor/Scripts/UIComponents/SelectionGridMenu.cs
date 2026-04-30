using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Yapp
{
    public class SelectionGridMenu
    {
        public static GenericMenu Create(List<PrefabSettings> prefabSettingsList)
        {
            SelectionGridMenu selectionGridMenu = new SelectionGridMenu(prefabSettingsList);
            return selectionGridMenu.menu;
        }


        private enum SortBy
        {
            Name,
            Reverse,
            AxisX,
            AxisY,
            AxisZ
        }

        private GenericMenu menu = new GenericMenu();

        private List<PrefabSettings> prefabSettingsList;

        private SelectionGridMenu(List<PrefabSettings> prefabSettingsList) 
        {
            this.prefabSettingsList = prefabSettingsList;

            AddMenuItemForSortAll(menu, "Sort All/Name", SortBy.Name);

            // an empty string will create a separator at the top level
            //menu.AddSeparator("");

            menu.AddSeparator("Sort All/");

            AddMenuItemForSortAll(menu, "Sort All/Axis X", SortBy.AxisX);
            AddMenuItemForSortAll(menu, "Sort All/Axis Y", SortBy.AxisY);
            AddMenuItemForSortAll(menu, "Sort All/Axis Z", SortBy.AxisZ);

            menu.AddSeparator("Sort All/");

            AddMenuItemForSortAll(menu, "Sort All/Reverse", SortBy.Reverse);

        }


        private void AddMenuItemForSortAll(GenericMenu menu, string menuPath, SortBy sortBy)
        {
            menu.AddItem(new GUIContent(menuPath), false, OnSortAll, sortBy);
        }

        private void OnSortAll(object sortByObject)
        {
            SortBy sortBy = (SortBy)sortByObject;

            switch (sortBy)
            {
                case SortBy.Name:
                    prefabSettingsList.Sort(new NameComparer());
                    break;

                case SortBy.Reverse:
                    prefabSettingsList.Reverse();
                    break;

                case SortBy.AxisX:
                    prefabSettingsList.Sort(new SizeComparer(prefabSettingsList, SizeComparer.Axis.X));
                    break;

                case SortBy.AxisY:
                    prefabSettingsList.Sort(new SizeComparer(prefabSettingsList, SizeComparer.Axis.Y));
                    break;

                case SortBy.AxisZ:
                    prefabSettingsList.Sort(new SizeComparer(prefabSettingsList, SizeComparer.Axis.Z));
                    break;
            }
        }

        public class NameComparer : Comparer<PrefabSettings>
        {
            public override int Compare(PrefabSettings a, PrefabSettings b)
            {
                if (a.prefab == null)
                    return -1;

                if (b.prefab == null)
                    return 1;

                return a.prefab.name.CompareTo(b.prefab.name);

            }
        }

        public class SizeComparer : Comparer<PrefabSettings>
        {
            public enum Axis
            {
                X,
                Y,
                Z
            }

            private Dictionary<PrefabSettings, float> prefabSizes = new Dictionary<PrefabSettings, float>();

            public SizeComparer(List<PrefabSettings> prefabSettingsList, Axis axis)
            {
                foreach (PrefabSettings settings in prefabSettingsList)
                {
                    if (settings.prefab == null)
                        continue;

                    Bounds bounds = BoundsUtils.CalculateBounds(settings.prefab);

                    switch (axis)
                    {
                        case Axis.X:
                            prefabSizes.Add(settings, bounds.size.x);
                            break;

                        case Axis.Y:
                            prefabSizes.Add(settings, bounds.size.y);
                            break;

                        case Axis.Z:
                            prefabSizes.Add(settings, bounds.size.z);
                            break;

                    }
                }
            }

            public override int Compare(PrefabSettings a, PrefabSettings b)
            {
                if (a.prefab == null)
                    return -1;

                if (b.prefab == null)
                    return 1;

                return prefabSizes[a].CompareTo(prefabSizes[b]);
            }
        }
    }
}