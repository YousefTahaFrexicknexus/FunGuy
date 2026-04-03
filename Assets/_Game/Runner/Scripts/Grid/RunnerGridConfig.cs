using UnityEngine;

namespace FunGuy.Runner
{
    [CreateAssetMenu(fileName = "RunnerGridConfig", menuName = "FunGuy/Runner/Grid Config")]
    public sealed class RunnerGridConfig : ScriptableObject
    {
        [Range(1, 3)]
        public int laneCount = 3;

        public int minLayer = -1;
        public int maxLayer = 1;
        public Vector3 cellSize = new(2.75f, 2f, 5.35f);
        public Vector3 worldOrigin = Vector3.zero;
        [Min(2)]
        public int previewForwardCells = 16;

        public void Sanitize()
        {
            laneCount = Mathf.Clamp(laneCount, 1, 3);
            cellSize.x = Mathf.Max(0.1f, cellSize.x);
            cellSize.y = Mathf.Max(0.1f, cellSize.y);
            cellSize.z = Mathf.Max(0.1f, cellSize.z);

            if (maxLayer < minLayer)
            {
                (minLayer, maxLayer) = (maxLayer, minLayer);
            }

            previewForwardCells = Mathf.Max(2, previewForwardCells);
        }

        private void OnValidate()
        {
            Sanitize();
        }
    }
}
