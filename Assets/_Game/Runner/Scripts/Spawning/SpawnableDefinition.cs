using UnityEngine;

namespace FunGuy.Runner
{
    [CreateAssetMenu(fileName = "SpawnableDefinition", menuName = "FunGuy/Runner/Spawnable Definition")]
    public sealed class SpawnableDefinition : ScriptableObject
    {
        [SerializeField] private SpawnableCategory category = SpawnableCategory.Platform;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Vector3 localOffset = Vector3.zero;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private float spawnWeight = 1f;
        [SerializeField] private bool usePooling = true;
        [SerializeField] private bool limitDifficultyRange;
        [SerializeField] private RunnerDifficultyTier minimumDifficulty = RunnerDifficultyTier.Easy;
        [SerializeField] private RunnerDifficultyTier maximumDifficulty = RunnerDifficultyTier.Hard;

        public SpawnableCategory Category => category;
        public GameObject Prefab => prefab;
        public Vector3 LocalOffset => localOffset;
        public Vector3 LocalScale => localScale;
        public float SpawnWeight => Mathf.Max(0.01f, spawnWeight);
        public bool UsePooling => usePooling;
        public bool LimitDifficultyRange => limitDifficultyRange;
        public RunnerDifficultyTier MinimumDifficulty => minimumDifficulty;
        public RunnerDifficultyTier MaximumDifficulty => maximumDifficulty;

        public bool AllowsDifficulty(RunnerDifficultyTier difficultyTier)
        {
            if (!limitDifficultyRange)
            {
                return true;
            }

            return difficultyTier >= minimumDifficulty && difficultyTier <= maximumDifficulty;
        }

        private void OnValidate()
        {
            if (maximumDifficulty < minimumDifficulty)
            {
                maximumDifficulty = minimumDifficulty;
            }

            spawnWeight = Mathf.Max(0.01f, spawnWeight);
        }
    }
}
