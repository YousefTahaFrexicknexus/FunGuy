using UnityEngine;
using System;
using System.Threading.Tasks;

using TMPro;

public class AppVersion_UI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] TMP_Text appVersionText;
    [SerializeField] TMP_Text statusText;

    [Header("PlayFab Title Data")]
    [SerializeField] string latestVersionKey = "version";

    async void Start()
    {
        await CheckVersionAsync();
    }

    public async Task CheckVersionAsync()
    {
        string currentVersion = Application.version;

        if (appVersionText != null)
        {
            appVersionText.text = $"Version {currentVersion}";
        }

        if (statusText != null)
        {
            statusText.text = "Checking version...";
        }

        string latestVersion = await PlayFab_TitleDataRemoteConfig.Instance.GetConfigData_Json_Async(latestVersionKey);

        if (string.IsNullOrEmpty(latestVersion))
        {
            SetStatus("Could not check app version.", false);
            return;
        }

        bool isUpToDate = IsVersionUpToDate(currentVersion, latestVersion);

        if (isUpToDate)
        {
            SetStatus("Up to date.", true);
        }
        else
        {
            SetStatus($"OUTDATED. \n  Latest version: \n {latestVersion}", false);
        }
    }
    
    bool IsVersionUpToDate(string currentVersion, string latestVersion)
    {
        Version current = new Version(currentVersion);
        Version latest = new Version(latestVersion);

        return current >= latest;
    }

    void SetStatus(string _message, bool _isUpToDate)
    {
        if (statusText != null)
        {
            statusText.text = _message;
            statusText.color = _isUpToDate ? Color.green : Color.red;
        }

        Debug.Log(_message);
    }
}