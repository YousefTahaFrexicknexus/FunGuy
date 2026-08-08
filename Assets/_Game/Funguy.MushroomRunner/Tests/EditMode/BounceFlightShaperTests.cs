using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Funguy.MushroomRunner.Tests.EditMode
{
    public sealed class BounceFlightShaperTests
    {
        private const string ProductionTuningPath = "Assets/_Game/Funguy.MushroomRunner/ScriptableObjects/Config/MovementTuningProfile.asset";
        private const string GenerationProfilePath = "Assets/_Game/Funguy.MushroomRunner/ScriptableObjects/Config/Generation/BounceAreaGenerationProfile.asset";
        private const string StandardBouncePath = "Assets/_Game/Funguy.MushroomRunner/ScriptableObjects/Config/StandardMushroomBounceProfile.asset";
        private const string BoostBouncePath = "Assets/_Game/Funguy.MushroomRunner/ScriptableObjects/Config/BoostMushroomBounceProfile.asset";
        private const string SlowBouncePath = "Assets/_Game/Funguy.MushroomRunner/ScriptableObjects/Config/SlowMushroomBounceProfile.asset";
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float SimulationStep = 0.02f;
        private const float MaxSimulationTime = 4f;

        private readonly List<Object> createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                {
                    Object.DestroyImmediate(createdObjects[index]);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void BounceLaunchVelocity_IsUnchanged_WhenFlightShaperIsEnabled()
        {
            MovementTuningProfile shaperEnabled = CreateTestTuningProfile(true);
            MovementTuningProfile shaperDisabled = CreateTestTuningProfile(false);
            Vector3 incomingVelocity = new(3.5f, -7f, 11.5f);
            BounceSurfaceResponse response = new(1.2f, 0.55f, 1.4f, 10.25f, 0.35f, new Vector3(0f, 1f, 1f), 0.6f);

            Vector3 shapedLaunch = BounceMovementMath.ApplyBounceResponse(incomingVelocity, response, shaperEnabled, Vector3.up);
            Vector3 baselineLaunch = BounceMovementMath.ApplyBounceResponse(incomingVelocity, response, shaperDisabled, Vector3.up);

            AssertVectorsEqual(shapedLaunch, baselineLaunch, 0.0001f);
        }

        [Test]
        public void BounceFlightShaper_DoesNotClampStrongerInitialUpwardVelocity()
        {
            MovementTuningProfile profile = CreateTestTuningProfile();
            Vector3 baseLaunch = new(14f, 8.5f, 0f);
            Vector3 strongerLaunch = new(14f, 12.5f, 0f);
            BounceFlightShapeState baseState = BounceMovementMath.CreateBounceFlightShapeState(baseLaunch, profile, Vector3.up);
            BounceFlightShapeState strongerState = BounceMovementMath.CreateBounceFlightShapeState(strongerLaunch, profile, Vector3.up);

            FlightSimulationResult baseResult = SimulateFlight(profile, baseLaunch, baseState, MovementInputFrame.Empty, false, MaxSimulationTime);
            FlightSimulationResult strongerResult = SimulateFlight(profile, strongerLaunch, strongerState, MovementInputFrame.Empty, false, MaxSimulationTime);

            Assert.That(strongerResult.ApexHeight, Is.GreaterThan(baseResult.ApexHeight + 1f));
            Assert.That(strongerResult.LandingTime, Is.GreaterThan(baseResult.LandingTime));
        }

        [Test]
        public void FasterLaunches_StayAirborneLongerAndTravelFarther()
        {
            MovementTuningProfile profile = CreateTestTuningProfile();
            Vector3 slowLaunch = new(8f, 10.5f, 0f);
            Vector3 fastLaunch = new(28f, 10.5f, 0f);
            BounceFlightShapeState slowState = BounceMovementMath.CreateBounceFlightShapeState(slowLaunch, profile, Vector3.up);
            BounceFlightShapeState fastState = BounceMovementMath.CreateBounceFlightShapeState(fastLaunch, profile, Vector3.up);

            FlightSimulationResult slowResult = SimulateFlight(profile, slowLaunch, slowState, MovementInputFrame.Empty, false, MaxSimulationTime);
            FlightSimulationResult fastResult = SimulateFlight(profile, fastLaunch, fastState, MovementInputFrame.Empty, false, MaxSimulationTime);

            Assert.That(fastResult.LandingTime, Is.GreaterThan(slowResult.LandingTime));
            Assert.That(fastResult.PlanarDistance, Is.GreaterThan(slowResult.PlanarDistance));
        }

        [Test]
        public void IntentChanges_DoNotChangeVerticalPositions_ForSameLaunchState()
        {
            MovementTuningProfile profile = CreateTestTuningProfile();
            Vector3 launchVelocity = new(0f, 10.5f, 14f);
            BounceFlightShapeState maintainState = BounceMovementMath.CreateBounceFlightShapeState(launchVelocity, profile, Vector3.up);
            BounceFlightShapeState brakeState = BounceMovementMath.CreateBounceFlightShapeState(launchVelocity, profile, Vector3.up);
            MovementInputFrame maintainInput = new(new Vector2(0f, 0.78f), Vector3.forward, Vector3.forward, 0.78f, false);
            MovementInputFrame brakeInput = new(new Vector2(0f, -0.55f), Vector3.forward, Vector3.forward, 0.55f, false);

            FlightSimulationResult maintainResult = SimulateFlight(profile, launchVelocity, maintainState, maintainInput, true, 3f);
            FlightSimulationResult brakeResult = SimulateFlight(profile, launchVelocity, brakeState, brakeInput, true, 3f);

            Assert.That(maintainResult.VerticalSamples.Count, Is.EqualTo(brakeResult.VerticalSamples.Count));
            for (int index = 0; index < maintainResult.VerticalSamples.Count; index++)
            {
                Assert.That(maintainResult.VerticalSamples[index], Is.EqualTo(brakeResult.VerticalSamples[index]).Within(0.0001f));
            }
        }

        [Test]
        public void ApexSnap_UsesExtraDownAcceleration_WithoutZeroingVerticalSpeed()
        {
            MovementTuningProfile profile = CreateTestTuningProfile();
            Vector3 velocity = new(0f, 1f, 13.5f);
            BounceFlightShapeState shapeState = BounceMovementMath.CreateBounceFlightShapeState(velocity, profile, Vector3.up);

            bool applied = BounceMovementMath.ApplyBounceFlightShaper(ref velocity, profile, Vector3.up, ref shapeState, SimulationStep);
            float expectedRiseMultiplier = profile.BounceFlightShaper.SlowRiseGravityMultiplier;
            float expectedVelocity = 1f + (Physics.gravity.y * profile.GravityScale * expectedRiseMultiplier * SimulationStep)
                - (profile.BounceFlightShaper.ApexExtraDownAcceleration * SimulationStep);

            Assert.That(applied, Is.True);
            Assert.That(shapeState.HasEnteredApexWindow, Is.True);
            Assert.That(velocity.y, Is.EqualTo(expectedVelocity).Within(0.0001f));
            Assert.That(Mathf.Abs(velocity.y), Is.GreaterThan(0.0001f));
        }

        [Test]
        public void ReachEvaluator_FindsReachableRouteLanding_ForProductionBounceProfiles()
        {
            MovementTuningProfile productionTuning = LoadRequiredAsset<MovementTuningProfile>(ProductionTuningPath);
            BounceAreaGenerationProfile generationProfile = LoadRequiredAsset<BounceAreaGenerationProfile>(GenerationProfilePath);
            MushroomBounceProfile standardProfile = LoadRequiredAsset<MushroomBounceProfile>(StandardBouncePath);
            MushroomBounceProfile boostProfile = LoadRequiredAsset<MushroomBounceProfile>(BoostBouncePath);
            MushroomBounceProfile slowProfile = LoadRequiredAsset<MushroomBounceProfile>(SlowBouncePath);

            Assert.That(HasReachableRouteLanding(productionTuning, generationProfile, standardProfile, BounceIntentDirective.Maintain), Is.True);
            Assert.That(HasReachableRouteLanding(productionTuning, generationProfile, boostProfile, BounceIntentDirective.Boost), Is.True);
            Assert.That(HasReachableRouteLanding(productionTuning, generationProfile, slowProfile, BounceIntentDirective.Brake), Is.True);
        }

        [Test]
        public void StandardMaintainLaunch_ClearsProductionMainRouteGapRange()
        {
            MovementTuningProfile productionTuning = LoadRequiredAsset<MovementTuningProfile>(ProductionTuningPath);
            BounceAreaGenerationProfile generationProfile = LoadRequiredAsset<BounceAreaGenerationProfile>(GenerationProfilePath);
            MushroomBounceProfile standardProfile = LoadRequiredAsset<MushroomBounceProfile>(StandardBouncePath);

            for (float gap = generationProfile.MinimumForwardGap; gap <= generationProfile.MaximumForwardGap; gap += 0.5f)
            {
                bool gapReachable = false;
                for (float forwardSpeed = 3f; forwardSpeed <= 6f; forwardSpeed += 0.5f)
                {
                    Vector3 incomingVelocity = new(0f, -generationProfile.InitialLandingSpeed, forwardSpeed);
                    BounceReachRequest request = new(
                        Vector3.zero,
                        incomingVelocity,
                        new Vector3(0f, 0f, gap),
                        standardProfile,
                        productionTuning,
                        BounceIntentDirective.Maintain,
                        Vector3.up,
                        generationProfile.SurfaceLandingHeight,
                        generationProfile.PlayerCollisionRadius,
                        generationProfile.LandingRadius,
                        generationProfile.LandingHeightTolerance,
                        generationProfile.SimulationTimeStep,
                        generationProfile.MaxSimulationTime);

                    if (BounceReachEvaluator.TryEvaluate(request, out _))
                    {
                        gapReachable = true;
                        break;
                    }
                }

                Assert.That(gapReachable, Is.True, $"Expected standard maintain launch to clear a forward gap of {gap:F2}.");
            }
        }

        private MovementTuningProfile CreateTestTuningProfile(bool useBounceFlightShaper = true)
        {
            MovementTuningProfile profile = ScriptableObject.CreateInstance<MovementTuningProfile>();
            createdObjects.Add(profile);
            SetPrivateField(profile, "moveAcceleration", 19f);
            SetPrivateField(profile, "airControlStrength", 0.5f);
            SetPrivateField(profile, "forwardAirControlMultiplier", 0.72f);
            SetPrivateField(profile, "airBrakeAcceleration", 24f);
            SetPrivateField(profile, "maxControllableSpeed", 13.5f);
            SetPrivateField(profile, "maxSpeed", 48f);
            SetPrivateField(profile, "overSpeedDrag", 7.5f);
            SetPrivateField(profile, "airDrag", 0.18f);
            SetPrivateField(profile, "gravityScale", 1f);
            SetPrivateField(profile, "jumpGravityMultiplier", 1.2f);
            SetPrivateField(profile, "fallGravityMultiplier", 2.1f);
            SetPrivateField(profile, "useBounceFlightShaper", useBounceFlightShaper);
            SetPrivateField(profile, "referencePlanarSpeed", 13.5f);
            SetPrivateField(profile, "maximumPlanarSpeed", 30f);
            SetPrivateField(profile, "slowRiseGravityMultiplier", 1.05f);
            SetPrivateField(profile, "fastRiseGravityMultiplier", 0.9f);
            SetPrivateField(profile, "slowFallGravityMultiplier", 2.15f);
            SetPrivateField(profile, "fastFallGravityMultiplier", 1.95f);
            SetPrivateField(profile, "apexTransitionVerticalSpeed", 1.1f);
            SetPrivateField(profile, "apexExtraDownAcceleration", 12f);
            SetPrivateField(profile, "apexExtraDownDuration", 0.06f);
            SetPrivateField(profile, "baseJumpForce", 12f);
            SetPrivateField(profile, "baseBounceSpeedGain", 1f);
            SetPrivateField(profile, "dashForce", 7.5f);
            SetPrivateField(profile, "dashCooldown", 0.16f);
            SetPrivateField(profile, "dashChargesPerBounce", 1);
            SetPrivateField(profile, "postBounceLowControlTime", 0.075f);
            SetPrivateField(profile, "postBounceAirControlMultiplier", 0.48f);
            SetPrivateField(profile, "postDashBonusControlTime", 0.18f);
            SetPrivateField(profile, "postDashAirControlMultiplier", 1.25f);
            SetPrivateField(profile, "bounceGraceTime", 0.1f);
            SetPrivateField(profile, "dashBufferTime", 0.1f);
            SetPrivateField(profile, "minGroundDot", 0.65f);
            return profile;
        }

        private static FlightSimulationResult SimulateFlight(
            MovementTuningProfile profile,
            Vector3 launchVelocity,
            BounceFlightShapeState flightShapeState,
            MovementInputFrame inputFrame,
            bool applyInput,
            float maxSimulationTime)
        {
            Vector3 velocity = launchVelocity;
            Vector3 position = Vector3.zero;
            float elapsedTime = 0f;
            float apexHeight = 0f;
            float landingTime = -1f;
            List<float> verticalSamples = new();

            while (elapsedTime < maxSimulationTime)
            {
                if (!BounceMovementMath.ApplyBounceFlightShaper(ref velocity, profile, Vector3.up, ref flightShapeState, SimulationStep))
                {
                    BounceMovementMath.ApplyShapedGravity(ref velocity, profile, Vector3.up, SimulationStep);
                }

                if (applyInput)
                {
                    BounceMovementMath.ApplyAirAcceleration(ref velocity, profile, inputFrame, Vector3.up, false, false, SimulationStep);
                    if (profile.AirDrag > 0f)
                    {
                        BounceMovementMath.ApplyPlanarDrag(ref velocity, Vector3.up, profile.AirDrag, SimulationStep);
                    }

                    BounceMovementMath.ApplySoftSpeedLimit(ref velocity, profile, Vector3.up, SimulationStep);
                }

                position += velocity * SimulationStep;
                elapsedTime += SimulationStep;
                apexHeight = Mathf.Max(apexHeight, position.y);
                verticalSamples.Add(position.y);

                if (landingTime < 0f && position.y <= 0f && velocity.y <= 0f)
                {
                    landingTime = elapsedTime;
                    break;
                }
            }

            Assert.That(landingTime, Is.GreaterThan(0f), "Expected the simulated launch to land during the allowed window.");
            return new FlightSimulationResult(apexHeight, landingTime, Vector3.ProjectOnPlane(position, Vector3.up).magnitude, verticalSamples);
        }

        private static bool HasReachableRouteLanding(
            MovementTuningProfile tuningProfile,
            BounceAreaGenerationProfile generationProfile,
            MushroomBounceProfile bounceProfile,
            BounceIntentDirective intent)
        {
            for (float forwardSpeed = 3f; forwardSpeed <= 6f; forwardSpeed += 0.5f)
            {
                Vector3 incomingVelocity = new(0f, -generationProfile.InitialLandingSpeed, forwardSpeed);

                for (float gap = generationProfile.MinimumForwardGap; gap <= generationProfile.MaximumForwardGap; gap += 0.25f)
                {
                    BounceReachRequest request = new(
                        Vector3.zero,
                        incomingVelocity,
                        new Vector3(0f, 0f, gap),
                        bounceProfile,
                        tuningProfile,
                        intent,
                        Vector3.up,
                        generationProfile.SurfaceLandingHeight,
                        generationProfile.PlayerCollisionRadius,
                        generationProfile.LandingRadius,
                        generationProfile.LandingHeightTolerance,
                        generationProfile.SimulationTimeStep,
                        generationProfile.MaxSimulationTime);

                    if (BounceReachEvaluator.TryEvaluate(request, out _))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static T LoadRequiredAsset<T>(string assetPath)
            where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            Assert.That(asset, Is.Not.Null, $"Missing asset at '{assetPath}'.");
            return asset;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = typeof(MovementTuningProfile).GetField(fieldName, InstanceNonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
            field.SetValue(target, value);
        }

        private static void AssertVectorsEqual(Vector3 actual, Vector3 expected, float tolerance)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance));
        }

        private readonly struct FlightSimulationResult
        {
            public FlightSimulationResult(float apexHeight, float landingTime, float planarDistance, List<float> verticalSamples)
            {
                ApexHeight = apexHeight;
                LandingTime = landingTime;
                PlanarDistance = planarDistance;
                VerticalSamples = verticalSamples;
            }

            public float ApexHeight { get; }

            public float LandingTime { get; }

            public float PlanarDistance { get; }

            public List<float> VerticalSamples { get; }
        }
    }
}
