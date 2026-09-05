using UnityEngine;

[DisallowMultipleComponent]
public sealed class GridCourseRunResetBridge : MonoBehaviour
{
    [SerializeField, Tooltip("Grid streamer rebuilt when the run resets.")]
    GridCourseStreamer gridStreamer;
    [SerializeField, Tooltip("If enabled, the grid is also rebuilt when the run starts.")]
    bool rebuildOnRunStarted;
    [SerializeField, Tooltip("If enabled, the grid is rebuilt when the run resets after death or manual reset.")]
    bool rebuildOnRunReset = true;
    [SerializeField, Tooltip("If enabled, the run spawn Z is used as the grid start Z.")]
    bool useRunSpawnZ = true;
    [SerializeField, Tooltip("Extra Z offset added to the grid rebuild start.")]
    float startZOffset;

    void Reset()
    {
        gridStreamer = GetComponent<GridCourseStreamer>();
    }

    void Awake()
    {
        ResolveGridStreamer();
    }

    void OnEnable()
    {
        MushroomRunnerEvents.RunStarted += HandleRunStarted;
        MushroomRunnerEvents.RunReset += HandleRunReset;
    }

    void OnDisable()
    {
        MushroomRunnerEvents.RunStarted -= HandleRunStarted;
        MushroomRunnerEvents.RunReset -= HandleRunReset;
    }

    public void ResetGrid(float startZ = 0f)
    {
        if (!ResolveGridStreamer())
        {
            return;
        }

        gridStreamer.BuildInitialGrid(startZ);
    }

    void HandleRunStarted(RunLifecycleEvent eventData)
    {
        if (rebuildOnRunStarted)
        {
            RebuildFromRun(eventData);
        }
    }

    void HandleRunReset(RunLifecycleEvent eventData)
    {
        if (rebuildOnRunReset)
        {
            RebuildFromRun(eventData);
        }
    }

    void RebuildFromRun(RunLifecycleEvent eventData)
    {
        float startZ = useRunSpawnZ ? eventData.SpawnPosition.z : 0f;
        ResetGrid(startZ + startZOffset);
    }

    bool ResolveGridStreamer()
    {
        if (gridStreamer != null)
        {
            return true;
        }

        gridStreamer = GetComponent<GridCourseStreamer>();
        if (gridStreamer != null)
        {
            return true;
        }

        Debug.LogWarning("[GridCourseRunResetBridge] Missing grid streamer.", this);
        return false;
    }
}
