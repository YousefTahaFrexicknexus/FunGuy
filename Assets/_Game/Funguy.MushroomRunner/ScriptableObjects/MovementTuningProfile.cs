using UnityEngine;

namespace Funguy.MushroomRunner
{
    public readonly struct BounceFlightShaperSettings
    {
        public BounceFlightShaperSettings(
            bool isEnabled,
            float referencePlanarSpeed,
            float maximumPlanarSpeed,
            float slowRiseGravityMultiplier,
            float fastRiseGravityMultiplier,
            float slowFallGravityMultiplier,
            float fastFallGravityMultiplier,
            float apexTransitionVerticalSpeed,
            float apexExtraDownAcceleration,
            float apexExtraDownDuration)
        {
            IsEnabled = isEnabled;
            ReferencePlanarSpeed = Mathf.Max(0f, referencePlanarSpeed);
            MaximumPlanarSpeed = Mathf.Max(ReferencePlanarSpeed, maximumPlanarSpeed);
            SlowRiseGravityMultiplier = Mathf.Max(0f, slowRiseGravityMultiplier);
            FastRiseGravityMultiplier = Mathf.Max(0f, fastRiseGravityMultiplier);
            SlowFallGravityMultiplier = Mathf.Max(0f, slowFallGravityMultiplier);
            FastFallGravityMultiplier = Mathf.Max(0f, fastFallGravityMultiplier);
            ApexTransitionVerticalSpeed = Mathf.Max(0f, apexTransitionVerticalSpeed);
            ApexExtraDownAcceleration = Mathf.Max(0f, apexExtraDownAcceleration);
            ApexExtraDownDuration = Mathf.Max(0f, apexExtraDownDuration);
        }

        public bool IsEnabled { get; }

        public float ReferencePlanarSpeed { get; }

        public float MaximumPlanarSpeed { get; }

        public float SlowRiseGravityMultiplier { get; }

        public float FastRiseGravityMultiplier { get; }

        public float SlowFallGravityMultiplier { get; }

        public float FastFallGravityMultiplier { get; }

        public float ApexTransitionVerticalSpeed { get; }

        public float ApexExtraDownAcceleration { get; }

        public float ApexExtraDownDuration { get; }
    }

    /// <summary>
    /// Central movement tuning shared by the player and reach-validation systems.
    /// </summary>
    [CreateAssetMenu(fileName = "MovementTuningProfile", menuName = "Funguy/MushroomRunner/Movement Tuning Profile")]
    public sealed class MovementTuningProfile : ScriptableObject
    {
        [Header("Air Control")]
        [SerializeField, Tooltip("Air acceleration applied when steering in a desired direction.")]
        private float moveAcceleration = 24f;
        [SerializeField, Tooltip("Overall strength of air steering relative to the desired input direction.")]
        private float airControlStrength = 1f;
        [SerializeField, Tooltip("Multiplier applied to forward steering so forward control can be looser or tighter than strafe control.")]
        private float forwardAirControlMultiplier = 0.6f;
        [SerializeField, Tooltip("How quickly brake input removes planar speed while airborne.")]
        private float airBrakeAcceleration = 18f;
        [SerializeField, Tooltip("Speed where normal air control starts to taper off.")]
        private float maxControllableSpeed = 12f;
        [SerializeField, Tooltip("Soft top speed target before overspeed drag pushes the player back down.")]
        private float maxSpeed = 18f;
        [SerializeField, Tooltip("Extra drag applied while the player is above Max Speed.")]
        private float overSpeedDrag = 8f;
        [SerializeField, Tooltip("Constant air drag applied every physics step.")]
        private float airDrag = 0.5f;

        [Header("Gravity")]
        [SerializeField, Tooltip("Base gravity multiplier applied to the player.")]
        private float gravityScale = 1f;
        [SerializeField, Tooltip("Gravity multiplier while the player is moving upward.")]
        private float jumpGravityMultiplier = 0.85f;
        [SerializeField, Tooltip("Gravity multiplier while the player is moving downward.")]
        private float fallGravityMultiplier = 1.35f;

        [Header("Bounce Flight Shaper")]
        [SerializeField, Tooltip("Enables the motor-owned post-bounce gravity shaper for a more cartoony airborne read without replacing the mushroom launch.")]
        private bool useBounceFlightShaper = true;
        [SerializeField, Tooltip("Planar speed where the bounce shaper starts treating a launch as a long-carry bounce.")]
        private float referencePlanarSpeed = 13.5f;
        [SerializeField, Tooltip("Planar speed where the bounce shaper reaches its loosest long-jump behavior.")]
        private float maximumPlanarSpeed = 30f;
        [SerializeField, Tooltip("Rise gravity used for slower launches.")]
        private float slowRiseGravityMultiplier = 1.05f;
        [SerializeField, Tooltip("Rise gravity used for faster launches.")]
        private float fastRiseGravityMultiplier = 0.9f;
        [SerializeField, Tooltip("Fall gravity used for slower launches.")]
        private float slowFallGravityMultiplier = 2.15f;
        [SerializeField, Tooltip("Fall gravity used for faster launches.")]
        private float fastFallGravityMultiplier = 1.95f;
        [SerializeField, Tooltip("Vertical speed where the bounce shaper starts its apex transition window.")]
        private float apexTransitionVerticalSpeed = 1.1f;
        [SerializeField, Tooltip("Extra downward acceleration applied briefly when the bounce enters the apex transition window.")]
        private float apexExtraDownAcceleration = 12f;
        [SerializeField, Tooltip("How long the extra apex-downward acceleration is applied.")]
        private float apexExtraDownDuration = 0.06f;

        [Header("Bounce And Dash")]
        [SerializeField, Tooltip("Base upward force used by standard bounce calculations.")]
        private float baseJumpForce = 9f;
        [SerializeField, Tooltip("Default planar speed gain added by bounce responses.")]
        private float baseBounceSpeedGain = 1f;
        [SerializeField, Tooltip("Impulse strength applied when a dash is consumed.")]
        private float dashForce = 8f;
        [SerializeField, Tooltip("Minimum time between successful dashes.")]
        private float dashCooldown = 0.2f;
        [SerializeField, Tooltip("How many dashes are restored each time the player bounces.")]
        private int dashChargesPerBounce = 1;
        [SerializeField, Tooltip("Short low-control window immediately after a bounce.")]
        private float postBounceLowControlTime = 0.1f;
        [SerializeField, Tooltip("Air-control multiplier used during the post-bounce low-control window.")]
        private float postBounceAirControlMultiplier = 0.35f;
        [SerializeField, Tooltip("Short bonus-control window immediately after a dash.")]
        private float postDashBonusControlTime = 0.18f;
        [SerializeField, Tooltip("Air-control multiplier used during the post-dash bonus-control window.")]
        private float postDashAirControlMultiplier = 1.35f;

        [Header("Forgiveness")]
        [SerializeField, Tooltip("Grace window that still accepts a bounce shortly after leaving a surface.")]
        private float bounceGraceTime = 0.1f;
        [SerializeField, Tooltip("How long a dash press can be buffered before it is executed.")]
        private float dashBufferTime = 0.1f;
        [SerializeField, Range(0f, 1f), Tooltip("Minimum contact normal dot with up that still counts as ground.")]
        private float minGroundDot = 0.65f;

        public float MoveAcceleration => moveAcceleration;

        public float AirControlStrength => airControlStrength;

        public float ForwardAirControlMultiplier => forwardAirControlMultiplier;

        public float AirBrakeAcceleration => airBrakeAcceleration;

        public float MaxControllableSpeed => maxControllableSpeed;

        public float MaxSpeed => Mathf.Max(maxControllableSpeed, maxSpeed);

        public float OverSpeedDrag => overSpeedDrag;

        public float AirDrag => airDrag;

        public float GravityScale => gravityScale;

        public float JumpGravityMultiplier => jumpGravityMultiplier;

        public float FallGravityMultiplier => fallGravityMultiplier;

        public bool UseBounceFlightShaper => useBounceFlightShaper;

        public BounceFlightShaperSettings BounceFlightShaper => new(
            useBounceFlightShaper,
            referencePlanarSpeed,
            maximumPlanarSpeed,
            slowRiseGravityMultiplier,
            fastRiseGravityMultiplier,
            slowFallGravityMultiplier,
            fastFallGravityMultiplier,
            apexTransitionVerticalSpeed,
            apexExtraDownAcceleration,
            apexExtraDownDuration);

        public float BaseJumpForce => baseJumpForce;

        public float BaseBounceSpeedGain => baseBounceSpeedGain;

        public float DashForce => dashForce;

        public float DashCooldown => dashCooldown;

        public int DashChargesPerBounce => Mathf.Max(1, dashChargesPerBounce);

        public float PostBounceLowControlTime => postBounceLowControlTime;

        public float PostBounceAirControlMultiplier => postBounceAirControlMultiplier;

        public float PostDashBonusControlTime => postDashBonusControlTime;

        public float PostDashAirControlMultiplier => postDashAirControlMultiplier;

        public float BounceGraceTime => bounceGraceTime;

        public float DashBufferTime => dashBufferTime;

        public float MinGroundDot => minGroundDot;

        private void OnValidate()
        {
            moveAcceleration = Mathf.Max(0f, moveAcceleration);
            airControlStrength = Mathf.Max(0f, airControlStrength);
            forwardAirControlMultiplier = Mathf.Max(0f, forwardAirControlMultiplier);
            airBrakeAcceleration = Mathf.Max(0f, airBrakeAcceleration);
            maxControllableSpeed = Mathf.Max(0f, maxControllableSpeed);
            maxSpeed = Mathf.Max(maxControllableSpeed, maxSpeed);
            overSpeedDrag = Mathf.Max(0f, overSpeedDrag);
            airDrag = Mathf.Max(0f, airDrag);
            gravityScale = Mathf.Max(0f, gravityScale);
            jumpGravityMultiplier = Mathf.Max(0f, jumpGravityMultiplier);
            fallGravityMultiplier = Mathf.Max(0f, fallGravityMultiplier);
            referencePlanarSpeed = Mathf.Max(0f, referencePlanarSpeed);
            maximumPlanarSpeed = Mathf.Max(referencePlanarSpeed, maximumPlanarSpeed);
            slowRiseGravityMultiplier = Mathf.Max(0f, slowRiseGravityMultiplier);
            fastRiseGravityMultiplier = Mathf.Max(0f, fastRiseGravityMultiplier);
            slowFallGravityMultiplier = Mathf.Max(0f, slowFallGravityMultiplier);
            fastFallGravityMultiplier = Mathf.Max(0f, fastFallGravityMultiplier);
            apexTransitionVerticalSpeed = Mathf.Max(0f, apexTransitionVerticalSpeed);
            apexExtraDownAcceleration = Mathf.Max(0f, apexExtraDownAcceleration);
            apexExtraDownDuration = Mathf.Max(0f, apexExtraDownDuration);
            baseJumpForce = Mathf.Max(0f, baseJumpForce);
            baseBounceSpeedGain = Mathf.Max(0f, baseBounceSpeedGain);
            dashForce = Mathf.Max(0f, dashForce);
            dashCooldown = Mathf.Max(0f, dashCooldown);
            dashChargesPerBounce = Mathf.Max(1, dashChargesPerBounce);
            postBounceLowControlTime = Mathf.Max(0f, postBounceLowControlTime);
            postBounceAirControlMultiplier = Mathf.Max(0f, postBounceAirControlMultiplier);
            postDashBonusControlTime = Mathf.Max(0f, postDashBonusControlTime);
            postDashAirControlMultiplier = Mathf.Max(0f, postDashAirControlMultiplier);
            bounceGraceTime = Mathf.Max(0f, bounceGraceTime);
            dashBufferTime = Mathf.Max(0f, dashBufferTime);
            minGroundDot = Mathf.Clamp01(minGroundDot);
        }
    }
}

