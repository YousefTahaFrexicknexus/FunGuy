using System;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Defines how the endless route is streamed, how reachable mushroom paths are built,
/// and which content can appear as difficulty increases.
/// </summary>
[CreateAssetMenu(fileName = "BounceAreaGenerationProfile", menuName = "Funguy/MushroomRunner/Bounce Area Generation Profile")]
public sealed class BounceAreaGenerationProfile : ScriptableObject
{
    [Header("Streaming")]
    [SerializeField, Tooltip("Length of one generated streaming area in world units.")]
    int areaLength = 32;
    [SerializeField, Tooltip("How many areas stay generated in front of the player's current area.")]
    int spawnAheadAreas = 4;
    [SerializeField, Tooltip("How many areas remain behind the player before they are recycled.")]
    int recycleBehindAreas = 2;
    [SerializeField, Tooltip("Number of opening areas that use gentler generation for the start of a run.")]
    int introAreaCount = 2;

    [Header("Playable Band")]
    [SerializeField, Tooltip("Half-width of the playable lane. Route mushrooms stay within +/- this X range.")]
    float areaHalfWidth = 8f;
    [SerializeField, Tooltip("Minimum Y height used for generated mushroom root positions.")]
    float minimumHeight = 0f;
    [SerializeField, Tooltip("Maximum Y height used for generated mushroom root positions.")]
    float maximumHeight = 4f;
    [SerializeField, Tooltip("Approximate landing height on top of a mushroom used by reach validation.")]
    float surfaceLandingHeight = 0.94f;
    [SerializeField, Tooltip("Approximate player collision radius used when validating spacing and landings.")]
    float playerCollisionRadius = 0.45f;
    [SerializeField, Tooltip("Initial downward landing speed assumed when seeding the first bounce.")]
    float initialLandingSpeed = 2.5f;

    [Header("Path Layout")]
    [SerializeField, Tooltip("Minimum number of main-route mushrooms generated per area.")]
    int minimumMainPathNodes = 4;
    [SerializeField, Tooltip("Maximum number of main-route mushrooms generated per area.")]
    int maximumMainPathNodes = 6;
    [SerializeField, Tooltip("Minimum number of optional side mushrooms generated per area.")]
    int minimumOptionalMushrooms = 1;
    [SerializeField, Tooltip("Maximum number of optional side mushrooms generated per area.")]
    int maximumOptionalMushrooms = 3;
    [SerializeField, Tooltip("How many candidate positions are tested before giving up on a main route hop.")]
    int candidateAttemptsPerHop = 18;
    [SerializeField, Tooltip("How many candidate positions are tested when placing optional mushrooms.")]
    int optionalCandidateAttempts = 18;
    [SerializeField, Tooltip("Minimum forward distance between consecutive route mushrooms.")]
    float minimumForwardGap = 4.5f;
    [SerializeField, Tooltip("Maximum forward distance between consecutive route mushrooms before difficulty bonuses.")]
    float maximumForwardGap = 8f;
    [SerializeField, Tooltip("Extra forward gap that can be added once the run reaches max difficulty.")]
    float maximumAdditionalForwardGapFromDifficulty = 3f;
    [SerializeField, Tooltip("Minimum side-to-side offset used when a hop changes lanes.")]
    float minimumLateralOffset = 2.1f;
    [SerializeField, Tooltip("Maximum side-to-side offset allowed for sampled hops.")]
    float maximumLateralOffset = 5.5f;
    [SerializeField, Range(0f, 1f), Tooltip("How strongly route candidates are scattered instead of clumping together.")]
    float validPathScatter = 0.65f;
    [SerializeField, Range(0f, 1f), Tooltip("Bias for pushing main-route samples toward the outer edges of the lane.")]
    float mainPathOuterBias = 0.55f;
    [SerializeField, Range(0f, 1f), Tooltip("Bias for pushing optional mushrooms farther from the main route.")]
    float optionalPathOuterBias = 0.7f;
    [SerializeField, Tooltip("Maximum Y change allowed between consecutive route mushrooms.")]
    float maximumVerticalStep = 2.25f;
    [SerializeField, Tooltip("Minimum clear space kept near the end of an area so the route can exit cleanly.")]
    float minimumExitBuffer = 2.5f;
    [SerializeField, Tooltip("Fallback forward gap used when normal route generation fails.")]
    float bailoutForwardGap = 4.25f;
    [SerializeField, Tooltip("Fallback vertical step used for bailout or recovery mushrooms.")]
    float bailoutVerticalStep = 0.55f;

    [Header("Reach Validation")]
    [SerializeField, Tooltip("Horizontal tolerance used when deciding whether a simulated landing counts.")]
    float landingRadius = 1.3f;
    [SerializeField, Tooltip("Vertical tolerance used when validating simulated landings.")]
    float landingHeightTolerance = 1.2f;
    [SerializeField, Tooltip("Longest flight time tested when checking whether a hop is reachable.")]
    float maxSimulationTime = 2.3f;
    [SerializeField, Tooltip("Time step used by the reach simulator. Smaller is more accurate but slower.")]
    float simulationTimeStep = 0.02f;
    [SerializeField, Tooltip("Minimum spacing between main-route mushrooms.")]
    float mainRouteClearanceRadius = 2.5f;
    [SerializeField, Tooltip("Minimum spacing between optional mushrooms and other placed mushrooms.")]
    float optionalMushroomClearanceRadius = 2.1f;

    [Header("Environment")]
    [SerializeField, Tooltip("Minimum spacing between spawned environment decorations.")]
    float decorationSeparationRadius = 4f;
    [SerializeField, Tooltip("Extra inset from the playable band before decorations may be placed.")]
    float decorationAreaPadding = 1.5f;
    [SerializeField, Tooltip("Vertical clearance kept above the route before placing decorations.")]
    float routeHeadroomClearance = 3.5f;
    [SerializeField, Tooltip("Clear space kept in front of the camera sightline when placing decorations.")]
    float cameraSightlineClearance = 6f;

    [Header("Difficulty")]
    [SerializeField, Tooltip("Score distance required to reach full generation difficulty.")]
    int difficultyRampDistance = 300;

    [Header("Randomness")]
    [SerializeField, Tooltip("Fixed random seed used when Randomize Seed is disabled.")]
    int seed = 1337;
    [SerializeField, Tooltip("If enabled, each run uses a new random seed instead of the fixed Seed value.")]
    bool randomizeSeed = true;

    [Header("Content")]
    [SerializeField, Tooltip("Spawn definitions the course generator can pick from for route mushrooms.")]
    List<BounceSpawnDefinition> mushroomDefinitions = new();
    [SerializeField, Tooltip("Decoration themes that unlock at different score thresholds.")]
    List<EnvironmentThemeTierDefinition> themeTiers = new();

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
    public float MinimumLateralOffset => Mathf.Clamp(minimumLateralOffset, 0f, MaximumLateralOffset);
    public float MaximumLateralOffset => Mathf.Max(0f, maximumLateralOffset);
    public float ValidPathScatter => Mathf.Clamp01(validPathScatter);
    public float MainPathOuterBias => Mathf.Clamp01(mainPathOuterBias);
    public float OptionalPathOuterBias => Mathf.Clamp01(optionalPathOuterBias);
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
        if (!randomizeSeed)
        {
            return seed;
        }

        unchecked
        {
            return Guid.NewGuid().GetHashCode() ^ Environment.TickCount ^ seed;
        }
    }

    void OnValidate()
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
        minimumLateralOffset = Mathf.Max(0f, minimumLateralOffset);
        maximumLateralOffset = Mathf.Max(0f, maximumLateralOffset);
        
        if (minimumLateralOffset > maximumLateralOffset)
        {
            minimumLateralOffset = maximumLateralOffset;
        }

        validPathScatter = Mathf.Clamp01(validPathScatter);
        mainPathOuterBias = Mathf.Clamp01(mainPathOuterBias);
        optionalPathOuterBias = Mathf.Clamp01(optionalPathOuterBias);
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