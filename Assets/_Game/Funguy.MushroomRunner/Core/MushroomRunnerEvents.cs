using System;
using UnityEngine;

public enum RunFailureReason
{
    Unknown = 0,
    FellBelowDeathPlane = 1,
    ManualReset = 2
}

public readonly struct PlayerRegisteredEvent
{
    public PlayerRegisteredEvent(MushroomRunnerPlayer player, RunnerMovementMotor movementMotor, RunnerInputSource inputSource, Transform cameraFollowTarget)
    {
        Player = player;
        MovementMotor = movementMotor;
        InputSource = inputSource;
        CameraFollowTarget = cameraFollowTarget;
    }
    public MushroomRunnerPlayer Player { get; }
    public RunnerMovementMotor MovementMotor { get; }
    public RunnerInputSource InputSource { get; }
    public Transform CameraFollowTarget { get; }
}

public readonly struct PlayerStateChangedEvent
{
    public PlayerStateChangedEvent(
        MushroomRunnerPlayer player,
        MushroomRunnerPlayer.PlayerState previousState,
        MushroomRunnerPlayer.PlayerState currentState)
    {
        Player = player;
        PreviousState = previousState;
        CurrentState = currentState;
    }
    public MushroomRunnerPlayer Player { get; }
    public MushroomRunnerPlayer.PlayerState PreviousState { get; }
    public MushroomRunnerPlayer.PlayerState CurrentState { get; }
}

public readonly struct PlayerBouncedEvent
{
    public PlayerBouncedEvent(MushroomRunnerPlayer player, BounceEventData bounceEvent)
    {
        Player = player;
        BounceEvent = bounceEvent;
    }
    public MushroomRunnerPlayer Player { get; }
    public BounceEventData BounceEvent { get; }
}

public readonly struct PlayerDashedEvent
{
    public PlayerDashedEvent(MushroomRunnerPlayer player, Vector3 resultingVelocity)
    {
        Player = player;
        ResultingVelocity = resultingVelocity;
    }

    public MushroomRunnerPlayer Player { get; }
    public Vector3 ResultingVelocity { get; }
}

public readonly struct RunLifecycleEvent
{
    public RunLifecycleEvent(MushroomRunnerPlayer player, Vector3 spawnPosition, Quaternion spawnRotation, int resetCount)
    {
        Player = player;
        SpawnPosition = spawnPosition;
        SpawnRotation = spawnRotation;
        ResetCount = resetCount;
    }

    public MushroomRunnerPlayer Player { get; }
    public Vector3 SpawnPosition { get; }
    public Quaternion SpawnRotation { get; }
    public int ResetCount { get; }
}

public readonly struct RunFailedEvent
{
    public RunFailedEvent(MushroomRunnerPlayer player, RunFailureReason reason, Vector3 worldPosition)
    {
        Player = player;
        Reason = reason;
        WorldPosition = worldPosition;
    }

    public MushroomRunnerPlayer Player { get; }
    public RunFailureReason Reason { get; }
    public Vector3 WorldPosition { get; }
}

public readonly struct RunScoreUpdatedEvent
{
    public RunScoreUpdatedEvent(MushroomRunnerPlayer player, RunScoreSnapshot snapshot)
    {
        Player = player;
        Snapshot = snapshot;
    }

    public MushroomRunnerPlayer Player { get; }
    public RunScoreSnapshot Snapshot { get; }
}

public static class MushroomRunnerEvents
{
    public static event Action<PlayerRegisteredEvent> PlayerRegistered;
    public static event Action<PlayerStateChangedEvent> PlayerStateChanged;
    public static event Action<PlayerBouncedEvent> PlayerBounced;
    public static event Action<PlayerDashedEvent> PlayerDashed;
    public static event Action<RunLifecycleEvent> RunStarted;
    public static event Action<RunLifecycleEvent> RunReset;
    public static event Action<RunFailedEvent> RunFailed;
    public static event Action<RunScoreUpdatedEvent> RunScoreUpdated;
    public static void RaisePlayerRegistered(PlayerRegisteredEvent eventData) => PlayerRegistered?.Invoke(eventData);
    public static void RaisePlayerStateChanged(PlayerStateChangedEvent eventData) => PlayerStateChanged?.Invoke(eventData);
    public static void RaisePlayerBounced(PlayerBouncedEvent eventData) => PlayerBounced?.Invoke(eventData);
    public static void RaisePlayerDashed(PlayerDashedEvent eventData) => PlayerDashed?.Invoke(eventData);
    public static void RaiseRunStarted(RunLifecycleEvent eventData) => RunStarted?.Invoke(eventData);
    public static void RaiseRunReset(RunLifecycleEvent eventData) => RunReset?.Invoke(eventData);
    public static void RaiseRunFailed(RunFailedEvent eventData) => RunFailed?.Invoke(eventData);
    public static void RaiseRunScoreUpdated(RunScoreUpdatedEvent eventData) => RunScoreUpdated?.Invoke(eventData);
}