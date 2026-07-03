using System;

public interface ISaveProvider
{
    void Save(PlayerSaveData data, Action onSuccess, Action<string> onError);
    void Load(Action<PlayerSaveData> onSuccess, Action<string> onError);
}