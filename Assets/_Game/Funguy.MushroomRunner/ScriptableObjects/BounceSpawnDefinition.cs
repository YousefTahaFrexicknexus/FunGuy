using UnityEngine;

namespace Funguy.MushroomRunner
{
    /// <summary>
    /// Describes one spawnable mushroom variant the course generator can place.
    /// </summary>
    [CreateAssetMenu(fileName = "BounceSpawnDefinition", menuName = "Funguy/MushroomRunner/Bounce Spawn Definition")]
    public sealed class BounceSpawnDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Prefab spawned for this mushroom or bounce surface entry.")]
        private GameObject prefab;
        [SerializeField, Tooltip("Bounce response applied to the spawned mushroom when the player hits it.")]
        private MushroomBounceProfile bounceProfileOverride;
        [SerializeField, Tooltip("High-level route role used by the generator when selecting this spawn.")]
        private BounceSpawnTag gameplayTag = BounceSpawnTag.Normal;
        [SerializeField, Tooltip("Offset applied from the sampled root position when spawning the prefab.")]
        private Vector3 localOffset = Vector3.zero;
        [SerializeField, Tooltip("Scale applied to the spawned prefab instance.")]
        private Vector3 localScale = Vector3.one;
        [SerializeField, Tooltip("Relative probability when multiple eligible spawn definitions can be chosen.")]
        private float spawnWeight = 1f;
        [SerializeField, Tooltip("If enabled, spawned instances are recycled through the course streamer's pool.")]
        private bool usePooling = true;
        [SerializeField, Tooltip("If enabled, this definition only appears inside the selected difficulty range.")]
        private bool limitDifficultyRange;
        [SerializeField, Tooltip("Lowest difficulty tier this definition can spawn in.")]
        private BounceDifficultyTier minimumDifficulty = BounceDifficultyTier.Easy;
        [SerializeField, Tooltip("Highest difficulty tier this definition can spawn in.")]
        private BounceDifficultyTier maximumDifficulty = BounceDifficultyTier.Hard;

        public GameObject Prefab => prefab;

        public MushroomBounceProfile BounceProfileOverride => bounceProfileOverride;

        public BounceSpawnTag GameplayTag => gameplayTag;

        public Vector3 LocalOffset => localOffset;

        public Vector3 LocalScale => localScale;

        public float SpawnWeight => Mathf.Max(0.01f, spawnWeight);

        public bool UsePooling => usePooling;

        public bool LimitDifficultyRange => limitDifficultyRange;

        public BounceDifficultyTier MinimumDifficulty => minimumDifficulty;

        public BounceDifficultyTier MaximumDifficulty => maximumDifficulty;

        public bool AllowsDifficulty(BounceDifficultyTier difficultyTier)
        {
            if (!limitDifficultyRange)
            {
                return true;
            }

            return difficultyTier >= minimumDifficulty && difficultyTier <= maximumDifficulty;
        }

        private void OnValidate()
        {
            spawnWeight = Mathf.Max(0.01f, spawnWeight);

            if (maximumDifficulty < minimumDifficulty)
            {
                maximumDifficulty = minimumDifficulty;
            }
        }
    }
}

