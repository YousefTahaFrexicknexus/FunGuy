using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class CreatureBouncer_XAxis : MonoBehaviour
{
    [Header("Forward Arc")]
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

    [Header("Air Jumps")]
    [Tooltip("How many times the creature can jump while airborne.")]
    [Range(1, 5)] public int maxAirJumps = 5;

    [Tooltip("Visual puff or trail to play on each air jump.")]
    public ParticleSystem airJumpParticles;

    [Tooltip("Sound for mid-air jump. Pitch rises with each jump.")]
    public AudioClip airJumpSound;

    [Range(0f, 0.3f)] public float airJumpPitchStep = 0.08f;

    [Header("Input")]
    [Tooltip("Which button triggers a mid-air jump.")]
    public KeyCode jumpKey = KeyCode.Space;

    [Tooltip("How long after leaving a surface the player can still jump (coyote time).")]
    [Range(0f, 0.2f)] public float coyoteTime = 0.1f;

    [Header("Horizontal Joystick Movement")]
    [Tooltip("Dynamic joystick used to steer the creature on the world X axis.")]
    [SerializeField] FloatingJoystick movementJoystick;

    [Tooltip("Maximum sideways speed applied from the joystick.")]
    [Range(0f, 100f)] [SerializeField] float horizontalSpeed = 5f;

    [Tooltip("How quickly the creature reaches the target sideways speed.")]
    [Range(0f, 40f)] [SerializeField] float horizontalAcceleration = 20f;

    [Tooltip("How quickly sideways speed returns to zero when the joystick is released.")]
    [Range(0f, 40f)] [SerializeField] float horizontalDeceleration = 24f;

    [Tooltip("Ignore tiny joystick movement around the center.")]
    [Range(0f, 0.5f)] [SerializeField] float horizontalDeadZone = 0.1f;

    [Tooltip("Clamp the player between the X bounds below.")]
    [SerializeField] bool clampXPosition = true;

    [Tooltip("Left-most world X position allowed.")]
    [SerializeField] float minX = -2.2f;

    [Tooltip("Right-most world X position allowed.")]
    [SerializeField] float maxX = 2.2f;

    [Header("Gravity")]
    [Range(1f, 6f)] public float gravityScale = 3f;
    [Range(1f, 4f)] public float fallGravityMultiplier = 1.8f;
    [Range(-80f, -5f)] public float maxFallSpeed = -45f;

    [Header("Squash & Stretch")]
    [Range(0.1f, 0.9f)] public float squashY = 0.40f;
    [Range(1.1f, 2.5f)] public float stretchY = 1.7f;
    [Range(0.02f, 0.2f)] public float squashDuration = 0.04f;
    [Range(0.05f, 0.4f)] public float stretchDuration = 0.10f;
    [Range(0.02f, 0.3f)] public float recoverDuration = 0.08f;
    [Range(0f, 3f)] public float overshootStrength = 2.2f;

    [Header("Airborne Wobble")]
    [Range(0f, 0.3f)] public float wobbleAmount = 0.1f;
    [Range(1f, 30f)] public float wobbleSpeed = 16f;

    [Header("Rotation")]
    [Tooltip("Creature tilts forward on launch, levels off at apex.")]
    public bool tiltWithVelocity = true;
    [Range(0f, 45f)] public float maxTiltAngle = 25f;
    [Range(1f, 20f)] public float tiltSmoothSpeed = 8f;

    [Header("Creature Mesh")]
    [SerializeField] Transform meshTransform;

    Rigidbody rb;
    Collider cachedCollider;
    Vector3 originalScale;
    Coroutine squashRoutine;
    AudioSource audioSource;

    int airJumpsRemaining;
    float coyoteTimer;
    bool wasGroundedLastFrame;
    float wobbleTime;
    float currentHorizontalSpeed;

    Vector3 currentForward = Vector3.forward;

    void Reset()
    {
        meshTransform = transform;
    }

    void OnValidate()
    {
        if (maxX < minX)
        {
            maxX = minX;
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        cachedCollider = GetComponent<Collider>();

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        if (meshTransform == null)
        {
            meshTransform = transform;
        }

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
        ApplyHorizontalMovement();
        ApplyCustomGravity();
        ClampFallSpeed();
    }

    void ApplyHorizontalMovement()
    {
        float inputX = ReadHorizontalInput();
        float targetXSpeed = inputX * horizontalSpeed;

        Vector3 position = rb.position;
        position.x += targetXSpeed * Time.fixedDeltaTime;

        if (clampXPosition)
        {
            position.x = Mathf.Clamp(position.x, minX, maxX);

            bool pushingIntoLeftLimit = position.x <= minX && currentHorizontalSpeed < 0f;
            bool pushingIntoRightLimit = position.x >= maxX && currentHorizontalSpeed > 0f;
            if (pushingIntoLeftLimit || pushingIntoRightLimit)
            {
                currentHorizontalSpeed = 0f;
            }
        }

        rb.position = position;

        Vector3 velocity = rb.linearVelocity;
        velocity.x = 0f;
        rb.linearVelocity = velocity;
    }

    float ReadHorizontalInput()
    {
        if (movementJoystick == null)
        {
            return 0f;
        }

        float inputX = Mathf.Clamp(movementJoystick.Horizontal, -1f, 1f);
        return Mathf.Abs(inputX) < horizontalDeadZone ? 0f : inputX;
    }

    void ApplyCustomGravity()
    {
        float multiplier = rb.linearVelocity.y < 0f ? fallGravityMultiplier : 1f;
        rb.AddForce(Physics.gravity * gravityScale * multiplier, ForceMode.Acceleration);
    }

    void ClampFallSpeed()
    {
        if (rb.linearVelocity.y < maxFallSpeed)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.y = maxFallSpeed;
            rb.linearVelocity = velocity;
        }
    }

    void HandleCoyoteTime()
    {
        bool isGrounded = IsGrounded();

        if (wasGroundedLastFrame && !isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        else if (!isGrounded)
        {
            coyoteTimer -= Time.deltaTime;
        }

        wasGroundedLastFrame = isGrounded;
    }

    bool IsGrounded()
    {
        if (cachedCollider == null)
        {
            return false;
        }

        return Physics.Raycast(
            transform.position,
            Vector3.down,
            cachedCollider.bounds.extents.y + 0.15f
        );
    }

    void HandleAirJumpInput()
    {
        if (!Input.GetKeyDown(jumpKey))
        {
            return;
        }

        if (airJumpsRemaining <= 0)
        {
            return;
        }

        if (IsGrounded() || coyoteTimer > 0f)
        {
            return;
        }

        PerformAirJump();
    }

    void PerformAirJump()
    {
        int jumpIndex = maxAirJumps - airJumpsRemaining;
        float decayedForce = airJumpForce * Mathf.Pow(airJumpDecay, jumpIndex) * globalSpeedMultiplier;
        float forwardZ = GetForwardZ();

        Vector3 velocity = rb.linearVelocity;
        velocity.x = 0f;
        velocity.y = 0f;
        velocity.z += forwardZ * airJumpForwardSpeed * globalSpeedMultiplier;
        rb.linearVelocity = velocity;
        rb.AddForce(Vector3.up * decayedForce, ForceMode.Impulse);

        airJumpsRemaining--;

        if (squashRoutine != null)
        {
            StopCoroutine(squashRoutine);
        }

        squashRoutine = StartCoroutine(SquashStretchSequence(0.75f));

        PlayAirJumpFX(jumpIndex);

        GameplayEvents.OnAirJump?.Invoke(airJumpsRemaining);
    }

    void PlayAirJumpFX(int jumpIndex)
    {
        if (airJumpParticles != null)
        {
            airJumpParticles.Play();
        }

        if (airJumpSound != null && audioSource != null)
        {
            audioSource.pitch = 1f + airJumpPitchStep * jumpIndex;
            audioSource.PlayOneShot(airJumpSound, 0.8f);
        }
    }

    public void TriggerBounce(float mushroomMultiplier = 1f, Vector3? launchDirection = null)
    {
        ResetAirJumps();

        currentForward = ResolveForward(launchDirection);

        float force = bounceForce * globalSpeedMultiplier * mushroomMultiplier;
        float forwardZ = GetForwardZ();

        Vector3 velocity = rb.linearVelocity;
        velocity.x = 0f;
        velocity.z = forwardZ * forwardSpeed * globalSpeedMultiplier * mushroomMultiplier;
        velocity.y = 0f;
        rb.linearVelocity = velocity;
        rb.AddForce(Vector3.up * force, ForceMode.Impulse);

        if (squashRoutine != null)
        {
            StopCoroutine(squashRoutine);
        }

        squashRoutine = StartCoroutine(SquashStretchSequence(1f));
    }

    Vector3 ResolveForward(Vector3? launchDirection)
    {
        Vector3 resolved = launchDirection.HasValue
            ? Vector3.ProjectOnPlane(launchDirection.Value, Vector3.up)
            : transform.forward;

        if (resolved.sqrMagnitude < 0.0001f)
        {
            resolved = Vector3.forward;
        }

        return resolved.normalized;
    }

    float GetForwardZ()
    {
        if (Mathf.Abs(currentForward.z) > 0.001f)
        {
            return Mathf.Sign(currentForward.z);
        }

        return 1f;
    }

    public void ResetAirJumps()
    {
        airJumpsRemaining = maxAirJumps;
    }

    IEnumerator SquashStretchSequence(float intensity)
    {
        float xzSquash = Mathf.Lerp(1f, 1.5f, intensity);
        float xzStretch = Mathf.Lerp(1f, 0.65f, intensity);
        float ySquash = Mathf.Lerp(1f, squashY, intensity);
        float yStretch = Mathf.Lerp(1f, stretchY, intensity);

        yield return ScaleTo(
            new Vector3(originalScale.x * xzSquash, originalScale.y * ySquash, originalScale.z * xzSquash),
            squashDuration * (2f - intensity));

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

    void ApplyVelocityTilt()
    {
        if (!tiltWithVelocity)
        {
            return;
        }

        float verticalVelocity = rb.linearVelocity.y;
        float tiltAngle = Mathf.Clamp(
            Mathf.InverseLerp(bounceForce, -Mathf.Abs(maxFallSpeed), verticalVelocity) * maxTiltAngle * 2f - maxTiltAngle,
            -maxTiltAngle,
            maxTiltAngle
        );

        Quaternion targetRotation = Quaternion.AngleAxis(-tiltAngle, meshTransform.right)
            * Quaternion.LookRotation(currentForward, Vector3.up);

        meshTransform.rotation = Quaternion.Slerp(
            meshTransform.rotation,
            targetRotation,
            Time.deltaTime * tiltSmoothSpeed
        );
    }

    void ApplyAirborneWobble()
    {
        if (Mathf.Abs(rb.linearVelocity.y) < 0.5f)
        {
            return;
        }

        wobbleTime += Time.deltaTime * wobbleSpeed;
        float wobble = Mathf.Sin(wobbleTime) * wobbleAmount;

        meshTransform.localScale = new Vector3(
            originalScale.x * (1f + wobble),
            meshTransform.localScale.y,
            originalScale.z * (1f - wobble)
        );
    }

    public void ResetVeclocity()
    {
        currentHorizontalSpeed = 0f;
        rb.linearVelocity = Vector3.zero;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.7f);
        Gizmos.DrawRay(transform.position, currentForward * 2f);

        if (clampXPosition)
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.7f);
            Gizmos.DrawLine(new Vector3(minX, transform.position.y - 1f, transform.position.z),
                new Vector3(minX, transform.position.y + 1f, transform.position.z));
            Gizmos.DrawLine(new Vector3(maxX, transform.position.y - 1f, transform.position.z),
                new Vector3(maxX, transform.position.y + 1f, transform.position.z));
        }

#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up,
            $"Air jumps left: {airJumpsRemaining}/{maxAirJumps}\nX range: {minX:F1} to {maxX:F1}"
        );
#endif
    }
}
