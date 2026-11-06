using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
public class RunnerCheats : MonoBehaviour
{
    [SerializeField] MushroomRunnerPlayer player;
    [SerializeField] MomentumSystem momentum;
    [SerializeField] DistanceScoreManager score;

    [Header("Fly Straight To World Z")]
    [SerializeField] float targetZ = 900f;
    [SerializeField, Min(1f)] float flightSpeed = 300f;

    [Header("Resume With")]
    [SerializeField, Range(1, 4)] int multiplier = 4;
    [SerializeField, Min(0f)] float currentSpeed = 50f;
    [ShowInInspector, ReadOnly] public bool IsFlying { get; private set; }

    Rigidbody body;
    bool previousCollisions;
    float previousTimeScale;

    void Reset() => ResolveReferences();

    bool ResolveReferences()
    {
        if (player == null) player = FindAnyObjectByType<MushroomRunnerPlayer>();
        if (momentum == null) momentum = FindAnyObjectByType<MomentumSystem>();
        if (score == null) score = FindAnyObjectByType<DistanceScoreManager>();
        if (player == null || player.MovementMotor == null) return false;
        body = player.GetComponent<Rigidbody>();
        return body != null;
    }

    [Button("Fly To Target & Resume"), EnableIf("@UnityEngine.Application.isPlaying")]
    public void FlyToTargetAndResume()
    {
        if (!Application.isPlaying || IsFlying || !ResolveReferences()) return;
        ClearEnding();
        ReleaseHammock();
        previousTimeScale = Time.timeScale;
        previousCollisions = body.detectCollisions;
        player.SetState(MushroomRunnerPlayer.PlayerState.Disabled);
        body.isKinematic = true;
        body.detectCollisions = false;
        score?.SetPaused(true);
        Time.timeScale = 0f;
        IsFlying = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPaused = false;
#endif
    }

    void Update()
    {
        if (!IsFlying) return;
        if (body == null || player == null) { RestoreFlight(); return; }

        Vector3 position = body.position;
        position.z = Mathf.MoveTowards(position.z, targetZ, Mathf.Max(1f, flightSpeed) * Time.unscaledDeltaTime);
        body.position = position;
        player.transform.position = position;
        // Streamers can follow the player while physics and gameplay are paused.
        Physics.SyncTransforms();
        if (position.z == targetZ) ApplySettingsAndUnpause();
    }

    [Button("Apply Speed / Multiplier & Unpause"), EnableIf("@UnityEngine.Application.isPlaying")]
    public void ApplySettingsAndUnpause()
    {
        if (!Application.isPlaying || !ResolveReferences()) return;
        RestoreFlight();
        ClearEnding();
        ReleaseHammock();
        body.isKinematic = false;
        RunnerMovementMotor motor = player.MovementMotor;
        motor.enabled = true;
        motor.ResetMotion(body.position, body.rotation);
        player.SetState(MushroomRunnerPlayer.PlayerState.Active);
        if (momentum != null)
        {
            momentum.IsRunning = true;
            momentum.SetPaused(false);
            momentum.SetMultiplierForDebug(multiplier);
        }
        body.linearVelocity = Vector3.forward * Mathf.Max(0f, currentSpeed);
        score?.SetPaused(false); // Sync position so the cheat flight does not award distance points.
        Time.timeScale = 1f;
        GameplayEvents.OnPauseGameplay?.Invoke(false);
        UIManager.Instance?.Close_PopupsAndPanels(UIType.Pause, true);
        GameplayEvents.OnSpeedChanged?.Invoke(body.linearVelocity.z, motor.CurrentMaxSpeed);
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPaused = false;
#endif
    }

    void ReleaseHammock()
    {
        var hammock = player.GetComponent<HammockSlingshot>();
        if (hammock != null && hammock.IsHolding) hammock.LaunchImmediately(Vector3.zero);
    }

    void ClearEnding()
    {
        foreach (var ending in FindObjectsByType<GlassBreakTreanstion>(FindObjectsSortMode.None))
            ending.ResetTransition();
    }

    void RestoreFlight()
    {
        if (!IsFlying) return;
        IsFlying = false;
        if (body != null)
        {
            body.isKinematic = false;
            body.detectCollisions = previousCollisions;
        }
        if (player != null) player.SetState(MushroomRunnerPlayer.PlayerState.Active);
        score?.SetPaused(false);
        Time.timeScale = previousTimeScale;
    }

    void OnDisable() => RestoreFlight();
}
