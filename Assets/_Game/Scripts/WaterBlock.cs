using System.Collections;
using UnityEngine;

public class WaterBlock : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            OnPlayerEnteredWater();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            OnPlayerExitedWater();
        }
    }

    void OnPlayerEnteredWater()
    {
        GameplayManager.Instance.SetIsPlayerInWater(true);
    }

    void OnPlayerExitedWater()
    {
        GameplayManager.Instance.SetIsPlayerInWater(false);
    }
}