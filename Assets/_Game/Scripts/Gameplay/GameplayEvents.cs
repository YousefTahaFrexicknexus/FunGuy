using System.Collections.Generic;
using UnityEngine;

public static class GameplayEvents
{
    // --- Game preperation ---
    public static System.Action<MovementTuningProfile> OnSetActiveTuningProfile;    // Current speed and maximum speed

    // --- Game state changes ---
    public static System.Action<GameState> OnGameStateChanged;                      // GameState
    public static System.Action GameplayReset;                                      // True if reset is due to player death, false if due to level completion
    
    // --- Gameplay changes --- 
    public static System.Action<int> OnAirJump;                                     // Jumps left
    public static System.Action OnMushroomJump; 
    public static System.Action<float, float> OnSpeedChanged;                       // Current speed and maximum speed
    
    // --- Momentum --- 
    public static System.Action<int> OnScoreChanged;                                // Current score
    public static System.Action<float> OnMomentumChanged;                           // Current momentum
    public static System.Action<float> OnMultiplierChanged;                         // Current multiplier

}
