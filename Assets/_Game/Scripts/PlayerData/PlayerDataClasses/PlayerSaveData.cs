using System;
using UnityEngine;

[Serializable]
public class PlayerSaveData
{
    public int SaveVersion = 1;

    public PlayerProfileData Profile = new();
    public PlayerProgressionData Progression = new();
    public PlayerStatisticsData Statistics = new();
    public PlayerSettingsData Settings = new();

    public void LogData()
    {
        Debug.Log($"SaveVersion: {SaveVersion}");
        Debug.Log($"Profile: {Profile}: {Profile.PlayerId}, Username: {Profile.Username}");
        Debug.Log($"Progression: {Progression}: SelectedCharacterId: {Progression.SelectedCharacterId}, Coins: {Progression.Coins} UnlockedCharacters: {string.Join(", ", Progression.UnlockedCharacters)}");
        Debug.Log($"Statistics: {Statistics} BestScore: {Statistics.BestScore}, BestDistance: {Statistics.BestDistance}, TotalRuns: {Statistics.TotalRuns}, TotalCoinsCollected: {Statistics.TotalCoinsCollected}, HighestMultiplier: {Statistics.HighestMultiplier}, TotalDistanceTravelled: {Statistics.TotalDistanceTravelled}, TotalPlayTime: {Statistics.TotalPlayTime}");
        Debug.Log($"Settings: {Settings} MusicVolume: {Settings.MusicVolume}, SFXVolume: {Settings.SFXVolume}, IsVibrationEnabled: {Settings.VibrationEnabled}");
    }
}