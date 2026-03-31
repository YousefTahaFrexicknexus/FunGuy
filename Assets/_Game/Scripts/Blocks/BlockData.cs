using UnityEngine;

/// <summary>
/// ScriptableObject defining a single block's prefab and its Z-axis length.
/// Create via: Assets > Create > Block Spawner > Block Data
/// </summary>
[CreateAssetMenu(fileName = "BlockData", menuName = "Block Spawner/Block Data")]
public class BlockData : ScriptableObject
{
    [Tooltip("The prefab to instantiate for this block.")]
    public GameObject prefab;

    [Tooltip("The length of this block along the Z-axis (world units).")]
    public float zLength = 40f;
}
