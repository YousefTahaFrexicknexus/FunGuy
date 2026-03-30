using UnityEngine;

// T must be a component that inherits from MonoBehaviour
public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    // The static reference to the instance
    public static T Instance { get; private set; }

    [Tooltip("If true, this object will persist across scene loads.")]
    [SerializeField] private bool _dontDestroyOnLoad = true;

    // Protected virtual allows you to override this in derived classes
    // (But remember to call base.Awake()!)
    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // If an instance already exists, destroy this new one
            Destroy(this.gameObject);
        }
        else
        {
            // Cast 'this' to T and assign it
            Instance = this as T;

            if (_dontDestroyOnLoad)
            {
                // Root GameObjects only can be DontDestroyOnLoad
                transform.SetParent(null); 
                DontDestroyOnLoad(this.gameObject);
            }
        }
    }
}