using UnityEngine;

namespace Funguy.IdkPlatformer
{
    [DisallowMultipleComponent]
    public sealed class InstantLosePlatform : MonoBehaviour
    {
        private const float ResetCooldown = 0.15f;
        private const float DefaultPostResetGraceTime = 0.25f;

        [SerializeField] private RunResetCoordinator resetCoordinator;
        [SerializeField] private PlayerController trackedPlayer;
        [SerializeField] private Transform trackedTarget;
        [SerializeField] private bool useAutomaticDeathHeight = true;
        [SerializeField] private float deathHeightY = -12f;
        [SerializeField] private bool enableDebugLogging = true;
        [SerializeField, Min(0f)] private float postResetGraceTime = DefaultPostResetGraceTime;

        private float lastResetTime = float.NegativeInfinity;
        private float ignoreDeathUntil = float.NegativeInfinity;
        private Collider cachedCollider;
        private bool hasLoggedBelowDeathHeight;

        private void Reset()
        {
            ResolveReferences();
            RemoveLegacyRigidbody();
            CacheCollider();
            RefreshDeathHeight();
        }

        private void Awake()
        {
            ResolveReferences();
            RemoveLegacyRigidbody();
            CacheCollider();
            RefreshDeathHeight();

            if (enableDebugLogging)
            {
                Debug.Log($"[InstantLosePlatform] Ready. Death height Y: {deathHeightY:F2} Trigger bounds: {GetTriggerBounds()} Tracked target: {DescribeTarget()}");
            }
        }

        public void Configure(RunResetCoordinator coordinator, Transform target, bool shouldFollowTargetOnZ, float targetZOffset)
        {
            resetCoordinator = coordinator;
            trackedTarget = target;
            trackedPlayer = target != null ? target.GetComponentInParent<PlayerController>() : null;
            RefreshDeathHeight();

            if (enableDebugLogging)
            {
                Debug.Log($"[InstantLosePlatform] Configured. Death height Y: {deathHeightY:F2} Trigger bounds: {GetTriggerBounds()} Tracked target: {DescribeTarget()}");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TryReset(other, "OnTriggerEnter");
        }

        private void Update()
        {
            CheckHeightKill();
        }

        private void TryReset(Collider other, string source)
        {
            if (other == null)
            {
                if (enableDebugLogging)
                {
                    Debug.LogWarning($"[InstantLosePlatform] {source} fired with a null collider.");
                }

                return;
            }

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null)
            {
                return;
            }

            TryReset(player, source);
        }

        private void TryReset(PlayerController player, string source)
        {
            if (player == null)
            {
                return;
            }

            if (resetCoordinator == null)
            {
                ResolveReferences();
            }

            if (resetCoordinator == null)
            {
                if (enableDebugLogging)
                {
                    Debug.LogWarning($"[InstantLosePlatform] {source} hit player '{player.name}', but no RunResetCoordinator was found. Trigger bounds: {GetTriggerBounds()}");
                }

                return;
            }

            if (Time.time < lastResetTime + ResetCooldown)
            {
                if (enableDebugLogging)
                {
                    Debug.Log($"[InstantLosePlatform] {source} hit player '{player.name}' but reset is still on cooldown. Player position: {player.transform.position}");
                }

                return;
            }

            if (enableDebugLogging)
            {
                Debug.LogWarning($"[InstantLosePlatform] {source} resetting player '{player.name}'. Player position: {player.transform.position} Trigger bounds: {GetTriggerBounds()}");
            }

            lastResetTime = Time.time;
            ignoreDeathUntil = Time.time + postResetGraceTime;
            hasLoggedBelowDeathHeight = false;
            resetCoordinator.ResetRun();
        }

        private void ResolveReferences()
        {
            if (resetCoordinator == null)
            {
                resetCoordinator = FindFirstObjectByType<RunResetCoordinator>();
            }

            if (trackedTarget == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                {
                    trackedPlayer = player;
                    trackedTarget = player.transform;
                }
            }
            else if (trackedPlayer == null)
            {
                trackedPlayer = trackedTarget.GetComponentInParent<PlayerController>();
            }
        }

        private void CacheCollider()
        {
            cachedCollider = GetComponent<Collider>();
        }

        private void RefreshDeathHeight()
        {
            if (!useAutomaticDeathHeight)
            {
                return;
            }

            deathHeightY = transform.position.y + (transform.lossyScale.y * 0.5f);
        }

        private Bounds GetTriggerBounds()
        {
            if (cachedCollider == null)
            {
                CacheCollider();
            }

            return cachedCollider != null
                ? cachedCollider.bounds
                : new Bounds(transform.position, Vector3.zero);
        }

        private void CheckHeightKill()
        {
            ResolveReferences();

            if (trackedPlayer == null || trackedTarget == null)
            {
                return;
            }

            if (Time.time < ignoreDeathUntil)
            {
                return;
            }

            if (trackedTarget.position.y > deathHeightY)
            {
                hasLoggedBelowDeathHeight = false;
                return;
            }

            if (enableDebugLogging && !hasLoggedBelowDeathHeight)
            {
                Debug.LogWarning($"[InstantLosePlatform] HeightThreshold detected player below death height. Player position: {trackedTarget.position} Death height Y: {deathHeightY:F2}");
                hasLoggedBelowDeathHeight = true;
            }

            TryReset(trackedPlayer, "HeightThreshold");
        }

        private string DescribeTarget()
        {
            return trackedTarget != null
                ? $"{trackedTarget.name} @ {trackedTarget.position}"
                : "<missing>";
        }

        private void RemoveLegacyRigidbody()
        {
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(body);
                }
                else
                {
                    DestroyImmediate(body);
                }
            }
        }
    }
}
