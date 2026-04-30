using UnityEngine;

namespace Rowlan.Yapp
{
    public enum CollectionType
    {
        [Tooltip("Regular prefab collection")]
        Prefab = 0,

        [Tooltip("MicroVerse specific collection")]
        [InspectorName("MicroVerse")]
        MicroVerse = 10,
    }

}
