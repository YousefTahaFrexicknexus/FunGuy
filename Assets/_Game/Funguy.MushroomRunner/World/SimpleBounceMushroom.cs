using UnityEngine;

namespace Funguy.MushroomRunner
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class SimpleBounceMushroom : MonoBehaviour
    {
        [SerializeField] private MushroomBounceProfile bounceProfile;
        [SerializeField, Tooltip("Optional transform whose local Z/forward axis points in the launch direction. If empty, world forward is used.")]
        private Transform launchDirection;
        [SerializeField] private Collider triggerCollider;
        [SerializeField] private MushroomBouncePresentation presentation;
        [SerializeField, Tooltip("Optional future extension points for special mushroom effects.")]
        private MonoBehaviour[] modifierBehaviours;

        public MushroomBounceProfile BounceProfile => bounceProfile;

        public Transform LaunchDirection => launchDirection;

        public Collider TriggerCollider => triggerCollider;

        private void Reset()
        {
            ResolveReferences();
            ConfigureTriggerCollider();
        }

        private void Awake()
        {
            ResolveReferences();
            ConfigureTriggerCollider();
        }

        private void OnValidate()
        {
            ResolveReferences();
            ConfigureTriggerCollider();
        }

        private void OnTriggerEnter(Collider other)
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

        private void ResolveReferences()
        {
            ResolveTriggerCollider();
            ResolveLaunchDirection();
            ResolvePresentation();
        }

        private Collider ResolveTriggerCollider()
        {
            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider>();
            }

            return triggerCollider;
        }

        private Transform ResolveLaunchDirection()
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

        private MushroomBouncePresentation ResolvePresentation()
        {
            if (presentation != null)
            {
                return presentation;
            }

            Transform searchRoot = transform.parent != null ? transform.parent : transform;
            presentation = searchRoot.GetComponentInChildren<MushroomBouncePresentation>(true);
            return presentation;
        }

        private void ConfigureTriggerCollider()
        {
            Collider collider = ResolveTriggerCollider();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
        }

        private void NotifyModifiers(RunnerMovementMotor movementMotor, Collider sourceCollider, Collider playerCollider)
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

        private static Vector3 ResolveContactPoint(Collider sourceCollider, Collider playerCollider, Vector3 fallback)
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

        private static RunnerMovementMotor ResolveMovementMotor(Collider other)
        {
            Rigidbody attachedBody = other.attachedRigidbody;
            if (attachedBody != null && attachedBody.TryGetComponent(out RunnerMovementMotor motor))
            {
                return motor;
            }

            return other.GetComponentInParent<RunnerMovementMotor>();
        }
    }
}
