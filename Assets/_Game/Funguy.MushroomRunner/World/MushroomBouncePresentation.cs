using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MushroomBouncePresentation : MonoBehaviour
{
    [SerializeField] Transform squashTarget;
    [SerializeField, Range(0.3f, 0.9f)] float squashY = 0.65f;
    [SerializeField, Range(0.02f, 0.2f)] float squashDuration = 0.07f;
    [SerializeField, Range(0.1f, 0.8f)] float recoverDuration = 0.28f;
    [SerializeField, Range(1f, 5f)] float elasticOscillations = 3f;

    Vector3 originalSquashScale = Vector3.one;
    Coroutine squashRoutine;

    void Reset()
    {
        ResolveSquashTarget();
        CacheOriginalSquashScale();
    }

    void Awake()
    {
        ResolveSquashTarget();
        CacheOriginalSquashScale();
        RestoreSquashState();
    }

    void OnEnable()
    {
        RestoreSquashState();
    }

    void OnDisable()
    {
        if (squashRoutine != null)
        {
            StopCoroutine(squashRoutine);
            squashRoutine = null;
        }

        RestoreSquashState();
    }

    public void SetSquashTarget(Transform target)
    {
        squashTarget = target;
        CacheOriginalSquashScale();
        RestoreSquashState();
    }

    public void PlayBounce()
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

        squashRoutine = StartCoroutine(PlaySquash(target));
    }

    IEnumerator PlaySquash(Transform target)
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

    void CacheOriginalSquashScale()
    {
        Transform target = ResolveSquashTarget();
        if (target != null)
        {
            originalSquashScale = target.localScale;
        }
    }

    void RestoreSquashState()
    {
        Transform target = ResolveSquashTarget();
        if (target != null)
        {
            target.localScale = originalSquashScale;
        }
    }

    Transform ResolveSquashTarget()
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

    float EaseOutElastic(float t)
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
}