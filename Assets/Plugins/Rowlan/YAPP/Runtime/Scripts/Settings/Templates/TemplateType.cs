using UnityEngine;

namespace Rowlan.Yapp
{
    public enum TemplateType
    {
        [Tooltip("Regular prefab handling")]
        Prefab = 0,

        [Tooltip("Handle dropped objects as MicroVerse Tree Stamps")]
        [InspectorName("MicroVerse Tree Stamp")]
        MicroVerseTreeStamp = 100,
    }

}
