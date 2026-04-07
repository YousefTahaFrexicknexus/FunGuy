using System;
using System.Collections.Generic;
using UnityEngine;

namespace Funguy.IdkPlatformer
{
    [CreateAssetMenu(fileName = "BounceAreaGenerationProfile", menuName = "Funguy/IdkPlatformer/Bounce Area Generation Profile")]
    public sealed class BounceAreaGenerationProfile : ScriptableObject
    {
        [Header("Streaming")]
        [SerializeField] private int areaLength = 32;
        [SerializeField] private int spawnAheadAreas = 4;
        [SerializeField] private int recycleBehindAreas = 2;
        [SerializeField] private int introAreaCount = 2;

        [Header("Playable Band")]
        [SerializeField] private float areaHalfWidth = 8f;
        [SerializeField] private float minimumHeight = 0f;
        [SerializeField] private float maximumHeight = 4f;
        [SerializeField] private float surfaceLandingHeight = 0.94f;
        [SerializeField] private float playerCollisionRadius = 0.45f;
        [SerializeField] private float initialLandingSpeed = 2.5f;

        [Header("Path Layout")]
        [SerializeField] private int minimumMainPathNodes = 4;
        [SerializeField] private int maximumMainPathNodes = 6;
        [SerializeField] private int minimumOptionalMushrooms = 1;
        [SerializeField] private int maximumOptionalMushrooms = 3;
        [SerializeField] private int candidateAttemptsPerHop = 18;
        [SerializeField] private int optionalCandidateAttempts = 18;
        [SerializeField] private float minimumForwardGap = 4.5f;
        [SerializeField] private float maximumForwardGap = 8f;
        [SerializeField] private float maximumAdditionalForwardGapFromDifficulty = 3f;
        [SerializeField] private float maximumLateralOffset = 5.5f;
        [SerializeField] private float maximumVerticalStep = 2.25f;
        [SerializeField] private float minimumExitBuffer = 2.5f;
        [SerializeField] private float bailoutForwardGap = 4.25f;
        [SerializeField] private float bailoutVerticalStep = 0.55f;

        [Header("Reach Validation")]
        [SerializeField] private float landingRadius = 1.3f;
        [SerializeField] private float landingHeightTolerance = 1.2f;
        [SerializeField] private float maxSimulationTime = 2.3f;
        [SerializeField] private float simulationTimeStep = 0.02f;
        [SerializeField] private float mainRouteClearanceRadius = 2.5f;
        [SerializeField] private float optionalMushroomClearanceRadius = 2.1f;

        [Header("Environment")]
        [SerializeField] private float decorationSeparationRadius = 4f;
        [SerializeField] private float decorationAreaPadding = 1.5f;
        [SerializeField] private float routeHeadroomClearance = 3.5f;
        [SerializeField] private float cameraSightlineClearance = 6f;

        [Header("Difficulty")]
        [SerializeField] private int difficultyRampDistance = 300;

        [Header("Randomness")]
        [SerializeField] private int seed = 1337;
        [SerializeField] private bool randomizeSeed = true;

        [Header("Content")]
        [SerializeField] private List<BounceSpawnDefinition> mushroomDefinitions = new();
        [SerializeField] private List<EnvironmentThemeTierDefinition> themeTiers = new();

        public int AreaLength => Mathf.Max(8, areaLength);
        public int SpawnAheadAreas => Mathf.Max(1, spawnAheadAreas);
        public int RecycleBehindAreas => Mathf.Max(0, recycleBehindAreas);
        public int IntroAreaCount => Mathf.Max(0, introAreaCount);
        public float AreaHalfWidth => Mathf.Max(1f, areaHalfWidth);
        public float MinimumHeight => minimumHeight;
        public float MaximumHeight => Mathf.Max(minimumHeight, maximumHeight);
        public float SurfaceLandingHeight => Mathf.Max(0f, surfaceLandingHeight);
        public float PlayerCollisionRadius => Mathf.Max(0.05f, playerCollisionRadius);
        public float InitialLandingSpeed => Mathf.Max(0f, initialLandingSpeed);
        public int MinimumMainPathNodes => Mathf.Max(1, minimumMainPathNodes);
        public int MaximumMainPathNodes => Mathf.Max(MinimumMainPathNodes, maximumMainPathNodes);
        public int MinimumOptionalMushrooms => Mathf.Max(0, minimumOptionalMushrooms);
        public int MaximumOptionalMushrooms => Mathf.Max(MinimumOptionalMushrooms, maximumOptionalMushrooms);
        public int CandidateAttemptsPerHop => Mathf.Max(1, candidateAttemptsPerHop);
        public int OptionalCandidateAttempts => Mathf.Max(1, optionalCandidateAttempts);
        public float MinimumForwardGap => Mathf.Max(1f, minimumForwardGap);
        public float MaximumForwardGap => Mathf.Max(MinimumForwardGap, maximumForwardGap);
        public float MaximumAdditionalForwardGapFromDifficulty => Mathf.Max(0f, maximumAdditionalForwardGapFromDifficulty);
        public float MaximumLateralOffset => Mathf.Max(0f, maximumLateralOffset);
        public float MaximumVerticalStep => Mathf.Max(0f, maximumVerticalStep);
        public float MinimumExitBuffer => Mathf.Max(0.5f, minimumExitBuffer);
        public float BailoutForwardGap => Mathf.Max(1f, bailoutForwardGap);
        public float BailoutVerticalStep => Mathf.Max(0f, bailoutVerticalStep);
        public float LandingRadius => Mathf.Max(0.1f, landingRadius);
        public float LandingHeightTolerance => Mathf.Max(0.05f, landingHeightTolerance);
        public float MaxSimulationTime => Mathf.Max(0.1f, maxSimulationTime);
        public float SimulationTimeStep => Mathf.Max(0.005f, simulationTimeStep);
        public float MainRouteClearanceRadius => Mathf.Max(0.1f, mainRouteClearanceRadius);
        public float OptionalMushroomClearanceRadius => Mathf.Max(0.1f, optionalMushroomClearanceRadius);
        public float DecorationSeparationRadius => Mathf.Max(0.1f, decorationSeparationRadius);
        public float DecorationAreaPadding => Mathf.Max(0f, decorationAreaPadding);
        public float RouteHeadroomClearance => Mathf.Max(0f, routeHeadroomClearance);
        public float CameraSightlineClearance => Mathf.Max(0f, cameraSightlineClearance);
        public IReadOnlyList<BounceSpawnDefinition> MushroomDefinitions => mushroomDefinitions;
        public IReadOnlyList<EnvironmentThemeTierDefinition> ThemeTiers => themeTiers;

        public float EvaluateDifficulty01(int score)
        {
            int rampDistance = difficultyRampDistance > 0 ? difficultyRampDistance : 300;
            return Mathf.Clamp01(score / (float)rampDistance);
        }

        public BounceDifficultyTier EvaluateDifficultyTier(int score)
        {
            float difficulty = EvaluateDifficulty01(score);
            if (difficulty < 0.34f)
            {
                return BounceDifficultyTier.Easy;
            }

            if (difficulty < 0.68f)
            {
                return BounceDifficultyTier.Medium;
            }

            return BounceDifficultyTier.Hard;
        }

        public EnvironmentThemeTierDefinition GetActiveTheme(int score)
        {
            EnvironmentThemeTierDefinition bestMatch = null;
            int bestThreshold = int.MinValue;

            for (int index = 0; index < themeTiers.Count; index++)
            {
                EnvironmentThemeTierDefinition tier = themeTiers[index];
                if (tier == null || tier.ScoreThreshold > score || tier.ScoreThreshold < bestThreshold)
                {
                    continue;
                }

                bestMatch = tier;
                bestThreshold = tier.ScoreThreshold;
            }

            if (bestMatch != null)
            {
                return bestMatch;
            }

            for (int index = 0; index < themeTiers.Count; index++)
            {
                if (themeTiers[index] != null)
                {
                    return themeTiers[index];
                }
            }

            return null;
        }

        public int GetSeed()
        {
            return randomizeSeed ? Environment.TickCount : seed;
        }

        private void OnValidate()
        {
            areaLength = Mathf.Max(8, areaLength);
            spawnAheadAreas = Mathf.Max(1, spawnAheadAreas);
            recycleBehindAreas = Mathf.Max(0, recycleBehindAreas);
            introAreaCount = Mathf.Max(0, introAreaCount);
            areaHalfWidth = Mathf.Max(1f, areaHalfWidth);
            maximumHeight = Mathf.Max(minimumHeight, maximumHeight);
            surfaceLandingHeight = Mathf.Max(0f, surfaceLandingHeight);
            playerCollisionRadius = Mathf.Max(0.05f, playerCollisionRadius);
            initialLandingSpeed = Mathf.Max(0f, initialLandingSpeed);
            minimumMainPathNodes = Mathf.Max(1, minimumMainPathNodes);
            maximumMainPathNodes = Mathf.Max(minimumMainPathNodes, maximumMainPathNodes);
            minimumOptionalMushrooms = Mathf.Max(0, minimumOptionalMushrooms);
            maximumOptionalMushrooms = Mathf.Max(minimumOptionalMushrooms, maximumOptionalMushrooms);
            candidateAttemptsPerHop = Mathf.Max(1, candidateAttemptsPerHop);
            optionalCandidateAttempts = Mathf.Max(1, optionalCandidateAttempts);
            minimumForwardGap = Mathf.Max(1f, minimumForwardGap);
            maximumForwardGap = Mathf.Max(minimumForwardGap, maximumForwardGap);
            maximumAdditionalForwardGapFromDifficulty = Mathf.Max(0f, maximumAdditionalForwardGapFromDifficulty);
            maximumLateralOffset = Mathf.Max(0f, maximumLateralOffset);
            maximumVerticalStep = Mathf.Max(0f, maximumVerticalStep);
            minimumExitBuffer = Mathf.Max(0.5f, minimumExitBuffer);
            bailoutForwardGap = Mathf.Max(1f, bailoutForwardGap);
            bailoutVerticalStep = Mathf.Max(0f, bailoutVerticalStep);
            landingRadius = Mathf.Max(0.1f, landingRadius);
            landingHeightTolerance = Mathf.Max(0.05f, landingHeightTolerance);
            maxSimulationTime = Mathf.Max(0.1f, maxSimulationTime);
            simulationTimeStep = Mathf.Max(0.005f, simulationTimeStep);
            mainRouteClearanceRadius = Mathf.Max(0.1f, mainRouteClearanceRadius);
            optionalMushroomClearanceRadius = Mathf.Max(0.1f, optionalMushroomClearanceRadius);
            decorationSeparationRadius = Mathf.Max(0.1f, decorationSeparationRadius);
            decorationAreaPadding = Mathf.Max(0f, decorationAreaPadding);
            routeHeadroomClearance = Mathf.Max(0f, routeHeadroomClearance);
            cameraSightlineClearance = Mathf.Max(0f, cameraSightlineClearance);
            difficultyRampDistance = Mathf.Max(1, difficultyRampDistance);
        }
    }
}
