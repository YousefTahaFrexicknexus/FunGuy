using UnityEngine;

namespace FunGuy.Runner
{
    public interface IRunnerSpawnInitializable
    {
        void InitializeOnSpawn(RunnerGridSystem gridSystem, Vector3Int cell);
    }
}
