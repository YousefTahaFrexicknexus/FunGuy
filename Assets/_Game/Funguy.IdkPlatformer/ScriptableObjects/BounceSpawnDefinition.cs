using UnityEngine;

namespace Funguy.IdkPlatformer
{
    [CreateAssetMenu(fileName = "BounceSpawnDefinition", menuName = "Funguy/IdkPlatformer/Bounce Spawn Definition")]
    public sealed class BounceSpawnDefinition : ScriptableObject
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private MushroomBounceProfile bounceProfileOverride;
        [SerializeField] private BounceSpawnTag gameplayTag = BounceSpawnTag.Normal;
        [SerializeField] private Vector3 localOffset = Vector3.zero;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private float spawnWeight = 1f;
        [SerializeField] private bool usePooling = true;
        [SerializeField] private bool limitDifficultyRange;
        [SerializeField] private BounceDifficultyTier minimumDifficulty = BounceDifficultyTier.Easy;
        [SerializeField] private BounceDifficultyTier maximumDifficulty = BounceDifficultyTier.Hard;

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
