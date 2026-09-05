using System;
using UnityEngine;

[Serializable]
public sealed class GridSpawnEntry
{
    [SerializeField, Tooltip("Prefab spawned when this entry is picked.")]
    GameObject prefab;
    [SerializeField, Min(0), Tooltip("Generated-distance score required before this entry can spawn.")]
    int minimumScore;
    [SerializeField, Min(0f), Tooltip("Relative chance when multiple unlocked entries are available.")]
    float spawnWeight = 1f;
    [SerializeField, Tooltip("Offset from the center of the chosen grid cell.")]
    Vector3 localOffset = Vector3.zero;
    [SerializeField, Tooltip("Scale applied to the spawned prefab instance.")]
    Vector3 localScale = Vector3.one;
    [SerializeField, Tooltip("If enabled, spawned instances are recycled instead of destroyed.")]
    bool usePooling = true;
    [SerializeField, Tooltip("Optional bounce profile applied when this entry spawns a mushroom prefab.")]
    MushroomBounceProfile bounceProfileOverride;

    public GameObject Prefab => prefab;

    public int MinimumScore => Mathf.Max(0, minimumScore);

    public float SpawnWeight => Mathf.Max(0f, spawnWeight);

    public Vector3 LocalOffset => localOffset;

    public Vector3 LocalScale => localScale;

    public bool UsePooling => usePooling;

    public MushroomBounceProfile BounceProfileOverride => bounceProfileOverride;

    public bool IsAvailable(int score)
    {
        return prefab != null && score >= MinimumScore && SpawnWeight > 0f;
    }

    public void Validate()
    {
        minimumScore = Mathf.Max(0, minimumScore);
        spawnWeight = Mathf.Max(0f, spawnWeight);
    }
}
