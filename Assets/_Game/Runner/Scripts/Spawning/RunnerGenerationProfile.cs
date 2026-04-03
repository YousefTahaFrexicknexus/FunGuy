using System.Collections.Generic;
using UnityEngine;

namespace FunGuy.Runner
{
    [CreateAssetMenu(fileName = "RunnerGenerationProfile", menuName = "FunGuy/Runner/Generation Profile")]
    public sealed class RunnerGenerationProfile : ScriptableObject
    {
        [SerializeField] private int chunkLength = 12;
        [SerializeField] private int spawnAheadChunks = 4;
        [SerializeField] private int recycleBehindChunks = 2;

        [Header("Intro")]
        [SerializeField] private int introFilledSlices = 5;
        [SerializeField] private bool introFillAllLanes = true;
        [SerializeField] private bool introDisableRandomContent = true;

        [Header("Difficulty Ramp")]
        [SerializeField] private int difficultyRampDistanceCells = 180;
        [SerializeField, Range(0f, 1f)] private float startingLaneChangeChance = 0.08f;
        [SerializeField, Range(0f, 1f)] private float startingLayerChangeChance = 0f;
        [SerializeField, Range(0f, 1f)] private float startingBonusPlatformChance = 0.1f;
        [SerializeField, Range(0f, 1f)] private float startingHazardChance = 0f;
        [SerializeField, Range(0f, 1f)] private float startingCollectibleChance = 0.55f;
        [SerializeField, Range(0f, 1f)] private float startingSupportPlatformChance = 0.42f;

        [Header("Path Variation")]
        [SerializeField, Range(0f, 1f)] private float laneChangeChance = 0.45f;
        [SerializeField, Range(0f, 1f)] private float layerChangeChance = 0.2f;

        [Header("Slice Density")]
        [SerializeField, Range(0f, 1f)] private float bonusPlatformChance = 0.35f;
        [SerializeField, Range(0f, 1f)] private float hazardChance = 0.2f;
        [SerializeField, Range(0f, 1f)] private float collectibleChance = 0.4f;
        [SerializeField, Range(0f, 1f)] private float supportPlatformChance = 0.16f;

        [Header("Support Platforms")]
        [SerializeField] private bool alwaysSpawnSupportPlatformOnClimb = true;
        [SerializeField] private int minimumSlicesBetweenSupportPlatforms = 4;

        [Header("Randomness")]
        [SerializeField] private int seed = 1337;
        [SerializeField] private bool randomizeSeed = true;

        [Header("Definitions")]
        [SerializeField] private List<SpawnableDefinition> platformDefinitions = new();
        [SerializeField] private List<SpawnableDefinition> supportPlatformDefinitions = new();
        [SerializeField] private List<SpawnableDefinition> hazardDefinitions = new();
        [SerializeField] private List<SpawnableDefinition> collectibleDefinitions = new();

        public int ChunkLength => chunkLength;
        public int SpawnAheadChunks => spawnAheadChunks;
        public int RecycleBehindChunks => recycleBehindChunks;
        public int IntroFilledSlices => introFilledSlices;
        public bool IntroFillAllLanes => introFillAllLanes;
        public bool IntroDisableRandomContent => introDisableRandomContent;
        public float LaneChangeChance => laneChangeChance;
        public float LayerChangeChance => layerChangeChance;
        public float BonusPlatformChance => bonusPlatformChance;
        public float HazardChance => hazardChance;
        public float CollectibleChance => collectibleChance;
        public IReadOnlyList<SpawnableDefinition> PlatformDefinitions => platformDefinitions;
        public IReadOnlyList<SpawnableDefinition> SupportPlatformDefinitions => supportPlatformDefinitions;
        public IReadOnlyList<SpawnableDefinition> HazardDefinitions => hazardDefinitions;
        public IReadOnlyList<SpawnableDefinition> CollectibleDefinitions => collectibleDefinitions;
        public bool AlwaysSpawnSupportPlatformOnClimb => alwaysSpawnSupportPlatformOnClimb;
        public int MinimumSlicesBetweenSupportPlatforms => minimumSlicesBetweenSupportPlatforms;

        public float EvaluateDifficulty01(int traveledCells)
        {
            int rampDistance = difficultyRampDistanceCells > 0 ? difficultyRampDistanceCells : 180;
            return Mathf.Clamp01(traveledCells / (float)rampDistance);
        }

        public RunnerDifficultyTier EvaluateDifficultyTier(int traveledCells)
        {
            float difficulty = EvaluateDifficulty01(traveledCells);

            if (difficulty < 0.34f)
            {
                return RunnerDifficultyTier.Easy;
            }

            if (difficulty < 0.68f)
            {
                return RunnerDifficultyTier.Medium;
            }

            return RunnerDifficultyTier.Hard;
        }

        public float GetLaneChangeChance(int traveledCells)
        {
            return Mathf.Lerp(startingLaneChangeChance, laneChangeChance, EvaluateDifficulty01(traveledCells));
        }

        public float GetLayerChangeChance(int traveledCells)
        {
            return Mathf.Lerp(startingLayerChangeChance, layerChangeChance, EvaluateDifficulty01(traveledCells));
        }

        public float GetBonusPlatformChance(int traveledCells)
        {
            return Mathf.Lerp(startingBonusPlatformChance, bonusPlatformChance, EvaluateDifficulty01(traveledCells));
        }

        public float GetHazardChance(int traveledCells)
        {
            return Mathf.Lerp(startingHazardChance, hazardChance, EvaluateDifficulty01(traveledCells));
        }

        public float GetCollectibleChance(int traveledCells)
        {
            return Mathf.Lerp(startingCollectibleChance, collectibleChance, EvaluateDifficulty01(traveledCells));
        }

        public float GetSupportPlatformChance(int traveledCells)
        {
            return Mathf.Lerp(startingSupportPlatformChance, supportPlatformChance, EvaluateDifficulty01(traveledCells));
        }

        public int GetSeed()
        {
            return randomizeSeed ? System.Environment.TickCount : seed;
        }

        private void OnValidate()
        {
            chunkLength = Mathf.Max(4, chunkLength);
            spawnAheadChunks = Mathf.Max(1, spawnAheadChunks);
            recycleBehindChunks = Mathf.Max(0, recycleBehindChunks);
            introFilledSlices = Mathf.Max(0, introFilledSlices);
            difficultyRampDistanceCells = Mathf.Max(1, difficultyRampDistanceCells);
            minimumSlicesBetweenSupportPlatforms = Mathf.Max(1, minimumSlicesBetweenSupportPlatforms);
        }
    }
}
