using UnityEngine;

public struct BounceFlightShapeState
{
    public BounceFlightShapeState(
        float launchPlanarSpeed,
        float normalizedSpeedFactor,
        float riseGravityMultiplier,
        float fallGravityMultiplier,
        float apexTransitionVerticalSpeed,
        float apexExtraDownAcceleration,
        float apexExtraDownDuration)
    {
        LaunchPlanarSpeed = Mathf.Max(0f, launchPlanarSpeed);
        NormalizedSpeedFactor = Mathf.Clamp01(normalizedSpeedFactor);
        RiseGravityMultiplier = Mathf.Max(0f, riseGravityMultiplier);
        FallGravityMultiplier = Mathf.Max(0f, fallGravityMultiplier);
        ApexTransitionVerticalSpeed = Mathf.Max(0f, apexTransitionVerticalSpeed);
        ApexExtraDownAcceleration = Mathf.Max(0f, apexExtraDownAcceleration);
        ApexExtraDownDuration = Mathf.Max(0f, apexExtraDownDuration);
        RemainingApexSnapTime = 0f;
        HasEnteredApexWindow = false;
        IsActive = true;
    }

    public float LaunchPlanarSpeed { get; set; }

    public float NormalizedSpeedFactor { get; set; }

    public float RiseGravityMultiplier { get; set; }

    public float FallGravityMultiplier { get; set; }

    public float ApexTransitionVerticalSpeed { get; set; }

    public float ApexExtraDownAcceleration { get; set; }

    public float ApexExtraDownDuration { get; set; }

    public float RemainingApexSnapTime { get; set; }

    public bool HasEnteredApexWindow { get; set; }

    public bool IsActive { get; set; }

    public void EnterApexWindow()
    {
        if (HasEnteredApexWindow)
        {
            return;
        }

        HasEnteredApexWindow = true;
        RemainingApexSnapTime = ApexExtraDownDuration;
    }

    public float ConsumeApexSnapTime(float deltaTime)
    {
        float consumed = Mathf.Min(RemainingApexSnapTime, Mathf.Max(0f, deltaTime));
        RemainingApexSnapTime = Mathf.Max(0f, RemainingApexSnapTime - consumed);
        return consumed;
    }

    public void Deactivate()
    {
        IsActive = false;
        RemainingApexSnapTime = 0f;
    }

    public void Reset()
    {
        this = default;
    }
}

public static class BounceMovementMath
{
    public const float MinimumDirectionSqrMagnitude = 0.0001f;

    public static void ApplyShapedGravity(
        ref Vector3 velocity,
        MovementTuningProfile tuningProfile,
        Vector3 worldUp,
        float deltaTime)
    {
        if (tuningProfile == null)
        {
            return;
        }

        Vector3 up = GetSafeUp(worldUp);
        float verticalSpeed = Vector3.Dot(velocity, up);
        float gravityMultiplier = verticalSpeed > 0f
            ? tuningProfile.JumpGravityMultiplier
            : tuningProfile.FallGravityMultiplier;

        velocity += Physics.gravity * tuningProfile.GravityScale * gravityMultiplier * deltaTime;
    }

    public static bool ShouldUseBounceFlightShaper(MovementTuningProfile tuningProfile)
    {
        return tuningProfile != null && tuningProfile.UseBounceFlightShaper;
    }

    public static BounceFlightShapeState CreateBounceFlightShapeState(
        Vector3 outgoingVelocity,
        MovementTuningProfile tuningProfile,
        Vector3 worldUp)
    {
        if (!ShouldUseBounceFlightShaper(tuningProfile))
        {
            return default;
        }

        BounceFlightShaperSettings settings = tuningProfile.BounceFlightShaper;
        Vector3 up = GetSafeUp(worldUp);
        float planarSpeed = Vector3.ProjectOnPlane(outgoingVelocity, up).magnitude;
        float speedFactor = settings.MaximumPlanarSpeed > settings.ReferencePlanarSpeed
            ? Mathf.InverseLerp(settings.ReferencePlanarSpeed, settings.MaximumPlanarSpeed, planarSpeed)
            : planarSpeed >= settings.ReferencePlanarSpeed
                ? 1f
                : 0f;
        float riseGravityMultiplier = Mathf.Lerp(settings.SlowRiseGravityMultiplier, settings.FastRiseGravityMultiplier, speedFactor);
        float fallGravityMultiplier = Mathf.Lerp(settings.SlowFallGravityMultiplier, settings.FastFallGravityMultiplier, speedFactor);

        return new BounceFlightShapeState(
            planarSpeed,
            speedFactor,
            riseGravityMultiplier,
            fallGravityMultiplier,
            settings.ApexTransitionVerticalSpeed,
            settings.ApexExtraDownAcceleration,
            settings.ApexExtraDownDuration);
    }

    public static bool ApplyBounceFlightShaper(
        ref Vector3 velocity,
        MovementTuningProfile tuningProfile,
        Vector3 worldUp,
        ref BounceFlightShapeState bounceFlightShape,
        float deltaTime)
    {
        if (!ShouldUseBounceFlightShaper(tuningProfile) ||
            !bounceFlightShape.IsActive ||
            deltaTime <= 0f)
        {
            return false;
        }

        Vector3 up = GetSafeUp(worldUp);
        float verticalSpeed = Vector3.Dot(velocity, up);

        if (verticalSpeed > 0f)
        {
            ApplyGravityMultiplier(ref velocity, tuningProfile.GravityScale, bounceFlightShape.RiseGravityMultiplier, deltaTime);

            if (verticalSpeed <= bounceFlightShape.ApexTransitionVerticalSpeed)
            {
                bounceFlightShape.EnterApexWindow();
                float apexSnapStep = bounceFlightShape.ConsumeApexSnapTime(deltaTime);
                if (apexSnapStep > 0f)
                {
                    ApplyVerticalAcceleration(ref velocity, up, bounceFlightShape.ApexExtraDownAcceleration, apexSnapStep);
                }
            }

            return true;
        }

        ApplyGravityMultiplier(ref velocity, tuningProfile.GravityScale, bounceFlightShape.FallGravityMultiplier, deltaTime);
        return true;
    }

    // Forward carry and lateral aiming are separate. Only deliberate forward input adds forward speed.
    public static void ApplyAirMovement(
        ref Vector3 velocity,
        MovementTuningProfile tuningProfile,
        in MovementInputFrame inputFrame,
        Vector3 worldUp,
        bool inPostBounceLowControl,
        bool inPostDashBoost,
        float deltaTime)
    {
        if (tuningProfile == null || deltaTime <= 0f) return;
        ApplyLateralControl(ref velocity, tuningProfile, inputFrame, worldUp, deltaTime);
        ApplyAirBraking(ref velocity, tuningProfile, inputFrame.BrakeAmount, worldUp, deltaTime);
        float forwardInput = Mathf.Clamp01(inputFrame.Move.y);
        if (forwardInput <= 0f || !inputFrame.HasMoveInput) return;

        Vector3 forward = ResolvePlanarForward(inputFrame.ReferenceForward, worldUp);
        float forwardSpeed = Vector3.Dot(velocity, forward);
        float speedToAdd = tuningProfile.MaxControllableSpeed * forwardInput - forwardSpeed;
        if (speedToAdd <= 0f) return;
        float acceleration = tuningProfile.AirAcceleration * forwardInput
            * ResolveContextualAirControlMultiplier(tuningProfile, 1f, inPostBounceLowControl, inPostDashBoost);
        velocity += forward * Mathf.Min(speedToAdd, acceleration * deltaTime);
    }

    public static void ApplyLateralControl(ref Vector3 velocity, MovementTuningProfile profile,
        in MovementInputFrame input, Vector3 worldUp, float deltaTime)
    {
        if (profile == null || deltaTime <= 0f) return;
        Vector3 up = GetSafeUp(worldUp);
        Vector3 forward = ResolvePlanarForward(input.ReferenceForward, up);
        Vector3 right = Vector3.Cross(up, forward);
        float sidewaysSpeed = Vector3.Dot(velocity, right);
        float targetSpeed = profile.StrafeSpeed * (input.HasMoveInput ? Mathf.Clamp(input.Move.x, -1f, 1f) : 0f);
        float response = 1f - Mathf.Exp(-deltaTime / profile.SteeringResponse);
        float nextSpeed = Mathf.Lerp(sidewaysSpeed, targetSpeed, response);
        // On release the target is zero: grip removes drift instead of redirecting it into free forward speed.
        velocity += right * (nextSpeed - sidewaysSpeed);
    }

    public static Vector3 ResolvePlanarForward(Vector3 referenceForward, Vector3 worldUp)
    {
        Vector3 up = GetSafeUp(worldUp);
        Vector3 forward = Vector3.ProjectOnPlane(referenceForward, up);
        if (forward.sqrMagnitude <= MinimumDirectionSqrMagnitude)
            forward = Vector3.ProjectOnPlane(Vector3.forward, up);
        if (forward.sqrMagnitude <= MinimumDirectionSqrMagnitude)
            forward = Vector3.ProjectOnPlane(Vector3.right, up);
        return forward.normalized;
    }

    public static void ApplyAirBraking(ref Vector3 velocity, MovementTuningProfile profile,
        float brakeAmount, Vector3 worldUp, float deltaTime)
    {
        if (profile == null || brakeAmount <= 0f || deltaTime <= 0f) return;
        Vector3 up = GetSafeUp(worldUp);
        Vector3 planar = Vector3.ProjectOnPlane(velocity, up);
        float retention = Mathf.Exp(-Mathf.Clamp01(brakeAmount) * deltaTime / profile.BrakeResponse);
        velocity = planar * retention + up * Vector3.Dot(velocity, up);
    }

    // Return the added downward speed so callers can exclude it from the next impact recovery.
    public static float ApplyDive(ref Vector3 velocity, MovementTuningProfile profile,
        float brakeAmount, Vector3 worldUp, float deltaTime)
    {
        if (profile == null || deltaTime <= 0f) return 0f;
        float addedSpeed = profile.DivePull * Mathf.Clamp01(brakeAmount) * deltaTime;
        velocity -= GetSafeUp(worldUp) * addedSpeed;
        return addedSpeed;
    }

    public static void ApplyPlanarDrag(ref Vector3 velocity, Vector3 worldUp, float drag, float deltaTime)
    {
        if (drag <= 0f)
        {
            return;
        }

        Vector3 up = GetSafeUp(worldUp);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, up);
        Vector3 verticalVelocity = up * Vector3.Dot(velocity, up);
        planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, drag * deltaTime);
        velocity = planarVelocity + verticalVelocity;
    }

    public static void ApplySoftSpeedLimit(
        ref Vector3 velocity,
        MovementTuningProfile tuningProfile,
        Vector3 worldUp,
        float maxSpeed,
        float deltaTime)
    {
        if (tuningProfile == null)
        {
            return;
        }

        Vector3 up = GetSafeUp(worldUp);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, up);
        float planarSpeed = planarVelocity.magnitude;
        float overflow = planarSpeed - maxSpeed;

        if (overflow <= 0f || planarSpeed <= MinimumDirectionSqrMagnitude)
        {
            return;
        }

        float dragAmount = Mathf.Min(overflow, tuningProfile.OverSpeedDrag * overflow * deltaTime);
        planarVelocity -= planarVelocity.normalized * dragAmount;
        velocity = planarVelocity + (up * Vector3.Dot(velocity, up));
    }

    public static Vector3 ApplyBounceResponse(
        Vector3 incomingVelocity,
        in BounceSurfaceResponse response,
        MovementTuningProfile tuningProfile,
        Vector3 worldUp,
        float diveAddedDownSpeed = 0f)
    {
        Vector3 up = GetSafeUp(worldUp);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(incomingVelocity, up);
        float planarSpeed = planarVelocity.magnitude;
        Vector3 planarDirection = ResolveSurfacePlanarDirection(planarVelocity, response, up);

        Vector3 redirectedPlanar = planarVelocity;
        if (planarSpeed > MinimumDirectionSqrMagnitude && planarDirection.sqrMagnitude > MinimumDirectionSqrMagnitude)
        {
            Vector3 currentDirection = planarVelocity.normalized;
            Vector3 blendedDirection = Vector3.Slerp(currentDirection, planarDirection, response.DirectionalInfluence);
            if (blendedDirection.sqrMagnitude <= MinimumDirectionSqrMagnitude)
            {
                blendedDirection = Vector3.Lerp(currentDirection, planarDirection, response.DirectionalInfluence);
            }

            if (blendedDirection.sqrMagnitude > MinimumDirectionSqrMagnitude)
            {
                redirectedPlanar = blendedDirection.normalized * planarSpeed;
            }
        }

        Vector3 planarOut = redirectedPlanar * response.VelocityScale;
        if (planarDirection.sqrMagnitude > MinimumDirectionSqrMagnitude && Mathf.Abs(response.PlanarBoost) > 0f)
        {
            float planarBoost = response.PlanarBoost;
            if (planarBoost < 0f)
            {
                float speedAlongLaunchDirection = Mathf.Max(0f, Vector3.Dot(planarOut, planarDirection));
                planarBoost = Mathf.Max(planarBoost, -speedAlongLaunchDirection);
            }

            planarOut += planarDirection * planarBoost;
        }

        if (tuningProfile != null &&
            !response.HasPlanarDragOverride &&
            response.VelocityScale >= 1f &&
            response.PlanarBoost >= 0f &&
            tuningProfile.BaseBounceSpeedGain > 0f)
        {
            Vector3 bonusDirection = planarOut.sqrMagnitude > MinimumDirectionSqrMagnitude
                ? planarOut.normalized
                : planarDirection.sqrMagnitude > MinimumDirectionSqrMagnitude
                    ? planarDirection
                    : planarVelocity.normalized;

            if (bonusDirection.sqrMagnitude > MinimumDirectionSqrMagnitude)
            {
                planarOut += bonusDirection * tuningProfile.BaseBounceSpeedGain;
            }
        }

        if (!response.HasPlanarDragOverride && response.VelocityScale >= 1f && response.PlanarBoost >= 0f)
        {
            float minimumPlanarSpeed = planarSpeed;
            float planarOutSpeed = planarOut.magnitude;
            if (planarOutSpeed < minimumPlanarSpeed)
            {
                Vector3 fallbackDirection = planarOut.sqrMagnitude > MinimumDirectionSqrMagnitude
                    ? planarOut.normalized
                    : planarDirection.sqrMagnitude > MinimumDirectionSqrMagnitude
                        ? planarDirection
                        : planarVelocity.normalized;

                if (fallbackDirection.sqrMagnitude > MinimumDirectionSqrMagnitude)
                {
                    planarOut = fallbackDirection.normalized * minimumPlanarSpeed;
                }
            }
        }

        float verticalSpeed = Vector3.Dot(incomingVelocity, up);
        float impactBonus = Mathf.Max(0f, -verticalSpeed - Mathf.Max(0f, diveAddedDownSpeed)) * response.ImpactRecoveryFactor;
        Vector3 verticalOut = up * (response.UpwardImpulse + impactBonus);
        return planarOut + verticalOut;
    }

    public static Vector3 ResolveSurfacePlanarDirection(
        Vector3 planarVelocity,
        in BounceSurfaceResponse response,
        Vector3 worldUp)
    {
        Vector3 up = GetSafeUp(worldUp);
        Vector3 launchDirection = response.LaunchDirection.sqrMagnitude > MinimumDirectionSqrMagnitude
            ? response.LaunchDirection.normalized
            : up;

        Vector3 blendedDirection = Vector3.Lerp(up, launchDirection, response.UpBlend);
        Vector3 planarDirection = Vector3.ProjectOnPlane(blendedDirection, up);

        if (planarDirection.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            planarDirection = Vector3.ProjectOnPlane(launchDirection, up);
        }

        if (planarDirection.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            planarDirection = planarVelocity;
        }

        if (planarDirection.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            return Vector3.zero;
        }

        return planarDirection.normalized;
    }

    public static float ResolveContextualAirControlMultiplier(
        MovementTuningProfile tuningProfile,
        float alignment,
        bool inPostBounceLowControl,
        bool inPostDashBoost)
    {
        if (tuningProfile == null)
        {
            return 0f;
        }

        float multiplier = 1f;
        float clampedAlignment = Mathf.Clamp01(alignment);

        if (clampedAlignment > 0f)
        {
            float forwardCommitment = clampedAlignment * clampedAlignment;
            multiplier *= Mathf.Lerp(1f, tuningProfile.ForwardAirControlMultiplier, forwardCommitment);
        }

        if (inPostDashBoost)
        {
            multiplier *= tuningProfile.PostDashAirControlMultiplier;
            return multiplier;
        }

        if (!inPostBounceLowControl)
        {
            return multiplier;
        }

        float postBounceMultiplier = tuningProfile.PostBounceAirControlMultiplier;
        if (clampedAlignment > 0f)
        {
            float forwardRelief = Mathf.Lerp(1f, 0.55f, clampedAlignment);
            postBounceMultiplier = Mathf.Lerp(postBounceMultiplier, 1f, forwardRelief);
        }

        multiplier *= postBounceMultiplier;
        return multiplier;
    }

    static Vector3 GetSafeUp(Vector3 worldUp)
    {
        return worldUp.sqrMagnitude > MinimumDirectionSqrMagnitude
            ? worldUp.normalized
            : Vector3.up;
    }

    static void ApplyGravityMultiplier(ref Vector3 velocity, float gravityScale, float gravityMultiplier, float deltaTime)
    {
        if (deltaTime <= 0f || gravityScale <= 0f || gravityMultiplier <= 0f)
        {
            return;
        }

        velocity += Physics.gravity * gravityScale * gravityMultiplier * deltaTime;
    }

    static void ApplyVerticalAcceleration(ref Vector3 velocity, Vector3 up, float accelerationMagnitude, float deltaTime)
    {
        if (accelerationMagnitude <= 0f || deltaTime <= 0f)
        {
            return;
        }

        velocity -= up * accelerationMagnitude * deltaTime;
    }
}