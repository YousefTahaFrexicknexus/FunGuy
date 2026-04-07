using UnityEngine;

namespace Funguy.IdkPlatformer
{
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

            if (alignment < 0f && tuningProfile.AirBrakeAcceleration > 0f)
            {
                float brakeDelta = tuningProfile.AirBrakeAcceleration * (-alignment) * inputFrame.Magnitude * deltaTime;
                planarVelocity = Vector3.MoveTowards(planarVelocity, Vector3.zero, brakeDelta);
            }

            float currentAlongWish = Vector3.Dot(planarVelocity, wishDirection);
            float targetAlongWish = tuningProfile.MaxControllableSpeed * inputFrame.Magnitude;
            float speedToAdd = targetAlongWish - currentAlongWish;
            if (speedToAdd <= 0f)
            {
                velocity = planarVelocity + verticalVelocity;
                return;
            }

            float contextualMultiplier = ResolveContextualAirControlMultiplier(
                tuningProfile,
                alignment,
                inPostBounceLowControl,
                inPostDashBoost);

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

        public static Vector3 ApplyBounceResponse(Vector3 incomingVelocity, in BounceSurfaceResponse response, Vector3 worldUp)
        {
            Vector3 up = GetSafeUp(worldUp);
            Vector3 planarVelocity = Vector3.ProjectOnPlane(incomingVelocity, up);
            float planarSpeed = planarVelocity.magnitude;
            Vector3 planarDirection = ResolveSurfacePlanarDirection(planarVelocity, response, up);

            Vector3 redirectedPlanar = planarVelocity;
            if (planarSpeed > MinimumDirectionSqrMagnitude && planarDirection.sqrMagnitude > MinimumDirectionSqrMagnitude)
            {
                redirectedPlanar = Vector3.Lerp(
                    planarVelocity,
                    planarDirection * planarSpeed,
                    response.DirectionalInfluence);
            }

            Vector3 planarOut = redirectedPlanar * response.VelocityScale;
            if (planarDirection.sqrMagnitude > MinimumDirectionSqrMagnitude && Mathf.Abs(response.PlanarBoost) > 0f)
            {
                planarOut += planarDirection * response.PlanarBoost;
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

        private static Vector3 GetSafeUp(Vector3 worldUp)
        {
            return worldUp.sqrMagnitude > MinimumDirectionSqrMagnitude
                ? worldUp.normalized
                : Vector3.up;
        }
    }
}
