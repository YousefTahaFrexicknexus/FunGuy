using UnityEngine;
using System.Collections;

public class Mushroom : MonoBehaviour
{
    [Header("— Bounce —")]
    [Range(0.5f, 3f)] public float bounceMultiplier = 1f;
    [Range(0f, 10f)] public float minImpactSpeed = 1f;

    [Header("— Launch Direction —")]
    [Tooltip("Off: creature keeps its own forward. On: mushroom dictates the launch angle.")]
    public bool overrideLaunchDirection = false;

    [Tooltip("World-space direction to launch the creature. Y is ignored (vertical handled by bounce force).")]
    public Vector3 launchDirection = Vector3.forward;

    [Tooltip("Draw the launch arc in the Scene view.")]
    public bool showArcGizmo = true;

    [Header("— Mushroom Squash —")]
    [Range(0.3f, 0.9f)] public float squashY = 0.65f;
    [Range(0.02f, 0.2f)] public float squashDuration = 0.07f;
    [Range(0.1f, 0.8f)] public float recoverDuration = 0.28f;
    [Range(1f, 5f)] public float elasticOscillations = 3f;

    [Header("— VFX —")]
    public ParticleSystem bounceParticles;
    public bool scaleParticlesWithImpact = true;
    [Range(0f, 3f)] public float particleImpactScale = 1.5f;

    [Header("— SFX —")]
    public AudioClip bounceSound;
    [Range(0f, 1f)] public float volume = 0.9f;
    [Range(0f, 0.3f)] public float pitchVariance = 0.1f;

    private Vector3 originalScale;
    private Coroutine squashRoutine;
    private AudioSource audioSource;

    void Awake()
    {
        originalScale = transform.localScale;
        audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1f;
        audioSource.playOnAwake = false;
    }

    void OnCollisionEnter(Collision collision)
    {
        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minImpactSpeed) return;

        Vector3? direction = overrideLaunchDirection ? launchDirection.normalized : null;
        if (!TryTriggerBounce(collision.gameObject, bounceMultiplier, direction))
        {
            return;
        }

        if (squashRoutine != null) StopCoroutine(squashRoutine);
        squashRoutine = StartCoroutine(MushroomSquash());

        PlayVFX(impactSpeed);
        PlaySFX();
    }

    bool TryTriggerBounce(GameObject target, float mushroomMultiplier, Vector3? direction)
    {
        CreatureBouncer_XAxis xAxisBouncer = target.GetComponent<CreatureBouncer_XAxis>();
        if (xAxisBouncer != null)
        {
            xAxisBouncer.TriggerBounce(mushroomMultiplier, direction);
            return true;
        }

        CreatureBouncer legacyBouncer = target.GetComponent<CreatureBouncer>();
        if (legacyBouncer != null)
        {
            legacyBouncer.TriggerBounce(mushroomMultiplier, direction);
            return true;
        }

        return false;
    }

    void PlayVFX(float impactSpeed)
    {
        if (bounceParticles == null) return;
        if (scaleParticlesWithImpact)
        {
            var main = bounceParticles.main;
            main.startSizeMultiplier = Mathf.Lerp(0.5f, particleImpactScale, impactSpeed / 20f);
        }
        bounceParticles.Play();
    }

    void PlaySFX()
    {
        if (bounceSound == null) return;
        audioSource.pitch = 1f + Random.Range(-pitchVariance, pitchVariance);
        audioSource.PlayOneShot(bounceSound, volume);
    }

    IEnumerator MushroomSquash()
    {
        Vector3 squashed = new Vector3(
            originalScale.x * 1.35f,
            originalScale.y * squashY,
            originalScale.z * 1.35f);

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / squashDuration;
            transform.localScale = Vector3.Lerp(originalScale, squashed, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }

        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / recoverDuration;
            transform.localScale = Vector3.LerpUnclamped(squashed, originalScale, EaseOutElastic(t));
            yield return null;
        }

        transform.localScale = originalScale;
    }

    float EaseOutElastic(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        float c4 = (2f * Mathf.PI) / elasticOscillations;
        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }

    void OnDrawGizmosSelected()
    {
        if (!showArcGizmo) return;

#if UNITY_EDITOR
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.8f);
        if (overrideLaunchDirection)
            Gizmos.DrawRay(transform.position, launchDirection.normalized * 3f);

        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.8f,
            $"x{bounceMultiplier:F2}{(overrideLaunchDirection ? "  [Redirect]" : "")}");
#endif
    }
}
