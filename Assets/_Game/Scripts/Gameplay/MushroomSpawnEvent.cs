using UnityEngine;
using System;

public static class MushroomSpawnEvent
{
    public static Action<Transform> OnMushroomSpawned;

    public static void Invoke(Transform mushroom)
    {
        OnMushroomSpawned?.Invoke(mushroom);
    }
}