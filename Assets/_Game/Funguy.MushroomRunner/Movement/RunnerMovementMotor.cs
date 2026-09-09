using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class RunnerMovementMotor : MonoBehaviour
{
    const float MinDirectionSqrMagnitude = 0.0001f;

    public Rigidbody rigidBody;
    MovementTuningProfile tuningProfile;
    [SerializeField] bool motorEnabled = true;
    [SerializeField] Vector3 worldUp = Vector3.up;

    MovementInputFrame currentInput = MovementInputFrame.Empty;
    float bufferedDashUntil = float.NegativeInfinity;
    float lowControlUntil = float.NegativeInfinity;
    float dashControlBoostUntil = float.NegativeInfinity;
    RunnerBounceContacts bounceContacts;
    RunnerBounceContacts Contacts => bounceContacts ??= new RunnerBounceContacts(this);
    Func<float> getSpeedLimit;
    float currentMaxSpeed;
    Func<bool> tryConsumeDashHandler;
    bool isGrounded;
    BounceFlightShapeState activeBounceFlightShape;
    float diveAddedDownSpeed;

    public event Action<BounceEventData> Bounced;
    public event Action Dashed;

    public Vector3 Velocity => rigidBody != null ? rigidBody.linearVelocity : Vector3.zero;

    public MovementTuningProfile TuningProfile => tuningProfile;

    public bool IsGrounded => isGrounded;

    public float CurrentMaxSpeed => currentMaxSpeed > 0f ? currentMaxSpeed : ResolveMaxSpeed();

    public void SetSpeedLimitProvider(Func<float> provider)
    {
        getSpeedLimit = provider;
        RefreshSpeedLimit();
    }

    float ResolveMaxSpeed()
    {
        float fallback = tuningProfile != null ? tuningProfile.GetMaxSpeed(1f) : 15f;
        float speed = getSpeedLimit != null ? getSpeedLimit() : fallback;
        return float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f ? fallback : speed;
    }

    void RefreshSpeedLimit()
    {
        currentMaxSpeed = ResolveMaxSpeed();
    }

    public Vector3 UpDirection => Up;

    Vector3 Up => worldUp.sqrMagnitude > MinDirectionSqrMagnitude ? worldUp.normalized : Vector3.up;

    void Reset()
    {
        rigidBody = GetComponent<Rigidbody>();
        Contacts.CacheBodyColliders();
    }

    void Awake()
    {
        if (rigidBody == null)
        {
            rigidBody = GetComponent<Rigidbody>();
        }

        Contacts.CacheBodyColliders();
        ConfigureRigidbody();
    }

    void OnValidate()
    {
        if (rigidBody == null)
        {
            rigidBody = GetComponent<Rigidbody>();
        }

        worldUp = worldUp.sqrMagnitude > MinDirectionSqrMagnitude ? worldUp.normalized : Vector3.up;

        if (rigidBody != null)
        {
            ConfigureRigidbody();
        }

        Contacts.CacheBodyColliders();
    }

    void OnDisable()
    {
        Contacts.RestoreIgnoredBounceSurface();
        diveAddedDownSpeed = 0f;
    }

    void FixedUpdate()
    {
        RefreshSpeedLimit();
        Contacts.RestoreIgnoredBounceSurfaceIfExpired();
        isGrounded = Contacts.ComputeGroundedState();
        if (isGrounded)
        {
            activeBounceFlightShape = default;
        }

        if (rigidBody == null || tuningProfile == null)
        {
            return;
        }

        if (!motorEnabled)
        {
            currentInput = MovementInputFrame.Empty;
            return;
        }

        float deltaTime = Time.fixedDeltaTime;
        Vector3 velocity = rigidBody.linearVelocity;

        ApplyShapedGravity(ref velocity, deltaTime);
        if (!isGrounded)
        {
            diveAddedDownSpeed += BounceMovementMath.ApplyDive(ref velocity, tuningProfile, currentInput.BrakeAmount, Up, deltaTime);
        }

        float planarDrag = 0f;
        bool applyPlanarDrag = false;
        bool bouncedThisStep = TryConsumeBounceCandidate(ref velocity, out BounceSurfaceResponse bounceResponse);
        if (bouncedThisStep && bounceResponse.HasPlanarDragOverride)
        {
            planarDrag = bounceResponse.PlanarDragOverride;
            applyPlanarDrag = planarDrag > 0f;
        }
        else if (!isGrounded)
        {
            planarDrag = tuningProfile.AirDrag;
            applyPlanarDrag = planarDrag > 0f;
        }

        if (bouncedThisStep)
        {
            BounceMovementMath.ApplyLateralControl(ref velocity, tuningProfile, currentInput, Up, deltaTime);
            BounceMovementMath.ApplyAirBraking(ref velocity, tuningProfile, currentInput.BrakeAmount, Up, deltaTime);
        }
        else if (!isGrounded)
        {
            ApplyAirMovement(ref velocity, currentInput, deltaTime);
        }

        if (applyPlanarDrag)
        {
            ApplyPlanarDrag(ref velocity, planarDrag, deltaTime);
        }

        ApplySoftSpeedLimit(ref velocity, deltaTime);

        TryConsumeBufferedDash(ref velocity, bouncedThisStep);

        rigidBody.linearVelocity = velocity;
        isGrounded = !bouncedThisStep && Contacts.ComputeGroundedState();

        GameplayEvents.OnSpeedChanged?.Invoke(velocity.z, CurrentMaxSpeed);
    }

    public void SetInput(MovementInputFrame inputFrame)
    {
        currentInput = inputFrame;
    }

    public void RequestDash()
    {
        if (tuningProfile == null)
        {
            return;
        }

        bufferedDashUntil = Mathf.Max(bufferedDashUntil, Time.time + tuningProfile.DashBufferTime);

        GameplayEvents.OnAirJump?.Invoke(0);
    }

    public void SetTuningProfile(MovementTuningProfile profile)
    {
        tuningProfile = profile;
        RefreshSpeedLimit();

        if (!BounceMovementMath.ShouldUseBounceFlightShaper(tuningProfile))
        {
            activeBounceFlightShape = default;
        }
    }

    public void SetMotorEnabled(bool enabled)
    {
        motorEnabled = enabled;

        if (!enabled)
        {
            bufferedDashUntil = float.NegativeInfinity;


            currentInput = MovementInputFrame.Empty;
            activeBounceFlightShape = default;
            diveAddedDownSpeed = 0f;
        }
    }

    public void SetDashResourceHandler(Func<bool> dashConsumer)
    {
        tryConsumeDashHandler = dashConsumer;
    }

    public void ResetMotion(Vector3 worldPosition, Quaternion worldRotation)
    {
        if (rigidBody == null)
        {
            rigidBody = GetComponent<Rigidbody>();
        }

        if (rigidBody == null)
        {
            return;
        }

        Contacts.ResetMotion();
        RefreshSpeedLimit();
        bufferedDashUntil = float.NegativeInfinity;
        lowControlUntil = float.NegativeInfinity;
        dashControlBoostUntil = float.NegativeInfinity;


        isGrounded = false;
        currentInput = MovementInputFrame.Empty;
        activeBounceFlightShape = default;
        diveAddedDownSpeed = 0f;

        rigidBody.linearVelocity = Vector3.zero;
        rigidBody.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(worldPosition, worldRotation);
        rigidBody.position = worldPosition;
        rigidBody.rotation = worldRotation;
        Physics.SyncTransforms();
        rigidBody.WakeUp();
        Contacts.RestoreIgnoredBounceSurface();
    }

    public bool TryBounce(Transform launchDirection, MushroomBounceProfile bounceProfile)
    {
        return TryBounce(launchDirection, bounceProfile, null, transform.position, Up);
    }

    public bool TryBounce(
        Transform launchDirection,
        MushroomBounceProfile bounceProfile,
        Collider sourceCollider,
        Vector3 contactPoint,
        Vector3 contactNormal)
    {
        if (rigidBody == null)
        {
            rigidBody = GetComponent<Rigidbody>();
        }

        if (rigidBody == null || tuningProfile == null || bounceProfile == null || !motorEnabled)
        {
            return false;
        }

        GameplayEvents.OnMushroomJump?.Invoke();

        Vector3 safeContactNormal = contactNormal.sqrMagnitude > MinDirectionSqrMagnitude
            ? contactNormal.normalized
            : Up;
        Vector3 incomingVelocity = rigidBody.linearVelocity;
        BounceContext context = new(
            incomingVelocity,
            contactPoint,
            safeContactNormal,
            Up,
            tuningProfile.BaseJumpForce,
            currentInput);

        BounceSurfaceResponse response = bounceProfile.CreateDirectedResponse(launchDirection, context);
        Vector3 outgoingVelocity = ApplyBounceResponse(incomingVelocity, response);
        Vector3 forceDelta = outgoingVelocity - incomingVelocity;
        rigidBody.AddForce(forceDelta, ForceMode.VelocityChange);

        CompleteBounce(sourceCollider);

        Bounced?.Invoke(new BounceEventData(
            sourceCollider,
            contactPoint,
            safeContactNormal,
            incomingVelocity,
            outgoingVelocity,
            response));

        return true;
    }

    void OnCollisionEnter(Collision collision)
    {
        Contacts.CacheBounceCandidate(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        Contacts.CacheBounceCandidate(collision);
    }

    void OnCollisionExit(Collision collision) => Contacts.OnCollisionExit(collision);

    void ConfigureRigidbody()
    {
        rigidBody.useGravity = false;
        rigidBody.constraints |= RigidbodyConstraints.FreezeRotation;
        rigidBody.interpolation = RigidbodyInterpolation.Interpolate;
        rigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    void ApplyShapedGravity(ref Vector3 velocity, float deltaTime)
    {
        if (BounceMovementMath.ApplyBounceFlightShaper(ref velocity, tuningProfile, Up, ref activeBounceFlightShape, deltaTime))
        {
            return;
        }

        BounceMovementMath.ApplyShapedGravity(ref velocity, tuningProfile, Up, deltaTime);
    }

    void ApplyAirMovement(ref Vector3 velocity, MovementInputFrame inputFrame, float deltaTime)
    {
        BounceMovementMath.ApplyAirMovement(
            ref velocity,
            tuningProfile,
            inputFrame,
            Up,
            Time.time < lowControlUntil && Time.time >= dashControlBoostUntil,
            Time.time < dashControlBoostUntil,
            deltaTime);
    }

    void ApplyPlanarDrag(ref Vector3 velocity, float drag, float deltaTime)
    {
        BounceMovementMath.ApplyPlanarDrag(ref velocity, Up, drag, deltaTime);
    }

    void ApplySoftSpeedLimit(ref Vector3 velocity, float deltaTime)
    {
        BounceMovementMath.ApplySoftSpeedLimit(ref velocity, tuningProfile, Up, CurrentMaxSpeed, deltaTime);
    }

    bool TryConsumeBounceCandidate(ref Vector3 velocity, out BounceSurfaceResponse response)
    {
        response = default;

        if (!Contacts.TryGetCandidate(out RunnerBounceContacts.BounceCandidate candidate))
        {
            return false;
        }

        Vector3 incomingVelocity = velocity;
        BounceContext context = new(
            incomingVelocity,
            candidate.ContactPoint,
            candidate.ContactNormal,
            Up,
            tuningProfile.BaseJumpForce,
            currentInput);

        response = candidate.Surface.GetBounceResponse(in context);
        velocity = ApplyBounceResponse(incomingVelocity, response);

        Bounced?.Invoke(new BounceEventData(
            candidate.Collider,
            candidate.ContactPoint,
            candidate.ContactNormal,
            incomingVelocity,
            velocity,
            response));

        Contacts.ApplyPostBounceCollisionIgnore(candidate.Surface, candidate.Collider);
        CompleteBounce(candidate.Collider);
        return true;
    }

    Vector3 ApplyBounceResponse(Vector3 incomingVelocity, BounceSurfaceResponse response)
    {
        Vector3 outgoingVelocity = BounceMovementMath.ApplyBounceResponse(incomingVelocity, response, tuningProfile, Up, diveAddedDownSpeed);
        activeBounceFlightShape = BounceMovementMath.CreateBounceFlightShapeState(outgoingVelocity, tuningProfile, Up);
        return outgoingVelocity;
    }

    void CompleteBounce(Collider surface)
    {
        diveAddedDownSpeed = 0f;
        Contacts.MarkConsumed(surface);
        lowControlUntil = Time.time + tuningProfile.PostBounceLowControlTime;
        isGrounded = false;
    }

    bool TryConsumeBufferedDash(ref Vector3 velocity, bool bouncedThisStep)
    {
        if (tuningProfile == null || bufferedDashUntil < Time.time)
        {
            if (bufferedDashUntil < Time.time)
            {
                bufferedDashUntil = float.NegativeInfinity;
            }

            return false;
        }

        if (!bouncedThisStep && isGrounded)
        {
            return false;
        }

        bool canConsumeDash = tryConsumeDashHandler == null || tryConsumeDashHandler.Invoke();
        if (!canConsumeDash)
        {
            return false;
        }

        Vector3 normalizedDashDirection = Up;
        activeBounceFlightShape = default;
        diveAddedDownSpeed = 0f;
        float speedAlongDash = Vector3.Dot(velocity, normalizedDashDirection);
        if (speedAlongDash < 0f)
        {
            // Treat the air jump like a fresh upward launch instead of a small brake when the player is falling.
            velocity -= normalizedDashDirection * speedAlongDash;
        }

        velocity += normalizedDashDirection * tuningProfile.DashForce;
        bufferedDashUntil = float.NegativeInfinity;
        dashControlBoostUntil = Time.time + tuningProfile.PostDashBonusControlTime;
        Dashed?.Invoke();
        return true;
    }

}
