using System;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Put this on the character (needs a Rigidbody), standing/lying in the hammock at the start of the level.
/// Drag down on screen to stretch the hammock back, release to be flung forward.
/// Pair it with HammockDeformer, which bends the hammock mesh to follow the character.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class HammockSlingshot : MonoBehaviour
{
    [Header("Aim")]
    [Tooltip("Horizontal direction (world space) the character is launched in.")]
    [SerializeField] private Vector3 launchDirection = Vector3.forward;
    [Tooltip("Upward angle of the launch in degrees. A little loft looks great.")]
    [SerializeField, Range(0f, 45f)] private float launchPitch = 12f;

    [Header("Pull")]
    [Tooltip("How far back (world units) the character moves at full pull.")]
    [SerializeField] private float maxPullDistance = 2.5f;
    [Tooltip("How many screen pixels of dragging equal a full pull.")]
    [SerializeField] private float maxDragPixels = 300f;
    [Tooltip("0 = linear. Higher = the hammock feels harder to stretch the further you pull.")]
    [SerializeField, Range(0f, 1f)] private float resistance = 0.5f;
    [Tooltip("How quickly the hammock follows your finger. Lower = heavier, floatier feel.")]
    [SerializeField] private float followSharpness = 25f;
    [Tooltip("Pull below this (0-1) cancels the launch and the hammock eases back.")]
    [SerializeField, Range(0f, 0.5f)] private float minPullToLaunch = 0.1f;
    [Tooltip("Speed (units/sec) the hammock eases back after a cancelled pull.")]
    [SerializeField] private float returnSpeed = 8f;
    [Tooltip("Tiny tremble when the hammock is near full stretch (world units). 0 = off.")]
    [SerializeField] private float strainShake = 0.015f;

    [Header("Launch")]
    [SerializeField] private float minLaunchSpeed = 15f;
    [SerializeField] private float maxLaunchSpeed = 40f;
    [SerializeField] private AnimationCurve powerCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("Runner hookup")]
    [Tooltip("Your normal run/steer scripts. Disabled until the launch happens.")]
    [SerializeField] private Behaviour[] disableUntilLaunch;
    [Tooltip("Invoked at the moment the character leaves the hammock, with the launch speed. " +
             "Use for the run animation trigger, sound, camera kick, etc.")]
    public UnityEvent<float> onLaunched;

    /// <summary>Raised when the character leaves the hammock: (direction, speed).</summary>
    public event Action<Vector3, float> Launched;

    /// <summary>0-1 how far the hammock is stretched right now.</summary>
    public float Pull01 => pull;
    public float MaxPullDistance => maxPullDistance;
    /// <summary>How far the character is from his resting spot in the hammock.</summary>
    public Vector3 Displacement => transform.position - startPosition;
    /// <summary>True while the character is still in the hammock (before he is launched).</summary>
    public bool IsHolding => state != State.Launched;

    private enum State { Idle, Aiming, Returning, Launching, Launched }

    private Rigidbody rb;
    private Vector3 startPosition;
    private Vector3 launchDir;
    private Vector3 shakeRight;
    private State state = State.Idle;

    private float dragStartY;
    private float pull;

    // Release phase (the hammock pulls the character back to rest like a spring)
    private float launchSpeed;
    private float releaseDistance;
    private float omega;
    private float launchTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        startPosition = transform.position;

        Vector3 flat = launchDirection; flat.y = 0f;
        flat = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.forward;
        shakeRight = Vector3.Cross(Vector3.up, flat);
        launchDir = Quaternion.AngleAxis(-launchPitch, shakeRight) * flat;

        rb.isKinematic = true;     // hold him in the hammock until the launch
        SetRunnerEnabled(false);
    }

    private void Update()
    {
        if (state == State.Launched) return;
        if (state == State.Launching) { UpdateLaunching(); return; }

        ReadPointer(out Vector2 pos, out bool down, out bool up);

        if (down)
        {
            dragStartY = pos.y;
            state = State.Aiming;
        }

        if (state == State.Aiming)
        {
            // Dragging downward = pulling back. Ease-out curve gives the "getting tighter" feel.
            float raw = Mathf.Clamp01((dragStartY - pos.y) / maxDragPixels);
            float target = Mathf.Lerp(raw, 1f - (1f - raw) * (1f - raw), resistance);
            pull = Mathf.Lerp(pull, target, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
            ApplyPullPosition();
            if (up) Release();
        }
        else if (state == State.Returning)
        {
            pull = Mathf.MoveTowards(pull, 0f, returnSpeed / maxPullDistance * Time.deltaTime);
            ApplyPullPosition();
            if (pull <= 0f) state = State.Idle;
        }
    }

    private void ApplyPullPosition()
    {
        Vector3 offset = -launchDir * (pull * maxPullDistance);

        if (strainShake > 0f && pull > 0.5f)
        {
            float s = strainShake * Mathf.InverseLerp(0.5f, 1f, pull);
            float t = Time.time * 40f;
            float nx = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f;
            float ny = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f;
            offset += (shakeRight * nx + Vector3.up * ny) * s;
        }

        transform.position = startPosition + offset;
    }

    private void Release()
    {
        if (pull < minPullToLaunch)
        {
            state = State.Returning;
            return;
        }

        launchSpeed = Mathf.Lerp(minLaunchSpeed, maxLaunchSpeed, powerCurve.Evaluate(pull));
        releaseDistance = pull * maxPullDistance;

        // Quarter of a spring oscillation: x(t) = x0 * cos(omega * t).
        // Speed at the rest position is x0 * omega, so choosing omega = speed / x0
        // makes the on-screen motion match the speed he is launched with.
        omega = launchSpeed / releaseDistance;
        launchTime = 0f;
        state = State.Launching;
    }

    private void UpdateLaunching()
    {
        launchTime += Time.deltaTime;
        float duration = Mathf.PI * 0.5f / omega;

        if (launchTime >= duration)
        {
            FinishLaunch();
            return;
        }

        float along = releaseDistance * Mathf.Cos(omega * launchTime);
        transform.position = startPosition - launchDir * along;
    }

    private void FinishLaunch()
    {
        transform.position = startPosition;

        rb.isKinematic = false;
        rb.AddForce(launchDir * launchSpeed, ForceMode.VelocityChange);   // instant speed, ignores mass

        SetRunnerEnabled(true);
        state = State.Launched;

        Launched?.Invoke(launchDir, launchSpeed);
        onLaunched?.Invoke(launchSpeed);
    }

    private void SetRunnerEnabled(bool value)
    {
        foreach (var b in disableUntilLaunch)
            if (b != null) b.enabled = value;
    }

    // Works with both the old Input Manager and the new Input System (mouse + touch).
    private static void ReadPointer(out Vector2 pos, out bool down, out bool up)
    {
#if ENABLE_INPUT_SYSTEM
        var p = Pointer.current;
        if (p == null) { pos = default; down = up = false; return; }
        pos  = p.position.ReadValue();
        down = p.press.wasPressedThisFrame;
        up   = p.press.wasReleasedThisFrame;
#else
        pos  = Input.mousePosition;
        down = Input.GetMouseButtonDown(0);
        up   = Input.GetMouseButtonUp(0);
#endif
    }
}
