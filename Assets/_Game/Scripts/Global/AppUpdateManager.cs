using UnityEngine;

public class AppUpdateManager : MonoBehaviour
{
    [Header("Main Properties")]
    [SerializeField] string android_Link = "";
    [SerializeField] string iOS_Link = "";

    [Header("Config keys"), Space]
    private const string MINIMUM_REQUIRED_GAME_VERSION_CONFIG_KEY = "MinimumRequiredGameVersion";
    private const string IS_SOFT_GAME_UPDATE_CONFIG_KEY = "IsSoftGameUpdate";

    #region Singleton
    private static AppUpdateManager _instance;
    public static AppUpdateManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindObjectOfType<AppUpdateManager>();

            return _instance;
        }
    }
    #endregion

    public static bool isShowPopupOnLoginScreen = false;

    private const string OpenCounterKey = "ShowUpdateCounter";
    private const string LastShownAppVersionKey = "LastShownAppVersion";
    private const string LastShownServerVersionKey = "LastShownServerVersion";

    private int openCounter = 0;
    private int maxOpens = 0;

    public void CheckUpdate()
    {
        // StartCoroutine(FirebaseRemoteConfigManager.Instance.GetConfigData_Json_WaitForResponse(IS_SOFT_GAME_UPDATE_CONFIG_KEY, (isSoftGameUpdate) =>
        // {
        //     bool isSoftUpdate = isSoftGameUpdate.ToLower() == "true";

        //     string currentAppVersion = Application.version;

        //     StartCoroutine(FirebaseRemoteConfigManager.Instance.GetConfigData_Json_WaitForResponse(MINIMUM_REQUIRED_GAME_VERSION_CONFIG_KEY, (minimumRequiredGameVersion) =>
        //     {
        //         bool isNewVersionAvailable = IsVersionNewer(currentAppVersion, minimumRequiredGameVersion);

        //         // Hard Update
        //         if (isNewVersionAvailable && !isSoftUpdate)
        //         {
        //             ShowUpdatePopup();
        //             PlayerPrefs.SetInt(OpenCounterKey, 0);
        //             PlayerPrefs.Save();
        //             return;
        //         }

        //         if (isShowPopupOnLoginScreen)
        //         {
        //             return;
        //         }

        //         // Soft Update
        //         maxOpens = DataManager.Instance.gameSettingsData_SO.app_update.display_number;

        //         string lastServerVersion = PlayerPrefs.GetString(LastShownServerVersionKey, "");
        //         string lastAppVersion = PlayerPrefs.GetString(LastShownAppVersionKey, "");
        //         bool versionChanged = !minimumRequiredGameVersion.Equals(lastServerVersion) || !currentAppVersion.Equals(lastAppVersion);

        //         if (versionChanged)
        //         {
        //             openCounter = 0;
        //             PlayerPrefs.SetInt(OpenCounterKey, openCounter);
        //             PlayerPrefs.SetString(LastShownServerVersionKey, minimumRequiredGameVersion);
        //             PlayerPrefs.SetString(LastShownAppVersionKey, currentAppVersion);
        //         }
        //         else
        //         {
        //             openCounter = PlayerPrefs.GetInt(OpenCounterKey, 0);
        //         }

        //         if (isNewVersionAvailable && isSoftUpdate)
        //         {
        //             if (openCounter < maxOpens)
        //             {
        //                 ShowUpdatePopup();
        //                 openCounter++;
        //                 PlayerPrefs.SetInt(OpenCounterKey, openCounter);
        //                 PlayerPrefs.Save();
        //             }
        //         }
        //     }));
        // }));
    }

    private void ShowUpdatePopup()
    {
        // TODO: Integrate update popup
        // UIManager.Instance.Open_PopupsAndPanels(UIType.appUpdate_Popup);
    }

    public void PerformUpdate()
    {
        #if UNITY_ANDROID
            Application.OpenURL(android_Link);
        #elif UNITY_IOS
            Application.OpenURL(iOS_Link);
        #endif
    }

    private bool IsVersionNewer(string current, string required)
    {
        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(required))
        {
            Debug.LogError("Version strings cannot be null or empty.");
            return false;
        }

        var currentParts = current.Split('.');
        var requiredParts = required.Split('.');

        for (int i = 0; i < Mathf.Max(currentParts.Length, requiredParts.Length); i++)
        {
            int cur = 0;
            int req = 0;

            // Validate and parse current version part
            if (i < currentParts.Length && !int.TryParse(currentParts[i], out cur))
            {
                Debug.LogError($"Invalid version format in 'current': {current}");
                return false;
            }

            // Validate and parse required version part
            if (i < requiredParts.Length && !int.TryParse(requiredParts[i], out req))
            {
                Debug.LogError($"Invalid version format in 'required': {required}");
                return false;
            }

            if (cur < req) return true;
            if (cur > req) return false;
        }

        return false;
    }
}
