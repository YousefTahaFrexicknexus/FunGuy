using System;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using UnityEngine;

public class PlayFabSaveProvider : ISaveProvider
{
    private const string SaveKey = "PlayerSaveData";

    public void Save(PlayerSaveData data, Action onSuccess, Action<string> onError)
    {
        string json = JsonUtility.ToJson(data);

        var request = new UpdateUserDataRequest
        {
            Data = new Dictionary<string, string>
            {
                { SaveKey, json }
            }
        };

        PlayFabClientAPI.UpdateUserData(
            request,
            result => onSuccess?.Invoke(),
            error => onError?.Invoke(error.ErrorMessage)
        );
    }

    public void Load(Action<PlayerSaveData> onSuccess, Action<string> onError)
    {
        PlayFabClientAPI.GetUserData(
            new GetUserDataRequest(),
            result =>
            {
                if (result.Data == null || !result.Data.ContainsKey(SaveKey))
                {
                    onSuccess?.Invoke(new PlayerSaveData());
                    return;
                }

                string json = result.Data[SaveKey].Value;
                PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);

                onSuccess?.Invoke(data);
            },
            error => onError?.Invoke(error.ErrorMessage)
        );
    }
}