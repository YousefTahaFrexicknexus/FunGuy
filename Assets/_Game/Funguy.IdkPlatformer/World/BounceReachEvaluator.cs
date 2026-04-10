using UnityEngine;

namespace Funguy.IdkPlatformer
{
    public readonly struct BounceReachRequest
    {
        public BounceReachRequest(
            Vector3 surfaceRootPosition,
            Vector3 incomingVelocity,
            Vector3 targetRootPosition,
            MushroomBounceProfile launchProfile,
            MovementTuningProfile tuningProfile,
            BounceIntentDirective intent,
            Vector3 worldUp,
            float surfaceLandingHeight,
            float playerCollisionRadius,
            float landingRadius,
            float landingHeightTolerance,
            float simulationTimeStep,
            float maxSimulationTime)
        {
            SurfaceRootPosition = surfaceRootPosition;
            IncomingVelocity = incomingVelocity;
            TargetRootPosition = targetRootPosition;
            LaunchProfile = launchProfile;
            TuningProfile = tuningProfile;
            Intent = intent;
            WorldUp = worldUp;
            SurfaceLandingHeight = surfaceLandingHeight;
            PlayerCollisionRadius = playerCollisionRadius;
            LandingRadius = landingRadius;
            LandingHeightTolerance = landingHeightTolerance;
            SimulationTimeStep = simulationTimeStep;
            MaxSimulationTime = maxSimulationTime;
        }

        public Vector3 SurfaceRootPosition { get; }

        public Vector3 IncomingVelocity { get; }

        public Vector3 TargetRootPosition { get; }

        public MushroomBounceProfile LaunchProfile { get; }

        public MovementTuningProfile TuningProfile { get; }

        public BounceIntentDirective Intent { get; }

        public Vector3 WorldUp { get; }

        public float SurfaceLandingHeight { get; }

        public float PlayerCollisionRadius { get; }

        public float LandingRadius { get; }

        public float LandingHeightTolerance { get; }

        public float SimulationTimeStep { get; }

        public float MaxSimulationTime { get; }
    }

    public readonly struct BounceReachResult
    {
        public BounceReachResult(Vector3 launchVelocity, Vector3 landingVelocity, Vector3 landingPosition, float flightTime)
        {
            LaunchVelocity = launchVelocity;
            LandingVelocity = landingVelocity;
            LandingPosition = landingPosition;
            FlightTime = flightTime;
        }

        public Vector3 LaunchVelocity { get; }

        public Vector3 LandingVelocity { get; }

        public Vector3 LandingPosition { get; }

        public float FlightTime { get; }
    }

    public static class BounceReachEvaluator
    {
        public static bool TryEvaluate(in BounceReachRequest request, out BounceReachResult result)
        {
            result = default;

            if (request.LaunchProfile == null || request.TuningProfile == null)
            {
                return false;
            }

            Vector3 up = request.WorldUp.sqrMagnitude > BounceMovementMath.MinimumDirectionSqrMagnitude
                ? request.WorldUp.normalized
                : Vector3.up;

            Vector3 bouncePoint = request.SurfaceRootPosition + up * (request.SurfaceLandingHeight + request.PlayerCollisionRadius);
            Vector3 targetPoint = request.TargetRootPosition + up * (request.SurfaceLandingHeight + request.PlayerCollisionRadius);

            BounceContext context = new(
                request.IncomingVelocity,
                bouncePoint,
                up,
                up,
                request.TuningProfile.BaseJumpForce,
                MovementInputFrame.Empty);

            BounceSurfaceResponse bounceResponse = request.LaunchProfile.CreateResponse(null, context);
            Vector3 launchVelocity = BounceMovementMath.ApplyBounceResponse(request.IncomingVelocity, bounceResponse, request.TuningProfile, up);
            Vector3 velocity = launchVelocity;
            Vector3 position = bouncePoint;
            float elapsedTime = 0f;
            float deltaTime = Mathf.Max(0.005f, request.SimulationTimeStep);
            float maxTime = Mathf.Max(deltaTime, request.MaxSimulationTime);
            float drag = bounceResponse.HasPlanarDragOverride
                ? bounceResponse.PlanarDragOverride
                : request.TuningProfile.AirDrag;

            while (elapsedTime < maxTime)
            {
                Vector3 previousPosition = position;
                BounceMovementMath.ApplyShapedGravity(ref velocity, request.TuningProfile, up, deltaTime);

                if (elapsedTime > 0f)
                {
                    MovementInputFrame inputFrame = ResolveIntentInput(request.Intent, position, velocity, targetPoint, up);
                    BounceMovementMath.ApplyAirAcceleration(
                        ref velocity,
                        request.TuningProfile,
                        inputFrame,
                        up,
                        elapsedTime < request.TuningProfile.PostBounceLowControlTime,
                        false,
                        deltaTime);

                    if (drag > 0f)
                    {
                        BounceMovementMath.ApplyPlanarDrag(ref velocity, up, drag, deltaTime);
                    }

                    BounceMovementMath.ApplySoftSpeedLimit(ref velocity, request.TuningProfile, up, deltaTime);
                }

                position += velocity * deltaTime;
                elapsedTime += deltaTime;

                if (Vector3.Dot(velocity, up) > 0f)
                {
                    continue;
                }

                if (SegmentHitsLandingWindow(previousPosition, position, targetPoint, up, request.LandingRadius, request.LandingHeightTolerance))
                {
                    result = new BounceReachResult(launchVelocity, velocity, position, elapsedTime);
                    return true;
                }
            }

            return false;
        }

        private static bool SegmentHitsLandingWindow(
            Vector3 segmentStart,
            Vector3 segmentEnd,
            Vector3 targetPoint,
            Vector3 up,
            float landingRadius,
            float landingHeightTolerance)
        {
            Vector3 segment = segmentEnd - segmentStart;
            float segmentLengthSqr = segment.sqrMagnitude;
            if (segmentLengthSqr <= BounceMovementMath.MinimumDirectionSqrMagnitude)
            {
                return IsPointInsideLandingWindow(segmentEnd, targetPoint, up, landingRadius, landingHeightTolerance);
            }

            float projection = Vector3.Dot(targetPoint - segmentStart, segment) / segmentLengthSqr;
            projection = Mathf.Clamp01(projection);
            Vector3 closestPoint = segmentStart + segment * projection;
            return IsPointInsideLandingWindow(closestPoint, targetPoint, up, landingRadius, landingHeightTolerance);
        }

        private static bool IsPointInsideLandingWindow(
            Vector3 point,
            Vector3 targetPoint,
            Vector3 up,
            float landingRadius,
            float landingHeightTolerance)
        {
            Vector3 delta = point - targetPoint;
            float verticalDelta = Mathf.Abs(Vector3.Dot(delta, up));
            if (verticalDelta > landingHeightTolerance)
            {
                return false;
            }

            Vector3 planarDelta = Vector3.ProjectOnPlane(delta, up);
            return planarDelta.magnitude <= landingRadius;
        }

        private static MovementInputFrame ResolveIntentInput(
            BounceIntentDirective intent,
            Vector3 position,
            Vector3 velocity,
            Vector3 targetPoint,
            Vector3 up)
        {
            Vector3 toTarget = Vector3.ProjectOnPlane(targetPoint - position, up);
            Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, up);

            Vector3 wishDirection = toTarget.sqrMagnitude > BounceMovementMath.MinimumDirectionSqrMagnitude
                ? toTarget.normalized
                : planarVelocity.sqrMagnitude > BounceMovementMath.MinimumDirectionSqrMagnitude
                    ? planarVelocity.normalized
                    : Vector3.forward;

            float magnitude = intent switch
            {
                BounceIntentDirective.Brake => 0.55f,
                BounceIntentDirective.Boost => 1f,
                _ => 0.78f
            };

            return new MovementInputFrame(new Vector2(0f, magnitude), wishDirection, wishDirection, magnitude, false);
        }
    }
}
