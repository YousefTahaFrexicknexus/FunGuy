using System;
using System.Collections.Generic;

[Serializable]
public class PlayerProgressionData
{
    public string SelectedCharacterId;
    public int Coins;

    public List<string> UnlockedCharacters = new();

    public List<UpgradeSaveData> Upgrades = new();
}