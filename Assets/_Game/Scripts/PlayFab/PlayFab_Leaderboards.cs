using UnityEngine;
using UnityEditor;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using PlayFab;
using PlayFab.ClientModels;

public class PlayFab_Leaderboards : MonoBehaviour
{
    const string StatisticName = "Global";

    public async Task<UpdatePlayerStatisticsResult> SendLeaderboardAsync(int score)
    {
        UpdatePlayerStatisticsRequest request = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate>
            {
                new StatisticUpdate
                {
                    StatisticName = StatisticName,
                    Value = score
                }
            }
        };

        UpdatePlayerStatisticsResult result = await UpdatePlayerStatisticsAsync(request);

        Debug.Log($"Leaderboard updated successfully with score: {score}");

        return result;
    }

    public async Task<GetLeaderboardResult> GetLeaderboardAsync(int startPosition = 0, int maxResultsCount = 10)
    {
        GetLeaderboardRequest request = new GetLeaderboardRequest
        {
            StatisticName = StatisticName,
            StartPosition = startPosition,
            MaxResultsCount = maxResultsCount
        };

        GetLeaderboardResult result = await RequestLeaderboardAsync(request);

        Debug.Log("Leaderboard retrieved successfully!");

        foreach (PlayerLeaderboardEntry entry in result.Leaderboard)
        {
            Debug.Log($"Position: {entry.Position}, PlayFabId: {entry.PlayFabId}, DisplayName: {entry.DisplayName}, Score: {entry.StatValue}");
        }

        return result;
    }

    Task<UpdatePlayerStatisticsResult> UpdatePlayerStatisticsAsync(UpdatePlayerStatisticsRequest request)
    {
        var taskCompletionSource = new TaskCompletionSource<UpdatePlayerStatisticsResult>();

        PlayFabClientAPI.UpdatePlayerStatistics(
            request,
            result =>
            {
                taskCompletionSource.TrySetResult(result);
            },
            error =>
            {
                taskCompletionSource.TrySetException(CreatePlayFabException("Failed to update leaderboard", error));
            });

        return taskCompletionSource.Task;
    }

    Task<GetLeaderboardResult> RequestLeaderboardAsync(GetLeaderboardRequest request)
    {
        var taskCompletionSource = new TaskCompletionSource<GetLeaderboardResult>();

        PlayFabClientAPI.GetLeaderboard(
            request,
            result =>
            {
                taskCompletionSource.TrySetResult(result);
            },
            error =>
            {
                taskCompletionSource.TrySetException(CreatePlayFabException("Failed to retrieve leaderboard", error));
            });

        return taskCompletionSource.Task;
    }

    Exception CreatePlayFabException(string message, PlayFabError error)
    {
        string errorReport = error?.GenerateErrorReport() ?? "Unknown PlayFab error.";

        Debug.LogError($"{message}: {errorReport}");

        return new Exception($"{message}: {errorReport}");
    }

    public async Task DebugPushLeaderboardEntry(string _displayName, int _scoreValue)
    {
        string statisticName = StatisticName;
        int numericValue = _scoreValue;
        string displayName = _displayName;

        // // 1. Set Display Name
        // await UpdateDisplayNameAsync(displayName);

        // 2. Update Leaderboard Statistic
        UpdatePlayerStatisticsRequest request = new UpdatePlayerStatisticsRequest
        {
            Statistics = new List<StatisticUpdate>
            {
                new StatisticUpdate
                {
                    StatisticName = statisticName,
                    Value = numericValue
                }
            }
        };

        await UpdatePlayerStatisticsAsync(request);

        // 3. Get Player ID
        string playerId = PlayFabSettings.staticPlayer?.PlayFabId;

        Debug.Log(
            $"Leaderboard Debug Push Successful\n" +
            $"Statistic: {statisticName}\n" +
            $"Value: {numericValue}\n" +
            $"Player ID: {playerId}\n" +
            $"Display Name: {displayName}"
        );
    }

    // Task<UpdateUserTitleDisplayNameResult> UpdateDisplayNameAsync(string displayName)
    // {
    //     var tcs = new TaskCompletionSource<UpdateUserTitleDisplayNameResult>();

    //     PlayFabClientAPI.UpdateUserTitleDisplayName(
    //         new UpdateUserTitleDisplayNameRequest
    //         {
    //             DisplayName = displayName
    //         },

    //         result =>
    //         {
    //             tcs.TrySetResult(result);
    //         },

    //         error =>
    //         {
    //             tcs.TrySetException(
    //                 CreatePlayFabException(
    //                     "Failed updating display name",
    //                     error
    //                 )
    //             );
    //         });


    //     return tcs.Task;
    // }

#if (UNITY_EDITOR)   

    [SerializeField] public int score = 100;
    [SerializeField] public string name = "PlayerName";

    [CustomEditor(typeof(PlayFab_Leaderboards))]
    public class CustomInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
    
            PlayFab_Leaderboards leaderboardManager = (PlayFab_Leaderboards) target;
    
            if(GUILayout.Button("Update Leaderboard"))
            {
                leaderboardManager.DebugPushLeaderboardEntry(leaderboardManager.name, leaderboardManager.score);
            }
        }
    }
#endif
}