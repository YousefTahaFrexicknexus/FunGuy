using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class GridSpawnEntry
{
    [SerializeField, Tooltip("Prefab spawned when this entry is picked.")]
    GameObject prefab;
    [SerializeField, Min(0), Tooltip("Generated-distance score required before this entry can spawn.")]
    int minimumScore;
    [SerializeField, SurfaceTag, Tooltip("Mushrooms only: tags permitted on the first surface below the grid point. Empty allows any surface, including no hit.")]
    List<string> allowedSurfaceTags = new();
    [SerializeField, SurfaceTag, Tooltip("Mushrooms only: tags forbidden on the first surface below the grid point. Blocked tags override allowed tags. Deeper surfaces are ignored.")]
    List<string> blockedSurfaceTags = new();
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

    public IReadOnlyList<string> AllowedSurfaceTags => allowedSurfaceTags;

    public IReadOnlyList<string> BlockedSurfaceTags => blockedSurfaceTags;

    public bool HasSurfaceRules => (allowedSurfaceTags != null && allowedSurfaceTags.Count > 0)
        || (blockedSurfaceTags != null && blockedSurfaceTags.Count > 0);

    public float SpawnWeight => Mathf.Max(0f, spawnWeight);

    public Vector3 LocalOffset => localOffset;

    public Vector3 LocalScale => localScale;

    public bool UsePooling => usePooling;

    public MushroomBounceProfile BounceProfileOverride => bounceProfileOverride;

    public bool IsAvailable(int score)
    {
        return prefab != null && score >= MinimumScore && SpawnWeight > 0f;
    }

    // A null tag represents no hit. Compare strings so an unknown configured tag
    // simply fails to match instead of throwing from Collider.CompareTag.
    public bool AllowsSurface(string surfaceTag)
    {
        if (surfaceTag != null && ContainsTag(blockedSurfaceTags, surfaceTag))
        {
            return false;
        }

        return allowedSurfaceTags == null || allowedSurfaceTags.Count == 0
            || (surfaceTag != null && ContainsTag(allowedSurfaceTags, surfaceTag));
    }

    static bool ContainsTag(List<string> tags, string surfaceTag)
    {
        if (tags == null) return false;
        for (int index = 0; index < tags.Count; index++)
        {
            if (tags[index] == surfaceTag) return true;
        }
        return false;
    }

    public void Validate()
    {
        minimumScore = Mathf.Max(0, minimumScore);
        spawnWeight = Mathf.Max(0f, spawnWeight);
    }
}
