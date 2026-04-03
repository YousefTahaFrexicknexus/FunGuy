using UnityEngine;

namespace FunGuy.Runner
{
    [DisallowMultipleComponent]
    public sealed class GridCollectibleActor : MonoBehaviour
    {
        [SerializeField] private SpawnableDefinition definition;

        public SpawnableDefinition Definition => definition;
        public Vector3Int Cell { get; private set; }
        public bool IsCollected { get; private set; }

        public void Bind(SpawnableDefinition spawnableDefinition, Vector3Int cell)
        {
            definition = spawnableDefinition;
            Cell = cell;
            IsCollected = false;
            name = $"{spawnableDefinition.name}_{cell.x}_{cell.y}_{cell.z}";
        }

        public void Collect()
        {
            if (IsCollected)
            {
                return;
            }

            IsCollected = true;
            gameObject.SetActive(false);
        }
    }
}
