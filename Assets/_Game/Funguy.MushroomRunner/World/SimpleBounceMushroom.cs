using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class SimpleBounceMushroom : MonoBehaviour
{
    [SerializeField] MushroomBounceProfile bounceProfile;
    [SerializeField, Tooltip("Optional transform whose local Z/forward axis points in the launch direction. If empty, world forward is used.")]
    Transform launchDirection;
    [SerializeField] Collider triggerCollider;
    [SerializeField] MushroomBouncePresentation presentation;
    [SerializeField, Tooltip("Optional future extension points for special mushroom effects.")]
    MonoBehaviour[] modifierBehaviours;

    public MushroomBounceProfile BounceProfile => bounceProfile;

    public Transform LaunchDirection => launchDirection;

    public Collider TriggerCollider => triggerCollider;

    void Reset()
    {
        ResolveReferences();
        ConfigureTriggerCollider();
    }

    void Awake()
    {
        ResolveReferences();
        ConfigureTriggerCollider();
    }

    void OnValidate()
    {
        ResolveReferences();
        ConfigureTriggerCollider();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        RunnerMovementMotor movementMotor = ResolveMovementMotor(other);
        if (movementMotor == null)
        {
            return;
        }

        TryBounce(movementMotor, other);
    }

    public void SetBounceProfile(MushroomBounceProfile profile)
    {
        bounceProfile = profile;
    }

    public void SetLaunchDirection(Transform direction)
    {
        launchDirection = direction;
    }

    public bool TryBounce(RunnerMovementMotor movementMotor, Collider playerCollider)
    {
        if (movementMotor == null || bounceProfile == null)
        {
            return false;
        }

        Collider sourceCollider = ResolveTriggerCollider();
        Vector3 contactPoint = ResolveContactPoint(sourceCollider, playerCollider, movementMotor.transform.position);
        bool didBounce = movementMotor.ApplyForce(
            launchDirection,
            bounceProfile,
            sourceCollider,
            contactPoint,
            Vector3.up);

        if (!didBounce)
        {
            return false;
        }

        ResolvePresentation()?.PlayBounce();
        NotifyModifiers(movementMotor, sourceCollider, playerCollider);
        return true;
    }

    void ResolveReferences()
    {
        ResolveTriggerCollider();
        ResolveLaunchDirection();
        ResolvePresentation();
    }

    Collider ResolveTriggerCollider()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider>();
        }

        return triggerCollider;
    }

    Transform ResolveLaunchDirection()
    {
        if (launchDirection != null)
        {
            return launchDirection;
        }

        Transform searchRoot = transform.parent != null ? transform.parent : transform;
        Transform foundDirection = searchRoot.Find("LaunchDirection");
        if (foundDirection != null)
        {
            launchDirection = foundDirection;
        }

        return launchDirection;
    }

    MushroomBouncePresentation ResolvePresentation()
    {
        if (presentation != null)
        {
            return presentation;
        }

        Transform searchRoot = transform.parent != null ? transform.parent : transform;
        presentation = searchRoot.GetComponentInChildren<MushroomBouncePresentation>(true);
        return presentation;
    }

    void ConfigureTriggerCollider()
    {
        Collider collider = ResolveTriggerCollider();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }

    void NotifyModifiers(RunnerMovementMotor movementMotor, Collider sourceCollider, Collider playerCollider)
    {
        if (modifierBehaviours == null)
        {
            return;
        }

        MushroomBounceModifierContext context = new(this, movementMotor, sourceCollider, playerCollider);
        for (int index = 0; index < modifierBehaviours.Length; index++)
        {
            if (modifierBehaviours[index] is IMushroomBounceModifier modifier)
            {
                modifier.OnMushroomBounce(in context);
            }
        }
    }

    static Vector3 ResolveContactPoint(Collider sourceCollider, Collider playerCollider, Vector3 fallback)
    {
        if (sourceCollider != null && playerCollider != null)
        {
            return sourceCollider.ClosestPoint(playerCollider.bounds.center);
        }

        if (sourceCollider != null)
        {
            return sourceCollider.bounds.center;
        }

        return fallback;
    }

    static RunnerMovementMotor ResolveMovementMotor(Collider other)
    {
        Rigidbody attachedBody = other.attachedRigidbody;
        if (attachedBody != null && attachedBody.TryGetComponent(out RunnerMovementMotor motor))
        {
            return motor;
        }

        return other.GetComponentInParent<RunnerMovementMotor>();
    }
}