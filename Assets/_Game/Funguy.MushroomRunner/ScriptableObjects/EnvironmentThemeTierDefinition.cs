using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Groups decoration blocks into a theme that becomes active after a score threshold.
/// </summary>
[CreateAssetMenu(fileName = "EnvironmentThemeTierDefinition", menuName = "Funguy/MushroomRunner/Environment Theme Tier Definition")]
public sealed class EnvironmentThemeTierDefinition : ScriptableObject
{
    [SerializeField, Tooltip("Minimum score required before this theme tier becomes active.")]
    int scoreThreshold;
    [SerializeField, Tooltip("Decoration definitions that can be randomly picked while this theme is active.")]
    List<EnvironmentDecorationDefinition> decorations = new();

    public int ScoreThreshold => Mathf.Max(0, scoreThreshold);

    public IReadOnlyList<EnvironmentDecorationDefinition> Blocks => decorations;

    public IReadOnlyList<EnvironmentDecorationDefinition> Decorations => decorations;

    void OnValidate()
    {
        scoreThreshold = Mathf.Max(0, scoreThreshold);
    }
}