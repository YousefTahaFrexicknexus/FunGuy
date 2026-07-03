using System;

using UnityEngine;
using UnityEditor;

using PlayFab;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; set; }

    public PlayerSaveData Data { get; set; }

    ISaveProvider jsonProvider;
    ISaveProvider playFabProvider;

    [SerializeField] bool usePlayFab = true;
    [SerializeField] bool alsoSaveLocalBackup = true;

    [SerializeField] PlayerSaveData DebuggingData;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        jsonProvider = new JsonSaveProvider();
        playFabProvider = new PlayFabSaveProvider();

        Data = new PlayerSaveData();
    }

    public void Load()
    {
        if (usePlayFab && PlayFabClientAPI.IsClientLoggedIn())
        {
            playFabProvider.Load(
                data =>
                {
                    Data = data;
                    Debug.Log("Loaded save from PlayFab.");

                },
                error =>
                {
                    Debug.LogWarning($"PlayFab load failed: {error}. Loading local backup.");

                    jsonProvider.Load(
                        data => Data = data,
                        localError => Debug.LogError($"Local load failed: {localError}")
                    );
                }
            );
        }
        else
        {
            jsonProvider.Load(
                data =>
                {
                    Data = data;
                    Debug.Log("Loaded save from JSON.");
                },
                error => Debug.LogError($"JSON load failed: {error}")
            );
        }

        Data.LogData();
    }

    public void Save()
    {
        if (usePlayFab && PlayFabClientAPI.IsClientLoggedIn())
        {
            playFabProvider.Save( Data, () =>
                {
                    Debug.Log("Saved to PlayFab.");

                    if (alsoSaveLocalBackup)
                    {
                        SaveLocalBackup();
                    }
                },
                error =>
                {
                    Debug.LogWarning($"PlayFab save failed: {error}. Saving locally.");
                    SaveLocalBackup();
                }
            );
        }
        else
        {
            SaveLocalBackup();
        }
    }

    void SaveLocalBackup()
    {
        jsonProvider.Save(
            Data,
            () => Debug.Log("Saved to local JSON."),
            error => Debug.LogError($"Local save failed: {error}")
        );
    }

    #if UNITY_EDITOR
    [CustomEditor(typeof(SaveManager))]
    public class SaveManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            SaveManager saveManager = (SaveManager) target;

            GUILayout.Space(10);

            if (GUILayout.Button("Save Data"))
            {
                saveManager.Save();
            }

            if (GUILayout.Button("Load Data"))
            {
                saveManager.Load();
            }
        }
    }
    #endif
}