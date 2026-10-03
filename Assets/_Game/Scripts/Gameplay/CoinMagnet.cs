using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collectable))]
public sealed class CoinMagnet : MonoBehaviour
{
    [Tooltip("Optional override. Otherwise all coins share a cached Player-tagged target.")]
    [SerializeField] Transform target;
    [SerializeField, Min(0.1f)] float attractionRadius = 5f;
    [SerializeField, Min(0.02f)] float proximityCheckInterval = 0.1f;
    [SerializeField, Min(0f)] float startingSpeed = 6f;
    [SerializeField, Min(0.1f)] float acceleration = 80f;
    [SerializeField, Min(0.1f)] float catchUpSpeed = 18f;
    [SerializeField, Min(0f)] float arrivalDistance = 0.25f;
    [SerializeField] Vector3 targetOffset = Vector3.zero;

    static Transform sharedPlayer;
    static float nextPlayerSearch;
    Collectable collectable;
    CoinIdleAnimation[] idleAnimations;
    Transform following;
    Vector3 previousTargetPosition;
    float nextCheck;
    float speed;
    float smoothedPlayerSpeed;

    public bool IsAttracting => isActiveAndEnabled && collectable != null && !collectable.IsCollected
        && following != null && following.gameObject.activeInHierarchy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache()
    {
        sharedPlayer = null;
        nextPlayerSearch = 0f;
    }

    void Awake()
    {
        collectable = GetComponent<Collectable>();
        idleAnimations = GetComponentsInChildren<CoinIdleAnimation>(true);
    }

    void OnEnable()
    {
        following = null;
        speed = startingSpeed;
        smoothedPlayerSpeed = 0f;
        nextCheck = Time.time + Random.Range(0f, proximityCheckInterval);
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (collectable.IsCollected || dt <= 0f) return;
        if (following == null || !following.gameObject.activeInHierarchy)
        {
            following = null;
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + proximityCheckInterval;

            if (target == null && (sharedPlayer == null || !sharedPlayer.gameObject.activeInHierarchy)
                && Time.time >= nextPlayerSearch)
            {
                // At most one scene lookup per second, shared by every coin.
                nextPlayerSearch = Time.time + 1f;
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                sharedPlayer = player != null ? player.transform : null;
            }

            Transform candidate = target != null ? target : sharedPlayer;
            if (candidate == null || !candidate.gameObject.activeInHierarchy) return;
            Vector3 destination = candidate.TransformPoint(targetOffset);
            if ((destination - transform.position).sqrMagnitude > attractionRadius * attractionRadius) return;
            following = candidate;
            previousTargetPosition = destination;
            speed = startingSpeed;
            smoothedPlayerSpeed = 0f;
            foreach (CoinIdleAnimation animation in idleAnimations)
            {
                if (animation != null) animation.StopForAttraction();
            }
        }

        // Follow the rendered player pose every frame, after normal movement updates.
        Vector3 targetPosition = following.TransformPoint(targetOffset);
        float playerSpeed = (targetPosition - previousTargetPosition).magnitude / dt;
        previousTargetPosition = targetPosition;
        // Filter physics-step velocity spikes instead of permanently retaining the largest one.
        smoothedPlayerSpeed = Mathf.Lerp(smoothedPlayerSpeed, playerSpeed, 1f - Mathf.Exp(-12f * dt));
        // No fixed speed cap that could prevent catching a fast player.
        speed = Mathf.MoveTowards(speed, Mathf.Max(startingSpeed, smoothedPlayerSpeed + catchUpSpeed), acceleration * dt);
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * dt);

        // Arrival fallback avoids missed trigger callbacks at high attraction speeds.
        if ((targetPosition - transform.position).sqrMagnitude <= arrivalDistance * arrivalDistance)
            collectable.Collect();
    }
}
