using UnityEngine;

namespace FunGuy.Runner
{
    [DisallowMultipleComponent]
    public sealed class GridSurfaceActor : MonoBehaviour
    {
        [SerializeField] private SpawnableDefinition definition;

        private readonly System.Collections.Generic.List<IRunnerSurfaceLandingFeedback> landingFeedbacks = new();
        private bool landingFeedbacksResolved;
        private bool landingEnabled = true;

        public SpawnableDefinition Definition => definition;
        public Vector3Int Cell { get; private set; }
        public bool IsHazard => definition != null && definition.Category == SpawnableCategory.Hazard;
        public bool IsLandingEnabled => landingEnabled;

        public void Bind(SpawnableDefinition spawnableDefinition, Vector3Int cell)
        {
            definition = spawnableDefinition;
            Cell = cell;
            landingEnabled = true;
            landingFeedbacksResolved = false;
            landingFeedbacks.Clear();
            string definitionName = spawnableDefinition != null ? spawnableDefinition.name : "GridSurface";
            name = $"{definitionName}_{cell.x}_{cell.y}_{cell.z}";
        }

        public void SetLandingEnabled(bool enabled)
        {
            landingEnabled = enabled;
        }

        public void PlayLandingFeedback()
        {
            ResolveLandingFeedbacks();

            for (int i = 0; i < landingFeedbacks.Count; i++)
            {
                landingFeedbacks[i]?.PlayLandingFeedback();
            }
        }

        private void ResolveLandingFeedbacks()
        {
            if (landingFeedbacksResolved)
            {
                return;
            }

            landingFeedbacksResolved = true;
            MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IRunnerSurfaceLandingFeedback feedback)
                {
                    landingFeedbacks.Add(feedback);
                }
            }
        }
    }
}
