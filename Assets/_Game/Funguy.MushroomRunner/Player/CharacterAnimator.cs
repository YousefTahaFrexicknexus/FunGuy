using UnityEngine;
using System.Collections;

public class CharacterAnimator : MonoBehaviour
{
    [Header("Main references")]
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
        if(animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    void SubscribeToGameplayEvents()
    {
        GameplayEvents.OnMushroomJump += OnMushroomJump;
        GameplayEvents.OnAirJump += OnAirJump;
    }

    void UnsubscribeToGameplayEvents()
    {
        GameplayEvents.OnMushroomJump += OnMushroomJump;
        GameplayEvents.OnAirJump += OnAirJump;
    }

    void OnMushroomJump()
    {
        TriggerBounce();
        animator.SetTrigger("MushroomBounce");
    }

    void OnAirJump(int _)
    {
        TriggerBounce();
        animator.SetTrigger("MushroomBounce");
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
}