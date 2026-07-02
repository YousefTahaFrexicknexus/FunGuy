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

    [Header("Managers")]
    public MushroomSpawner_XAxis mushroomSpawner;
    [SerializeField] Transform playerRoot;
    Vector3 playerStartPos;

    void Awake()
    {
        if (!playerRoot)
        {
            CreatureBouncer_XAxis xAxisBouncer = FindAnyObjectByType<CreatureBouncer_XAxis>();
            if (xAxisBouncer != null)
            {
                playerRoot = xAxisBouncer.transform;
            }
            else
            {
                CreatureBouncer legacyBouncer = FindAnyObjectByType<CreatureBouncer>();
                if (legacyBouncer != null)
                {
                    playerRoot = legacyBouncer.transform;
                }
            }
        }

        if (playerRoot == null)
        {
            Debug.LogWarning("GameManager could not find a player root to reset.");
            return;
        }

        playerStartPos = playerRoot.position;
    }

    public void ResetPlayerPosition()
    {
        if (playerRoot == null)
        {
            return;
        }

        Vector3 lastSpawnedPos = mushroomSpawner.GetLastSpawned().transform.position;
        playerRoot.position = new Vector3(lastSpawnedPos.x, 12f, lastSpawnedPos.z);
    }
}

public enum GameState
{
    Menu,
    Playing,
    GameOver
}
