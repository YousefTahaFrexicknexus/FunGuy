using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Funguy.IdkPlatformer
{
    [DisallowMultipleComponent]
    public sealed class EndlessBounceAreaStreamer : MonoBehaviour
    {
        private const float OverspeedGapMultiplier = 0.65f;
        private const float OverspeedLookaheadMultiplier = 1.35f;
        private const float MaximumAreaLookaheadMultiplier = 1.5f;
        private const float EmergencySearchStep = 1.25f;
        private const int EmergencyLateralSampleCount = 5;
        private const float ForcedRecoveryMinimumGap = 2.75f;
        private const float ForcedRecoveryMaximumGap = 4.25f;
        private const float ForcedRecoveryClearanceMultiplier = 0.55f;

        private sealed class ActiveArea
        {
            public int Index;
            public float StartZ;
            public float EndZ;
            public readonly List<SpawnedRuntime> SpawnedObjects = new();
            public readonly List<RouteNodeState> RouteNodes = new();
        }

        private readonly struct SpawnedRuntime
        {
            public SpawnedRuntime(Object poolKey, GameObject instance, bool usePooling)
            {
                PoolKey = poolKey;
                Instance = instance;
                UsePooling = usePooling;
            }

            public Object PoolKey { get; }

            public GameObject Instance { get; }

            public bool UsePooling { get; }
        }

        private readonly struct RouteNodeState
        {
            public RouteNodeState(Vector3 rootPosition, Vector3 incomingVelocity, BounceSpawnDefinition spawnDefinition)
            {
                RootPosition = rootPosition;
                IncomingVelocity = incomingVelocity;
                SpawnDefinition = spawnDefinition;
            }

            public Vector3 RootPosition { get; }

            public Vector3 IncomingVelocity { get; }

            public BounceSpawnDefinition SpawnDefinition { get; }

            public MushroomBounceProfile BounceProfile => SpawnDefinition != null ? SpawnDefinition.BounceProfileOverride : null;
        }

        [SerializeField] private Transform player;
        [SerializeField] private Transform mushroomRoot;
        [SerializeField] private Transform decorationRoot;
        [SerializeField] private BounceAreaGenerationProfile generationProfile;
        [SerializeField] private MovementTuningProfile tuningProfile;
        [SerializeField] private ForwardProgressScoreTracker scoreTracker;
        [SerializeField] private BounceSpawnDefinition startSpawnDefinition;
        [SerializeField] private Vector3 startMushroomPosition = Vector3.zero;
        [SerializeField] private bool autoInitializeOnStart = true;
        [SerializeField] private Vector3 worldUp = Vector3.up;

        private readonly Queue<ActiveArea> activeAreas = new();
        private readonly Dictionary<Object, Stack<GameObject>> pools = new();
        private readonly List<Vector3> occupiedPositions = new(32);

        private System.Random random;
        private RouteNodeState routeExitState;
        private float runStartZ;
        private int nextAreaIndexToGenerate;
        private bool initialized;

        private Vector3 Up => worldUp.sqrMagnitude > BounceMovementMath.MinimumDirectionSqrMagnitude
            ? worldUp.normalized
            : Vector3.up;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void Start()
        {
            if (autoInitializeOnStart)
            {
                BuildInitialWorld();
            }
        }

        private void Update()
        {
            if (!initialized || !ResolveReferences())
            {
                return;
            }

            int playerArea = GetPlayerAreaIndex();
            EnsureAreasForPlayer(playerArea);
            RecycleAreasBehindPlayer(playerArea);
        }

        public void BuildInitialWorld()
        {
            if (!ResolveReferences() || generationProfile == null || tuningProfile == null)
            {
                Debug.LogError("[EndlessBounceAreaStreamer] Missing required references.");
                return;
            }

            EnsureResetFloorSetup();

            BounceSpawnDefinition initialDefinition = ResolveStartDefinition();
            if (initialDefinition == null || initialDefinition.Prefab == null || initialDefinition.BounceProfileOverride == null)
            {
                Debug.LogError("[EndlessBounceAreaStreamer] Missing a valid start bounce definition.");
                return;
            }

            ClearSpawnedWorld();

            random = new System.Random(generationProfile.GetSeed());
            runStartZ = startMushroomPosition.z;
            routeExitState = new RouteNodeState(
                startMushroomPosition,
                -Up * generationProfile.InitialLandingSpeed,
                initialDefinition);
            nextAreaIndexToGenerate = 0;
            initialized = true;

            if (scoreTracker != null)
            {
                scoreTracker.SetTarget(player);
                scoreTracker.ResetProgress(runStartZ);
            }

            EnsureAreasForPlayer(GetPlayerAreaIndex());
        }

        public void ClearGeneratedWorld()
        {
            ClearSpawnedWorld();
            initialized = false;
        }

        private bool ResolveReferences()
        {
            if (player == null)
            {
                PlayerController playerController = FindFirstObjectByType<PlayerController>();
                player = playerController != null ? playerController.transform : null;
            }

            if (scoreTracker == null)
            {
                scoreTracker = FindFirstObjectByType<ForwardProgressScoreTracker>();
            }

            if (mushroomRoot == null)
            {
                mushroomRoot = transform;
            }

            if (decorationRoot == null)
            {
                decorationRoot = transform;
            }

            return player != null;
        }

        private int GetPlayerAreaIndex()
        {
            float relativeZ = player.position.z - runStartZ;
            return Mathf.Max(0, Mathf.FloorToInt(relativeZ / Mathf.Max(1, generationProfile.AreaLength)));
        }

        private void EnsureAreasForPlayer(int playerArea)
        {
            int targetArea = playerArea + generationProfile.SpawnAheadAreas;
            while (nextAreaIndexToGenerate <= targetArea)
            {
                GenerateArea(nextAreaIndexToGenerate);
                nextAreaIndexToGenerate++;
            }
        }

        private void GenerateArea(int areaIndex)
        {
            ActiveArea area = new()
            {
                Index = areaIndex,
                StartZ = GetAreaStartZ(areaIndex),
                EndZ = GetAreaStartZ(areaIndex) + generationProfile.AreaLength
            };

            bool isIntroArea = areaIndex < generationProfile.IntroAreaCount;
            int areaScore = Mathf.Max(0, Mathf.FloorToInt(area.StartZ - runStartZ));
            RouteNodeState currentState = routeExitState;

            if (areaIndex == 0)
            {
                SpawnMushroom(currentState.RootPosition, currentState.SpawnDefinition, area);
                area.RouteNodes.Add(currentState);
            }

            int targetNodes = ResolveMainPathNodeCount(areaScore, isIntroArea);
            int safetyBudget = Mathf.Max(10, targetNodes * 3);
            int generatedNodes = 0;

            while (safetyBudget-- > 0 &&
                   (generatedNodes < targetNodes || currentState.RootPosition.z < ResolveRouteTargetEndZ(area, currentState)))
            {
                BounceIntentDirective intent = ResolveIntentForCurrentNode(currentState, isIntroArea);
                BounceSpawnDefinition nextDefinition = PickNextRouteDefinition(areaScore, isIntroArea);

                if (!TryBuildRouteNode(currentState, nextDefinition, intent, area, isIntroArea, areaScore, out RouteNodeState nextState))
                {
                    if (!TryBuildBailoutNode(currentState, nextDefinition, area, out nextState))
                    {
                        break;
                    }
                }

                SpawnMushroom(nextState.RootPosition, nextState.SpawnDefinition, area);
                area.RouteNodes.Add(nextState);
                currentState = nextState;
                generatedNodes++;
            }

            if (generatedNodes == 0 || currentState.RootPosition.z < area.EndZ - generationProfile.MinimumExitBuffer)
            {
                while (generatedNodes < targetNodes || currentState.RootPosition.z < area.EndZ - generationProfile.MinimumExitBuffer)
                {
                    if (!TryBuildEmergencyRecoveryNode(currentState, area, areaScore, out RouteNodeState recoveryState))
                    {
                        break;
                    }

                    SpawnMushroom(recoveryState.RootPosition, recoveryState.SpawnDefinition, area);
                    area.RouteNodes.Add(recoveryState);
                    currentState = recoveryState;
                    generatedNodes++;
                }
            }

            routeExitState = currentState;
            SpawnOptionalMushrooms(area, areaScore, isIntroArea);
            SpawnEnvironmentBlock(area, areaScore);
            activeAreas.Enqueue(area);
        }

        private bool TryBuildRouteNode(
            RouteNodeState currentState,
            BounceSpawnDefinition nextDefinition,
            BounceIntentDirective intent,
            ActiveArea area,
            bool isIntroArea,
            int areaScore,
            out RouteNodeState nextState)
        {
            float difficulty01 = generationProfile.EvaluateDifficulty01(areaScore);
            if (isIntroArea)
            {
                difficulty01 *= 0.45f;
            }

            for (int attempt = 0; attempt < generationProfile.CandidateAttemptsPerHop; attempt++)
            {
                Vector3 candidate = SampleGoldenPathPosition(currentState, area, intent, difficulty01);
                if (!IsRoutePositionClear(candidate, currentState.RootPosition, area.RouteNodes, generationProfile.MainRouteClearanceRadius))
                {
                    continue;
                }

                if (!TryEvaluateHop(currentState, candidate, intent, out BounceReachResult result))
                {
                    continue;
                }

                nextState = new RouteNodeState(candidate, result.LandingVelocity, nextDefinition);
                return true;
            }

            nextState = default;
            return false;
        }

        private bool TryBuildBailoutNode(
            RouteNodeState currentState,
            BounceSpawnDefinition preferredNextDefinition,
            ActiveArea area,
            out RouteNodeState nextState)
        {
            BounceSpawnDefinition fallbackDefinition = preferredNextDefinition ?? ResolveStartDefinition();
            float fallbackGap = generationProfile.BailoutForwardGap;

            for (int attempt = 0; attempt < 5; attempt++)
            {
                float targetZ = Mathf.Min(
                    currentState.RootPosition.z + fallbackGap + (attempt * 0.35f),
                    area.EndZ - Mathf.Max(0.75f, generationProfile.MinimumExitBuffer * 0.5f));

                if (targetZ <= currentState.RootPosition.z + 0.5f)
                {
                    continue;
                }

                Vector3 candidate = new(
                    Mathf.MoveTowards(currentState.RootPosition.x, 0f, 1.75f + attempt),
                    Mathf.Clamp(
                        Mathf.MoveTowards(currentState.RootPosition.y, 0.75f, generationProfile.BailoutVerticalStep + (attempt * 0.15f)),
                        generationProfile.MinimumHeight,
                        generationProfile.MaximumHeight),
                    targetZ);

                if (!IsRoutePositionClear(candidate, currentState.RootPosition, area.RouteNodes, generationProfile.MainRouteClearanceRadius))
                {
                    continue;
                }

                if (!TryEvaluateHop(currentState, candidate, ResolveIntentForCurrentNode(currentState, false), out BounceReachResult result))
                {
                    continue;
                }

                nextState = new RouteNodeState(candidate, result.LandingVelocity, fallbackDefinition);
                return true;
            }

            nextState = default;
            return false;
        }

        private bool TryBuildEmergencyRecoveryNode(
            RouteNodeState currentState,
            ActiveArea area,
            int areaScore,
            out RouteNodeState nextState)
        {
            nextState = default;

            BounceDifficultyTier difficultyTier = generationProfile.EvaluateDifficultyTier(areaScore);
            BounceSpawnDefinition recoveryDefinition = PickDefinitionByTag(BounceSpawnTag.Slow, difficultyTier, true)
                ?? PickDefinitionByTag(BounceSpawnTag.Normal, difficultyTier, true)
                ?? ResolveStartDefinition();

            if (recoveryDefinition == null)
            {
                return false;
            }

            float baseSearchZ = currentState.RootPosition.z + Mathf.Max(generationProfile.BailoutForwardGap, 2f);
            float searchEndZ = ResolveRouteSearchLimitZ(area, currentState) + (generationProfile.AreaLength * 0.35f);
            float centeredY = Mathf.Clamp(
                Mathf.MoveTowards(currentState.RootPosition.y, Mathf.Max(generationProfile.MinimumHeight, 0.8f), generationProfile.BailoutVerticalStep + 0.25f),
                generationProfile.MinimumHeight,
                generationProfile.MaximumHeight);

            for (float targetZ = baseSearchZ; targetZ <= searchEndZ; targetZ += EmergencySearchStep)
            {
                for (int lateralIndex = 0; lateralIndex < EmergencyLateralSampleCount; lateralIndex++)
                {
                    float lateralT = EmergencyLateralSampleCount == 1
                        ? 0f
                        : lateralIndex / (float)(EmergencyLateralSampleCount - 1);
                    float lateralOffset = Mathf.Lerp(-1.75f, 1.75f, lateralT);
                    float targetX = Mathf.Clamp(
                        Mathf.MoveTowards(currentState.RootPosition.x, 0f, 2.25f) + lateralOffset,
                        -generationProfile.AreaHalfWidth,
                        generationProfile.AreaHalfWidth);

                    Vector3 candidate = new(targetX, centeredY, targetZ);
                    if (!IsRoutePositionClear(candidate, currentState.RootPosition, area.RouteNodes, generationProfile.MainRouteClearanceRadius))
                    {
                        continue;
                    }

                    if (!TryEvaluateHop(currentState, candidate, BounceIntentDirective.Brake, out BounceReachResult result) &&
                        !TryEvaluateHop(currentState, candidate, BounceIntentDirective.Maintain, out result))
                    {
                        continue;
                    }

                    nextState = new RouteNodeState(candidate, result.LandingVelocity, recoveryDefinition);
                    return true;
                }
            }

            if (TryBuildForcedRecoveryNode(currentState, area, recoveryDefinition, out nextState))
            {
                return true;
            }

            Debug.LogWarning($"[EndlessBounceAreaStreamer] Failed to build a recovery bridge for area {area.Index}. Current route position: {currentState.RootPosition}");
            return false;
        }

        private bool TryBuildForcedRecoveryNode(
            RouteNodeState currentState,
            ActiveArea area,
            BounceSpawnDefinition recoveryDefinition,
            out RouteNodeState nextState)
        {
            nextState = default;

            float conservativeGap = Mathf.Clamp(
                Mathf.Min(generationProfile.BailoutForwardGap, generationProfile.MinimumForwardGap),
                ForcedRecoveryMinimumGap,
                ForcedRecoveryMaximumGap);
            float targetY = Mathf.Clamp(
                Mathf.MoveTowards(currentState.RootPosition.y, Mathf.Max(generationProfile.MinimumHeight, 0.9f), generationProfile.BailoutVerticalStep + 0.35f),
                generationProfile.MinimumHeight,
                generationProfile.MaximumHeight);
            float targetZ = Mathf.Min(
                currentState.RootPosition.z + conservativeGap,
                ResolveRouteSearchLimitZ(area, currentState));

            if (targetZ <= currentState.RootPosition.z + 0.5f)
            {
                targetZ = currentState.RootPosition.z + 0.5f;
            }

            float[] lateralOffsets = { 0f, -0.75f, 0.75f, -1.5f, 1.5f };
            float relaxedClearance = Mathf.Max(0.5f, generationProfile.MainRouteClearanceRadius * ForcedRecoveryClearanceMultiplier);

            for (int index = 0; index < lateralOffsets.Length; index++)
            {
                float targetX = Mathf.Clamp(
                    Mathf.MoveTowards(currentState.RootPosition.x, 0f, 1.5f) + lateralOffsets[index],
                    -generationProfile.AreaHalfWidth,
                    generationProfile.AreaHalfWidth);
                Vector3 candidate = new(targetX, targetY, targetZ);

                if (!IsRoutePositionClear(candidate, currentState.RootPosition, area.RouteNodes, relaxedClearance))
                {
                    continue;
                }

                nextState = new RouteNodeState(
                    candidate,
                    EstimateForcedLandingVelocity(currentState),
                    recoveryDefinition);
                return true;
            }

            return false;
        }

        private void SpawnOptionalMushrooms(ActiveArea area, int areaScore, bool isIntroArea)
        {
            if (isIntroArea || area.RouteNodes.Count == 0)
            {
                return;
            }

            int targetCount = RandomRangeInclusive(
                generationProfile.MinimumOptionalMushrooms,
                generationProfile.MaximumOptionalMushrooms);

            occupiedPositions.Clear();
            for (int index = 0; index < area.RouteNodes.Count; index++)
            {
                occupiedPositions.Add(area.RouteNodes[index].RootPosition);
            }

            for (int count = 0; count < targetCount; count++)
            {
                for (int attempt = 0; attempt < generationProfile.OptionalCandidateAttempts; attempt++)
                {
                    RouteNodeState anchor = area.RouteNodes[random.Next(area.RouteNodes.Count)];
                    Vector3 candidate = SampleOptionalPosition(anchor, area, areaScore);

                    if (!IsPositionClear(candidate, occupiedPositions, generationProfile.OptionalMushroomClearanceRadius))
                    {
                        continue;
                    }

                    if (!TryEvaluateHop(anchor, candidate, ResolveIntentForCurrentNode(anchor, false), out _))
                    {
                        continue;
                    }

                    BounceSpawnDefinition definition = PickNextRouteDefinition(areaScore, false);
                    SpawnMushroom(candidate, definition, area);
                    occupiedPositions.Add(candidate);
                    break;
                }
            }
        }

        private void SpawnEnvironmentBlock(ActiveArea area, int areaScore)
        {
            EnvironmentThemeTierDefinition theme = generationProfile.GetActiveTheme(areaScore);
            if (theme == null || theme.Blocks == null || theme.Blocks.Count == 0)
            {
                return;
            }

            EnvironmentDecorationDefinition definition = PickDecorationDefinition(theme);
            if (definition == null || definition.Prefab == null)
            {
                return;
            }

            SpawnEnvironmentBlockInstance(area, definition);
        }

        private bool TryEvaluateHop(
            RouteNodeState currentState,
            Vector3 targetPosition,
            BounceIntentDirective intent,
            out BounceReachResult result)
        {
            result = default;

            MushroomBounceProfile launchProfile = currentState.BounceProfile;
            if (launchProfile == null)
            {
                return false;
            }

            BounceReachRequest request = new(
                currentState.RootPosition,
                currentState.IncomingVelocity,
                targetPosition,
                launchProfile,
                tuningProfile,
                intent,
                Up,
                generationProfile.SurfaceLandingHeight,
                generationProfile.PlayerCollisionRadius,
                generationProfile.LandingRadius,
                generationProfile.LandingHeightTolerance,
                generationProfile.SimulationTimeStep,
                generationProfile.MaxSimulationTime);

            return BounceReachEvaluator.TryEvaluate(request, out result);
        }

        private Vector3 SampleGoldenPathPosition(
            RouteNodeState currentState,
            ActiveArea area,
            BounceIntentDirective intent,
            float difficulty01)
        {
            ResolveForwardGapRange(currentState, difficulty01, out float baseMinGap, out float baseMaxGap);

            float minGapMultiplier = intent switch
            {
                BounceIntentDirective.Brake => 0.82f,
                BounceIntentDirective.Boost => 1.2f,
                _ => 1f
            };

            float maxGapMultiplier = intent switch
            {
                BounceIntentDirective.Brake => 0.92f,
                BounceIntentDirective.Boost => 1.35f,
                _ => 1.05f
            };

            float lateralLimit = Mathf.Lerp(1.35f, generationProfile.MaximumLateralOffset, difficulty01);
            float verticalLimit = Mathf.Lerp(0.4f, generationProfile.MaximumVerticalStep, difficulty01);

            float targetZ = currentState.RootPosition.z + RandomRange(baseMinGap * minGapMultiplier, baseMaxGap * maxGapMultiplier);
            targetZ = Mathf.Min(targetZ, ResolveRouteSearchLimitZ(area, currentState));
            if (targetZ <= currentState.RootPosition.z + 0.75f)
            {
                targetZ = currentState.RootPosition.z + 0.75f;
            }

            float targetX = Mathf.Clamp(
                currentState.RootPosition.x + RandomRange(-lateralLimit, lateralLimit),
                -generationProfile.AreaHalfWidth,
                generationProfile.AreaHalfWidth);

            float targetY = Mathf.Clamp(
                currentState.RootPosition.y + RandomRange(-verticalLimit, verticalLimit),
                generationProfile.MinimumHeight,
                generationProfile.MaximumHeight);

            return new Vector3(targetX, targetY, targetZ);
        }

        private void ResolveForwardGapRange(RouteNodeState currentState, float difficulty01, out float minimumGap, out float maximumGap)
        {
            float speedGapBonus = ResolveOverspeedGapBonus(currentState);
            minimumGap = generationProfile.MinimumForwardGap + (speedGapBonus * 0.4f);
            maximumGap = generationProfile.MaximumForwardGap
                + (generationProfile.MaximumAdditionalForwardGapFromDifficulty * difficulty01)
                + speedGapBonus;

            if (maximumGap < minimumGap)
            {
                maximumGap = minimumGap;
            }
        }

        private Vector3 SampleOptionalPosition(RouteNodeState anchor, ActiveArea area, int areaScore)
        {
            float difficulty01 = generationProfile.EvaluateDifficulty01(areaScore);
            float sideOffset = Mathf.Lerp(2.75f, generationProfile.AreaHalfWidth * 0.9f, 0.45f + (difficulty01 * 0.35f));
            float sign = random.NextDouble() <= 0.5d ? -1f : 1f;
            float x = Mathf.Clamp(anchor.RootPosition.x + (sign * RandomRange(2.25f, sideOffset)), -generationProfile.AreaHalfWidth, generationProfile.AreaHalfWidth);
            float y = Mathf.Clamp(anchor.RootPosition.y + RandomRange(-1f, 1.25f), generationProfile.MinimumHeight, generationProfile.MaximumHeight);
            float z = Mathf.Clamp(
                anchor.RootPosition.z + RandomRange(-1.5f, 4.5f),
                area.StartZ,
                area.EndZ - generationProfile.MinimumExitBuffer);
            return new Vector3(x, y, z);
        }

        private bool IsRoutePositionClear(
            Vector3 candidate,
            Vector3 currentRootPosition,
            List<RouteNodeState> routeNodes,
            float clearanceRadius)
        {
            if (!IsPositionClear(candidate, routeNodes, clearanceRadius))
            {
                return false;
            }

            Vector3 planarDelta = Vector3.ProjectOnPlane(candidate - currentRootPosition, Up);
            return planarDelta.magnitude >= clearanceRadius * 0.4f;
        }

        private bool IsPositionClear(Vector3 candidate, List<RouteNodeState> routeNodes, float clearanceRadius)
        {
            for (int index = 0; index < routeNodes.Count; index++)
            {
                if ((routeNodes[index].RootPosition - candidate).sqrMagnitude < clearanceRadius * clearanceRadius)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsPositionClear(Vector3 candidate, List<Vector3> positions, float clearanceRadius)
        {
            float minDistanceSqr = clearanceRadius * clearanceRadius;
            for (int index = 0; index < positions.Count; index++)
            {
                if ((positions[index] - candidate).sqrMagnitude < minDistanceSqr)
                {
                    return false;
                }
            }

            return true;
        }

        private int ResolveMainPathNodeCount(int areaScore, bool isIntroArea)
        {
            float difficulty01 = generationProfile.EvaluateDifficulty01(areaScore);
            if (isIntroArea)
            {
                difficulty01 *= 0.4f;
            }

            return Mathf.RoundToInt(Mathf.Lerp(
                generationProfile.MinimumMainPathNodes,
                generationProfile.MaximumMainPathNodes,
                difficulty01));
        }

        private float ResolveRouteTargetEndZ(ActiveArea area, RouteNodeState currentState)
        {
            return ResolveRouteSearchLimitZ(area, currentState);
        }

        private float ResolveRouteSearchLimitZ(ActiveArea area, RouteNodeState currentState)
        {
            float lookahead = ResolveOverspeedLookahead(currentState);
            return area.EndZ + lookahead - generationProfile.MinimumExitBuffer;
        }

        private float ResolveOverspeedLookahead(RouteNodeState currentState)
        {
            float launchSpeed = EstimateLaunchPlanarSpeed(currentState);
            float referenceSpeed = tuningProfile != null
                ? Mathf.Max(1f, tuningProfile.MaxControllableSpeed)
                : Mathf.Max(1f, generationProfile.MaximumForwardGap);
            float overspeed = Mathf.Max(0f, launchSpeed - referenceSpeed);
            float lookahead = overspeed * OverspeedLookaheadMultiplier;
            float maxLookahead = generationProfile.AreaLength * MaximumAreaLookaheadMultiplier;
            return Mathf.Clamp(lookahead, 0f, maxLookahead);
        }

        private float ResolveOverspeedGapBonus(RouteNodeState currentState)
        {
            float launchSpeed = EstimateLaunchPlanarSpeed(currentState);
            float referenceSpeed = tuningProfile != null
                ? Mathf.Max(1f, tuningProfile.MaxControllableSpeed)
                : Mathf.Max(1f, generationProfile.MaximumForwardGap);
            float overspeed = Mathf.Max(0f, launchSpeed - referenceSpeed);
            return overspeed * OverspeedGapMultiplier;
        }

        private float EstimateLaunchPlanarSpeed(RouteNodeState currentState)
        {
            Vector3 launchVelocity = EstimateLaunchVelocity(currentState);
            return Vector3.ProjectOnPlane(launchVelocity, Up).magnitude;
        }

        private Vector3 EstimateLaunchVelocity(RouteNodeState currentState)
        {
            if (tuningProfile == null || currentState.BounceProfile == null)
            {
                return currentState.IncomingVelocity;
            }

            BounceContext context = new(
                currentState.IncomingVelocity,
                currentState.RootPosition,
                Up,
                Up,
                tuningProfile.BaseJumpForce,
                MovementInputFrame.Empty);

            BounceSurfaceResponse response = currentState.BounceProfile.CreateResponse(null, context);
            return BounceMovementMath.ApplyBounceResponse(currentState.IncomingVelocity, response, Up);
        }

        private Vector3 EstimateForcedLandingVelocity(RouteNodeState currentState)
        {
            Vector3 launchVelocity = EstimateLaunchVelocity(currentState);
            Vector3 planarVelocity = Vector3.ProjectOnPlane(launchVelocity, Up);
            float minimumPlanarSpeed = Mathf.Max(1.5f, generationProfile.InitialLandingSpeed);
            float cappedPlanarSpeed = tuningProfile != null
                ? Mathf.Min(planarVelocity.magnitude, tuningProfile.MaxControllableSpeed * 0.9f)
                : planarVelocity.magnitude;

            if (planarVelocity.sqrMagnitude > BounceMovementMath.MinimumDirectionSqrMagnitude)
            {
                planarVelocity = planarVelocity.normalized * Mathf.Max(minimumPlanarSpeed, cappedPlanarSpeed);
            }
            else
            {
                planarVelocity = Vector3.forward * minimumPlanarSpeed;
            }

            Vector3 downwardVelocity = -Up * Mathf.Max(generationProfile.InitialLandingSpeed, 2f);
            return planarVelocity + downwardVelocity;
        }

        private BounceIntentDirective ResolveIntentForCurrentNode(RouteNodeState routeNode, bool isIntroArea)
        {
            if (routeNode.SpawnDefinition == null)
            {
                return BounceIntentDirective.Maintain;
            }

            if (isIntroArea && routeNode.SpawnDefinition.GameplayTag == BounceSpawnTag.Boost)
            {
                return BounceIntentDirective.Maintain;
            }

            return routeNode.SpawnDefinition.GameplayTag switch
            {
                BounceSpawnTag.Slow => BounceIntentDirective.Brake,
                BounceSpawnTag.Boost => BounceIntentDirective.Boost,
                _ => BounceIntentDirective.Maintain
            };
        }

        private BounceSpawnDefinition ResolveStartDefinition()
        {
            if (startSpawnDefinition != null)
            {
                return startSpawnDefinition;
            }

            return PickDefinitionByTag(BounceSpawnTag.Normal, BounceDifficultyTier.Easy, true)
                   ?? PickDefinitionByTag(BounceSpawnTag.Slow, BounceDifficultyTier.Easy, true)
                   ?? PickAnyDefinition();
        }

        private BounceSpawnDefinition PickNextRouteDefinition(int areaScore, bool isIntroArea)
        {
            BounceDifficultyTier difficultyTier = generationProfile.EvaluateDifficultyTier(areaScore);
            float difficulty01 = generationProfile.EvaluateDifficulty01(areaScore);

            float boostWeight = Mathf.Lerp(0.08f, 0.4f, difficulty01);
            float slowWeight = Mathf.Lerp(0.24f, 0.18f, difficulty01);
            float normalWeight = Mathf.Max(0.1f, 1f - boostWeight - slowWeight);

            if (isIntroArea)
            {
                boostWeight *= 0.15f;
                normalWeight += 0.15f;
            }

            float total = normalWeight + boostWeight + slowWeight;
            float roll = (float)random.NextDouble() * total;

            BounceSpawnTag tag = roll <= normalWeight
                ? BounceSpawnTag.Normal
                : roll <= normalWeight + boostWeight
                    ? BounceSpawnTag.Boost
                    : BounceSpawnTag.Slow;

            return PickDefinitionByTag(tag, difficultyTier, true)
                   ?? PickDefinitionByTag(BounceSpawnTag.Normal, difficultyTier, true)
                   ?? PickAnyDefinition();
        }

        private BounceSpawnDefinition PickDefinitionByTag(BounceSpawnTag tag, BounceDifficultyTier difficultyTier, bool allowFallback)
        {
            IReadOnlyList<BounceSpawnDefinition> definitions = generationProfile.MushroomDefinitions;
            if (definitions == null || definitions.Count == 0)
            {
                return null;
            }

            float totalWeight = 0f;
            bool foundEligibleDefinition = false;

            for (int index = 0; index < definitions.Count; index++)
            {
                BounceSpawnDefinition definition = definitions[index];
                if (definition == null || definition.GameplayTag != tag || !definition.AllowsDifficulty(difficultyTier))
                {
                    continue;
                }

                totalWeight += definition.SpawnWeight;
                foundEligibleDefinition = true;
            }

            if (!foundEligibleDefinition)
            {
                if (!allowFallback)
                {
                    return null;
                }

                for (int index = 0; index < definitions.Count; index++)
                {
                    BounceSpawnDefinition definition = definitions[index];
                    if (definition == null || definition.GameplayTag != tag)
                    {
                        continue;
                    }

                    totalWeight += definition.SpawnWeight;
                }
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float roll = (float)random.NextDouble() * totalWeight;
            float cursor = 0f;

            for (int index = 0; index < definitions.Count; index++)
            {
                BounceSpawnDefinition definition = definitions[index];
                if (definition == null || definition.GameplayTag != tag)
                {
                    continue;
                }

                if (foundEligibleDefinition && !definition.AllowsDifficulty(difficultyTier))
                {
                    continue;
                }

                cursor += definition.SpawnWeight;
                if (roll <= cursor)
                {
                    return definition;
                }
            }

            return null;
        }

        private BounceSpawnDefinition PickAnyDefinition()
        {
            IReadOnlyList<BounceSpawnDefinition> definitions = generationProfile.MushroomDefinitions;
            if (definitions == null || definitions.Count == 0)
            {
                return null;
            }

            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index] != null)
                {
                    return definitions[index];
                }
            }

            return null;
        }

        private EnvironmentDecorationDefinition PickDecorationDefinition(EnvironmentThemeTierDefinition theme)
        {
            IReadOnlyList<EnvironmentDecorationDefinition> definitions = theme.Blocks;
            if (definitions == null || definitions.Count == 0)
            {
                return null;
            }

            float totalWeight = 0f;
            for (int index = 0; index < definitions.Count; index++)
            {
                if (definitions[index] != null)
                {
                    totalWeight += definitions[index].SpawnWeight;
                }
            }

            if (totalWeight <= 0f)
            {
                return null;
            }

            float roll = (float)random.NextDouble() * totalWeight;
            float cursor = 0f;

            for (int index = 0; index < definitions.Count; index++)
            {
                EnvironmentDecorationDefinition definition = definitions[index];
                if (definition == null)
                {
                    continue;
                }

                cursor += definition.SpawnWeight;
                if (roll <= cursor)
                {
                    return definition;
                }
            }

            return null;
        }

        private void SpawnMushroom(Vector3 rootPosition, BounceSpawnDefinition definition, ActiveArea area)
        {
            if (definition == null || definition.Prefab == null)
            {
                return;
            }

            GameObject instance = GetInstance(definition, definition.Prefab, definition.UsePooling);
            if (instance == null)
            {
                return;
            }

            instance.transform.SetParent(mushroomRoot, false);
            instance.transform.position = rootPosition + definition.LocalOffset;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = definition.LocalScale;

            Mushroom mushroom = instance.GetComponent<Mushroom>();
            if (mushroom == null)
            {
                mushroom = instance.GetComponentInChildren<Mushroom>();
            }

            if (mushroom != null && definition.BounceProfileOverride != null)
            {
                mushroom.SetBounceProfile(definition.BounceProfileOverride);
            }

            area.SpawnedObjects.Add(new SpawnedRuntime(definition, instance, definition.UsePooling));
        }

        private void SpawnEnvironmentBlockInstance(ActiveArea area, EnvironmentDecorationDefinition definition)
        {
            if (definition == null || definition.Prefab == null)
            {
                return;
            }

            GameObject instance = GetInstance(definition, definition.Prefab, definition.UsePooling);
            if (instance == null)
            {
                return;
            }

            instance.transform.SetParent(decorationRoot, false);
            instance.transform.localPosition = new Vector3(0f, 0f, area.StartZ) + definition.LocalOffset;
            instance.transform.localRotation = definition.AuthoredLocalRotation;
            instance.transform.localScale = definition.AuthoredLocalScale;
            area.SpawnedObjects.Add(new SpawnedRuntime(definition, instance, definition.UsePooling));
        }

        private GameObject GetInstance(Object poolKey, GameObject prefab, bool usePooling)
        {
            if (usePooling && poolKey != null && pools.TryGetValue(poolKey, out Stack<GameObject> pool) && pool.Count > 0)
            {
                GameObject pooled = pool.Pop();
                pooled.SetActive(true);
                return pooled;
            }

            GameObject created = Instantiate(prefab);
            created.SetActive(true);
            return created;
        }

        private void RecycleAreasBehindPlayer(int playerArea)
        {
            while (activeAreas.Count > 0 && playerArea - activeAreas.Peek().Index > generationProfile.RecycleBehindAreas)
            {
                RecycleArea(activeAreas.Dequeue());
            }
        }

        private void RecycleArea(ActiveArea area)
        {
            for (int index = 0; index < area.SpawnedObjects.Count; index++)
            {
                SpawnedRuntime spawned = area.SpawnedObjects[index];
                if (spawned.Instance == null)
                {
                    continue;
                }

                if (spawned.UsePooling && spawned.PoolKey != null)
                {
                    spawned.Instance.SetActive(false);

                    if (!pools.TryGetValue(spawned.PoolKey, out Stack<GameObject> pool))
                    {
                        pool = new Stack<GameObject>();
                        pools.Add(spawned.PoolKey, pool);
                    }

                    pool.Push(spawned.Instance);
                }
                else
                {
                    Destroy(spawned.Instance);
                }
            }
        }

        private void ClearSpawnedWorld()
        {
            while (activeAreas.Count > 0)
            {
                RecycleArea(activeAreas.Dequeue());
            }
        }

        private float GetAreaStartZ(int areaIndex)
        {
            return startMushroomPosition.z + (areaIndex * generationProfile.AreaLength);
        }

        private void EnsureResetFloorSetup()
        {
            PlayerController playerController = player != null ? player.GetComponent<PlayerController>() : null;
            if (playerController == null)
            {
                playerController = FindFirstObjectByType<PlayerController>();
            }

            if (playerController == null)
            {
                return;
            }

            RunResetCoordinator resetCoordinator = FindFirstObjectByType<RunResetCoordinator>();
            if (resetCoordinator == null)
            {
                GameObject coordinatorObject = new("RunResetCoordinator");
                resetCoordinator = coordinatorObject.AddComponent<RunResetCoordinator>();
            }

            resetCoordinator.Configure(playerController, this);

            InstantLosePlatform losePlatform = FindFirstObjectByType<InstantLosePlatform>();
            if (losePlatform == null)
            {
                GameObject losePlatformObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                losePlatformObject.name = "InstantLosePlatform";
                losePlatformObject.transform.SetParent(transform, false);
                losePlatformObject.transform.position = new Vector3(0f, -22f, 10000f);
                losePlatformObject.transform.localScale = new Vector3(256f, 20f, 22000f);

                BoxCollider collider = losePlatformObject.GetComponent<BoxCollider>();
                if (collider != null)
                {
                    collider.isTrigger = true;
                }

                losePlatform = losePlatformObject.AddComponent<InstantLosePlatform>();
            }

            losePlatform.transform.position = new Vector3(0f, -22f, 10000f);
            losePlatform.transform.localScale = new Vector3(256f, 20f, 22000f);

            BoxCollider losePlatformCollider = losePlatform.GetComponent<BoxCollider>();
            if (losePlatformCollider != null)
            {
                losePlatformCollider.isTrigger = true;
            }

            losePlatform.Configure(resetCoordinator, playerController.transform, false, 0f);
        }

        private float RandomRange(float minimum, float maximum)
        {
            if (maximum <= minimum)
            {
                return minimum;
            }

            return minimum + ((float)random.NextDouble() * (maximum - minimum));
        }

        private int RandomRangeInclusive(int minimum, int maximum)
        {
            if (maximum <= minimum)
            {
                return minimum;
            }

            return random.Next(minimum, maximum + 1);
        }
    }
}
