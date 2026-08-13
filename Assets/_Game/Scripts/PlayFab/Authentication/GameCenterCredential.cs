[System.Serializable]
public class GameCenterCredential
{
    public string playerId;
    public string publicKeyUrl;
    public string salt;
    public string signature;
    public uint timestamp;
}