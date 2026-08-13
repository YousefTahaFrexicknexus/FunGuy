using UnityEngine;

using PlayFab;
using PlayFab.ClientModels;

using System.Threading.Tasks;

public class PlayFabManager : Singleton<PlayFabManager>
{
    [Header("PlayFab Main References"), Space]
    [SerializeField] PlayFab_Authentication playfab_Authentication;
    [SerializeField] PlayFab_TitleDataRemoteConfig playfab_TitleDataRemoteConfig;
    [SerializeField] PlayFab_Leaderboards playFab_Leaderboards;

    async Task InitializePlayFab()
    {
        // TODO: check if initialization required
        // await playfab_Authentication.TryLogin();
        // await playfab_TitleDataRemoteConfig.InitializeAsync();
    }

    public Task TryLogin()
    {
        return playfab_Authentication.TryLogin();
    }

    public Task TryFetchTitleData()
    {
        return playfab_TitleDataRemoteConfig.InitializeAsync();
    }

    public Task TryFetchLeaderboardData()
    {
        return playFab_Leaderboards.GetLeaderboardAsync();
    }

    #region Title Data / Remote Config
    void GetTitleData()
    {
        PlayFabClientAPI.GetTitleData(new GetTitleDataRequest(), OnGetTitleDataSuccess, OnGetTitleDataError);
    }

    void OnGetTitleDataSuccess(GetTitleDataResult result)
    {
        // Handle success
        Debug.Log("Title Data retrieved successfully.");

        foreach (var entry in result.Data)
        {
            Debug.Log($"Key: {entry.Key}, Value: {entry.Value}");
        }
    }

    void OnGetTitleDataError(PlayFabError error)
    {
        // Handle error
        Debug.LogError("Error retrieving Title Data: " + error.ErrorMessage);
    }
    #endregion --- Title Data / Remote Config ---
}