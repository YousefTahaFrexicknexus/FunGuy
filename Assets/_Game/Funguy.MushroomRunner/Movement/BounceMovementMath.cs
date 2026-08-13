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
    const float BackwardBrakeMultiplier = 0.75f;

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

    public static void ApplyAirAcceleration(
        ref Vector3 velocity,
        MovementTuningProfile tuningProfile,
        in MovementInputFrame inputFrame,
        Vector3 worldUp,
        bool inPostBounceLowControl,
        bool inPostDashBoost,
        float deltaTime)
    {
        if (tuningProfile == null || !inputFrame.HasMoveInput)
        {
            return;
        }

        Vector3 up = GetSafeUp(worldUp);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, up);
        Vector3 verticalVelocity = up * Vector3.Dot(velocity, up);
        Vector3 wishDirection = inputFrame.WishDirection.normalized;

        if (planarVelocity.sqrMagnitude <= MinimumDirectionSqrMagnitude)
        {
            float initialAccelerationDelta = tuningProfile.MoveAcceleration
                * ResolveContextualAirControlMultiplier(tuningProfile, 0f, inPostBounceLowControl, inPostDashBoost)
                * inputFrame.Magnitude
                * deltaTime;

            planarVelocity += wishDirection * initialAccelerationDelta;
            velocity = planarVelocity + verticalVelocity;
            return;
        }

        Vector3 planarDirection = planarVelocity.normalized;
        float alignment = Vector3.Dot(planarDirection, wishDirection);
        float contextualMultiplier = ResolveContextualAirControlMultiplier(
            tuningProfile,
            alignment,
            inPostBounceLowControl,
            inPostDashBoost);

        float currentAlongWish = Vector3.Dot(planarVelocity, wishDirection);
        Vector3 sideVelocity = planarVelocity - (wishDirection * currentAlongWish);

        if (sideVelocity.sqrMagnitude > MinimumDirectionSqrMagnitude && tuningProfile.AirBrakeAcceleration > 0f)
        {
            // Bleed velocity that fights the new wish direction so diagonal swaps do not feel ignored in-air.
            float turnSharpness = 1f - Mathf.Clamp01(alignment);
            float turnBrakeDelta = (tuningProfile.AirBrakeAcceleration + tuningProfile.MoveAcceleration)
                * turnSharpness
                * inputFrame.Magnitude
                * deltaTime;
            sideVelocity = Vector3.MoveTowards(sideVelocity, Vector3.zero, turnBrakeDelta);
            planarVelocity = (wishDirection * currentAlongWish) + sideVelocity;
        }

        if (alignment < 0f && tuningProfile.AirBrakeAcceleration > 0f)
        {
            float brakeDelta = tuningProfile.AirBrakeAcceleration * (-alignment) * inputFrame.Magnitude * deltaTime;
            planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, brakeDelta);
        }

        if (inputFrame.BrakeAmount > 0f && tuningProfile.AirBrakeAcceleration > 0f)
        {
            float backwardBrakeDelta = tuningProfile.AirBrakeAcceleration
                * inputFrame.BrakeAmount
                * BackwardBrakeMultiplier
                * deltaTime;
            planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, backwardBrakeDelta);
        }

        currentAlongWish = Vector3.Dot(planarVelocity, wishDirection);
        float targetAlongWish = tuningProfile.MaxControllableSpeed * inputFrame.Magnitude;
        float speedToAdd = targetAlongWish - currentAlongWish;
        if (speedToAdd <= 0f)
        {
            velocity = planarVelocity + verticalVelocity;
            return;
        }

        float accelerationDelta = tuningProfile.MoveAcceleration
            * contextualMultiplier
            * inputFrame.Magnitude
            * deltaTime;

        planarVelocity += wishDirection * Mathf.Min(speedToAdd, accelerationDelta);
        velocity = planarVelocity + verticalVelocity;
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
        float deltaTime)
    {
        if (tuningProfile == null)
        {
            return;
        }

        Vector3 up = GetSafeUp(worldUp);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, up);
        float planarSpeed = planarVelocity.magnitude;
        float overflow = planarSpeed - tuningProfile.MaxSpeed;

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
        Vector3 worldUp)
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
        float impactBonus = Mathf.Max(0f, -verticalSpeed) * response.ImpactRecoveryFactor;
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

        float multiplier = tuningProfile.AirControlStrength;
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