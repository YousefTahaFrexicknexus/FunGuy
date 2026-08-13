using UnityEngine;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using PlayFab;
using PlayFab.ClientModels;

public class PlayFab_TitleDataRemoteConfig : MonoBehaviour
{
    [Header("Title Data Properties"), Space]
    [SerializeField] List<RemoteConfigProperties> remoteConfigProperties = new();

    readonly Dictionary<string, string> titleDataCache = new();

    bool fetchingConfig = false;
    public static bool isConfigFetched = false;

    [Header("Retry Settings"), Space]
    [SerializeField] int maxRetryAttempts = 3;
    [SerializeField] float initialRetryDelay = 2f;

    public static PlayFab_TitleDataRemoteConfig Instance { get; set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public async Task<bool> InitializeAsync()
    {
        Debug.Log("Fetching PlayFab Title Data...");

        bool success = await FetchTitleDataWithRetryAsync();

        if (!success)
        {
            ShowNoInternetConnectionPopup();
            return false;
        }

        return true;
    }

    async Task<bool> FetchTitleDataWithRetryAsync()
    {
        if (fetchingConfig)
        {
            Debug.LogWarning("Title Data fetch already in progress.");
            return false;
        }

        fetchingConfig = true;

        float retryDelay = initialRetryDelay;

        for (int attempt = 1; attempt <= maxRetryAttempts; attempt++)
        {
            Debug.Log($"Fetching PlayFab Title Data... Attempt {attempt}/{maxRetryAttempts}");

            try
            {
                Dictionary<string, string> data = await GetTitleDataAsync();

                ApplyTitleData(data);

                fetchingConfig = false;
                isConfigFetched = true;

                Debug.Log("PlayFab Title Data fetched successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Title Data fetch failed: {ex.Message}");

                if (attempt < maxRetryAttempts)
                {
                    Debug.LogWarning($"Retrying in {retryDelay} seconds...");
                    await Task.Delay(TimeSpan.FromSeconds(retryDelay));
                    retryDelay *= 2f;
                }
            }
        }

        fetchingConfig = false;
        isConfigFetched = false;

        Debug.LogError("Max retry attempts reached. PlayFab Title Data fetch failed.");
        return false;
    }

    Task<Dictionary<string, string>> GetTitleDataAsync()
    {
        var tcs = new TaskCompletionSource<Dictionary<string, string>>();

        var request = new GetTitleDataRequest();

        PlayFabClientAPI.GetTitleData(
            request,
            result =>
            {
                tcs.SetResult(result.Data ?? new Dictionary<string, string>());
            },
            error =>
            {
                tcs.SetException(new Exception(error.GenerateErrorReport()));
            });

        return tcs.Task;
    }

    void ApplyTitleData(Dictionary<string, string> data)
    {
        titleDataCache.Clear();
        remoteConfigProperties.Clear();

        foreach (var pair in data)
        {
            titleDataCache[pair.Key] = pair.Value;
            remoteConfigProperties.Add(new RemoteConfigProperties(pair.Key, pair.Value));
        }
    }

    public string GetConfigData_Json(string key)
    {
        if (!isConfigFetched)
        {
            Debug.LogWarning("Title Data has not been fetched yet.");
            return string.Empty;
        }

        if (titleDataCache.TryGetValue(key, out string value))
        {
            return value;
        }

        Debug.LogError($"Title Data key '{key}' not found.");
        return string.Empty;
    }

    public async Task<string> GetConfigData_Json_Async(string key)
    {
        if (!isConfigFetched)
        {
            await InitializeAsync();
        }

        return GetConfigData_Json(key);
    }

    void ShowNoInternetConnectionPopup()
    {
        UIManager.Instance.Open_PopupsAndPanels(UIType.noInternet, true);
    }
}

[Serializable]
public class RemoteConfigProperties
{
    public string Key;
    public string Value;

    public RemoteConfigProperties(string key, string value)
    {
        Key = key;
        Value = value;
    }
}