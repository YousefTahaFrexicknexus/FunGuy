using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "GridCourseGenerationProfile", menuName = "Funguy/MushroomRunner/Grid Course Generation Profile")]
public sealed class GridCourseGenerationProfile : ScriptableObject
{
    [Header("Grid")]
    [SerializeField, Min(0.1f), Tooltip("One cube size used for cell width, height, and forward length when Use Uniform Cell Size is enabled.")]
    float cellSize = 5f;
    [SerializeField, Tooltip("If enabled, all grid cells are cubes controlled by Cell Size.")]
    bool useUniformCellSize = true;
    [FormerlySerializedAs("columnSpacing")]
    [SerializeField, Min(0.1f), Tooltip("Cell size on X, from left to right. Used only when Use Uniform Cell Size is disabled.")]
    float cellWidth = 5f;
    [FormerlySerializedAs("levelHeight")]
    [SerializeField, Min(0.1f), Tooltip("Cell size on Y, from bottom to top. Used only when Use Uniform Cell Size is disabled.")]
    float cellHeight = 5f;
    [FormerlySerializedAs("gridCellLength")]
    [SerializeField, Min(0.1f), Tooltip("Cell size on Z, from back to forward. Used only when Use Uniform Cell Size is disabled.")]
    float cellLength = 5f;
    [SerializeField, Min(1), Tooltip("How many grid slices are kept alive after Build Initial Grid.")]
    int visibleGridCount = 12;

    [Header("Difficulty")]
    [SerializeField, Min(1), Tooltip("Generated-distance score required to reach full spawn difficulty.")]
    int difficultyRampDistance = 300;
    [SerializeField, Min(0), Tooltip("Highest number of empty mushroom rows allowed in a row at full difficulty.")]
    int maximumEmptyMushroomRows = 2;

    [Header("Mushrooms")]
    [SerializeField, Range(0f, 1f), Tooltip("Chance for a bottom row to contain no mushrooms at the start of the run.")]
    float emptyMushroomRowChanceStart = 0.05f;
    [SerializeField, Range(0f, 1f), Tooltip("Chance for a bottom row to contain no mushrooms at full difficulty.")]
    float emptyMushroomRowChanceEnd = 0.35f;
    [SerializeField, Range(1, 3), Tooltip("Minimum mushrooms spawned in a non-empty bottom row.")]
    int minimumMushroomsPerActiveRow = 1;
    [SerializeField, Range(1, 3), Tooltip("Maximum mushrooms spawned in a non-empty bottom row.")]
    int maximumMushroomsPerActiveRow = 3;
    [SerializeField, Range(0f, 1f), Tooltip("Random left-right mushroom offset inside the cell. 1 means half of the distance from cell center to cell edge.")]
    float mushroomSideToSideVariation = 1f;
    [SerializeField, Range(0f, 1f), Tooltip("Random forward-back mushroom offset inside the cell. 1 means half of the distance from cell center to cell edge.")]
    float mushroomForwardBackVariation = 1f;
    [SerializeField, Tooltip("Mushroom prefabs that can spawn on grid level 0.")]
    List<GridSpawnEntry> mushroomEntries = new();

    [Header("Upper Levels")]
    [SerializeField, Range(0f, 1f), Tooltip("Chance for an upper grid cell to spawn a coin prefab at the start of the run.")]
    float coinChanceStart = 0f;
    [SerializeField, Range(0f, 1f), Tooltip("Chance for an upper grid cell to spawn a coin prefab at full difficulty.")]
    float coinChanceEnd = 0.35f;
    [SerializeField, Range(0f, 1f), Tooltip("Chance for an upper grid cell to spawn an obstacle prefab at the start of the run.")]
    float obstacleChanceStart = 0f;
    [SerializeField, Range(0f, 1f), Tooltip("Chance for an upper grid cell to spawn an obstacle prefab at full difficulty.")]
    float obstacleChanceEnd = 0.25f;
    [SerializeField, Tooltip("Coin or coin-cluster prefabs that can spawn on grid levels 1 and 2.")]
    List<GridSpawnEntry> coinEntries = new();
    [SerializeField, Tooltip("Obstacle prefabs that can spawn on grid levels 1 and 2.")]
    List<GridSpawnEntry> obstacleEntries = new();

    [Header("Randomness")]
    [SerializeField, Tooltip("Fixed random seed used when Randomize Seed is disabled.")]
    int seed = 1337;
    [SerializeField, Tooltip("If enabled, each generated run uses a new random seed.")]
    bool randomizeSeed = true;

    public float CellSize => Mathf.Max(0.1f, cellSize);

    public bool UseUniformCellSize => useUniformCellSize;

    public float CellWidth => useUniformCellSize ? CellSize : Mathf.Max(0.1f, cellWidth);

    public float CellHeight => useUniformCellSize ? CellSize : Mathf.Max(0.1f, cellHeight);

    public float CellLength => useUniformCellSize ? CellSize : Mathf.Max(0.1f, cellLength);

    public float GridCellLength => CellLength;

    public float ColumnSpacing => CellWidth;

    public float LevelHeight => CellHeight;

    public int VisibleGridCount => Mathf.Max(1, visibleGridCount);

    public int DifficultyRampDistance => Mathf.Max(1, difficultyRampDistance);

    public int MaximumEmptyMushroomRows => Mathf.Max(0, maximumEmptyMushroomRows);

    public int MinimumMushroomsPerActiveRow => Mathf.Clamp(minimumMushroomsPerActiveRow, 1, 3);

    public int MaximumMushroomsPerActiveRow => Mathf.Clamp(Mathf.Max(minimumMushroomsPerActiveRow, maximumMushroomsPerActiveRow), 1, 3);

    public float MushroomSideToSideOffset => ColumnSpacing * 0.25f * Mathf.Clamp01(mushroomSideToSideVariation);

    public float MushroomForwardBackOffset => GridCellLength * 0.25f * Mathf.Clamp01(mushroomForwardBackVariation);

    public IReadOnlyList<GridSpawnEntry> MushroomEntries => mushroomEntries;

    public IReadOnlyList<GridSpawnEntry> CoinEntries => coinEntries;

    public IReadOnlyList<GridSpawnEntry> ObstacleEntries => obstacleEntries;

    public float EvaluateDifficulty01(int score)
    {
        return Mathf.Clamp01(Mathf.Max(0, score) / (float)DifficultyRampDistance);
    }

    public int GetCurrentMaximumEmptyMushroomRows(int score)
    {
        int maximumRows = MaximumEmptyMushroomRows;
        if (maximumRows <= 0)
        {
            return 0;
        }

        return Mathf.Clamp(
            Mathf.RoundToInt(Mathf.Lerp(1f, maximumRows, EvaluateDifficulty01(score))),
            1,
            maximumRows);
    }

    public float EvaluateEmptyMushroomRowChance(int score)
    {
        return Mathf.Lerp(emptyMushroomRowChanceStart, emptyMushroomRowChanceEnd, EvaluateDifficulty01(score));
    }

    public float EvaluateCoinChance(int score)
    {
        return Mathf.Lerp(coinChanceStart, coinChanceEnd, EvaluateDifficulty01(score));
    }

    public float EvaluateObstacleChance(int score)
    {
        return Mathf.Lerp(obstacleChanceStart, obstacleChanceEnd, EvaluateDifficulty01(score));
    }

    public int GetSeed()
    {
        if (!randomizeSeed)
        {
            return seed;
        }

        unchecked
        {
            return Guid.NewGuid().GetHashCode() ^ Environment.TickCount ^ seed;
        }
    }

    void OnValidate()
    {
        cellSize = Mathf.Max(0.1f, cellSize);
        cellWidth = Mathf.Max(0.1f, cellWidth);
        cellHeight = Mathf.Max(0.1f, cellHeight);
        cellLength = Mathf.Max(0.1f, cellLength);

        if (useUniformCellSize)
        {
            cellWidth = cellSize;
            cellHeight = cellSize;
            cellLength = cellSize;
        }

        visibleGridCount = Mathf.Max(1, visibleGridCount);
        difficultyRampDistance = Mathf.Max(1, difficultyRampDistance);
        maximumEmptyMushroomRows = Mathf.Max(0, maximumEmptyMushroomRows);
        emptyMushroomRowChanceStart = Mathf.Clamp01(emptyMushroomRowChanceStart);
        emptyMushroomRowChanceEnd = Mathf.Clamp01(emptyMushroomRowChanceEnd);
        minimumMushroomsPerActiveRow = Mathf.Clamp(minimumMushroomsPerActiveRow, 1, 3);
        maximumMushroomsPerActiveRow = Mathf.Clamp(Mathf.Max(minimumMushroomsPerActiveRow, maximumMushroomsPerActiveRow), 1, 3);
        mushroomSideToSideVariation = Mathf.Clamp01(mushroomSideToSideVariation);
        mushroomForwardBackVariation = Mathf.Clamp01(mushroomForwardBackVariation);
        coinChanceStart = Mathf.Clamp01(coinChanceStart);
        coinChanceEnd = Mathf.Clamp01(coinChanceEnd);
        obstacleChanceStart = Mathf.Clamp01(obstacleChanceStart);
        obstacleChanceEnd = Mathf.Clamp01(obstacleChanceEnd);

        ValidateEntries(mushroomEntries);
        ValidateEntries(coinEntries);
        ValidateEntries(obstacleEntries);
    }

    static void ValidateEntries(List<GridSpawnEntry> entries)
    {
        if (entries == null)
        {
            return;
        }

        for (int index = 0; index < entries.Count; index++)
        {
            entries[index]?.Validate();
        }
    }
}
