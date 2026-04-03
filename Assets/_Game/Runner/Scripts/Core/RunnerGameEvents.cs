using System;
using UnityEngine;

namespace FunGuy.Runner
{
    public static class RunnerGameEvents
    {
        public static Action<RunnerGameState> GameStateChanged;
        public static Action<Vector3Int, Vector3Int> PlayerJumpStarted;
        public static Action<Vector3Int> PlayerLanded;
        public static Action<int, int> ExtraJumpsChanged;
        public static Action<int> ScoreChanged;
        public static Action<Vector3Int> CollectibleCollected;
        public static Action<int> CountdownTick;
        public static Action CountdownFinished;
        public static Action PlayerDied;
        public static Action LevelCompleted;

        public static void RaiseGameStateChanged(RunnerGameState state)
        {
            GameStateChanged?.Invoke(state);
        }

        public static void RaisePlayerJumpStarted(Vector3Int fromCell, Vector3Int toCell)
        {
            PlayerJumpStarted?.Invoke(fromCell, toCell);
        }

        public static void RaisePlayerLanded(Vector3Int cell)
        {
            PlayerLanded?.Invoke(cell);
        }

        public static void RaiseExtraJumpsChanged(int current, int max)
        {
            ExtraJumpsChanged?.Invoke(current, max);
        }

        public static void RaiseScoreChanged(int score)
        {
            ScoreChanged?.Invoke(score);
        }

        public static void RaiseCollectibleCollected(Vector3Int cell)
        {
            CollectibleCollected?.Invoke(cell);
        }

        public static void RaiseCountdownTick(int secondsRemaining)
        {
            CountdownTick?.Invoke(secondsRemaining);
        }

        public static void RaiseCountdownFinished()
        {
            CountdownFinished?.Invoke();
        }

        public static void RaisePlayerDied()
        {
            PlayerDied?.Invoke();
        }

        public static void RaiseLevelCompleted()
        {
            LevelCompleted?.Invoke();
        }
    }
}
