using UnityEngine;
using UnityEditor;

using System.Collections.Generic;

using PlayFab.ClientModels;

public class PlayFab_Leaderboards : MonoBehaviour
{
    public void SendLeaderBoard(int _score)
    {
        var request = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate>
            {
                new StatisticUpdate
                {
                    StatisticName = "Global",
                    Value = _score
                }
            }
        };

        PlayFab.PlayFabClientAPI.UpdatePlayerStatistics(request, OnLeaderboardUpdate, OnError);
    }

    void OnLeaderboardUpdate(UpdatePlayerStatisticsResult _result)
    {
        Debug.Log("Leaderboard updated successfully!");
    }

    void OnError(PlayFab.PlayFabError _error)
    {
        Debug.LogError("Error updating leaderboard: " + _error.GenerateErrorReport());
    }

    
    public void GetLeaderboard()
    {
        var request = new GetLeaderboardRequest
        {
            StatisticName = "Global",
            StartPosition = 0,
            MaxResultsCount = 10
        };

        PlayFab.PlayFabClientAPI.GetLeaderboard(request, OnLeaderboardGet, OnError);
    }

    void OnLeaderboardGet(GetLeaderboardResult _result)
    {
        Debug.Log("Leaderboard retrieved successfully!");
        
        foreach (var entry in _result.Leaderboard)
        {
            Debug.Log($"Position: {entry.Position}, PlayFabId: {entry.PlayFabId}, DisplayName: {entry.DisplayName}, Score: {entry.StatValue}");
        }
    }
}

[CustomEditor(typeof(PlayFab_Leaderboards))]
public class LeaderboardManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PlayFab_Leaderboards manager = (PlayFab_Leaderboards)target;

        GUILayout.Space(10);

        if (GUILayout.Button("Get Leaderboard"))
        {
            manager.GetLeaderboard();
        }
    }
}