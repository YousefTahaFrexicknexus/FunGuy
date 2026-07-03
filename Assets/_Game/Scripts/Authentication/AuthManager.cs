using System;

using UnityEngine;
using UnityEngine.UI;

using PlayFab;
using PlayFab.ClientModels;

#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

/// <summary>
/// Performs PlayFab authentication using the highest-priority provider available for the current platform.
/// </summary>
public sealed class AuthManager : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField]
    TMPro.TextMeshProUGUI statusLabel;

    [SerializeField]
    Button retryButton;

    [Header("iOS")]
    [SerializeField]
    bool useGamePlayerIdForAppleArcade;

    bool _isAuthenticating;
    bool _guestFallbackStarted;
    bool _loginCompleted;

    const string LogPrefix = "[AuthManager]";

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void PlayFabAuth_RequestGameCenterIdentityVerification(
        string gameObjectName,
        string successCallbackName,
        string errorCallbackName);
#endif

    [Serializable]
    sealed class GameCenterIdentityPayload
    {
        public string playerId;
        public string teamPlayerId;
        public string gamePlayerId;
        public string publicKeyUrl;
        public string signature;
        public string salt;
        public string timestamp;
    }

    void Awake()
    {
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(RetryAuthentication);
            retryButton.onClick.AddListener(RetryAuthentication);
            retryButton.gameObject.SetActive(false);
        }

        UpdateStatus("Ready to sign in.");
    }

    void Start()
    {
        BeginAuthentication();
    }

    void OnDestroy()
    {
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(RetryAuthentication);
        }
    }

    /// <summary>
    /// Starts the full authentication flow for the active platform.
    /// </summary>
    public void BeginAuthentication()
    {
        if (_isAuthenticating)
        {
            return;
        }

        HideRetryButton();

        _isAuthenticating = true;
        _guestFallbackStarted = false;
        _loginCompleted = false;

        if (!IsPlayFabConfigured())
        {
            FailAuthentication("PlayFab TitleId is missing. Configure the PlayFab SDK settings and try again.");
            return;
        }

        UpdateStatus("Signing in...");

#if UNITY_ANDROID
        BeginAndroidAuthentication();
#elif UNITY_IOS
        BeginIosAuthentication();
#else
        BeginGuestAuthentication("Platform sign-in is not available on this build.");
#endif
    }

    /// <summary>
    /// Retries the full authentication flow after a failure.
    /// </summary>
    public void RetryAuthentication()
    {
        BeginAuthentication();
    }

    #region Android Authentication

    void BeginAndroidAuthentication()
    {
#if UNITY_ANDROID
        UpdateStatus("Signing in with Google Play Games...");

        if (!IsGooglePlayGamesConfigured(out string configurationIssue))
        {
            FallBackToGuest("Google Play Games", configurationIssue);
            return;
        }

        try
        {
            PlayGamesPlatform.DebugLogEnabled = Debug.isDebugBuild;
            PlayGamesPlatform.Activate();

            PlayGamesPlatform.Instance.Authenticate(status =>
            {
                if (status != SignInStatus.Success || !PlayGamesPlatform.Instance.IsAuthenticated())
                {
                    FallBackToGuest("Google Play Games", $"Silent sign-in returned {status}.");
                    return;
                }

                UpdateStatus("Authorizing Google Play Games with PlayFab...");

                try
                {
                    PlayGamesPlatform.Instance.RequestServerSideAccess(false, serverAuthCode =>
                    {
                        if (string.IsNullOrWhiteSpace(serverAuthCode))
                        {
                            FallBackToGuest("Google Play Games", "The plugin did not return a server auth code.");
                            return;
                        }

                        LoginWithGooglePlayGames(serverAuthCode);
                    });
                }
                catch (Exception exception)
                {
                    FallBackToGuest("Google Play Games", "RequestServerSideAccess threw an exception.", exception);
                }
            });
        }
        catch (Exception exception)
        {
            FallBackToGuest("Google Play Games", "Google Play Games initialization failed.", exception);
        }
#endif
    }

    void LoginWithGooglePlayGames(string serverAuthCode)
    {
#if UNITY_ANDROID
        var request = new LoginWithGooglePlayGamesServicesRequest
        {
            CreateAccount = true,
            ServerAuthCode = serverAuthCode,
            TitleId = PlayFabSettings.TitleId
        };

        PlayFabClientAPI.LoginWithGooglePlayGamesServices(
            request,
            result => HandleLoginSuccess(result, "Google Play Games", "Signed in via Google Play Games"),
            error => FallBackToGuestFromPlayFab("Google Play Games", $"PlayFab login failed: {error.ErrorMessage}", error));
#endif
    }

    bool IsGooglePlayGamesConfigured(out string issue)
    {
#if UNITY_ANDROID
        if (!GameInfo.ApplicationIdInitialized())
        {
            issue = "Google Play Games APP_ID is still using the generated placeholder. Run the Android setup step.";
            return false;
        }

        if (!GameInfo.WebClientIdInitialized())
        {
            issue = "Google Play Games WEB_CLIENTID is still using the generated placeholder. Configure the Web client ID for server auth code exchange.";
            return false;
        }
#endif

        issue = string.Empty;
        return true;
    }

    #endregion

    #region iOS Authentication

    void BeginIosAuthentication()
    {
        UpdateStatus("Signing in with Game Center...");

        try
        {
            Social.localUser.Authenticate(success =>
            {
                if (!success)
                {
                    FallBackToGuest("Game Center", "The player is not signed into Game Center or dismissed the sign-in prompt.");
                    return;
                }

                UpdateStatus("Authorizing Game Center with PlayFab...");
                RequestGameCenterIdentityVerification();
            });
        }
        catch (Exception exception)
        {
            FallBackToGuest("Game Center", "Game Center authentication threw an exception.", exception);
        }
    }

    void RequestGameCenterIdentityVerification()
    {
#if UNITY_IOS && !UNITY_EDITOR
        try
        {
            PlayFabAuth_RequestGameCenterIdentityVerification(
                gameObject.name,
                nameof(OnGameCenterIdentitySuccess),
                nameof(OnGameCenterIdentityError));
        }
        catch (Exception exception)
        {
            FallBackToGuest("Game Center", "The native Game Center identity request failed to start.", exception);
        }
#else
        FallBackToGuest("Game Center", "Secure Game Center verification is only available on an iOS device.");
#endif
    }

    /// <summary>
    /// Receives the native Game Center identity verification payload and forwards it to PlayFab.
    /// </summary>
    /// <param name="json">Serialized Game Center identity verification data.</param>
    public void OnGameCenterIdentitySuccess(string json)
    {
        try
        {
            var payload = JsonUtility.FromJson<GameCenterIdentityPayload>(json);
            var playerId = ResolveGameCenterPlayerId(payload);

            if (payload == null ||
                string.IsNullOrWhiteSpace(playerId) ||
                string.IsNullOrWhiteSpace(payload.publicKeyUrl) ||
                string.IsNullOrWhiteSpace(payload.signature) ||
                string.IsNullOrWhiteSpace(payload.salt) ||
                string.IsNullOrWhiteSpace(payload.timestamp))
            {
                FallBackToGuest("Game Center", "The native Game Center payload was incomplete.");
                return;
            }

            var request = new LoginWithGameCenterRequest
            {
                CreateAccount = true,
                PlayerId = playerId,
                PublicKeyUrl = payload.publicKeyUrl,
                Salt = payload.salt,
                Signature = payload.signature,
                Timestamp = payload.timestamp,
                TitleId = PlayFabSettings.TitleId
            };

            PlayFabClientAPI.LoginWithGameCenter(
                request,
                result => HandleLoginSuccess(result, "Game Center", "Signed in via Game Center"),
                error => FallBackToGuestFromPlayFab("Game Center", $"PlayFab login failed: {error.ErrorMessage}", error));
        }
        catch (Exception exception)
        {
            FallBackToGuest("Game Center", "Game Center payload parsing failed.", exception);
        }
    }

    /// <summary>
    /// Receives an error from the native Game Center identity verification bridge.
    /// </summary>
    /// <param name="message">The native error message.</param>
    public void OnGameCenterIdentityError(string message)
    {
        FallBackToGuest("Game Center", message);
    }

    string ResolveGameCenterPlayerId(GameCenterIdentityPayload payload)
    {
        if (payload == null)
        {
            return string.Empty;
        }

        if (useGamePlayerIdForAppleArcade && !string.IsNullOrWhiteSpace(payload.gamePlayerId))
        {
            return payload.gamePlayerId;
        }

        if (!string.IsNullOrWhiteSpace(payload.teamPlayerId))
        {
            return payload.teamPlayerId;
        }

        if (!string.IsNullOrWhiteSpace(payload.gamePlayerId))
        {
            return payload.gamePlayerId;
        }

        return payload.playerId;
    }

    #endregion

    #region Guest Authentication

    void BeginGuestAuthentication(string reason)
    {
        if (_guestFallbackStarted || _loginCompleted)
        {
            return;
        }

        _guestFallbackStarted = true;

        Debug.LogWarning($"{LogPrefix} Falling back to guest login. Reason: {reason}");
        UpdateStatus("Signing in as Guest...");

        var customId = SystemInfo.deviceUniqueIdentifier;
        if (string.IsNullOrWhiteSpace(customId))
        {
            FailAuthentication("Guest sign-in failed because this device does not expose a unique identifier.");
            return;
        }

        var request = new LoginWithCustomIDRequest
        {
            CreateAccount = true,
            CustomId = customId,
            TitleId = PlayFabSettings.TitleId
        };

        PlayFabClientAPI.LoginWithCustomID(
            request,
            result => HandleLoginSuccess(result, "Guest", "Signed in as Guest"),
            error => HandleGuestLoginFailure(error));
    }

    void HandleGuestLoginFailure(PlayFabError error)
    {
        Debug.LogError($"{LogPrefix} Guest login failed.\n{error.GenerateErrorReport()}");
        FailAuthentication(BuildFriendlyFailureMessage("Guest sign-in failed", error));
    }

    #endregion

    #region Shared Helpers

    void HandleLoginSuccess(LoginResult result, string authProvider, string successStatus)
    {
        _loginCompleted = true;
        _isAuthenticating = false;

        HideRetryButton();

        Debug.Log($"{LogPrefix} Successfully authenticated via {authProvider}.");
        Debug.Log($"{LogPrefix} PlayFabId: {result.PlayFabId}");
        Debug.Log($"{LogPrefix} SessionTicket: {result.SessionTicket}");

        UpdateStatus($"{successStatus}\nPlayFabId: {result.PlayFabId}");
    }

    void FallBackToGuest(string provider, string reason)
    {
        Debug.LogWarning($"{LogPrefix} {provider} sign-in failed and will fall back to guest. Reason: {reason}");
        UpdateStatus($"{provider} unavailable. Falling back to Guest...");
        BeginGuestAuthentication(reason);
    }

    void FallBackToGuest(string provider, string reason, Exception exception)
    {
        if (exception != null)
        {
            Debug.LogWarning($"{LogPrefix} {provider} sign-in failed and will fall back to guest.\nReason: {reason}\n{exception}");
        }
        else
        {
            Debug.LogWarning($"{LogPrefix} {provider} sign-in failed and will fall back to guest. Reason: {reason}");
        }

        UpdateStatus($"{provider} unavailable. Falling back to Guest...");
        BeginGuestAuthentication(reason);
    }

    void FallBackToGuestFromPlayFab(string provider, string reason, PlayFabError error)
    {
        if (error != null)
        {
            Debug.LogWarning($"{LogPrefix} {provider} PlayFab login failed and will fall back to guest.\n{error.GenerateErrorReport()}");
        }
        else
        {
            Debug.LogWarning($"{LogPrefix} {provider} sign-in failed and will fall back to guest. Reason: {reason}");
        }

        UpdateStatus($"{provider} unavailable. Falling back to Guest...");
        BeginGuestAuthentication(reason);
    }

    bool IsPlayFabConfigured()
    {
        return !string.IsNullOrWhiteSpace(PlayFabSettings.TitleId);
    }

    void FailAuthentication(string message)
    {
        _isAuthenticating = false;
        _loginCompleted = false;
        _guestFallbackStarted = false;

        Debug.LogError($"{LogPrefix} {message}");
        UpdateStatus(message);
        ShowRetryButton();
    }

    void UpdateStatus(string message)
    {
        if (statusLabel != null)
        {
            statusLabel.text = message;
        }

        Debug.Log($"{LogPrefix} Status: {message}");
    }

    void ShowRetryButton()
    {
        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(true);
        }
    }

    void HideRetryButton()
    {
        if (retryButton != null)
        {
            retryButton.gameObject.SetActive(false);
        }
    }

    static string BuildFriendlyFailureMessage(string prefix, PlayFabError error)
    {
        if (error == null)
        {
            return prefix + ". Please try again.";
        }

        if (!string.IsNullOrWhiteSpace(error.ErrorMessage))
        {
            return $"{prefix}: {error.ErrorMessage}";
        }

        return $"{prefix}: {error.Error}";
    }

    #endregion
}
