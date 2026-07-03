using System;
using System.IO;
using UnityEngine;

public class JsonSaveProvider : ISaveProvider
{
    private readonly string savePath;

    public JsonSaveProvider()
    {
        savePath = Path.Combine(Application.persistentDataPath, "player_save.json");
    }

    public void Save(PlayerSaveData data, Action onSuccess, Action<string> onError)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(savePath, json);

            onSuccess?.Invoke();
        }
        catch (Exception e)
        {
            onError?.Invoke(e.Message);
        }
    }

    public void Load(Action<PlayerSaveData> onSuccess, Action<string> onError)
    {
        try
        {
            if (!File.Exists(savePath))
            {
                onSuccess?.Invoke(new PlayerSaveData());
                return;
            }

            string json = File.ReadAllText(savePath);
            PlayerSaveData data = JsonUtility.FromJson<PlayerSaveData>(json);

            onSuccess?.Invoke(data);
        }
        catch (Exception e)
        {
            onError?.Invoke(e.Message);
        }
    }
}