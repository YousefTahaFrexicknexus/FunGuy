using UnityEngine;

namespace Funguy.IdkPlatformer
{
    [CreateAssetMenu(fileName = "EnvironmentBlockDefinition", menuName = "Funguy/IdkPlatformer/Environment Block Definition")]
    public sealed class EnvironmentDecorationDefinition : ScriptableObject
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private Vector3 localOffset = Vector3.zero;
        [SerializeField] private float blockLength = 32f;
        [SerializeField] private float spawnWeight = 1f;
        [SerializeField] private bool usePooling = true;

        public GameObject Prefab => prefab;

        public Vector3 LocalOffset => localOffset;

        public float BlockLength => Mathf.Max(1f, blockLength);

        public Vector3 AuthoredLocalScale => prefab != null ? prefab.transform.localScale : Vector3.one;

        public Quaternion AuthoredLocalRotation => prefab != null ? prefab.transform.localRotation : Quaternion.identity;

        public float SpawnWeight => Mathf.Max(0.01f, spawnWeight);

        public bool UsePooling => usePooling;

        private void OnValidate()
        {
            blockLength = Mathf.Max(1f, blockLength);
            spawnWeight = Mathf.Max(0.01f, spawnWeight);
        }
    }
}
