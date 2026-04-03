using UnityEngine;

namespace FunGuy.Runner
{
    public sealed class RunnerGridSystem : MonoBehaviour
    {
        [SerializeField] private RunnerGridConfig config;

        public RunnerGridConfig Config => config;
        public int LaneCount => config != null ? config.laneCount : 0;
        public int MinLayer => config != null ? config.minLayer : 0;
        public int MaxLayer => config != null ? config.maxLayer : 0;

        public void Configure(RunnerGridConfig newConfig)
        {
            config = newConfig;

            if (config != null)
            {
                config.Sanitize();
            }
        }

        public Vector3 CellToWorld(Vector3Int cell)
        {
            if (config == null)
            {
                return transform.position;
            }

            float laneCenterOffset = (config.laneCount - 1) * 0.5f;
            Vector3 origin = transform.position + config.worldOrigin;

            return new Vector3(
                origin.x + (cell.x - laneCenterOffset) * config.cellSize.x,
                origin.y + cell.y * config.cellSize.y,
                origin.z + cell.z * config.cellSize.z);
        }

        public Vector3Int WorldToCell(Vector3 worldPosition)
        {
            if (config == null)
            {
                return Vector3Int.zero;
            }

            Vector3 local = worldPosition - (transform.position + config.worldOrigin);
            float laneCenterOffset = (config.laneCount - 1) * 0.5f;

            int x = Mathf.RoundToInt(local.x / config.cellSize.x + laneCenterOffset);
            int y = Mathf.RoundToInt(local.y / config.cellSize.y);
            int z = Mathf.RoundToInt(local.z / config.cellSize.z);

            return new Vector3Int(x, y, z);
        }

        public bool IsLanePlayable(int lane)
        {
            return config != null && lane >= 0 && lane < config.laneCount;
        }

        public bool IsLayerPlayable(int layer)
        {
            return config != null && layer >= config.minLayer && layer <= config.maxLayer;
        }

        public bool IsWithinPlayableBounds(Vector3Int cell)
        {
            return IsLanePlayable(cell.x) && IsLayerPlayable(cell.y) && cell.z >= 0;
        }

        public Vector3Int GetDefaultStartCell(int preferredLane, int preferredLayer)
        {
            if (config == null)
            {
                return new Vector3Int(preferredLane, preferredLayer, 0);
            }

            return new Vector3Int(
                Mathf.Clamp(preferredLane, 0, config.laneCount - 1),
                Mathf.Clamp(preferredLayer, config.minLayer, config.maxLayer),
                0);
        }

        private void OnDrawGizmosSelected()
        {
            if (config == null)
            {
                return;
            }

            config.Sanitize();
            Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.35f);

            for (int z = 0; z < config.previewForwardCells; z++)
            {
                for (int lane = 0; lane < config.laneCount; lane++)
                {
                    for (int layer = config.minLayer; layer <= config.maxLayer; layer++)
                    {
                        Vector3 world = CellToWorld(new Vector3Int(lane, layer, z));
                        Gizmos.DrawWireCube(world, config.cellSize * 0.92f);
                    }
                }
            }
        }
    }
}
