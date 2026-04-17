using System.Collections.Generic;
using UnityEngine;

public class MushroomSpawner_XAxis : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform mushroomParent;
    [SerializeField] Transform playerRoot;
    [SerializeField] GameObject mushroomPrefab;

    [Header("Spawn Count")]
    [SerializeField] int initialCount = 12;
    [SerializeField] int keepAliveCount = 18;

    [Header("Path (World Z forward)")]
    [SerializeField] float startZOffset = 0f;
    [SerializeField] float spawnAheadDistance = 35f;
    [SerializeField] float despawnBehindDistance = 15f;

    [Header("Randomization")]
    [SerializeField] float xRange = 2.2f;
    [SerializeField] Vector2 yRange = new Vector2(-0.4f, 0.4f);

    [Header("Reachability (IMPORTANT)")]
    [Tooltip("Minimum gap between mushrooms on Z.")]
    [SerializeField] float minZGap = 2.2f;

    [Tooltip("Maximum gap between mushrooms on Z (keep <= what the player can reach).")]
    [SerializeField] float maxZGap = 4.2f;

    [Tooltip("Max sideways offset difference between consecutive mushrooms.")]
    [SerializeField] float maxDeltaX = 1.6f;

    [Tooltip("Max vertical offset difference between consecutive mushrooms.")]
    [SerializeField] float maxDeltaY = 0.45f;

    [Header("Pooling (optional)")]
    [SerializeField] bool usePooling = true;

    readonly Queue<GameObject> pool = new();
    readonly LinkedList<Transform> alive = new();

    float lastZ;
    Vector3 lastPos;

    void Start()
    {
        // Start spawning from player position (or spawner position if you prefer)
        var basePos = playerRoot ? playerRoot.position : transform.position;
        lastZ = basePos.z + startZOffset;
        lastPos = new Vector3(basePos.x, basePos.y, lastZ);

        // Spawn initial chain
        for (int i = 0; i < initialCount; i++)
        {
            SpawnNext();
        }

        // Ensure we have enough alive
        while (alive.Count < keepAliveCount)
        {
            SpawnNext();
        }
    }

    void Update()
    {
        if (!playerRoot)
        {
            return;
        }

        // Keep spawning ahead
        while (lastZ < playerRoot.position.z + spawnAheadDistance)
        {
            SpawnNext();
        }

        // Despawn behind player
        while (alive.Count > 0)
        {
            Transform first = alive.First.Value;

            if (first.position.z < playerRoot.position.z - despawnBehindDistance)
            {
                Despawn(first.gameObject);
                alive.RemoveFirst();
            }
            else
            {
                break;
            }
        }

        // Keep alive count stable (optional)
        while (alive.Count < keepAliveCount)
        {
            SpawnNext();
        }
    }

    void SpawnNext()
    {
        float zGap = Random.Range(minZGap, maxZGap);
        lastZ += zGap;

        // Make next X/Y based on last position, clamped for reachability
        float targetX = Mathf.Clamp(
            lastPos.x + Random.Range(-maxDeltaX, maxDeltaX),
            -xRange, xRange);

        float targetY = Mathf.Clamp(
            lastPos.y + Random.Range(-maxDeltaY, maxDeltaY),
            yRange.x, yRange.y);

        Vector3 pos = new Vector3(targetX, targetY, lastZ);

        GameObject go = Spawn(pos);
        alive.AddLast(go.transform);

        lastPos = pos;
    }

    GameObject Spawn(Vector3 position)
    {
        GameObject go;

        if (usePooling && pool.Count > 0)
        {
            go = pool.Dequeue();
            go.SetActive(true);
        }
        else
        {
            go = Instantiate(mushroomPrefab, mushroomParent);
        }

        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
        go.transform.localScale = Random.Range(1, 1.5f) * Vector3.one; // Optional random scale for variety

        return go;
    }

    void Despawn(GameObject go)
    {
        if (usePooling)
        {
            go.SetActive(false);
            pool.Enqueue(go);
        }
        else
        {
            Destroy(go);
        }
    }

    // Handy for restart
    public void ResetSpawner()
    {
        foreach (var t in alive)
        {
            Despawn(t.gameObject);
        }

        alive.Clear();

        var basePos = playerRoot ? playerRoot.position : transform.position;
        lastZ = basePos.z + startZOffset;
        lastPos = new Vector3(basePos.x, basePos.y, lastZ);

        for (int i = 0; i < initialCount; i++)
        {
            SpawnNext();
        }

        while (alive.Count < keepAliveCount)
        {
            SpawnNext();
        }
    }
}