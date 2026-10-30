using UnityEngine;

/// <summary>Speed-driven wind particles. Presentation only; never changes movement.</summary>
[DisallowMultipleComponent]
public sealed class RunnerSpeedLines : MonoBehaviour
{
    [SerializeField] MushroomRunnerPlayer player;
    [SerializeField] RunnerMovementMotor motor;
    [SerializeField] Transform effectRoot;
    [SerializeField, Min(0f), Tooltip("Forward speed at which wind lines start appearing.")]
    float minimumSpeed = 15f;
    [SerializeField, Min(0.1f), Tooltip("Forward speed at which wind lines reach full density.")]
    float fullEffectSpeed = 45f;
    [SerializeField, Min(0f)] float fadeResponse = 5f;
    [SerializeField, Range(0f, 2f)] float density = 0.65f;

    ParticleSystem[] particles;
    float[] baseRates;
    float[] baseSpeeds;
    float intensity;

    public float Intensity => intensity;
    public Transform EffectRoot => effectRoot;

    void Awake()
    {
        if (player == null) player = GetComponent<MushroomRunnerPlayer>();
        if (motor == null) motor = GetComponent<RunnerMovementMotor>();
        CacheParticles();
    }

    void OnEnable()
    {
        GameplayEvents.GameplayReset += ClearEffect;
        MushroomRunnerEvents.PlayerStateChanged += HandlePlayerStateChanged;
        ClearEffect();
    }

    void OnDisable()
    {
        GameplayEvents.GameplayReset -= ClearEffect;
        MushroomRunnerEvents.PlayerStateChanged -= HandlePlayerStateChanged;
        ClearEffect();
    }

    void CacheParticles()
    {
        particles = effectRoot != null ? effectRoot.GetComponentsInChildren<ParticleSystem>(true) : new ParticleSystem[0];
        baseRates = new float[particles.Length];
        baseSpeeds = new float[particles.Length];
        for (int i = 0; i < particles.Length; i++)
        {
            baseRates[i] = particles[i].emission.rateOverTimeMultiplier;
            baseSpeeds[i] = particles[i].main.simulationSpeed;
        }
    }

    void LateUpdate()
    {
        if (motor == null || effectRoot == null || player == null) return;
        if (player.State != MushroomRunnerPlayer.PlayerState.Active)
        {
            ClearEffect();
            return;
        }

        UpdateSpeed(motor.Velocity.z, Time.deltaTime);
    }

    public void UpdateSpeed(float forwardSpeed, float deltaTime)
    {
        if (particles == null) return;
        // Falling and upward bounce velocity do not count as forward speed.
        float target = Mathf.InverseLerp(minimumSpeed, Mathf.Max(minimumSpeed + 0.1f, fullEffectSpeed),
            Mathf.Max(0f, forwardSpeed));
        intensity = Mathf.Lerp(intensity, target, 1f - Mathf.Exp(-fadeResponse * Mathf.Max(0f, deltaTime)));
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null) continue;
            var emission = particles[i].emission;
            emission.rateOverTimeMultiplier = baseRates[i] * density * intensity;
            var main = particles[i].main;
            main.simulationSpeed = baseSpeeds[i] * Mathf.Lerp(0.75f, 1.5f, intensity);
            if (intensity > 0.01f && !particles[i].isPlaying) particles[i].Play(false);
            else if (intensity <= 0.01f && particles[i].isPlaying)
                particles[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void HandlePlayerStateChanged(PlayerStateChangedEvent data)
    {
        if (data.Player == player && player.State != MushroomRunnerPlayer.PlayerState.Active) ClearEffect();
    }

    public void ClearEffect()
    {
        intensity = 0f;
        if (particles == null) return;
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null) continue;
            var emission = particles[i].emission;
            emission.rateOverTimeMultiplier = 0f;
            particles[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
