using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Yapp
{
    [CreateAssetMenu(fileName = Constants.TemplateCollection_FileName, menuName = Constants.TemplateCollection_MenuName)]
    [System.Serializable]
    public class PrefabTemplateCollection : ScriptableObject
    {
        /// <summary>
        /// The name which will be displayed in the prefab template grid of the inspector
        /// </summary>
        public string displayName;

        /// <summary>
        /// How to handle the objects that are dropped on a template
        /// </summary>
        public CollectionType collectionType = CollectionType.Prefab;

        /// <summary>
        /// Whether the collection is used or not
        /// </summary>
        public bool active = true;

        /// <summary>
        /// Collection of various prefab settings templates
        /// </summary>
        [SerializeField]
        public List<PrefabSettingsTemplate> templates = new List<PrefabSettingsTemplate>();
    }
}