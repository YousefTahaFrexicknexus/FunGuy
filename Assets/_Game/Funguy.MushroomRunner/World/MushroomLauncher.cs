using System;
using System.Collections.Generic;
using UnityEngine;

public enum MushroomSpeedMode
{
    Add,
    Multiply,
    Set
}

[Serializable]
public struct MushroomSpeedRule
{
    public MushroomSpeedMode mode;
    [Tooltip("Add accepts negative numbers. The final speed is always clamped to zero or above.")]
    public float value;

    public MushroomSpeedRule(MushroomSpeedMode mode, float value)
    {
        this.mode = mode;
        this.value = value;
    }

    public float Evaluate(float incomingSpeed, float effectiveness)
    {
        float effectiveSpeed = Mathf.Max(0f, incomingSpeed) * Mathf.Clamp(effectiveness, 0f, 2f);
        return Mathf.Max(0f, Apply(effectiveSpeed));
    }

    /// <summary>Applies one step. Effectiveness and the final speed clamp belong to the whole sequence.</summary>
    public float Apply(float speed)
    {
        return mode switch
        {
            MushroomSpeedMode.Multiply => speed * value,
            MushroomSpeedMode.Set => value,
            _ => speed + value
        };
    }
}

/// <summary>One launch per contact. The mushroom alone calculates the outgoing velocity.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class MushroomLauncher : MonoBehaviour
{
    public const float MinimumForwardDirection = 0.1f;

    [Header("Launch Direction")]
    [SerializeField, Tooltip("The blue Z arrow specifies the entire launch direction. It must point forward along world Z.")]
    Transform launchDirection;

    [Header("Landing Colliders")]
    [SerializeField, InspectorName("Normal / Bad Trigger"), Tooltip("The main trigger, on this GameObject. Entering it launches the player.")]
    Collider normalTrigger;
    [SerializeField] Collider goodTrigger;
    [SerializeField] Collider perfectTrigger;

    [Header("Speed Rules")]
    [SerializeField, InspectorName("Normal / Bad")] List<MushroomSpeedRule> normalRules = new() { new(MushroomSpeedMode.Add, 0f) };
    [SerializeField, InspectorName("Good")] List<MushroomSpeedRule> goodRules = new() { new(MushroomSpeedMode.Add, 5f) };
    [SerializeField, InspectorName("Perfect")] List<MushroomSpeedRule> perfectRules = new() { new(MushroomSpeedMode.Add, 10f) };

    [Header("Optional Visuals")]
    [SerializeField] MushroomBouncePresentation presentation;

    readonly HashSet<RunnerMovementMotor> launchedPlayers = new();
    readonly List<RunnerMovementMotor> exitedPlayers = new();
    readonly List<Collider> playerColliders = new();

    public string ConfigurationWarning
    {
        get
        {
            if (launchDirection == null)
                return "Assign a launch direction transform. Its blue Z arrow controls the launch.";
            if (!IsValidDirection(launchDirection.forward))
                return "The launch arrow must point forward along world Z (normalized Z >= 0.1). Vertical, backward, and nearly sideways arrows cannot preserve the forward-speed readout.";
            if (normalTrigger == null || normalTrigger.gameObject != gameObject || !normalTrigger.isTrigger)
                return "Assign the main trigger on this GameObject and enable Is Trigger.";
            if (goodTrigger == null || perfectTrigger == null || !goodTrigger.isTrigger || !perfectTrigger.isTrigger)
                return "Assign both Good and Perfect trigger colliders and enable Is Trigger.";
            return null;
        }
    }

    void Reset()
    {
        normalTrigger = GetComponent<Collider>();
        if (normalTrigger != null)
            normalTrigger.isTrigger = true;
    }

    void OnDisable()
    {
        launchedPlayers.Clear();
        exitedPlayers.Clear();
        playerColliders.Clear();
    }

    void OnTriggerEnter(Collider other)
    {
        RunnerMovementMotor motor = ResolveMotor(other);
        if (motor != null)
            TryLaunch(motor);
    }

    void OnTriggerExit(Collider other)
    {
        RunnerMovementMotor motor = ResolveMotor(other);
        if (motor != null && !HasPlayerOverlap(motor, normalTrigger))
            launchedPlayers.Remove(motor);
    }

    void FixedUpdate()
    {
        // Disabled/destroyed colliders and teleports do not always generate exit callbacks.
        exitedPlayers.Clear();
        foreach (RunnerMovementMotor motor in launchedPlayers)
        {
            if (motor == null || !HasPlayerOverlap(motor, normalTrigger))
                exitedPlayers.Add(motor);
        }
        foreach (RunnerMovementMotor motor in exitedPlayers)
            launchedPlayers.Remove(motor);
    }

    public bool TryLaunch(RunnerMovementMotor motor)
    {
        if (!isActiveAndEnabled || motor == null || motor.TuningProfile == null ||
            launchedPlayers.Contains(motor) || ConfigurationWarning != null || !HasPlayerOverlap(motor, normalTrigger))
        {
            return false;
        }

        LandingQuality quality = HasPlayerOverlap(motor, perfectTrigger) ? LandingQuality.Perfect
            : HasPlayerOverlap(motor, goodTrigger) ? LandingQuality.Good : LandingQuality.Bad;
        List<MushroomSpeedRule> rules = quality switch
        {
            LandingQuality.Perfect => perfectRules,
            LandingQuality.Good => goodRules,
            _ => normalRules
        };
        if (!TryCalculateLaunch(motor.Velocity, motor.TuningProfile.MushroomBounceEffectiveness,
            rules, launchDirection.forward, out Vector3 outgoingVelocity))
        {
            return false;
        }

        Vector3 contactPoint = normalTrigger.ClosestPoint(motor.rigidBody.worldCenterOfMass);
        launchedPlayers.Add(motor);
        if (!motor.ApplyMushroomLaunch(outgoingVelocity, normalTrigger, contactPoint, motor.UpDirection))
        {
            launchedPlayers.Remove(motor);
            return false;
        }

        if (motor.MomentumSource != null)
            motor.MomentumSource.OnMushroomLanded(quality);
        if (presentation != null)
            presentation.PlayBounce();
        return true;
    }

    public static bool TryCalculateLaunch(Vector3 incomingVelocity, float effectiveness, MushroomSpeedRule rule,
        Vector3 direction, out Vector3 outgoingVelocity)
    {
        return TryCalculateLaunch(incomingVelocity, effectiveness, new[] { rule }, direction, out outgoingVelocity);
    }

    public static bool TryCalculateLaunch(Vector3 incomingVelocity, float effectiveness, IReadOnlyList<MushroomSpeedRule> rules,
        Vector3 direction, out Vector3 outgoingVelocity)
    {
        outgoingVelocity = Vector3.zero;
        if (!IsValidDirection(direction) || !IsFinite(incomingVelocity.z) || !IsFinite(effectiveness))
            return false;

        // Scale incoming speed once, then apply each rule in Inspector order. An empty list adds nothing.
        float speed = Mathf.Max(0f, incomingVelocity.z) * Mathf.Clamp(effectiveness, 0f, 2f);
        if (!IsFinite(speed))
            return false;
        if (rules != null)
        {
            for (int index = 0; index < rules.Count; index++)
            {
                MushroomSpeedRule rule = rules[index];
                if (!IsFinite(rule.value))
                    return false;
                speed = rule.Apply(speed);
                if (!IsFinite(speed))
                    return false;
            }
        }

        direction.Normalize();
        speed = Mathf.Max(0f, speed);
        outgoingVelocity = direction * (speed / direction.z);
        return IsFinite(outgoingVelocity.x) && IsFinite(outgoingVelocity.y) && IsFinite(outgoingVelocity.z);
    }

    static bool IsValidDirection(Vector3 direction)
    {
        return IsFinite(direction.x) && IsFinite(direction.y) && IsFinite(direction.z)
            && direction.sqrMagnitude > 0f && direction.normalized.z >= MinimumForwardDirection;
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    bool HasPlayerOverlap(RunnerMovementMotor motor, Collider zone)
    {
        if (motor == null || !motor.gameObject.activeInHierarchy || motor.rigidBody == null ||
            zone == null || !zone.enabled || !zone.gameObject.activeInHierarchy)
            return false;

        playerColliders.Clear();
        motor.rigidBody.GetComponentsInChildren(false, playerColliders);
        foreach (Collider playerCollider in playerColliders)
        {
            if (playerCollider == null || !playerCollider.enabled || !playerCollider.gameObject.activeInHierarchy || playerCollider.isTrigger ||
                playerCollider.attachedRigidbody != motor.rigidBody)
                continue;

            if (Physics.ComputePenetration(playerCollider, playerCollider.transform.position, playerCollider.transform.rotation,
                zone, zone.transform.position, zone.transform.rotation, out _, out _))
                return true;
        }
        return false;
    }

    static RunnerMovementMotor ResolveMotor(Collider other)
    {
        if (other == null || other.attachedRigidbody == null || other.isTrigger)
            return null;
        return other.attachedRigidbody.GetComponent<RunnerMovementMotor>();
    }
}
