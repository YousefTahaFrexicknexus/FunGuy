using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class CreatureBouncer : MonoBehaviour
{
    [Header("— Forward Arc —")]
    [Tooltip("How fast the creature moves forward on a mushroom bounce.")]
    [Range(1f, 30f)] public float forwardSpeed = 12f;

    [Tooltip("Forward speed on a mid-air jump (usually less than mushroom bounce).")]
    [Range(0f, 20f)] public float airJumpForwardSpeed = 6f;

    [Tooltip("Upward force on mushroom bounce.")]
    [Range(5f, 50f)] public float bounceForce = 26f;

    [Tooltip("Upward force per mid-air jump. Can scale down per jump for diminishing returns.")]
    [Range(3f, 40f)] public float airJumpForce = 16f;

    [Tooltip("Multiply airJumpForce by this each successive jump. 1 = equal, 0.8 = diminishing.")]
    [Range(0.5f, 1f)] public float airJumpDecay = 0.85f;

    [Tooltip("Master speed multiplier. The quick feel dial.")]
    [Range(0.5f, 3f)] public float globalSpeedMultiplier = 1.6f;

    [Header("— Air Jumps —")]
    [Tooltip("How many times the creature can jump while airborne.")]
    [Range(1, 5)] public int maxAirJumps = 5;

    [Tooltip("Visual puff or trail to play on each air jump.")]
    public ParticleSystem airJumpParticles;

    [Tooltip("Sound for mid-air jump. Pitch rises with each jump.")]
    public AudioClip airJumpSound;

    [Range(0f, 0.3f)] public float airJumpPitchStep = 0.08f;

    [Header("— Input —")]
    [Tooltip("Which button triggers a mid-air jump.")]
    public KeyCode jumpKey = KeyCode.Space;

    [Tooltip("How long after leaving a surface the player can still jump (coyote time).")]
    [Range(0f, 0.2f)] public float coyoteTime = 0.1f;

    [Header("— Gravity —")]
    [Range(1f, 6f)] public float gravityScale = 3f;
    [Range(1f, 4f)] public float fallGravityMultiplier = 1.8f;
    [Range(-80f, -5f)] public float maxFallSpeed = -45f;

    [Header("— Squash & Stretch —")]
    [Range(0.1f, 0.9f)] public float squashY = 0.40f;
    [Range(1.1f, 2.5f)] public float stretchY = 1.7f;
    [Range(0.02f, 0.2f)] public float squashDuration = 0.04f;
    [Range(0.05f, 0.4f)] public float stretchDuration = 0.10f;
    [Range(0.02f, 0.3f)] public float recoverDuration = 0.08f;
    [Range(0f, 3f)] public float overshootStrength = 2.2f;

    [Header("— Airborne Wobble —")]
    [Range(0f, 0.3f)] public float wobbleAmount = 0.1f;
    [Range(1f, 30f)] public float wobbleSpeed = 16f;

    [Header("— Rotation —")]
    [Tooltip("Creature tilts forward on launch, levels off at apex.")]
    public bool tiltWithVelocity = true;
    [Range(0f, 45f)] public float maxTiltAngle = 25f;
    [Range(1f, 20f)] public float tiltSmoothSpeed = 8f;

    [Header("Creature mesh")]
    [SerializeField] Transform meshTransform;

    // ── Runtime ────────────────────────────────────────────────
    private Rigidbody rb;
    private Vector3 originalScale;
    private Coroutine squashRoutine;
    private AudioSource audioSource;

    private int airJumpsRemaining;
    private float coyoteTimer;
    private bool wasGroundedLastFrame;
    private float wobbleTime;

    // Direction the creature is currently travelling forward
    private Vector3 currentForward = Vector3.forward;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        originalScale = meshTransform.localScale;

        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;

        airJumpsRemaining = maxAirJumps;
    }

    void Update()
    {
        HandleCoyoteTime();
        HandleAirJumpInput();
        ApplyAirborneWobble();
        ApplyVelocityTilt();
    }

    void FixedUpdate()
    {
        ApplyCustomGravity();
        ClampFallSpeed();
    }

    // ── Gravity ────────────────────────────────────────────────

    void ApplyCustomGravity()
    {
        float mult = (rb.linearVelocity.y < 0f) ? fallGravityMultiplier : 1f;
        rb.AddForce(Physics.gravity * gravityScale * mult, ForceMode.Acceleration);
    }

    void ClampFallSpeed()
    {
        if (rb.linearVelocity.y < maxFallSpeed)
        {
            Vector3 v = rb.linearVelocity;
            v.y = maxFallSpeed;
            rb.linearVelocity = v;
        }
    }

    // ── Coyote time ────────────────────────────────────────────

    void HandleCoyoteTime()
    {
        // Coyote window ticks down after leaving ground naturally
        if (wasGroundedLastFrame && !IsGrounded())
            coyoteTimer = coyoteTime;
        else if (!IsGrounded())
            coyoteTimer -= Time.deltaTime;

        wasGroundedLastFrame = IsGrounded();
    }

    bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down,
            GetComponent<Collider>().bounds.extents.y + 0.15f);
    }

    // ── Air jump input ─────────────────────────────────────────

    void HandleAirJumpInput()
    {
        if (!Input.GetKeyDown(jumpKey)) return;
        if (airJumpsRemaining <= 0) return;

        // Don't consume an air jump if still on ground / coyote window
        if (IsGrounded() || coyoteTimer > 0f) return;

        PerformAirJump();
    }

    void PerformAirJump()
    {
        int jumpIndex = maxAirJumps - airJumpsRemaining; // 0 = first air jump
        float decayedForce = airJumpForce * Mathf.Pow(airJumpDecay, jumpIndex) * globalSpeedMultiplier;

        // Redirect velocity: keep horizontal, override vertical
        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        v += currentForward * airJumpForwardSpeed * globalSpeedMultiplier;
        rb.linearVelocity = v;
        rb.AddForce(Vector3.up * decayedForce, ForceMode.Impulse);

        airJumpsRemaining--;

        // Slightly smaller squash for air jumps — feels distinct from mushroom bounce
        if (squashRoutine != null) StopCoroutine(squashRoutine);
        squashRoutine = StartCoroutine(SquashStretchSequence(0.75f));

        PlayAirJumpFX(jumpIndex);
    }

    void PlayAirJumpFX(int jumpIndex)
    {
        if (airJumpParticles != null) airJumpParticles.Play();

        if (airJumpSound != null && audioSource != null)
        {
            audioSource.pitch = 1f + airJumpPitchStep * jumpIndex;
            audioSource.PlayOneShot(airJumpSound, 0.8f);
        }
    }

    // ── Mushroom bounce (called by Mushroom.cs) ────────────────

    public void TriggerBounce(float mushroomMultiplier = 1f, Vector3? launchDirection = null)
    {
        ResetAirJumps();

        currentForward = launchDirection.HasValue
            ? Vector3.ProjectOnPlane(launchDirection.Value, Vector3.up).normalized
            : transform.forward;

        float force = bounceForce * globalSpeedMultiplier * mushroomMultiplier;

        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        v = currentForward * forwardSpeed * globalSpeedMultiplier * mushroomMultiplier;
        rb.linearVelocity = v;
        rb.AddForce(Vector3.up * force, ForceMode.Impulse);

        if (squashRoutine != null) StopCoroutine(squashRoutine);
        squashRoutine = StartCoroutine(SquashStretchSequence(1f));
    }

    public void ResetAirJumps()
    {
        airJumpsRemaining = maxAirJumps;
    }

    // ── Squash & stretch ───────────────────────────────────────

    IEnumerator SquashStretchSequence(float intensity)
    {
        float xzSquash = Mathf.Lerp(1f, 1.5f, intensity);
        float xzStretch = Mathf.Lerp(1f, 0.65f, intensity);
        float ySquash = Mathf.Lerp(1f, squashY, intensity);
        float yStretch = Mathf.Lerp(1f, stretchY, intensity);

        yield return ScaleTo(
            new Vector3(originalScale.x * xzSquash, originalScale.y * ySquash, originalScale.z * xzSquash),
            squashDuration * (2f - intensity)); // air jumps squash a touch slower

        yield return ScaleTo(
            new Vector3(originalScale.x * xzStretch, originalScale.y * yStretch, originalScale.z * xzStretch),
            stretchDuration);

        yield return ScaleTo(originalScale, recoverDuration);
    }

    IEnumerator ScaleTo(Vector3 target, float duration)
    {
        Vector3 start = meshTransform.localScale;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(duration, 0.001f);
            meshTransform.localScale = Vector3.LerpUnclamped(start, target, EaseOutBack(Mathf.Clamp01(t)));
            yield return null;
        }
        meshTransform.localScale = target;
    }

    float EaseOutBack(float t)
    {
        float c3 = overshootStrength + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + overshootStrength * Mathf.Pow(t - 1f, 2f);
    }

    // ── Tilt with velocity ─────────────────────────────────────

    void ApplyVelocityTilt()
    {
        if (!tiltWithVelocity) return;

        float verticalVelocity = rb.linearVelocity.y;
        float tiltAngle = Mathf.Clamp(
            Mathf.InverseLerp(bounceForce, -Mathf.Abs(maxFallSpeed), verticalVelocity) * maxTiltAngle * 2f - maxTiltAngle,
            -maxTiltAngle, maxTiltAngle
        );

        Quaternion targetRotation = Quaternion.AngleAxis(-tiltAngle, meshTransform.right)
                                  * Quaternion.LookRotation(currentForward, Vector3.up);

        meshTransform.rotation = Quaternion.Slerp(meshTransform.rotation, targetRotation,
            Time.deltaTime * tiltSmoothSpeed);
    }

    // ── Wobble ─────────────────────────────────────────────────

    void ApplyAirborneWobble()
    {
        if (Mathf.Abs(rb.linearVelocity.y) < 0.5f) return;
        wobbleTime += Time.deltaTime * wobbleSpeed;
        float w = Mathf.Sin(wobbleTime) * wobbleAmount;
        meshTransform.localScale = new Vector3(
            originalScale.x * (1f + w),
            meshTransform.localScale.y,
            originalScale.z * (1f - w)
        );
    }

    public void ResetVeclocity()
    {
        rb.linearVelocity = Vector3.zero;
    }

    // ── Gizmos ─────────────────────────────────────────────────
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.7f);
        Gizmos.DrawRay(transform.position, currentForward * 2f);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up,
            $"Air jumps left: {airJumpsRemaining}/{maxAirJumps}\n" +
            $"Forward: {forwardSpeed * globalSpeedMultiplier:F1}  Up: {bounceForce * globalSpeedMultiplier:F1}");
#endif
    }
}