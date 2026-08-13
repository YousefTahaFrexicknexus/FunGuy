using UnityEngine;

/// <summary>
/// Defines one streamed environment decoration block that can appear beside the route.
/// </summary>
[CreateAssetMenu(fileName = "EnvironmentBlockDefinition", menuName = "Funguy/MushroomRunner/Environment Block Definition")]
public sealed class EnvironmentDecorationDefinition : ScriptableObject
{
    [SerializeField, Tooltip("Decoration prefab spawned for this environment block.")]
    GameObject prefab;
    [SerializeField, Tooltip("Local offset applied when placing the decoration block.")]
    Vector3 localOffset = Vector3.zero;
    [SerializeField, Tooltip("Fallback block length used when render bounds cannot determine the forward size.")]
    float blockLength = 32f;
    [SerializeField, Tooltip("Relative probability when this decoration is eligible inside a theme.")]
    float spawnWeight = 1f;
    [SerializeField, Tooltip("If enabled, decoration instances are recycled through the streamer's pool.")]
    bool usePooling = true;

    public GameObject Prefab => prefab;

    public Vector3 LocalOffset => localOffset;

    public float BlockLength => Mathf.Max(1f, blockLength);

    public Vector3 AuthoredLocalScale => prefab != null ? prefab.transform.localScale : Vector3.one;

    public Quaternion AuthoredLocalRotation => prefab != null ? prefab.transform.localRotation : Quaternion.identity;

    public float SpawnWeight => Mathf.Max(0.01f, spawnWeight);

    public bool UsePooling => usePooling;

    void OnValidate()
    {
        blockLength = Mathf.Max(1f, blockLength);
        spawnWeight = Mathf.Max(0.01f, spawnWeight);
    }
}