using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public class Collectable : MonoBehaviour
{
    [SerializeField, Min(1)] int value = 1;
    [SerializeField] UnityEvent<int> onCollected = new();

    public bool IsCollected { get; private set; }
    public bool IsBeingAttracted => magnet != null && magnet.IsAttracting;

    CoinMagnet magnet;
    Vector3 spawnLocalPosition;
    Quaternion spawnLocalRotation;
    Vector3 spawnLocalScale;
    bool hasSpawnPose;

    void Awake() => CaptureSpawnPose();

    void Reset() => GetComponent<SphereCollider>().isTrigger = true;

    void OnEnable() => IsCollected = false;

    void CaptureSpawnPose()
    {
        if (hasSpawnPose) return;
        magnet = GetComponent<CoinMagnet>();
        spawnLocalPosition = transform.localPosition;
        spawnLocalRotation = transform.localRotation;
        spawnLocalScale = transform.localScale;
        hasSpawnPose = true;
    }

    // Called while the bundle is inactive, before the grid places it in its new cell.
    // Also supports inactive prefab children whose Awake has not run yet.
    public void ResetForReuse()
    {
        CaptureSpawnPose();
        transform.localPosition = spawnLocalPosition;
        transform.localRotation = spawnLocalRotation;
        transform.localScale = spawnLocalScale;
        IsCollected = false;
    }

    void OnTriggerEnter(Collider other)
    {
        // Supports colliders on children of the tagged player object.
        for (Transform candidate = other.transform; candidate != null; candidate = candidate.parent)
        {
            if (!candidate.CompareTag("Player")) continue;
            Collect();
            return;
        }
    }

    public void Collect()
    {
        if (IsCollected || !isActiveAndEnabled) return;
        IsCollected = true;
        // Disable immediately so multiple player colliders cannot collect twice.
        gameObject.SetActive(false);
        transform.localPosition = spawnLocalPosition;
        transform.localRotation = spawnLocalRotation;
        transform.localScale = spawnLocalScale;
        GameplayEvents.OnCoinCollected?.Invoke(value);
        onCollected.Invoke(value);
    }
}
