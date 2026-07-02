using UnityEngine;
using System.Collections;

namespace Funguy.IdkPlatformer
{
    [DisallowMultipleComponent]
    public sealed class Mushroom : MonoBehaviour, IBounceSurface
    {
        [SerializeField] private MushroomBounceProfile bounceProfile;
        [Header("Squash")]
        [SerializeField] private Transform squashTarget;
        [SerializeField, Range(0.3f, 0.9f)] private float squashY = 0.65f;
        [SerializeField, Range(0.02f, 0.2f)] private float squashDuration = 0.07f;
        [SerializeField, Range(0.1f, 0.8f)] private float recoverDuration = 0.28f;
        [SerializeField, Range(1f, 5f)] private float elasticOscillations = 3f;
        [Header("Visual Variants")]
        [SerializeField] private Material randomizedBaseMaterial;
        [SerializeField] private Renderer[] randomizedMaterialRenderers;

        private Vector3 originalSquashScale = Vector3.one;
        private Coroutine squashRoutine;
        private Material appliedRandomizedMaterial;

        public MushroomBounceProfile BounceProfile => bounceProfile;

        private void Reset()
        {
            ResolveSquashTarget();
            ResolveRandomizedMaterialRenderers(true);
            CacheOriginalSquashScale();
        }

        private void Awake()
        {
            ResolveSquashTarget();
            ResolveRandomizedMaterialRenderers(false);
            CacheOriginalSquashScale();
            RestoreSquashState();
            ApplySpawnMaterial(null);
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

        public void ApplySpawnMaterial(Material material)
        {
            ResolveRandomizedMaterialRenderers(false);

            Material resolvedMaterial = material != null ? material : randomizedBaseMaterial;
            if (resolvedMaterial == null || randomizedMaterialRenderers == null || randomizedMaterialRenderers.Length == 0)
            {
                return;
            }

            if (appliedRandomizedMaterial == resolvedMaterial)
            {
                return;
            }

            for (int index = 0; index < randomizedMaterialRenderers.Length; index++)
            {
                Renderer renderer = randomizedMaterialRenderers[index];
                if (renderer != null && renderer.sharedMaterial != resolvedMaterial)
                {
                    renderer.sharedMaterial = resolvedMaterial;
                }
            }

            appliedRandomizedMaterial = resolvedMaterial;
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

        private void ResolveRandomizedMaterialRenderers(bool forceRefresh)
        {
            if (!forceRefresh && randomizedMaterialRenderers != null && randomizedMaterialRenderers.Length > 0)
            {
                if (randomizedBaseMaterial == null)
                {
                    for (int index = 0; index < randomizedMaterialRenderers.Length; index++)
                    {
                        if (randomizedMaterialRenderers[index] != null)
                        {
                            randomizedBaseMaterial = randomizedMaterialRenderers[index].sharedMaterial;
                            break;
                        }
                    }
                }

                return;
            }

            Renderer capRenderer = ResolveCapRenderer();
            if (randomizedBaseMaterial == null && capRenderer != null)
            {
                randomizedBaseMaterial = capRenderer.sharedMaterial;
            }

            if (randomizedBaseMaterial == null)
            {
                randomizedMaterialRenderers = capRenderer != null ? new[] { capRenderer } : new Renderer[0];
                return;
            }

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            int matchCount = 0;
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null && renderers[index].sharedMaterial == randomizedBaseMaterial)
                {
                    matchCount++;
                }
            }

            if (matchCount == 0)
            {
                randomizedMaterialRenderers = capRenderer != null ? new[] { capRenderer } : new Renderer[0];
                return;
            }

            randomizedMaterialRenderers = new Renderer[matchCount];
            int writeIndex = 0;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer != null && renderer.sharedMaterial == randomizedBaseMaterial)
                {
                    randomizedMaterialRenderers[writeIndex++] = renderer;
                }
            }
        }

        private Renderer ResolveCapRenderer()
        {
            Transform capTransform = transform.Find("Cap");
            if (capTransform != null && capTransform.TryGetComponent(out Renderer capRenderer))
            {
                return capRenderer;
            }

            return null;
        }
    }
}
