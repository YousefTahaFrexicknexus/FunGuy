using UnityEngine;
using System.Collections.Generic;

public class CollectableSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform collectableParent;
    [SerializeField] GameObject coinPrefab;

    [Header("Spawn Rules")]
    [SerializeField] float spawnChance = 0.8f;

    [SerializeField] int minimumCoins = 3;
    [SerializeField] int maximumCoins = 8;

    [Header("Arc Settings")]
    [SerializeField] float arcHeight = 3f;
    [SerializeField] float sideOffset = 0.5f;

    readonly List<GameObject> spawnedCollectables = new();

    Transform previousMushroom;


    void OnEnable()
    {
        MushroomSpawnEvent.OnMushroomSpawned += SpawnBetweenMushrooms;
    }

    void OnDisable()
    {
        MushroomSpawnEvent.OnMushroomSpawned -= SpawnBetweenMushrooms;
    }

    void SpawnBetweenMushrooms(Transform currentMushroom)
    {
        if(previousMushroom == null)
        {
            previousMushroom = currentMushroom;
            return;
        }

        if(Random.value > spawnChance)
        {
            previousMushroom = currentMushroom;
            return;
        }

        int amount = Random.Range(minimumCoins, maximumCoins + 1);

        for(int i = 0; i < amount; i++)
        {
            SpawnCoin(previousMushroom.position, currentMushroom.position, i, amount);
        }

        previousMushroom = currentMushroom;
    }

    void SpawnCoin(Vector3 start, Vector3 end, int index, int count)
    {
        GameObject coin = Instantiate(coinPrefab, collectableParent);

        float t = (float)index / (count - 1);

        // Straight line between mushrooms
        Vector3 position = Vector3.Lerp(start, end, t);


        // Parabolic jump arc
        float height = Mathf.Sin(t * Mathf.PI) * arcHeight;

        // Optional sideways curve variation
        float side = Mathf.Sin(t * Mathf.PI) * sideOffset;

        position.y += height;
        position.x += side;

        coin.transform.position = position;

        spawnedCollectables.Add(coin);
    }

    public void Clear()
    {
        foreach(GameObject item in spawnedCollectables)
        {
            Destroy(item);
        }

        spawnedCollectables.Clear();

        previousMushroom = null;
    }
}