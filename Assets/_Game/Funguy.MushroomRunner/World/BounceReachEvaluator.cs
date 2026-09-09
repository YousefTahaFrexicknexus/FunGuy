using UnityEngine;

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
            float maxSimulationTime,
            float maxSpeed,
            float incomingDiveAddedDownSpeed = 0f)
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
            MaxSpeed = maxSpeed;
            IncomingDiveAddedDownSpeed = incomingDiveAddedDownSpeed;
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

        public float MaxSpeed { get; }

        public float IncomingDiveAddedDownSpeed { get; }
    }

    public readonly struct BounceReachResult
    {
        public BounceReachResult(Vector3 launchVelocity, Vector3 landingVelocity, Vector3 landingPosition, float flightTime, float diveAddedDownSpeed = 0f)
        {
            LaunchVelocity = launchVelocity;
            LandingVelocity = landingVelocity;
            LandingPosition = landingPosition;
            FlightTime = flightTime;
            DiveAddedDownSpeed = diveAddedDownSpeed;
        }

        public Vector3 LaunchVelocity { get; }

        public Vector3 LandingVelocity { get; }

        public Vector3 LandingPosition { get; }

        public float FlightTime { get; }

        public float DiveAddedDownSpeed { get; }
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
            Vector3 launchVelocity = BounceMovementMath.ApplyBounceResponse(request.IncomingVelocity, bounceResponse, request.TuningProfile, up, request.IncomingDiveAddedDownSpeed);
            Vector3 velocity = launchVelocity;
            Vector3 position = bouncePoint;
            float elapsedTime = 0f;
            float diveAddedDownSpeed = 0f;
            float deltaTime = Mathf.Max(0.005f, request.SimulationTimeStep);
            float maxTime = Mathf.Max(deltaTime, request.MaxSimulationTime);
            float drag = bounceResponse.HasPlanarDragOverride
                ? bounceResponse.PlanarDragOverride
                : request.TuningProfile.AirDrag;
            BounceFlightShapeState bounceFlightShape = BounceMovementMath.CreateBounceFlightShapeState(launchVelocity, request.TuningProfile, up);

            while (elapsedTime < maxTime)
            {
                Vector3 previousPosition = position;
                if (!BounceMovementMath.ApplyBounceFlightShaper(ref velocity, request.TuningProfile, up, ref bounceFlightShape, deltaTime))
                {
                    BounceMovementMath.ApplyShapedGravity(ref velocity, request.TuningProfile, up, deltaTime);
                }

                if (elapsedTime > 0f)
                {
                    MovementInputFrame inputFrame = ResolveIntentInput(request.Intent, position, targetPoint, up, request.TuningProfile);
                    diveAddedDownSpeed += BounceMovementMath.ApplyDive(ref velocity, request.TuningProfile, inputFrame.BrakeAmount, up, deltaTime);
                    BounceMovementMath.ApplyAirMovement(
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

                    BounceMovementMath.ApplySoftSpeedLimit(ref velocity, request.TuningProfile, up, request.MaxSpeed, deltaTime);
                }

                position += velocity * deltaTime;
                elapsedTime += deltaTime;

                if (Vector3.Dot(velocity, up) > 0f)
                {
                    continue;
                }

                if (SegmentHitsLandingWindow(previousPosition, position, targetPoint, up, request.LandingRadius, request.LandingHeightTolerance))
                {
                    result = new BounceReachResult(launchVelocity, velocity, position, elapsedTime, diveAddedDownSpeed);
                    return true;
                }
            }

            return false;
        }

        static bool SegmentHitsLandingWindow(
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

        static bool IsPointInsideLandingWindow(
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

        static MovementInputFrame ResolveIntentInput(
            BounceIntentDirective intent,
            Vector3 position,
            Vector3 targetPoint,
            Vector3 up,
            MovementTuningProfile profile)
        {
            // Course progression uses world-forward. Predict the same bounded sideways correction as the motor,
            // rather than rotating a virtual camera toward the target and granting unlimited lateral reach.
            Vector3 forward = BounceMovementMath.ResolvePlanarForward(Vector3.forward, up);
            Vector3 right = Vector3.Cross(up, forward);
            float lateralError = Vector3.Dot(targetPoint - position, right);
            float approachTime = Mathf.Max(.25f, profile.SteeringResponse * 3f);
            float sidewaysInput = profile.StrafeSpeed > 0f
                ? Mathf.Clamp(lateralError / (approachTime * profile.StrafeSpeed), -1f, 1f) : 0f;
            float forwardInput = intent switch
            {
                BounceIntentDirective.Brake => -.55f,
                BounceIntentDirective.Boost => 1f,
                _ => .78f
            };
            Vector2 move = Vector2.ClampMagnitude(new Vector2(sidewaysInput, forwardInput), 1f);
            Vector3 wish = (forward * move.y + right * move.x).normalized;
            return new MovementInputFrame(move, wish, forward, move.magnitude, false);
        }
    }
