using UnityEngine;

using System;
using System.Threading.Tasks;

public class AppUpdateManager : MonoBehaviour
{
    [Header("Store Links")]
    [SerializeField] string android_Link = "";
    [SerializeField] string iOS_Link = "";

    [Header("Config Keys")]
    const string MINIMUM_REQUIRED_GAME_VERSION_CONFIG_KEY = "MinimumRequiredGameVersion";
    const string IS_SOFT_GAME_UPDATE_CONFIG_KEY = "IsSoftGameUpdate";

    [Header("Soft Update Settings")]
    [Tooltip("Maximum number of times the optional update popup is shown per version.")]
    [SerializeField] int softUpdateDisplayCount = 3;

    #region Singleton

    static AppUpdateManager _instance;

    public static AppUpdateManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<AppUpdateManager>();
            }

            return _instance;
        }
    }

    #endregion

    public bool isShowPopupOnLoginScreen = false;
    public AppUpdateCheckResult appUpdateCheckResult;

    const string OpenCounterKey = "ShowUpdateCounter";
    const string LastShownAppVersionKey = "LastShownAppVersion";
    const string LastShownServerVersionKey = "LastShownServerVersion";

    int openCounter;

    /// <summary>
    /// Use this method from UnityEvents or buttons.
    /// </summary>
    public async Task CheckUpdate()
    {
        try
        {
            await CheckUpdateAsync();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    public bool IsVersionUpToDate()
    {
        return appUpdateCheckResult == AppUpdateCheckResult.UpToDate;
    }

    /// <summary>
    /// Checks PlayFab Title Data and returns the update result.
    /// This method can be awaited by another initialization system.
    /// </summary>
    public async Task<AppUpdateCheckResult> CheckUpdateAsync()
    {
        if (PlayFab_TitleDataRemoteConfig.Instance == null)
        {
            Debug.LogError("PlayFab_TitleDataRemoteConfig instance was not found.");

            return appUpdateCheckResult = AppUpdateCheckResult.Failed;
        }

        string softUpdateValue = PlayFab_TitleDataRemoteConfig.Instance.GetConfigData_Json(IS_SOFT_GAME_UPDATE_CONFIG_KEY);

        string minimumRequiredVersion = PlayFab_TitleDataRemoteConfig.Instance.GetConfigData_Json(MINIMUM_REQUIRED_GAME_VERSION_CONFIG_KEY);

        if (!TryParseBoolean(softUpdateValue, out bool isSoftUpdate))
        {
            Debug.LogError($"Invalid Title Data value for '{IS_SOFT_GAME_UPDATE_CONFIG_KEY}': '{softUpdateValue}'.");

            return appUpdateCheckResult = AppUpdateCheckResult.Failed;
        }

        if (string.IsNullOrWhiteSpace(minimumRequiredVersion))
        {
            Debug.LogError($"PlayFab Title Data key " + $"'{MINIMUM_REQUIRED_GAME_VERSION_CONFIG_KEY}' is empty.");

            return appUpdateCheckResult = AppUpdateCheckResult.Failed;
        }

        string currentAppVersion = Application.version.Trim();
        minimumRequiredVersion = minimumRequiredVersion.Trim();

        bool isNewVersionAvailable = IsVersionNewer(currentAppVersion, minimumRequiredVersion);

        if (!isNewVersionAvailable)
        {
            Debug.Log($"App is up to date. Current: {currentAppVersion}, " + $"Required: {minimumRequiredVersion}");

            ResetPopupTrackingIfVersionChanged(currentAppVersion, minimumRequiredVersion);

            return appUpdateCheckResult = AppUpdateCheckResult.UpToDate;
        }

        // Hard update
        if (!isSoftUpdate)
        {
            Debug.LogWarning($"Hard update required. Current: {currentAppVersion}, Required: {minimumRequiredVersion}");

            ShowUpdatePopup();

            PlayerPrefs.SetInt(OpenCounterKey, 0);
            PlayerPrefs.SetString(LastShownAppVersionKey, currentAppVersion);
            PlayerPrefs.SetString(LastShownServerVersionKey, minimumRequiredVersion);
            PlayerPrefs.Save();

            return appUpdateCheckResult = AppUpdateCheckResult.HardUpdateRequired;
        }

        // Do not show another optional popup when one is already being
        // handled on the login screen.
        if (isShowPopupOnLoginScreen)
        {
            return appUpdateCheckResult = AppUpdateCheckResult.SoftUpdateAvailable;
        }

        HandleSoftUpdate(currentAppVersion, minimumRequiredVersion);

        return appUpdateCheckResult = AppUpdateCheckResult.SoftUpdateAvailable;
    }

    void HandleSoftUpdate(string currentAppVersion, string minimumRequiredVersion)
    {
        bool versionChanged = ResetPopupTrackingIfVersionChanged(currentAppVersion, minimumRequiredVersion);

        if (!versionChanged)
        {
            openCounter = PlayerPrefs.GetInt(OpenCounterKey, 0);
        }

        if (openCounter >= softUpdateDisplayCount)
        {
            Debug.Log($"Soft update popup display limit reached: {openCounter}/{softUpdateDisplayCount}");

            return;
        }

        ShowUpdatePopup();

        openCounter++;

        PlayerPrefs.SetInt(OpenCounterKey, openCounter);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Resets the soft-update popup counter when either the installed
    /// version or the server version changes.
    /// </summary>
    bool ResetPopupTrackingIfVersionChanged(string currentAppVersion, string serverVersion)
    {
        string lastAppVersion = PlayerPrefs.GetString(LastShownAppVersionKey, "");

        string lastServerVersion = PlayerPrefs.GetString(LastShownServerVersionKey, "");

        bool versionChanged = !string.Equals(currentAppVersion, lastAppVersion, StringComparison.Ordinal)
                            || !string.Equals(serverVersion, lastServerVersion, StringComparison.Ordinal);

        if (!versionChanged)
        {
            return false;
        }

        openCounter = 0;

        PlayerPrefs.SetInt(OpenCounterKey, openCounter);
        PlayerPrefs.SetString(LastShownAppVersionKey, currentAppVersion);
        PlayerPrefs.SetString(LastShownServerVersionKey, serverVersion);
        PlayerPrefs.Save();

        return true;
    }

    public void ShowUpdatePopup()
    {
        FindAnyObjectByType<AppUpdate_Popup>(FindObjectsInactive.Include)?.Init();
        UIManager.Instance.Open_PopupsAndPanels(UIType.appUpdate_Popup);
    }

    public void PerformUpdate()
    {
#if UNITY_ANDROID
        if (string.IsNullOrWhiteSpace(android_Link))
        {
            Debug.LogError("Android store link is empty.");
            return;
        }

        Application.OpenURL(android_Link);

#elif UNITY_IOS
        if (string.IsNullOrWhiteSpace(iOS_Link))
        {
            Debug.LogError("iOS store link is empty.");
            return;
        }

        Application.OpenURL(iOS_Link);

#else
        Debug.LogWarning("App update links are only configured for Android and iOS.");
#endif
    }

    bool TryParseBoolean(string value, out bool result)
    {
        result = false;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        value = value.Trim();

        if (bool.TryParse(value, out result))
        {
            return true;
        }

        if (value == "1")
        {
            result = true;
            return true;
        }

        if (value == "0")
        {
            result = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns true when the required version is newer than the
    /// installed application version.
    /// </summary>
    bool IsVersionNewer(string current, string required)
    {
        if (!TryParseVersion(current, out Version currentVersion))
        {
            Debug.LogError($"Invalid current application version: '{current}'.");

            return false;
        }

        if (!TryParseVersion(required, out Version requiredVersion))
        {
            Debug.LogError($"Invalid required application version: '{required}'.");

            return false;
        }

        return requiredVersion > currentVersion;
    }

    bool TryParseVersion(string versionString, out Version version)
    {
        version = null;

        if (string.IsNullOrWhiteSpace(versionString))
        {
            return false;
        }

        string normalizedVersion = versionString.Trim();

        // Supports Title Data values such as "v1.2.3".
        if (normalizedVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalizedVersion = normalizedVersion.Substring(1);
        }

        return Version.TryParse(normalizedVersion, out version);
    }
}

public enum AppUpdateCheckResult
{
    Failed,
    UpToDate,
    SoftUpdateAvailable,
    HardUpdateRequired
}