using UnityEngine;

public class GameManager : MonoBehaviour
{
#region Instance
    private static GameManager _instance;

    public static GameManager Instance
    {
        get
        {
            if (!_instance)
                _instance = GameObject.FindAnyObjectByType<GameManager>();
            return _instance;
        }
    }
#endregion --- Instance ---

    CreatureBouncer creatureBouncer;
    Vector3 playerStartPos;

    void Awake()
    {
        if(!creatureBouncer)
        {
            creatureBouncer = FindAnyObjectByType<CreatureBouncer>();
        }

        playerStartPos = creatureBouncer.transform.position;
    }

    public void ResetPlayerPosition()
    {
        creatureBouncer.transform.position = playerStartPos;
    }
}

public enum GameState
{
    Menu,
    Playing,
    GameOver
}