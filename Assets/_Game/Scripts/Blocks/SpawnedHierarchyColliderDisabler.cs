using UnityEngine;

/// <summary>
/// Keeps streamed environment chunks visual-only by disabling colliders on any
/// child hierarchy spawned under this object.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpawnedHierarchyColliderDisabler : MonoBehaviour
{
    private void OnEnable()
    {
        DisableChildColliders();
    }

    private void Start()
    {
        DisableChildColliders();
    }

    private void OnTransformChildrenChanged()
    {
        DisableChildColliders();
    }

    private void DisableChildColliders()
    {
        for (int childIndex = 0; childIndex < transform.childCount; childIndex++)
        {
            Transform child = transform.GetChild(childIndex);
            Collider[] colliders = child.GetComponentsInChildren<Collider>(true);
            for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
            {
                if (colliders[colliderIndex] != null)
                {
                    colliders[colliderIndex].enabled = false;
                }
            }
        }
    }
}
