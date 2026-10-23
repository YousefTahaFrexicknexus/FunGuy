using UnityEngine;
using System.Collections;

public class CharacterAnimator : MonoBehaviour
{
    [Header("Main references")]
    [SerializeField] RunnerMovementMotor runnerMovementMotor;
    [SerializeField] Animator animator;

    void OnEnable()
    {
        SubscribeToGameplayEvents();
    }

    void OnDisable()
    {
        UnsubscribeToGameplayEvents();
    }

    void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        defaultVisualRotation = tumblePivot.localRotation;
    }

    void SubscribeToGameplayEvents()
    {
        GameplayEvents.OnMushroomJump += OnMushroomJump;
        GameplayEvents.OnAirJump += OnAirJump;

        runnerMovementMotor.Bounced += HandleBounce;
    }

    void UnsubscribeToGameplayEvents()
    {
        GameplayEvents.OnMushroomJump -= OnMushroomJump;
        GameplayEvents.OnAirJump -= OnAirJump;
    }

    void OnMushroomJump()
    {
        TriggerBounce();
        animator.SetTrigger("MushroomBounce");
    }

    void OnAirJump(int _)
    {
        DisableTumble();

        tumblePivot.localRotation = defaultVisualRotation;

        TriggerBounce();
        animator.SetTrigger("MushroomBounce");
        AirJumpVFX.Play();
        Invoke(nameof(EnableTumble), 0.1f);
    }

    // TODO: Migrate this to its own behavior
    [Range(5f, 50f)] public float bounceForce = 26f;
    [Range(-80f, -5f)] public float maxFallSpeed = -45f;
    Vector3 currentForward = Vector3.forward;

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
    [Header("VFX")]
    [SerializeField] ParticleSystem AirJumpVFX;
    // ── Runtime ────────────────────────────────────────────────
    [SerializeField] Rigidbody rb;
    Vector3 originalScale = Vector3.one;
    Coroutine squashRoutine;
    float wobbleTime;

    void Update()
    {
        ApplyAirborneWobble();
        ApplyVelocityTilt();
    }


    // ── Squash & stretch ───────────────────────────────────────
    public void TriggerBounce()
    {
        if (squashRoutine != null)
        {
            StopCoroutine(squashRoutine);
        }

        squashRoutine = StartCoroutine(SquashStretchSequence(1f));
    }

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
        if (!tiltWithVelocity)
        {
            return;
        }

        float verticalVelocity = rb.linearVelocity.y;
        float tiltAngle = Mathf.Clamp(Mathf.InverseLerp(bounceForce, -Mathf.Abs(maxFallSpeed), verticalVelocity) * maxTiltAngle * 2f - maxTiltAngle, -maxTiltAngle, maxTiltAngle);

        Quaternion targetRotation = Quaternion.AngleAxis(-tiltAngle, meshTransform.right) * Quaternion.LookRotation(currentForward, Vector3.up);

        meshTransform.rotation = Quaternion.Slerp(meshTransform.rotation, targetRotation, Time.deltaTime * tiltSmoothSpeed);
    }

    // ── Wobble ─────────────────────────────────────────────────

    void ApplyAirborneWobble()
    {
        if (Mathf.Abs(rb.linearVelocity.y) < 0.5f)
        {
            return;
        }

        wobbleTime += Time.deltaTime * wobbleSpeed;

        float wobbleValue = Mathf.Sin(wobbleTime) * wobbleAmount;
        meshTransform.localScale = new Vector3(originalScale.x * (1f + wobbleValue), meshTransform.localScale.y, originalScale.z * (1f - wobbleValue));
    }

    #region Tumble
    [SerializeField] Transform tumblePivot;
    [SerializeField] float spinImpulse = 720f;
    [SerializeField] float angularDrag = 0.6f;
    [SerializeField] float uprightStrength = 2f;
    [SerializeField] float uprightFadeSpeed = 360f;

    bool updateTumble = true;
    Vector3 angularVelocity;
    Quaternion defaultVisualRotation;


    void LateUpdate()
    {
        TumbleTick();
    }

    void TumbleTick()
    {
        if (!updateTumble)
        {
            return;
        }

        float remainingTime = Time.deltaTime;

        // Small simulation steps keep the spring stable during slower frames.
        while (remainingTime > 0f)
        {
            float step = Mathf.Min(remainingTime, 1f / 120f);
            SimulateTumble(step);
            remainingTime -= step;
        }
    }

    void EnableTumble()
    {
        updateTumble = true;
    }

    void DisableTumble()
    {
        updateTumble = false;
    }

    void SimulateTumble(float _deltaTime)
    {
        Quaternion rotationError = (defaultVisualRotation * Quaternion.Inverse(tumblePivot.localRotation)).normalized;
        rotationError.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f)
        {
            angle -= 360f;
        }

        // Let him spin freely at high speed; recover more strongly as he slows.
        float speedRatio = angularVelocity.magnitude / Mathf.Max(uprightFadeSpeed, 0.01f);
        float recoveryWeight = 1f / (1f + speedRatio * speedRatio);

        if (Mathf.Abs(angle) > 0.001f)
        {
            angularVelocity += axis * angle * uprightStrength * recoveryWeight * _deltaTime;
        }

        angularVelocity *= Mathf.Exp(-angularDrag * _deltaTime);

        float speed = angularVelocity.magnitude;

        if (speed > 0.001f)
        {
            Quaternion rotationStep = Quaternion.AngleAxis(speed * _deltaTime, angularVelocity / speed);
            tumblePivot.localRotation = (rotationStep * tumblePivot.localRotation).normalized;
        }
    }

    void HandleBounce(BounceEventData _bounceData)
    {
        StartBounceTumble(_bounceData.ContactPoint, _bounceData.SurfaceCollider);
    }

    void StartBounceTumble(Vector3 _characterPosition, Collider _mushroomCollider)
    {
        Vector3 offset = _characterPosition - _mushroomCollider.bounds.center;
        Vector3 horizontalOffset = Vector3.ProjectOnPlane(offset, Vector3.up);

        if (horizontalOffset.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 worldAxis = Vector3.Cross(Vector3.up, horizontalOffset.normalized);
        Vector3 localAxis = tumblePivot.parent != null ? tumblePivot.parent.InverseTransformDirection(worldAxis).normalized : worldAxis;

        float radius = Mathf.Max(_mushroomCollider.bounds.extents.x, _mushroomCollider.bounds.extents.z);
        float edgeAmount = Mathf.Clamp01(horizontalOffset.magnitude / Mathf.Max(radius, 0.01f));

        // Add to existing velocity instead of restarting the tumble.
        angularVelocity += localAxis * spinImpulse * edgeAmount;
    }
    #endregion
}