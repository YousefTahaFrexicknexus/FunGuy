using System.Collections.Generic;
using UnityEngine;

namespace Funguy.IdkPlatformer
{
    [CreateAssetMenu(fileName = "EnvironmentThemeTierDefinition", menuName = "Funguy/IdkPlatformer/Environment Theme Tier Definition")]
    public sealed class EnvironmentThemeTierDefinition : ScriptableObject
    {
        [SerializeField] private int scoreThreshold;
        [SerializeField] private List<EnvironmentDecorationDefinition> decorations = new();

        public int ScoreThreshold => Mathf.Max(0, scoreThreshold);

        public IReadOnlyList<EnvironmentDecorationDefinition> Blocks => decorations;

        public IReadOnlyList<EnvironmentDecorationDefinition> Decorations => decorations;

        private void OnValidate()
        {
            scoreThreshold = Mathf.Max(0, scoreThreshold);
        }
    }
}
