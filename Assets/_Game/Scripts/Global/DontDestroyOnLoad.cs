using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DontDestroyOnLoad : MonoBehaviour
{
    // This method is called when the script instance is being loaded
    void Awake()
    {
        // Makes the GameObject this script is attached to not be destroyed when loading a new scene
        DontDestroyOnLoad(gameObject);
    }
}
