using System;
using UnityEngine;

/// <summary>Per-player timed effects. Shared definitions never hold runtime state.</summary>
[DisallowMultipleComponent]
public sealed class RunnerModifierController : MonoBehaviour
{
    sealed class EffectState
    {
        public MushroomModifierDefinition Definition;
        public float Remaining;
        public GameObject Vfx;
        public GameObject VfxPrefab;
        public ParticleSystem[] Particles = Array.Empty<ParticleSystem>();
    }

    [SerializeField, Tooltip("Optional attachment point. Defaults to the player root.")] Transform vfxAnchor;
    readonly EffectState[] effects = { new(), new() };
    RunnerMovementMotor motor;
    bool paused;

    public event Action Changed;
    public float HorizontalSpeedMultiplier => IsActive(MushroomModifierType.Chilling)
        ? effects[0].Definition.SpeedMultiplier : 1f;
    public bool ForcesPerfectLandings => IsActive(MushroomModifierType.PerfectLandings);
    public bool IsActive(MushroomModifierType type) => GetRemaining(type) > 0f;
    public float GetRemaining(MushroomModifierType type) => effects[(int)type].Remaining;
    public MushroomModifierDefinition GetDefinition(MushroomModifierType type) => effects[(int)type].Definition;
    public float GetNormalizedRemaining(MushroomModifierType type)
    {
        EffectState state = effects[(int)type];
        return state.Definition != null ? Mathf.Clamp01(state.Remaining / state.Definition.Duration) : 0f;
    }

    void Awake() => motor = GetComponent<RunnerMovementMotor>();

    void OnEnable()
    {
        GameplayEvents.OnPauseGameplay += SetPaused;
        GameplayEvents.GameplayReset += Clear;
        MushroomRunnerEvents.PlayerStateChanged += OnPlayerStateChanged;
    }

    void OnDisable()
    {
        GameplayEvents.OnPauseGameplay -= SetPaused;
        GameplayEvents.GameplayReset -= Clear;
        MushroomRunnerEvents.PlayerStateChanged -= OnPlayerStateChanged;
        Clear();
    }

    void Update() => AdvanceTimers(Time.deltaTime);

    public void AdvanceTimers(float deltaTime)
    {
        if (paused || !isActiveAndEnabled || deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
        bool expired = false;
        foreach (EffectState state in effects)
        {
            if (state.Remaining <= 0f) continue;
            state.Remaining = Mathf.Max(0f, state.Remaining - deltaTime);
            if (state.Remaining == 0f) { StopVfx(state); expired = true; }
        }
        if (expired) { SyncMotor(); Changed?.Invoke(); }
    }

    public bool Apply(MushroomModifierDefinition definition)
    {
        if (!isActiveAndEnabled || definition == null || !definition.IsValid) return false;
        EffectState state = effects[(int)definition.EffectType];
        bool wasActive = state.Remaining > 0f;
        bool changedPrefab = state.VfxPrefab != definition.PlayerVfxPrefab;
        state.Definition = definition;
        state.Remaining = definition.Duration;
        SyncMotor();

        if (changedPrefab)
        {
            StopVfx(state);
            if (state.Vfx != null) Destroy(state.Vfx);
            state.Vfx = null;
            state.VfxPrefab = definition.PlayerVfxPrefab;
            state.Particles = Array.Empty<ParticleSystem>();
        }

        if (state.Vfx == null && state.VfxPrefab != null)
        {
            state.Vfx = Instantiate(state.VfxPrefab, vfxAnchor != null ? vfxAnchor : transform, false);
            state.Vfx.name = definition.EffectType + " VFX";
            state.Particles = state.Vfx.GetComponentsInChildren<ParticleSystem>(true);
            wasActive = false;
        }

        if (state.Vfx != null && (!wasActive || changedPrefab))
        {
            state.Vfx.SetActive(true);
            foreach (ParticleSystem particle in state.Particles) particle.Play(false);
            if (paused) PauseParticles();
        }

        if (definition.ActivationSound != null && AudioManager.Instance != null)
        {
            GameplayEvents.OnApplyModifier?.Invoke(definition.EffectType);
        }

        Changed?.Invoke();
        return true;
    }

    public void Clear()
    {
        foreach (EffectState state in effects) { state.Remaining = 0f; StopVfx(state); }
        SyncMotor();
        Changed?.Invoke();
    }

    void SyncMotor()
    {
        if (motor == null) motor = GetComponent<RunnerMovementMotor>();
        if (motor != null) motor.SetHorizontalSpeedMultiplier(HorizontalSpeedMultiplier);
    }

    public void SetPaused(bool value)
    {
        paused = value;
        if (paused) PauseParticles();
        else foreach (EffectState state in effects)
            if (state.Remaining > 0f)
                foreach (ParticleSystem particle in state.Particles)
                    if (particle != null && particle.isPaused) particle.Play(false);
    }

    void PauseParticles()
    {
        foreach (EffectState state in effects)
            foreach (ParticleSystem particle in state.Particles)
                if (particle != null && particle.isPlaying) particle.Pause(false);
    }

    void OnPlayerStateChanged(PlayerStateChangedEvent data)
    {
        if (data.Player != null && data.Player.gameObject == gameObject &&
            data.CurrentState != MushroomRunnerPlayer.PlayerState.Active) Clear();
    }

    static void StopVfx(EffectState state)
    {
        foreach (ParticleSystem particle in state.Particles)
            if (particle != null) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (state.Vfx != null) state.Vfx.SetActive(false);
    }
}
