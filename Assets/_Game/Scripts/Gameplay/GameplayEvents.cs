using System.Collections.Generic;
using UnityEngine;

public static class GameplayEvents
{
    // --- Game state changes ---
    public static System.Action<GameState> OnGameStateChanged;                  // GameState
    public static System.Action<int> OnAirJump;                                 // Jumps left
    public static System.Action OnMushroomJump;
    public static System.Action GameplayReset;                            // True if reset is due to player death, false if due to level completion
}
