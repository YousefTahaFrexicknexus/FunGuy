using UnityEngine;
using System.Collections.Generic;

using PlayFab;
using PlayFab.ClientModels;

public class Leaderboard_Popup : MonoBehaviour
{
    [Header("Leaderboard")]
    [SerializeField] string leaderboardName = "Global";
    [SerializeField] int maxResults = 10;

    [Header("UI")]
    [SerializeField] Transform content;
    [SerializeField] Leaderboard_Cell leaderboardCellPrefab;

    [Header("Profile Images")]
    [SerializeField] ProfileImageDatabase profileDatabase;

    void OnEnable()
    {
        LoadLeaderboard();
    }

    public void LoadLeaderboard()
    {
        var request = new GetLeaderboardRequest
        {
            StatisticName = leaderboardName,
            StartPosition = 0,
            MaxResultsCount = maxResults
        };

        PlayFabClientAPI.GetLeaderboard(request, OnLeaderboardReceived, OnLeaderboardError );
    }

    void OnLeaderboardReceived(GetLeaderboardResult result)
    {
        ClearLeaderboard();

        foreach (PlayerLeaderboardEntry player in result.Leaderboard)
        {
            Leaderboard_Cell cell = Instantiate(leaderboardCellPrefab, content );

            int profileIndex = GetProfileIndex(player);

            cell.Setup(player.Position + 1, player.DisplayName, player.StatValue, profileDatabase.GetSprite(profileIndex) );
        }
    }

    int GetProfileIndex(PlayerLeaderboardEntry player)
    {
        // if (player.Profile == null || player.Profile.Data == null || !player.Profile.Data.ContainsKey("ProfileIndex"))
        // {
        //     return 0;
        // }

        // return int.Parse(player.Profile.Data["ProfileIndex"].Value );

        return 0;
    }

    void ClearLeaderboard()
    {
        foreach (Transform child in content)
        {
            Destroy(child.gameObject);
        }
    }

    void OnLeaderboardError(PlayFabError error)
    {
        Debug.LogError("Leaderboard Error: " + error.GenerateErrorReport() );
    }
}