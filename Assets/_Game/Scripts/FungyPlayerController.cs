using UnityEngine;

public class FungyPlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Rigidbody rb;
    [SerializeField] Transform visual;
    [SerializeField] LayerMask mushroomLayer;

    [Header("Bounce On Mushroom")]
    [SerializeField] float bounceHeight = 2.5f;
    [SerializeField] float bounceRiseDuration = 0.22f;
    [SerializeField] float bounceFallDuration = 0.14f;
    [SerializeField] bool stopForwardVelocityOnBounce = true;
    [SerializeField] bool stopHorizontalVelocityOnBounce = false;

    [Header("Tap Jump")]
    [SerializeField] int maxAirJumps = 5;
    [SerializeField] float tapUpVelocity = 4f;
    [SerializeField] float tapForwardVelocity = 6f;
    [SerializeField] float tapCooldown = 0.08f;
    [SerializeField] bool useWorldForward = true;

    [Header("General Gravity")]
    [SerializeField] float gravityMultiplier = 2.5f;
    [SerializeField] float maxFallSpeed = 18f;
    [SerializeField] float extraFallAccel = 25f;

    [Header("Visual Bounce")]
    [SerializeField] Vector3 squashScale = new(1.2f, 0.75f, 1.2f);
    [SerializeField] Vector3 stretchScale = new(0.85f, 1.25f, 0.85f);
    [SerializeField] float squashDuration = 0.04f;
    [SerializeField] float stretchDuration = 0.10f;
    [SerializeField] float returnDuration = 0.10f;
    [SerializeField] bool tiltVisualByVelocity = true;
    [SerializeField] float tiltAmount = 12f;
    [SerializeField] float tiltLerpSpeed = 12f;

    int remainingAirJumps;
    float lastTapTime;

    bool isBounceActive;
    bool isBounceFalling;

    float bounceUpGravity;
    float bounceDownGravity;
    float bounceStartUpVelocity;

    Vector3 visualBaseScale = Vector3.one;
    float visualAnimTimer;
    enum VisualAnimState
    {
        None,
        Squash,
        Stretch,
        Return
    }
    VisualAnimState visualAnimState = VisualAnimState.None;

    Vector3 ForwardDir => useWorldForward ? Vector3.forward : transform.forward;

    public int RemainingAirJumps => remainingAirJumps;

    void Reset()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (visual != null)
        {
            visualBaseScale = visual.localScale;
        }

        remainingAirJumps = maxAirJumps;
        RecalculateBounceData();
    }

    void OnValidate()
    {
        bounceHeight = Mathf.Max(0.01f, bounceHeight);
        bounceRiseDuration = Mathf.Max(0.01f, bounceRiseDuration);
        bounceFallDuration = Mathf.Max(0.01f, bounceFallDuration);
        tapCooldown = Mathf.Max(0f, tapCooldown);
        maxAirJumps = Mathf.Max(0, maxAirJumps);
        maxFallSpeed = Mathf.Max(0.1f, maxFallSpeed);
        gravityMultiplier = Mathf.Max(0f, gravityMultiplier);
        extraFallAccel = Mathf.Max(0f, extraFallAccel);

        squashDuration = Mathf.Max(0.01f, squashDuration);
        stretchDuration = Mathf.Max(0.01f, stretchDuration);
        returnDuration = Mathf.Max(0.01f, returnDuration);

        RecalculateBounceData();
    }

    void RecalculateBounceData()
    {
        bounceUpGravity = (2f * bounceHeight) / (bounceRiseDuration * bounceRiseDuration);
        bounceStartUpVelocity = bounceUpGravity * bounceRiseDuration;
        bounceDownGravity = (2f * bounceHeight) / (bounceFallDuration * bounceFallDuration);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            TryTapJump();
        }

        UpdateVisualAnimation(Time.deltaTime);
        UpdateVisualTilt(Time.deltaTime);
    }

    void FixedUpdate()
    {
        Vector3 velocity = rb.linearVelocity;

        if (isBounceActive)
        {
            HandleBounceMovement(ref velocity);
        }
        else
        {
            HandleNormalGravity(ref velocity);
        }

        ClampFallSpeed(ref velocity);
        rb.linearVelocity = velocity;
    }

    void TryTapJump()
    {
        if (Time.time - lastTapTime < tapCooldown)
        {
            return;
        }

        if (remainingAirJumps <= 0)
        {
            return;
        }

        lastTapTime = Time.time;
        remainingAirJumps--;

        isBounceActive = false;
        isBounceFalling = false;

        Vector3 velocity = rb.linearVelocity;

        Vector3 forward = ForwardDir.normalized;

        // clear current forward + vertical so jump always starts clean
        velocity.y = 0f;
        velocity -= forward * Vector3.Dot(velocity, forward);

        // stronger forward pop + sharper vertical
        velocity += forward * tapForwardVelocity;
        velocity.y = tapUpVelocity;

        rb.linearVelocity = velocity;

        GameplayEvents.OnAirJump?.Invoke(remainingAirJumps);
    }

    void HandleBounceMovement(ref Vector3 velocity)
    {
        if (!isBounceFalling)
        {
            velocity.y -= bounceUpGravity * Time.fixedDeltaTime;

            if (velocity.y <= 0f)
            {
                velocity.y = 0f;
                isBounceFalling = true;
            }
        }
        else
        {
            velocity.y -= bounceDownGravity * Time.fixedDeltaTime;
        }
    }

    void HandleNormalGravity(ref Vector3 velocity)
    {
        if (velocity.y < 0f)
        {
            float ramp = Mathf.InverseLerp(0f, -maxFallSpeed, velocity.y);
            float added = extraFallAccel * ramp;

            velocity += Physics.gravity * (gravityMultiplier - 1f) * Time.fixedDeltaTime;
            velocity += Vector3.down * added * Time.fixedDeltaTime;
        }
    }

    void ClampFallSpeed(ref Vector3 velocity)
    {
        if (velocity.y < -maxFallSpeed)
        {
            velocity.y = -maxFallSpeed;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!IsOnLayer(collision.gameObject.layer, mushroomLayer))
        {
            return;
        }

        if (!LandedFromAbove(collision))
        {
            return;
        }

        HandleMushroomTouch();
    }

    bool IsOnLayer(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    bool LandedFromAbove(Collision collision)
    {
        for (int i = 0; i < collision.contactCount; i++)
        {
            Vector3 normal = collision.GetContact(i).normal;
            if (Vector3.Dot(normal, Vector3.up) > 0.5f)
            {
                return true;
            }
        }

        return false;
    }

    void HandleMushroomTouch()
    {
        // Event
        GameplayEvents.OnMushroomJump?.Invoke(); 

        remainingAirJumps = maxAirJumps;

        Vector3 velocity = rb.linearVelocity;

        if (stopHorizontalVelocityOnBounce)
        {
            velocity.x = 0f;
            velocity.z = 0f;
        }
        else if (stopForwardVelocityOnBounce)
        {
            Vector3 forward = ForwardDir.normalized;
            float forwardSpeed = Vector3.Dot(velocity, forward);
            velocity -= forward * forwardSpeed;
        }

        isBounceActive = true;
        isBounceFalling = false;

        velocity.y = bounceStartUpVelocity;
        rb.linearVelocity = velocity;

        PlayBounceVisual();
    }

    void PlayBounceVisual()
    {
        if (visual == null)
        {
            return;
        }

        visualAnimState = VisualAnimState.Squash;
        visualAnimTimer = 0f;
        visual.localScale = visualBaseScale;
    }

    void UpdateVisualAnimation(float deltaTime)
    {
        if (visual == null)
        {
            return;
        }

        switch (visualAnimState)
        {
            case VisualAnimState.None:
                visual.localScale = Vector3.Lerp(visual.localScale, visualBaseScale, deltaTime * 12f);
                break;

            case VisualAnimState.Squash:
            {
                visualAnimTimer += deltaTime;
                float t = Mathf.Clamp01(visualAnimTimer / squashDuration);
                float eased = EaseOutQuad(t);
                visual.localScale = Vector3.LerpUnclamped(visualBaseScale, Vector3.Scale(visualBaseScale, squashScale), eased);

                if (t >= 1f)
                {
                    visualAnimState = VisualAnimState.Stretch;
                    visualAnimTimer = 0f;
                }

                break;
            }

            case VisualAnimState.Stretch:
            {
                visualAnimTimer += deltaTime;
                float t = Mathf.Clamp01(visualAnimTimer / stretchDuration);
                float eased = EaseOutQuad(t);
                Vector3 from = Vector3.Scale(visualBaseScale, squashScale);
                Vector3 to = Vector3.Scale(visualBaseScale, stretchScale);
                visual.localScale = Vector3.LerpUnclamped(from, to, eased);

                if (t >= 1f)
                {
                    visualAnimState = VisualAnimState.Return;
                    visualAnimTimer = 0f;
                }

                break;
            }

            case VisualAnimState.Return:
            {
                visualAnimTimer += deltaTime;
                float t = Mathf.Clamp01(visualAnimTimer / returnDuration);
                float eased = EaseOutBack(t);
                Vector3 from = Vector3.Scale(visualBaseScale, stretchScale);
                visual.localScale = Vector3.LerpUnclamped(from, visualBaseScale, eased);

                if (t >= 1f)
                {
                    visualAnimState = VisualAnimState.None;
                    visualAnimTimer = 0f;
                    visual.localScale = visualBaseScale;
                }

                break;
            }
        }
    }

    void UpdateVisualTilt(float deltaTime)
    {
        if (visual == null || !tiltVisualByVelocity)
        {
            return;
        }

        Vector3 velocity = rb.linearVelocity;
        float normalizedVertical = Mathf.Clamp(velocity.y / 10f, -1f, 1f);
        float xAngle = -normalizedVertical * tiltAmount;

        Quaternion targetRotation = Quaternion.Euler(xAngle, 0f, 0f);
        visual.localRotation = Quaternion.Slerp(visual.localRotation, targetRotation, deltaTime * tiltLerpSpeed);
    }

    float EaseOutQuad(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
