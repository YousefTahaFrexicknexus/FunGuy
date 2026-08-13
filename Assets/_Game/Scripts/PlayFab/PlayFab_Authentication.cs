using UnityEngine;

using System;
using System.Threading.Tasks;

using PlayFab;
using PlayFab.ClientModels;
using GooglePlayGames;

public class PlayFab_Authentication : MonoBehaviour
{
    public Task<LoginResult> TryLogin()
    {
        var tcs = new TaskCompletionSource<LoginResult>();

        if (Application.platform == RuntimePlatform.Android)
        {
            LoginWithGooglePlay
            (
                result =>
                {
                    tcs.SetResult(result);
                },
                error =>
                {
                    tcs.SetException(new Exception(error.GenerateErrorReport()));
                }
            );
        }
        else if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            LoginWithGameCenter
            (
                result=>
                {
                    tcs.SetResult(result);
                },
                error =>
                {
                    tcs.SetException(new Exception(error.GenerateErrorReport()));
                }
            );
        }
        else
        {
            CustomIDLogin
            (
                result =>
                {
                    tcs.SetResult(result);
                },
                error =>
                {
                    tcs.SetException(new Exception(error.GenerateErrorReport()));
                }
            );
        }

        return tcs.Task;
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

    public void LoginWithGooglePlay(Action<LoginResult> onSuccess, Action<PlayFabError> onFailure)
    {
    #if UNITY_ANDROID && !UNITY_EDITOR

        Social.localUser.Authenticate(success =>
        {
            if (!success)
            {
                onFailure?.Invoke(new PlayFabError
                {
                    ErrorMessage = "Google Play authentication failed."
                });
                return;
            }

            PlayGamesPlatform.Instance.RequestServerSideAccess(false, authCode =>
            {
                if (string.IsNullOrEmpty(authCode))
                {
                    onFailure?.Invoke(new PlayFabError
                    {
                        ErrorMessage = "No auth code."
                    });
                    return;
                }

                LoginToPlayFab(authCode, onSuccess, onFailure);
            });
        });

    #endif
    }

    public void LoginWithGameCenter(Action<LoginResult> onSuccess, Action<PlayFabError> onFailure)
    {
    #if UNITY_IOS && !UNITY_EDITOR

        GameCenterAuthBridge.GetCredential(
            credential => OnCredentialReceived(credential, onSuccess, onFailure),
            error =>
            {
                onFailure?.Invoke(new PlayFabError
                {
                    ErrorMessage = error
                });
            });

    #else

        onFailure?.Invoke(new PlayFabError
        {
            ErrorMessage = "Game Center login only works on iOS device builds."
        });

    #endif
    }

    void OnCredentialReceived(GameCenterCredential credential)
    {
        LoginWithGameCenterRequest request = new LoginWithGameCenterRequest
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

    void LoginToPlayFab(string authCode, Action<LoginResult> onSuccess, Action<PlayFabError> onFailure)
    {
        var request = new LoginWithGooglePlayGamesServicesRequest
        {
            ServerAuthCode = authCode,
            CreateAccount = true
        };

        PlayFabClientAPI.LoginWithGooglePlayGamesServices(
            request,
            onSuccess,
            onFailure);
    }

    void OnCredentialReceived(GameCenterCredential credential, Action<LoginResult> onSuccess, Action<PlayFabError> onFailure)
    {
        LoginWithGameCenterRequest request = new LoginWithGameCenterRequest
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

                onSuccess?.Invoke(result);
            },
            error =>
            {
                Debug.LogError(error.GenerateErrorReport());

                onFailure?.Invoke(error);
            });
    }

    void CustomIDLogin(Action<LoginResult> onSuccess, Action<PlayFabError> onFailure)
    {
        LoginWithCustomIDRequest request = new LoginWithCustomIDRequest
        {
            CustomId = "GettingStartedGuide",
            CreateAccount = true
        };

        PlayFabClientAPI.LoginWithCustomID(
            request,
            result =>
            {
                Debug.Log("PlayFab Custom ID login success");
                Debug.Log("PlayFabId: " + result.PlayFabId);

                onSuccess?.Invoke(result);
            },
            error =>
            {
                Debug.LogError(error.GenerateErrorReport());

                onFailure?.Invoke(error);
            });
    }
}