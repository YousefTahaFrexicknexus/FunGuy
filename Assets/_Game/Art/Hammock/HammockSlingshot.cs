using System;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Put this on the character (next to RunnerMovementMotor), resting in the hammock at the start of the level.
/// The character rides along with the hammock's own sway until the player grabs it. Drag on screen
/// to stretch it: down = more power, left/right = aim. Release hard enough and he is flung;
/// release too weakly and the hammock settles back into its sway.
/// The trajectory preview uses the motor's own flight math, so it matches the real flight.
/// Pair it with HammockDeformer, which bends the hammock mesh to follow the character.
/// </summary>
[DefaultExecutionOrder(100)]   // runs before HammockDeformer (200), after animations have moved the hammock
[RequireComponent(typeof(Rigidbody))]
public class HammockSlingshot : MonoBehaviour
{
    [Header("Hammock motion")]
    [Tooltip("The object that makes the hammock sway: the one your Animator/script moves, or a bone in the " +
             "middle of the hammock. The character rides along with it. " +
             "Leave empty to use the built-in sway below instead.")]
    [SerializeField] private Transform hammockSeat;
    [Tooltip("Also tilt the character with the hammock's rocking.")]
    [SerializeField] private bool followSeatRotation = true;

    [Header("Aim")]
    [Tooltip("Horizontal direction (world space) the character is launched in when aiming straight.")]
    [SerializeField] private Vector3 launchDirection = Vector3.forward;
    [Tooltip("Upward angle of the launch in degrees. A little loft looks great.")]
    [SerializeField, Range(0f, 45f)] private float launchPitch = 12f;
    [Tooltip("How far left/right (degrees) the player can aim. Like a real slingshot: pull right = aim left.")]
    [SerializeField, Range(0f, 80f)] private float maxAimAngle = 40f;
    [Tooltip("Tick this if you'd rather drag toward the side you want to go.")]
    [SerializeField] private bool invertHorizontal = false;

    [Header("Built-in sway (only used when Hammock Seat is empty)")]
    [Tooltip("How far (world units) the hammock swings back and forth while waiting. 0 = no sway.")]
    [SerializeField] private float swayAmplitude = 0.2f;
    [Tooltip("Swings per second.")]
    [SerializeField] private float swayFrequency = 0.4f;
    [Tooltip("Seconds to fade the sway out when the player grabs the hammock, and back in after a weak pull.")]
    [SerializeField] private float swayBlendTime = 0.4f;

    [Header("Pull")]
    [Tooltip("How far back (world units) the character moves at full pull.")]
    [SerializeField] private float maxPullDistance = 2.5f;
    [Tooltip("How many screen pixels of dragging equal a full pull (in any direction).")]
    [SerializeField] private float maxDragPixels = 300f;
    [Tooltip("0 = linear. Higher = the hammock feels harder to stretch the further you pull.")]
    [SerializeField, Range(0f, 1f)] private float resistance = 0.5f;
    [Tooltip("How quickly the hammock follows your finger. Lower = heavier, floatier feel.")]
    [SerializeField] private float followSharpness = 25f;
    [Tooltip("Releasing below this pull (0-1) does NOT launch: the hammock goes back to swaying. " +
             "The trajectory line only appears once you are past this point.")]
    [SerializeField, Range(0f, 0.9f)] private float minPullToLaunch = 0.25f;
    [Tooltip("Seconds the hammock takes to settle back after a weak pull.")]
    [SerializeField] private float returnTime = 0.35f;
    [Tooltip("Tiny tremble when the hammock is near full stretch (world units). 0 = off.")]
    [SerializeField] private float strainShake = 0.015f;

    [Header("Input")]
    [SerializeField] private bool enablePointer = true;    // mouse + touch
    [SerializeField] private bool enableKeyboard = true;
    [SerializeField] private bool enableGamepad = true;
    [Tooltip("Seconds of holding Space / S / Down to reach a full pull.")]
    [SerializeField] private float keyboardChargeTime = 1.2f;
    [Tooltip("How fast A/D sweep the aim, in 'full range per second'.")]
    [SerializeField] private float keyboardAimSpeed = 1.5f;
    [Tooltip("Stick deflection that starts a pull.")]
    [SerializeField, Range(0.05f, 0.9f)] private float stickPullThreshold = 0.3f;
    [Tooltip("Stick deflection below which the stick counts as let go. Keep below the pull threshold.")]
    [SerializeField, Range(0.01f, 0.5f)] private float stickReleaseThreshold = 0.15f;
    [Tooltip("A stick springs back in a few frames. On release, the strongest value from this many seconds ago is used, so the snap-back doesn't weaken the shot.")]
    [SerializeField] private float stickSnapbackWindow = 0.12f;

    [Header("Launch")]
    [Tooltip("Note: speed above the motor's max speed is slowly bled off by its soft speed limit.")]
    [SerializeField] private float minLaunchSpeed = 15f;
    [SerializeField] private float maxLaunchSpeed = 40f;
    [SerializeField] private AnimationCurve powerCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Trajectory preview")]
    [Tooltip("A LineRenderer. Leave empty to turn the preview off.")]
    [SerializeField] private LineRenderer trajectoryLine;
    [Tooltip("Optional object placed where the arc lands (a flat disc/ring whose up axis points away from the surface).")]
    [SerializeField] private Transform landingMarker;
    [Tooltip("Where the predicted arc starts, relative to the character's resting position. " +
             "Use his collider's centre so the arc stops when his body would actually touch something.")]
    [SerializeField] private Vector3 trajectoryOriginOffset = new Vector3(0f, 0.9f, 0f);
    [Tooltip("Radius of the 'body' used to detect hits. Match your character's collider radius. 0 = thin ray.")]
    [SerializeField] private float trajectoryProbeRadius = 0.3f;
    [Tooltip("Number of points in the line.")]
    [SerializeField, Range(10, 120)] private int trajectoryPoints = 40;
    [Tooltip("How many seconds of flight to predict. The line stops earlier if he would land.")]
    [SerializeField] private float trajectoryDuration = 3f;
    [Tooltip("What the preview stops on. Exclude the Player and Hammock layers.")]
    [SerializeField] private LayerMask trajectoryCollisionMask = ~0;
    [Tooltip("Only used if no RunnerMovementMotor is found: 1 = normal gravity.")]
    [SerializeField] private float gravityScale = 1f;

    [Header("Runner hookup")]
    [Tooltip("Your RunnerMovementMotor. Found automatically on this object. The trajectory preview uses its " +
             "tuning (gravity shaping, air drag, speed limit) and ResetSlingshot() calls its ResetMotion().")]
    [SerializeField] private RunnerMovementMotor motor;
    [Tooltip("Your normal run/steer scripts (include the RunnerMovementMotor). Disabled until the launch happens.")]
    [SerializeField] private Behaviour[] disableUntilLaunch;
    [Tooltip("Seconds after launch before the scripts above are enabled. KEEP AT 0 with RunnerMovementMotor: " +
             "it supplies the gravity (the Rigidbody has Use Gravity off), so he would fly straight until it is enabled.")]
    [SerializeField] private float runnerHandoffDelay = 0f;
    [Tooltip("Invoked at the moment the character leaves the hammock, with the launch speed. " +
             "Use for the run animation trigger, sound, camera kick, etc.")]
    public UnityEvent<float> onLaunched;
    [Tooltip("Invoked when ResetSlingshot() is called. Use it to put the character's Animator back " +
             "to the lying pose, reset the camera, and so on.")]
    public UnityEvent onReset;

    /// <summary>Raised when the character leaves the hammock: (direction, speed).</summary>
    public event Action<Vector3, float> Launched;

    /// <summary>Raised when the slingshot is reset for another attempt.</summary>
    public event Action WasReset;

    /// <summary>0-1 how far the hammock is stretched right now.</summary>
    public float Pull01 => pull;
    /// <summary>The direction he will be launched in if released now.</summary>
    public Vector3 AimDirection => aimDir;
    public float MaxPullDistance => maxPullDistance;
    /// <summary>
    /// How far the character is from his seat in the hammock (the pull, plus built-in sway if used).
    /// The hammock's own sway is NOT included, because the hammock already moves with it.
    /// </summary>
    public Vector3 Displacement => transform.position - RestPosition;
    /// <summary>True while the character is still in the hammock (before he is launched).</summary>
    public bool IsHolding => state != State.Launched;

    private enum State { Swaying, Aiming, Returning, Launching, Launched }

    private const int CastStride = 2;   // physics steps covered by each hit-test segment of the preview

    private Rigidbody rb;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 seatLocalPosition;       // where he sits, in the seat's local space
    private Quaternion seatRotationOffset;
    private Vector3 flatDir;      // horizontal launch direction (also the built-in sway axis)
    private Vector3 baseDir;      // straight-ahead launch direction (with pitch)
    private Vector3 aimDir;       // current direction after left/right aiming
    private Vector3 lockedDir;    // direction locked in at release
    private Vector3 shakeRight;
    private State state = State.Swaying;

    private Vector2 dragStart;
    private Vector2 pullVec;      // x = sideways, y = backward, magnitude 0-1
    private Vector2 pullVelocity;
    private float pull;
    private float aimYaw;

    private enum InputSource { None, Pointer, Keyboard, Gamepad }

    private struct AimInput
    {
        public Vector2 raw;      // x: drag right, y: drag back (before easing)
        public bool begin;       // a pull started this frame
        public bool release;     // the pull was let go this frame
        public bool instant;     // skip smoothing this frame (stick snap-back)
    }

    private InputSource activeSource = InputSource.None;
    private float keyCharge;
    private float keyAim;            // -1 = aim left, +1 = aim right
    private Vector2 stickPeak;
    private float stickPeakTime;
    private bool stickArmed = true;  // stick must return to neutral before a new pull can start

    private float swayTime;
    private float swayWeight = 1f;

    // Release phase (the hammock pulls the character back to rest like a spring)
    private float launchSpeed;
    private float releaseDistance;
    private float omega;
    private float launchTime;

    private bool runnerPending;
    private float handoffTimer;

    private Vector3[] simPositions;        // predicted body path, one sample per physics step
    private Vector3[] trajectoryBuffer;    // what the LineRenderer receives

    /// <summary>Where he rests right now: follows the hammock's own sway if a seat is assigned.</summary>
    private Vector3 RestPosition =>
        hammockSeat != null ? hammockSeat.TransformPoint(seatLocalPosition) : startPosition;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (motor == null) motor = GetComponent<RunnerMovementMotor>();

        startPosition = transform.position;
        startRotation = transform.rotation;

        if (hammockSeat != null)
        {
            // Remember where he lies relative to the hammock, so he keeps that spot as it moves.
            seatLocalPosition = hammockSeat.InverseTransformPoint(startPosition);
            seatRotationOffset = Quaternion.Inverse(hammockSeat.rotation) * transform.rotation;
        }

        Vector3 flat = launchDirection; flat.y = 0f;
        flatDir = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
        shakeRight = Vector3.Cross(Vector3.up, flatDir);
        baseDir = Quaternion.AngleAxis(-launchPitch, shakeRight) * flatDir;
        aimDir = baseDir;

        if (trajectoryLine != null) trajectoryLine.useWorldSpace = true;
        HideTrajectory();

        rb.isKinematic = true;     // hold him in the hammock until the launch
        SetRunnerEnabled(false);
    }

    private void Update()
    {
        if (state == State.Launched)
        {
            UpdateHandoff();
            return;
        }

        // Built-in sway keeps running; its weight fades out while the player holds the hammock.
        swayTime += Time.deltaTime;
        float swayTarget = (state == State.Swaying || state == State.Returning) ? 1f : 0f;
        swayWeight = Mathf.MoveTowards(swayWeight, swayTarget, Time.deltaTime / Mathf.Max(0.01f, swayBlendTime));

        if (state == State.Launching)
        {
            launchTime += Time.deltaTime;
            if (launchTime >= Mathf.PI * 0.5f / omega) FinishLaunch();
            return;
        }

        AimInput input = ReadAimInput();

        if (input.begin)
        {
            pullVelocity = Vector2.zero;
            state = State.Aiming;
        }

        if (state == State.Aiming)
        {
            Vector2 target = PullTarget(input.raw);
            float blend = input.instant ? 1f : 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            pullVec = Vector2.Lerp(pullVec, target, blend);
            pull = pullVec.magnitude;

            UpdateAimDirection();
            UpdateTrajectory();

            if (input.release) Release();
        }
        else if (state == State.Returning)
        {
            pullVec = Vector2.SmoothDamp(pullVec, Vector2.zero, ref pullVelocity, Mathf.Max(0.01f, returnTime));
            pull = pullVec.magnitude;
            UpdateAimDirection();

            if (pull < 0.002f)
            {
                pullVec = Vector2.zero;
                pull = 0f;
                state = State.Swaying;
            }
        }
    }

    // The pose is applied in LateUpdate, after Animators/scripts have moved the hammock this frame,
    // so the character never lags a frame behind it.
    private void LateUpdate()
    {
        if (state == State.Launched) return;

        Vector3 offset;

        if (state == State.Launching)
        {
            float along = releaseDistance * Mathf.Cos(omega * launchTime);
            offset = SwayOffset() * swayWeight - lockedDir * along;
        }
        else
        {
            // The character is pulled along the opposite of where he will fly.
            offset = SwayOffset() * swayWeight - aimDir * (pull * maxPullDistance);

            if (strainShake > 0f && pull > 0.5f)
            {
                float s = strainShake * Mathf.InverseLerp(0.5f, 1f, pull);
                float t = Time.time * 40f;
                float nx = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f;
                float ny = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f;
                offset += (shakeRight * nx + Vector3.up * ny) * s;
            }
        }

        transform.position = RestPosition + offset;

        if (hammockSeat != null && followSeatRotation)
            transform.rotation = hammockSeat.rotation * seatRotationOffset;
    }

    private void UpdateAimDirection()
    {
        // Keep the last direction when the hammock is barely stretched, so it doesn't jitter.
        if (pull > 0.01f)
        {
            float angle = Mathf.Atan2(pullVec.x, pullVec.y) * Mathf.Rad2Deg;   // 0 = straight back
            angle = Mathf.Clamp(angle, -maxAimAngle, maxAimAngle);
            aimYaw = -angle;                                                  // pulled right -> aim left
        }
        aimDir = Quaternion.AngleAxis(aimYaw, Vector3.up) * baseDir;
    }

    private Vector3 SwayOffset()
    {
        // With a seat assigned the hammock sways by itself, so we add nothing on top.
        if (hammockSeat != null || swayAmplitude <= 0f) return Vector3.zero;

        // Two out-of-sync sines so the swing never looks mechanical.
        float t = swayTime * 2f * Mathf.PI * swayFrequency;
        float x = Mathf.Sin(t) * 0.75f + Mathf.Sin(t * 1.9f + 1.3f) * 0.25f;
        return flatDir * (x * swayAmplitude);
    }

    private void Release()
    {
        HideTrajectory();

        if (pull < minPullToLaunch)
        {
            // Too weak: no launch. The hammock settles and goes back to swaying.
            state = State.Returning;
            return;
        }

        lockedDir = aimDir;
        launchSpeed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, powerCurve.Evaluate(pull));
        releaseDistance = pull * maxPullDistance;

        // Quarter of a spring oscillation: x(t) = x0 * cos(omega * t).
        // Speed at the rest position is x0 * omega, so choosing omega = speed / x0
        // makes the on-screen motion match the speed he is launched with.
        omega = launchSpeed / releaseDistance;
        launchTime = 0f;
        state = State.Launching;

        GameplayEvents.OnSlingShotLaunched?.Invoke();
    }

    private void FinishLaunch()
    {
        pull = 0f;

        // Leave from his seat as it is right now.
        transform.position = RestPosition + SwayOffset() * swayWeight;

        // Set the velocity directly: the motor reads it in its first FixedUpdate.
        rb.isKinematic = false;
        SetBodyVelocity(lockedDir * launchSpeed);
        rb.angularVelocity = Vector3.zero;

        if (runnerHandoffDelay > 0f)
        {
            runnerPending = true;
            handoffTimer = runnerHandoffDelay;
        }
        else
        {
            SetRunnerEnabled(true);
        }

        state = State.Launched;

        Launched?.Invoke(lockedDir, launchSpeed);
        onLaunched?.Invoke(launchSpeed);
    }

    private void UpdateHandoff()
    {
        if (!runnerPending) return;

        handoffTimer -= Time.deltaTime;
        if (handoffTimer <= 0f)
        {
            runnerPending = false;
            SetRunnerEnabled(true);
        }
    }

    /// <summary>
    /// Puts the character back in the hammock and makes everything ready for another launch.
    /// Call this from your retry button / game-over / respawn code.
    /// </summary>
    public void ResetSlingshot()
    {
        Vector3 resetPosition = RestPosition;
        Quaternion resetRotation = (hammockSeat != null && followSeatRotation)
            ? hammockSeat.rotation * seatRotationOffset
            : startRotation;

        if (motor != null)
        {
            // The motor clears its own state (bounce candidate, speed floor, momentum speed, ...)
            // and zeroes the velocity. Velocity can only be set while the body is not kinematic.
            rb.isKinematic = false;
            motor.ResetMotion(resetPosition, resetRotation);
        }
        else if (!rb.isKinematic)
        {
            SetBodyVelocity(Vector3.zero);
            rb.angularVelocity = Vector3.zero;
        }
        rb.isKinematic = true;

        // Runner off again until the next launch.
        runnerPending = false;
        SetRunnerEnabled(false);
        HideTrajectory();

        // Back to the waiting state.
        state = State.Swaying;
        pull = 0f;
        pullVec = Vector2.zero;
        pullVelocity = Vector2.zero;
        aimYaw = 0f;
        aimDir = baseDir;
        activeSource = InputSource.None;
        keyCharge = 0f;
        keyAim = 0f;
        stickPeak = Vector2.zero;
        stickArmed = false;   // a stick still held after retry must return to neutral first
        swayTime = 0f;
        swayWeight = 1f;
        launchTime = 0f;

        // Back into the hammock.
        transform.SetPositionAndRotation(resetPosition, resetRotation);

        WasReset?.Invoke();   // lets HammockDeformer restore the hammock mesh
        onReset?.Invoke();
    }

    /// <summary>Debug handoff into normal gameplay without pulling the hammock.</summary>
    public void LaunchImmediately(Vector3 velocity)
    {
        runnerPending = false;
        pull = 0f;
        state = State.Launched;
        HideTrajectory();
        rb.isKinematic = false;
        SetBodyVelocity(velocity);
        rb.angularVelocity = Vector3.zero;
        SetRunnerEnabled(true);
        GameplayEvents.OnSlingShotLaunched?.Invoke();
        Launched?.Invoke(velocity.normalized, velocity.magnitude);
        onLaunched?.Invoke(velocity.magnitude);
    }

    // ---------- Trajectory preview ----------

    private void UpdateTrajectory()
    {
        if (trajectoryLine == null) return;

        if (pull < minPullToLaunch)
        {
            HideTrajectory();
            return;
        }

        float speed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, powerCurve.Evaluate(pull));
        Vector3 launchVelocity = aimDir * speed;
        Vector3 start = RestPosition + trajectoryOriginOffset;

        // 1) Predict the flight: with the motor's own math if we have it.
        int count = (motor != null && motor.TuningProfile != null)
            ? SimulateWithMotor(start, launchVelocity)
            : SimulateBasic(start, launchVelocity);

        // 2) Walk along it until the body would first touch something: that is the landing spot.
        bool hit = false;
        RaycastHit hitInfo = default;
        int pathCount = count;

        int from = 0;
        while (from < count - 1)
        {
            int to = Mathf.Min(from + CastStride, count - 1);
            Vector3 segment = simPositions[to] - simPositions[from];
            float dist = segment.magnitude;

            if (dist > 0.0001f && Cast(simPositions[from], segment / dist, dist, out hitInfo))
            {
                simPositions[from + 1] = hitInfo.point;   // end the path on the surface
                pathCount = from + 2;
                hit = true;
                break;
            }
            from = to;
        }

        // 3) Resample the path into the fixed number of line points.
        if (trajectoryBuffer == null || trajectoryBuffer.Length != trajectoryPoints)
            trajectoryBuffer = new Vector3[trajectoryPoints];

        int last = pathCount - 1;
        for (int k = 0; k < trajectoryPoints; k++)
        {
            float t = trajectoryPoints > 1 ? (float)k / (trajectoryPoints - 1) * last : 0f;
            int i0 = Mathf.Min((int)t, last);
            int i1 = Mathf.Min(i0 + 1, last);
            trajectoryBuffer[k] = Vector3.Lerp(simPositions[i0], simPositions[i1], t - i0);
        }

        trajectoryLine.enabled = true;
        if (trajectoryLine.positionCount != trajectoryPoints) trajectoryLine.positionCount = trajectoryPoints;
        trajectoryLine.SetPositions(trajectoryBuffer);

        if (landingMarker != null)
        {
            landingMarker.gameObject.SetActive(hit);
            if (hit)
            {
                landingMarker.position = hitInfo.point + hitInfo.normal * 0.02f;
                landingMarker.rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
            }
        }
    }

    /// <summary>
    /// Uses RunnerMovementMotor's own prediction (shaped gravity, air drag, soft speed limit, speed floor,
    /// horizontal multiplier), so the preview follows the same rules as the real flight.
    /// Assumes no steering input in the air.
    /// </summary>
    private int SimulateWithMotor(Vector3 start, Vector3 launchVelocity)
    {
        EnsureSimBuffer(Mathf.CeilToInt(trajectoryDuration / Time.fixedDeltaTime) + 2);

        float multiplier = motor.UnmodifiedMaxSpeed > 0.0001f
            ? motor.CurrentMaxSpeed / motor.UnmodifiedMaxSpeed
            : 1f;

        int steps = motor.PredictLandingTrajectory(
            launchVelocity, MovementInputFrame.Empty, multiplier, trajectoryDuration, simPositions);

        // The motor predicts from wherever the body is right now (pulled back in the hammock).
        // The flight shape doesn't depend on position, so just slide it to the launch point.
        Vector3 shift = start - simPositions[0];
        for (int i = 0; i <= steps; i++) simPositions[i] += shift;

        return steps + 1;
    }

    /// <summary>Fallback when there is no motor: plain Unity physics (gravity + Rigidbody drag).</summary>
    private int SimulateBasic(Vector3 start, Vector3 launchVelocity)
    {
        float dt = Time.fixedDeltaTime;
        int steps = Mathf.CeilToInt(trajectoryDuration / dt);
        EnsureSimBuffer(steps + 2);

        Vector3 gravity = rb.useGravity ? Physics.gravity * gravityScale : Vector3.zero;
        float damp = Mathf.Clamp01(1f - GetDrag() * dt);

        Vector3 p = start;
        Vector3 v = launchVelocity;
        simPositions[0] = p;
        for (int i = 1; i <= steps; i++)
        {
            v += gravity * dt;
            v *= damp;
            p += v * dt;
            simPositions[i] = p;
        }
        return steps + 1;
    }

    private void EnsureSimBuffer(int size)
    {
        if (simPositions == null || simPositions.Length < size)
            simPositions = new Vector3[size];
    }

    private bool Cast(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
    {
        if (trajectoryProbeRadius > 0.001f)
            return Physics.SphereCast(origin, trajectoryProbeRadius, direction, out hit, distance,
                                      trajectoryCollisionMask, QueryTriggerInteraction.Ignore);

        return Physics.Raycast(origin, direction, out hit, distance,
                               trajectoryCollisionMask, QueryTriggerInteraction.Ignore);
    }

    private float GetDrag()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearDamping;
#else
        return rb.drag;
#endif
    }

    private void SetBodyVelocity(Vector3 velocity)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = velocity;
#else
        rb.velocity = velocity;
#endif
    }

    private void HideTrajectory()
    {
        if (trajectoryLine != null) trajectoryLine.enabled = false;
        if (landingMarker != null) landingMarker.gameObject.SetActive(false);
    }

    // ---------- Misc ----------

    private void SetRunnerEnabled(bool value)
    {
        foreach (var b in disableUntilLaunch)
            if (b != null) b.enabled = value;
    }

    // Works with both the old Input Manager and the new Input System (mouse + touch).
    // ---------- Input ----------

    /// <summary>Raw pull request to the eased pull vector (the same math the drag used before).</summary>
    private Vector2 PullTarget(Vector2 raw)
    {
        raw.y = Mathf.Max(0f, raw.y);
        raw = Vector2.ClampMagnitude(raw, 1f);

        float mag = raw.magnitude;
        float eased = Mathf.Lerp(mag, 1f - (1f - mag) * (1f - mag), resistance);
        return mag > 0.0001f ? raw * (eased / mag) : Vector2.zero;
    }

    private bool CanRead(InputSource source) =>
        activeSource == InputSource.None || activeSource == source;

    private AimInput ReadAimInput()
    {
        AimInput input = default;

        if (enablePointer && CanRead(InputSource.Pointer))
        {
            ReadPointerInput(ref input);
            if (input.begin || input.release) return input;
        }
    #if ENABLE_INPUT_SYSTEM
        if (enableKeyboard && CanRead(InputSource.Keyboard))
        {
            ReadKeyboardInput(ref input);
            if (input.begin || input.release) return input;
        }
        if (enableGamepad && CanRead(InputSource.Gamepad))
        {
            ReadGamepadInput(ref input);
        }
    #endif
        return input;
    }

    // Mouse + touch (and the legacy Input Manager if the new Input System isn't enabled).
    private void ReadPointerInput(ref AimInput input)
    {
        Vector2 pos;
        bool pressed, released;
    #if ENABLE_INPUT_SYSTEM
        var p = Pointer.current;
        if (p == null) return;
        pos      = p.position.ReadValue();
        pressed  = p.press.wasPressedThisFrame;
        released = p.press.wasReleasedThisFrame;
    #else
        pos      = Input.mousePosition;
        pressed  = Input.GetMouseButtonDown(0);
        released = Input.GetMouseButtonUp(0);
    #endif

        if (activeSource == InputSource.None)
        {
            if (!pressed) return;
            activeSource = InputSource.Pointer;
            dragStart = pos;
            input.begin = true;
        }

        // x: drag right/left. y: dragging DOWN pulls the hammock back.
        Vector2 raw = new Vector2(pos.x - dragStart.x, dragStart.y - pos.y) / maxDragPixels;
        if (invertHorizontal) raw.x = -raw.x;
        input.raw = raw;

        if (released)
        {
            input.release = true;
            activeSource = InputSource.None;
        }
    }

    #if ENABLE_INPUT_SYSTEM
    // Hold Space / S / Down to charge, A/D to aim, W/Up to ease off, let go to launch.
    private void ReadKeyboardInput(ref AimInput input)
    {
        var k = Keyboard.current;
        if (k == null) return;

        float dt = Time.deltaTime;

        // Aim sweeps with A/D (works before and during the charge). D always aims right.
        float aimIn = 0f;
        if (k.dKey.isPressed || k.rightArrowKey.isPressed) aimIn += 1f;
        if (k.aKey.isPressed || k.leftArrowKey.isPressed)  aimIn -= 1f;
        keyAim = Mathf.Clamp(keyAim + aimIn * keyboardAimSpeed * dt, -1f, 1f);

        bool chargeHeld = k.spaceKey.isPressed || k.sKey.isPressed || k.downArrowKey.isPressed;
        bool chargePressed = k.spaceKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame
                            || k.downArrowKey.wasPressedThisFrame;

        if (activeSource == InputSource.None)
        {
            if (!chargePressed) return;
            activeSource = InputSource.Keyboard;
            keyCharge = 0f;
            input.begin = true;
        }

        float rate = dt / Mathf.Max(0.05f, keyboardChargeTime);
        if (k.wKey.isPressed || k.upArrowKey.isPressed) keyCharge -= rate;
        else if (chargeHeld)                            keyCharge += rate;
        keyCharge = Mathf.Clamp01(keyCharge);

        // Build the same "drag" vector a finger would: pulling right = aiming left, so negate.
        float a = -keyAim * maxAimAngle * Mathf.Deg2Rad;
        input.raw = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * keyCharge;

        if (!chargeHeld)
        {
            input.release = true;
            activeSource = InputSource.None;
        }
    }

    // Pull the left stick (or D-pad) like a finger drag; let go or press Cross (X) to launch.
    private void ReadGamepadInput(ref AimInput input)
    {
        var pad = Gamepad.current;
        if (pad == null) return;

        Vector2 stick = pad.leftStick.ReadValue();
        Vector2 dpad = pad.dpad.ReadValue();
        if (dpad.sqrMagnitude > stick.sqrMagnitude) stick = dpad;

        float strength = stick.magnitude;
        float now = Time.unscaledTime;

        // Stick down = pull back, stick right = drag right (same as a finger).
        Vector2 raw = new Vector2(stick.x, -stick.y);
        if (invertHorizontal) raw.x = -raw.x;

        if (activeSource == InputSource.None)
        {
            if (strength < stickReleaseThreshold) stickArmed = true;
            if (!stickArmed || strength < stickPullThreshold) return;

            stickArmed = false;
            activeSource = InputSource.Gamepad;
            stickPeak = raw;
            stickPeakTime = now;
            input.begin = true;
        }
        else if (strength < stickReleaseThreshold)
        {
            // Stick let go: use the strongest recent value, not the half-sprung-back one.
            input.raw = (now - stickPeakTime <= stickSnapbackWindow) ? stickPeak : raw;
            input.instant = true;
            input.release = true;
            activeSource = InputSource.None;
            return;
        }

        if (raw.sqrMagnitude >= stickPeak.sqrMagnitude || now - stickPeakTime > stickSnapbackWindow)
        {
            stickPeak = raw;
            stickPeakTime = now;
        }

        input.raw = raw;

        if (pad.buttonSouth.wasPressedThisFrame)   // Cross on PS4
        {
            input.release = true;
            activeSource = InputSource.None;
        }
    }
    #endif
}
