using UnityEngine;

/// <summary>Speed-driven wind particles. Presentation only; never changes movement.</summary>
[DisallowMultipleComponent]
public sealed class RunnerSpeedLines : MonoBehaviour
{
    [SerializeField] MushroomRunnerPlayer player;
    [SerializeField] RunnerMovementMotor motor;
    [SerializeField] Transform effectRoot;
    [SerializeField, Min(0f), Tooltip("Forward speed at which wind lines start appearing.")]
    float minimumSpeed = 12f;
    [SerializeField, Min(0.1f), Tooltip("Forward speed at which wind lines reach full density.")]
    float fullEffectSpeed = 35f;
    [SerializeField, Min(0f)] float fadeResponse = 9f;
    [SerializeField, Range(0f, 2f)] float density = 1.1f;
    [SerializeField, Range(0.25f, 2f), Tooltip("Below 1 makes the lines readable sooner after crossing Minimum Speed.")]
    float responseExponent = 0.65f;
    [SerializeField, Min(1f), Tooltip("Streak travel speed at full intensity. Does not shorten particle lifetime.")]
    float maximumParticleSpeedMultiplier = 1.6f;
    [SerializeField, Min(1f)] float maximumLineWidthMultiplier = 1.25f;

    ParticleSystem[] particles;
    float[] baseRates;
    float[] baseSpeeds;
    float[] baseSizes;
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
        baseSizes = new float[particles.Length];
        for (int i = 0; i < particles.Length; i++)
        {
            baseRates[i] = particles[i].emission.rateOverTimeMultiplier;
            baseSpeeds[i] = particles[i].main.startSpeedMultiplier;
            baseSizes[i] = particles[i].main.startSizeMultiplier;
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

        // The runner can carry high speed while steering across X as well as Z.
        // Use actual ground-plane speed so the lines do not disappear on fast turns.
        float planarSpeed = Vector3.ProjectOnPlane(motor.Velocity, motor.UpDirection).magnitude;
        UpdateSpeed(planarSpeed, Time.deltaTime);
    }

    public void UpdateSpeed(float forwardSpeed, float deltaTime)
    {
        if (particles == null) return;
        // Falling and upward bounce velocity do not count as forward speed.
        float target = Mathf.InverseLerp(minimumSpeed, Mathf.Max(minimumSpeed + 0.1f, fullEffectSpeed),
            Mathf.Max(0f, forwardSpeed));
        target = Mathf.Pow(target, responseExponent);
        // React quickly to acceleration, then let the wind ease away when slowing down.
        float response = target > intensity ? fadeResponse : fadeResponse * 0.5f;
        intensity = Mathf.Lerp(intensity, target, 1f - Mathf.Exp(-response * Mathf.Max(0f, deltaTime)));
        for (int i = 0; i < particles.Length; i++)
        {
            if (particles[i] == null) continue;
            var emission = particles[i].emission;
            emission.rateOverTimeMultiplier = baseRates[i] * density * intensity;
            var main = particles[i].main;
            // Speeding up simulation also ages particles faster and makes short trails vanish.
            // Change travel speed and width instead, keeping their visible lifetime intact.
            main.startSpeedMultiplier = baseSpeeds[i] * Mathf.Lerp(0.8f, maximumParticleSpeedMultiplier, intensity);
            main.startSizeMultiplier = baseSizes[i] * Mathf.Lerp(0.75f, maximumLineWidthMultiplier, intensity);
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
