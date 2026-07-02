using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using GooglePlayGames;

public class PlayFabLogin : MonoBehaviour
{
    public void Start()
    {
        if (string.IsNullOrEmpty(PlayFabSettings.staticSettings.TitleId))
        {
            /*
            Please change the titleId below to your own titleId from PlayFab Game Manager.
            If you have already set the value in the Editor Extensions, this can be skipped.
            */
            
            PlayFabSettings.staticSettings.TitleId = "42";
        }

        // ActivateGooglePlayGames();
        CustomIDLogin();
    }

    void ActivateGooglePlayGames()
    {
        PlayGamesPlatform.Activate();
    }

    void OnLoginSuccess(LoginResult result)
    {
        Debug.Log("Congratulations, Login successful!");
    }

    void OnLoginFailure(PlayFabError error)
    {
        Debug.LogWarning("Something went wrong with your first API call.  :(");
        Debug.LogError("Here's some debug information:");
        Debug.LogError(error.GenerateErrorReport());
    }

    public void LoginWithGooglePlay()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        Social.localUser.Authenticate(success =>
        {
            if (!success)
            {
                Debug.LogError("Google Play Games login failed.");
                return;
            }

            PlayGamesPlatform.Instance.RequestServerSideAccess(
                forceRefreshToken: false,
                authCode =>
                {
                    if (string.IsNullOrEmpty(authCode))
                    {
                        Debug.LogError("Failed to get Google server auth code.");
                        return;
                    }

                    LoginToPlayFab(authCode);
                });
        });
#else
        Debug.LogWarning("Google Play Games login only works on Android device builds.");
#endif
    }

    public void LoginWithGameCenter()
    {
#if UNITY_IOS && !UNITY_EDITOR
        GameCenterAuthBridge.GetCredential(OnCredentialReceived, OnError);
#else
        Debug.LogWarning("Game Center login only works on iOS device builds.");
#endif
    }

    void OnCredentialReceived(GameCenterCredential credential)
    {
        var request = new LoginWithGameCenterRequest
        {
            PlayerId = credential.playerId,
            PublicKeyUrl = credential.publicKeyUrl,
            Salt = credential.salt,
            Signature = credential.signature,
            Timestamp = credential.timestamp.ToString(),
            CreateAccount = true
        };

        PlayFabClientAPI.LoginWithGameCenter(
            request,
            result =>
            {
                Debug.Log("PlayFab Game Center login success");
                Debug.Log("PlayFabId: " + result.PlayFabId);
            },
            error =>
            {
                Debug.LogError(error.GenerateErrorReport());
            });
    }

    void OnError(string error)
    {
        Debug.LogError("Game Center auth failed: " + error);
    }

    void LoginToPlayFab(string serverAuthCode)
    {
        LoginWithGooglePlayGamesServicesRequest request = new LoginWithGooglePlayGamesServicesRequest
        {
            ServerAuthCode = serverAuthCode,
            CreateAccount = true
        };

        PlayFabClientAPI.LoginWithGooglePlayGamesServices(
            request,
            result =>
            {
                Debug.Log("PlayFab login success!");
                Debug.Log("PlayFabId: " + result.PlayFabId);
            },
            error =>
            {
                Debug.LogError("PlayFab login failed:");
                Debug.LogError(error.GenerateErrorReport());
            });
    }

    void CustomIDLogin()
    {
        LoginWithCustomIDRequest request = new LoginWithCustomIDRequest { CustomId = "GettingStartedGuide", CreateAccount = true};
        PlayFabClientAPI.LoginWithCustomID(request, OnLoginSuccess, OnLoginFailure);
    }
}