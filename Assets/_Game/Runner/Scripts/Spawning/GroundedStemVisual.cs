using UnityEngine;

namespace FunGuy.Runner
{
    public sealed class GroundedStemVisual : MonoBehaviour, IRunnerSpawnInitializable
    {
        [SerializeField] private Transform stem;
        [SerializeField] private float minimumStemHalfHeight = 0.5f;

        private float originalStemTopLocalY;
        private Vector3 originalStemScale = Vector3.one;
        private bool hasCachedStemMetrics;

        private void Awake()
        {
            AutoAssignStemIfNeeded();
            CacheStemMetrics();
        }

        public void InitializeOnSpawn(RunnerGridSystem gridSystem, Vector3Int cell)
        {
            AutoAssignStemIfNeeded();
            CacheStemMetrics();

            if (stem == null || gridSystem == null || gridSystem.Config == null)
            {
                return;
            }

            float groundY = gridSystem.CellToWorld(new Vector3Int(cell.x, gridSystem.MinLayer, cell.z)).y
                - gridSystem.Config.cellSize.y * 0.5f;

            float bottomLocalY = groundY - transform.position.y;
            float desiredHalfHeight = Mathf.Max(minimumStemHalfHeight, (originalStemTopLocalY - bottomLocalY) * 0.5f);
            float desiredCenterY = originalStemTopLocalY - desiredHalfHeight;

            stem.localPosition = new Vector3(stem.localPosition.x, desiredCenterY, stem.localPosition.z);
            stem.localScale = new Vector3(originalStemScale.x, desiredHalfHeight, originalStemScale.z);
        }

        private void AutoAssignStemIfNeeded()
        {
            if (stem != null)
            {
                return;
            }

            Transform foundStem = transform.Find("Stem");
            if (foundStem != null)
            {
                stem = foundStem;
            }
        }

        private void CacheStemMetrics()
        {
            if (hasCachedStemMetrics || stem == null)
            {
                return;
            }

            originalStemScale = stem.localScale;
            originalStemTopLocalY = stem.localPosition.y + stem.localScale.y;
            hasCachedStemMetrics = true;
        }
    }
}
