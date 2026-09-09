using UnityEngine;

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
    [SerializeField, Tooltip("Soft horizontal speed ceilings in world units/second. Entries correspond to x1, x2, x3, and so on.")]
    float[] speedGears = { 15f, 25f, 40f, 60f };

    [SerializeField, Tooltip("Air propulsion acceleration in world units/second squared, before directional and temporary control modifiers.")]
    float airAcceleration = 24f;
    [SerializeField, Tooltip("Maximum sideways aiming speed in world units/second. Forward momentum is carried separately.")]
    float strafeSpeed = 12f;
    [SerializeField, Tooltip("Seconds to close 63% of the gap to requested sideways speed. Also controls how quickly sideways drift stops on release.")]
    float steeringResponse = 0.1f;
    [SerializeField, Tooltip("Propulsion multiplier when already aligned with the requested heading.")]
    float forwardAirControlMultiplier = 0.6f;
    [SerializeField, Tooltip("Seconds for full backward input to remove about 63% of horizontal speed.")]
    float brakeResponse = 0.25f;
    [SerializeField, Tooltip("Extra downward acceleration from backward input while airborne, in world units/second squared.")]
    float divePull = 40f;
    [SerializeField, Tooltip("Forward propulsion limit in world units/second. Sideways control remains available above this speed, independently of gears.")]
    float maxControllableSpeed = 12f;
    [SerializeField, Tooltip("Proportional slowdown above the current gear ceiling, in inverse seconds.")]
    float overSpeedDrag = 8f;
    [SerializeField, Tooltip("Horizontal air resistance in world units/second squared. Speed lost here stays lost until input or a bounce adds it back.")]
    float airDrag = 0.5f;

    [SerializeField, Tooltip("Base gravity multiplier applied to the player.")]
    float gravityScale = 1f;
    [SerializeField, Tooltip("Gravity multiplier while the player is moving upward.")]
    float jumpGravityMultiplier = 0.85f;
    [SerializeField, Tooltip("Gravity multiplier while the player is moving downward.")]
    float fallGravityMultiplier = 1.35f;

    [SerializeField, Tooltip("Enables the motor-owned post-bounce gravity shaper for a more cartoony airborne read without replacing the mushroom launch.")]
    bool useBounceFlightShaper = true;
    [SerializeField, Tooltip("Planar speed where the bounce shaper starts treating a launch as a long-carry bounce.")]
    float referencePlanarSpeed = 13.5f;
    [SerializeField, Tooltip("Planar speed where the bounce shaper reaches its loosest long-jump behavior.")]
    float maximumPlanarSpeed = 30f;
    [SerializeField, Tooltip("Rise gravity used for slower launches.")]
    float slowRiseGravityMultiplier = 1.05f;
    [SerializeField, Tooltip("Rise gravity used for faster launches.")]
    float fastRiseGravityMultiplier = 0.9f;
    [SerializeField, Tooltip("Fall gravity used for slower launches.")]
    float slowFallGravityMultiplier = 2.15f;
    [SerializeField, Tooltip("Fall gravity used for faster launches.")]
    float fastFallGravityMultiplier = 1.95f;
    [SerializeField, Tooltip("Vertical speed where the bounce shaper starts its apex transition window.")]
    float apexTransitionVerticalSpeed = 1.1f;
    [SerializeField, Tooltip("Extra downward acceleration applied briefly when the bounce enters the apex transition window.")]
    float apexExtraDownAcceleration = 12f;
    [SerializeField, Tooltip("How long the extra apex-downward acceleration is applied.")]
    float apexExtraDownDuration = 0.06f;

    [SerializeField, Tooltip("Base bounce launch speed in world units/second; mushroom profiles can scale it.")]
    float baseJumpForce = 9f;
    [SerializeField, Tooltip("Default planar speed gain added by bounce responses.")]
    float baseBounceSpeedGain = 1f;
    [SerializeField, Tooltip("Upward speed added by an air jump in world units/second.")]
    float dashForce = 8f;
    [SerializeField, Tooltip("Minimum seconds between successful air jumps.")]
    float dashCooldown = 0.2f;
    [SerializeField, Tooltip("Air jump charges restored on each bounce.")]
    int dashChargesPerBounce = 1;
    [SerializeField, Tooltip("Short low-propulsion window immediately after a bounce.")]
    float postBounceLowControlTime = 0.1f;
    [SerializeField, Tooltip("Propulsion multiplier used during the post-bounce low-control window.")]
    float postBounceAirControlMultiplier = 0.35f;
    [SerializeField, Tooltip("Short bonus-control window immediately after a dash.")]
    float postDashBonusControlTime = 0.18f;
    [SerializeField, Tooltip("Propulsion multiplier used during the post-dash bonus-control window.")]
    float postDashAirControlMultiplier = 1.35f;

    [SerializeField, Tooltip("Grace window that still accepts a bounce shortly after leaving a surface.")]
    float bounceGraceTime = 0.1f;
    [SerializeField, Tooltip("How long a dash press can be buffered before it is executed.")]
    float dashBufferTime = 0.1f;
    [SerializeField, Range(0f, 1f), Tooltip("Minimum contact normal dot with up that still counts as ground.")]
    float minGroundDot = 0.65f;

    public float AirAcceleration => airAcceleration;

    public float StrafeSpeed => NonNegativeFinite(strafeSpeed);

    public float SteeringResponse => Mathf.Max(0.01f, NonNegativeFinite(steeringResponse));

    public float ForwardAirControlMultiplier => forwardAirControlMultiplier;

    public float BrakeResponse => Mathf.Max(0.01f, NonNegativeFinite(brakeResponse));

    public float DivePull => NonNegativeFinite(divePull);

    public float MaxControllableSpeed => maxControllableSpeed;

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

    // A gear changes capacity, not velocity. Fractional multipliers use the completed gear.
    public float GetMaxSpeed(float multiplier)
    {
        int count = speedGears != null && speedGears.Length > 0 ? speedGears.Length : 4;
        if (float.IsNaN(multiplier)) multiplier = 1f;
        int index = Mathf.FloorToInt(Mathf.Clamp(multiplier, 1f, count)) - 1;
        if (speedGears == null || speedGears.Length == 0)
        {
            return index switch { 0 => 15f, 1 => 25f, 2 => 40f, _ => 60f };
        }
        float speed = 0.01f;
        for (int i = 0; i <= index; i++)
        {
            speed = Mathf.Max(speed, NonNegativeFinite(speedGears[i]));
        }
        return speed;
    }

    static float NonNegativeFinite(float value) =>
        float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);

    void OnValidate()
    {
        airAcceleration = NonNegativeFinite(airAcceleration);
        strafeSpeed = NonNegativeFinite(strafeSpeed);
        steeringResponse = Mathf.Max(0.01f, NonNegativeFinite(steeringResponse));
        forwardAirControlMultiplier = Mathf.Max(0f, forwardAirControlMultiplier);
        brakeResponse = Mathf.Max(0.01f, NonNegativeFinite(brakeResponse));
        divePull = NonNegativeFinite(divePull);
        maxControllableSpeed = Mathf.Max(0f, maxControllableSpeed);
        if (speedGears == null || speedGears.Length == 0)
        {
            speedGears = new[] { 15f, 25f, 40f, 60f };
        }
        float previousSpeed = 0.01f;
        for (int i = 0; i < speedGears.Length; i++)
        {
            speedGears[i] = Mathf.Max(previousSpeed, NonNegativeFinite(speedGears[i]));
            previousSpeed = speedGears[i];
        }
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