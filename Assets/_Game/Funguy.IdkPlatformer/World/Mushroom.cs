using UnityEngine;
using System.Collections;

namespace Funguy.IdkPlatformer
{
    [DisallowMultipleComponent]
    public sealed class Mushroom : MonoBehaviour, IBounceSurface, IBounceContactResolver, IBounceSurfaceBehavior
    {
        [SerializeField] private MushroomBounceProfile bounceProfile;
        [Header("Collision")]
        [SerializeField] private MeshFilter collisionMeshSource;
        [SerializeField] private MeshCollider collisionMeshCollider;
        [Header("Bounce Feel")]
        [SerializeField] private bool bounceOnGlancingTouches = true;
        [SerializeField, Range(0f, 0.2f)] private float postBounceCollisionIgnoreDuration = 0.08f;
        [Header("Squash")]
        [SerializeField] private Transform squashTarget;
        [SerializeField, Range(0.3f, 0.9f)] private float squashY = 0.65f;
        [SerializeField, Range(0.02f, 0.2f)] private float squashDuration = 0.07f;
        [SerializeField, Range(0.1f, 0.8f)] private float recoverDuration = 0.28f;
        [SerializeField, Range(1f, 5f)] private float elasticOscillations = 3f;

        private Vector3 originalSquashScale = Vector3.one;
        private Coroutine squashRoutine;

        public MushroomBounceProfile BounceProfile => bounceProfile;

        public bool AllowsBounceWhileMovingUpward => bounceOnGlancingTouches;

        public float PostBounceCollisionIgnoreDuration => Mathf.Max(0f, postBounceCollisionIgnoreDuration);

        private void Reset()
        {
            EnsureCollisionSetup();
            ResolveSquashTarget();
            CacheOriginalSquashScale();
        }

        private void Awake()
        {
            EnsureCollisionSetup();
            ResolveSquashTarget();
            CacheOriginalSquashScale();
            RestoreSquashState();
        }

        private void OnValidate()
        {
            EnsureCollisionSetup();
        }

        private void OnEnable()
        {
            RestoreSquashState();
        }

        private void OnDisable()
        {
            if (squashRoutine != null)
            {
                StopCoroutine(squashRoutine);
                squashRoutine = null;
            }

            RestoreSquashState();
        }

        public void SetBounceProfile(MushroomBounceProfile profile)
        {
            bounceProfile = profile;
        }

        public BounceSurfaceResponse GetBounceResponse(in BounceContext context)
        {
            TriggerSquash();

            if (bounceProfile != null)
            {
                return bounceProfile.CreateResponse(transform, context);
            }

            return new BounceSurfaceResponse(
                1f,
                0.4f,
                0f,
                context.BaseJumpForce,
                0.25f,
                transform.up,
                1f);
        }

        public bool TryResolveBounceContact(
            in Collision collision,
            Vector3 worldUp,
            float minGroundDot,
            out Vector3 contactPoint,
            out Vector3 contactNormal,
            out float groundDot)
        {
            contactPoint = transform.position;
            contactNormal = ResolveStableBounceNormal(worldUp);
            groundDot = 1f;

            if (collision == null || collision.contactCount <= 0)
            {
                return false;
            }

            float bestHeight = float.NegativeInfinity;
            Vector3 up = worldUp.sqrMagnitude > 0.0001f ? worldUp.normalized : Vector3.up;

            for (int index = 0; index < collision.contactCount; index++)
            {
                ContactPoint contact = collision.GetContact(index);
                float height = Vector3.Dot(contact.point, up);
                if (height <= bestHeight)
                {
                    continue;
                }

                bestHeight = height;
                contactPoint = contact.point;
            }

            return true;
        }

        private void TriggerSquash()
        {
            Transform target = ResolveSquashTarget();
            if (!isActiveAndEnabled || target == null)
            {
                return;
            }

            if (squashRoutine != null)
            {
                StopCoroutine(squashRoutine);
            }

            squashRoutine = StartCoroutine(MushroomSquash(target));
        }

        private IEnumerator MushroomSquash(Transform target)
        {
            Vector3 squashed = new(
                originalSquashScale.x * 1.35f,
                originalSquashScale.y * squashY,
                originalSquashScale.z * 1.35f);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / squashDuration;
                target.localScale = Vector3.Lerp(originalSquashScale, squashed, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / recoverDuration;
                target.localScale = Vector3.LerpUnclamped(squashed, originalSquashScale, EaseOutElastic(t));
                yield return null;
            }

            target.localScale = originalSquashScale;
            squashRoutine = null;
        }

        private float EaseOutElastic(float t)
        {
            if (t <= 0f)
            {
                return 0f;
            }

            if (t >= 1f)
            {
                return 1f;
            }

            float c4 = (2f * Mathf.PI) / elasticOscillations;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
        }

        private void CacheOriginalSquashScale()
        {
            Transform target = ResolveSquashTarget();
            if (target != null)
            {
                originalSquashScale = target.localScale;
            }
        }

        private void RestoreSquashState()
        {
            Transform target = ResolveSquashTarget();
            if (target != null)
            {
                target.localScale = originalSquashScale;
            }
        }

        private Transform ResolveSquashTarget()
        {
            if (squashTarget != null)
            {
                return squashTarget;
            }

            for (int childIndex = 0; childIndex < transform.childCount; childIndex++)
            {
                Transform child = transform.GetChild(childIndex);
                if (!child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (child.GetComponentInChildren<Renderer>(true) != null)
                {
                    squashTarget = child;
                    return squashTarget;
                }
            }

            squashTarget = transform;
            return squashTarget;
        }

        private void EnsureCollisionSetup()
        {
            MeshFilter meshSource = ResolveCollisionMeshSource();
            if (meshSource == null || meshSource.sharedMesh == null)
            {
                return;
            }

            collisionMeshSource = meshSource;
            collisionMeshCollider = meshSource.GetComponent<MeshCollider>();
            if (collisionMeshCollider == null)
            {
                collisionMeshCollider = meshSource.gameObject.AddComponent<MeshCollider>();
            }

            collisionMeshCollider.sharedMesh = meshSource.sharedMesh;
            collisionMeshCollider.enabled = true;

            DisableOtherColliders(collisionMeshCollider);
        }

        private MeshFilter ResolveCollisionMeshSource()
        {
            if (collisionMeshSource != null && collisionMeshSource.sharedMesh != null)
            {
                return collisionMeshSource;
            }

            MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>(true);
            MeshFilter bestFilter = null;
            float bestScore = float.NegativeInfinity;

            for (int index = 0; index < meshFilters.Length; index++)
            {
                MeshFilter meshFilter = meshFilters[index];
                if (meshFilter == null || meshFilter.sharedMesh == null || !meshFilter.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Renderer renderer = meshFilter.GetComponent<Renderer>();
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                float score = renderer.bounds.size.sqrMagnitude;
                if (bestFilter == null || score > bestScore)
                {
                    bestFilter = meshFilter;
                    bestScore = score;
                }
            }

            return bestFilter;
        }

        private void DisableOtherColliders(Collider keepCollider)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null || collider == keepCollider)
                {
                    continue;
                }

                collider.enabled = false;
            }
        }

        private Vector3 ResolveStableBounceNormal(Vector3 worldUp)
        {
            Vector3 stableUp = transform.up;
            if (stableUp.sqrMagnitude <= 0.0001f)
            {
                stableUp = worldUp;
            }

            if (stableUp.sqrMagnitude <= 0.0001f)
            {
                stableUp = Vector3.up;
            }

            stableUp.Normalize();

            Vector3 fallbackUp = worldUp.sqrMagnitude > 0.0001f ? worldUp.normalized : Vector3.up;
            return Vector3.Dot(stableUp, fallbackUp) >= 0f ? stableUp : fallbackUp;
        }
    }
}
