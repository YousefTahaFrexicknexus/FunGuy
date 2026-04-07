using UnityEngine;

namespace Funguy.IdkPlatformer
{
    [DisallowMultipleComponent]
    public sealed class RunResetCoordinator : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private EndlessBounceAreaStreamer areaStreamer;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private bool captureInitialPlayerPose = true;
        [SerializeField] private bool autoFindReferences = true;

        private Vector3 cachedSpawnPosition;
        private Quaternion cachedSpawnRotation = Quaternion.identity;
        private bool hasCachedSpawnPose;

        private void Reset()
        {
            ResolveReferences();
            CacheSpawnPoseIfNeeded();
        }

        private void Awake()
        {
            ResolveReferences();
            CacheSpawnPoseIfNeeded();
        }

        public void ResetRun()
        {
            if (!ResolveReferences())
            {
                Debug.LogWarning("[RunResetCoordinator] Cannot reset the run because the player reference is missing.");
                return;
            }

            CacheSpawnPoseIfNeeded();
            player.ResetRun(cachedSpawnPosition, cachedSpawnRotation);

            if (areaStreamer != null)
            {
                areaStreamer.BuildInitialWorld();
            }
        }

        public void SetSpawnPoint(Transform newSpawnPoint)
        {
            spawnPoint = newSpawnPoint;
            hasCachedSpawnPose = false;
            CacheSpawnPoseIfNeeded();
        }

        public void Configure(PlayerController playerController, EndlessBounceAreaStreamer streamer)
        {
            player = playerController;
            areaStreamer = streamer;

            if (!hasCachedSpawnPose)
            {
                CacheSpawnPoseIfNeeded();
            }
        }

        public void SetSpawnPose(Vector3 worldPosition, Quaternion worldRotation)
        {
            cachedSpawnPosition = worldPosition;
            cachedSpawnRotation = worldRotation;
            hasCachedSpawnPose = true;
        }

        private bool ResolveReferences()
        {
            if (player == null && autoFindReferences)
            {
                player = FindFirstObjectByType<PlayerController>();
            }

            if (areaStreamer == null && autoFindReferences)
            {
                areaStreamer = FindFirstObjectByType<EndlessBounceAreaStreamer>();
            }

            return player != null;
        }

        private void CacheSpawnPoseIfNeeded()
        {
            if (hasCachedSpawnPose)
            {
                return;
            }

            if (spawnPoint != null)
            {
                cachedSpawnPosition = spawnPoint.position;
                cachedSpawnRotation = spawnPoint.rotation;
                hasCachedSpawnPose = true;
                return;
            }

            if (captureInitialPlayerPose && player != null)
            {
                cachedSpawnPosition = player.transform.position;
                cachedSpawnRotation = player.transform.rotation;
                hasCachedSpawnPose = true;
            }
        }
    }
}
