using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class LevelGenerationDistanceVerification
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    static int SliceCount(GridCourseStreamer grid) => ((ICollection)typeof(GridCourseStreamer)
        .GetField("activeSlices", Fields).GetValue(grid)).Count;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    [MenuItem("Tools/Funguy/Verify Level Generation Distance")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        GridCourseGenerationProfile profile = null;
        BlockData block = null;
        try
        {
            SceneManager.SetActiveScene(scene);
            GameObject template = new GameObject("Distance verification template");
            template.SetActive(false);
            Transform player = new GameObject("Distance verification cursor").transform;
            BlockSpawner blocks = new GameObject("Distance verification blocks").AddComponent<BlockSpawner>();
            block = ScriptableObject.CreateInstance<BlockData>();
            block.prefab = template;
            block.zLength = 40f;
            Set(blocks, "blockSequence", new List<BlockData> { block });
            Set(blocks, "player", player);
            Set(blocks, "initialSpawnZ", -10f);
            Set(blocks, "maximumDistance", 95f);
            Set(blocks, "preSpawnCount", 20);
            blocks.ResetSpawner();
            Check(blocks.ActiveBlockCount == 3 && blocks.NextSpawnZ == 110f,
                "Non-aligned limit must keep the whole final block and stop subsequent blocks.");
            blocks.EnsureSpawnedThrough(10000f);
            Check(blocks.ActiveBlockCount == 3, "Catch-up must obey the block limit.");
            blocks.ResetSpawner();
            Check(blocks.ActiveBlockCount == 3, "Reset must reapply the block limit.");
            Set(blocks, "maximumDistance", 70f);
            blocks.ResetSpawner();
            Check(blocks.ActiveBlockCount == 2, "A block must not start exactly at the limit.");
            Set(blocks, "maximumDistance", 0f);
            Set(blocks, "preSpawnCount", 3);
            blocks.ResetSpawner();
            blocks.EnsureSpawnedThrough(200f);
            Check(blocks.NextSpawnZ > 200f, "Zero must preserve unlimited block generation.");

            profile = ScriptableObject.CreateInstance<GridCourseGenerationProfile>();
            Set(profile, "maximumDistance", 95f);
            Set(profile, "cellSize", 10f);
            Set(profile, "visibleGridCount", 20);
            GridCourseStreamer grid = new GameObject("Distance verification grid").AddComponent<GridCourseStreamer>();
            Set(grid, "generationProfile", profile);
            grid.SetStreamingTarget(player);
            Set(blocks, "maximumDistance", 95f);
            Check(grid.GenerationEndZ == blocks.GenerationEndZ, "Different starts must share the same endpoint.");
            grid.BuildInitialGrid(15f);
            Check(SliceCount(grid) == 8 && grid.HasReachedMaximumDistance,
                "Initial grid must stop before the endpoint, including with a nonzero start.");
            grid.GenerateToSliceCount(100);
            grid.GenerateNextSlice();
            Check(SliceCount(grid) == 8, "Manual generation must stop without looping at the limit.");
            Check(grid.GetInitialEnvironmentEndZ(15f) == 95f && grid.RequiredEnvironmentEndZ == 95f,
                "Grid environment lookahead must be capped.");
            player.position = new Vector3(0f, 0f, 10000f);
            grid.UpdateStreaming();
            Check(SliceCount(grid) == 0, "Finished grid must drain after a forward teleport.");
            grid.BuildInitialGrid(100f);
            Check(SliceCount(grid) == 0, "Starting beyond the end must generate nothing.");
            grid.BuildInitialGrid(0f);
            Check(SliceCount(grid) == 10, "Non-aligned endpoint must allow the last partial grid interval.");
            Set(profile, "visibleGridCount", 2);
            grid.BuildInitialGrid(0f);
            grid.UpdateStreaming();
            Check(SliceCount(grid) == 0 && grid.HasReachedMaximumDistance,
                "Streaming catch-up must reach the end and terminate.");

            // Check final object origins, including entry offsets and row randomization.
            GridSpawnEntry entry = new GridSpawnEntry();
            Set(entry, "prefab", template);
            Set(entry, "localOffset", Vector3.forward * 100f);
            Set(profile, "mushroomEntries", new List<GridSpawnEntry> { entry });
            grid.BuildInitialGrid(90f);
            Check(grid.transform.childCount == 0, "Entry offsets must not spawn origins beyond the endpoint.");
            Set(entry, "localOffset", Vector3.zero);
            grid.BuildInitialGrid(94f);
            foreach (Transform child in grid.transform)
                if (child.gameObject.activeSelf) Check(child.position.z < 95f, "Random offsets must respect the endpoint.");
            Set(profile, "maximumDistance", 0f);
            grid.BuildInitialGrid(1000f);
            grid.GenerateToSliceCount(20);
            Check(SliceCount(grid) == 20 && !grid.HasReachedMaximumDistance,
                "Zero must preserve unlimited grid generation after resets.");
            Set(profile, "endPrefab", template);
            grid.BuildInitialGrid(0f);
            Check(grid.EndPrefabInstance == null, "Unlimited courses must not spawn an end prefab.");
            Set(profile, "maximumDistance", 95f);
            Set(profile, "endPrefabCellsAfterEnd", 2);
            Set(profile, "mushroomEntries", new List<GridSpawnEntry>());
            Set(profile, "useUniformCellSize", false);
            Set(profile, "cellWidth", 8f);
            Set(profile, "cellHeight", 6f);
            Set(profile, "cellLength", 10f);
            template.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            template.transform.localScale = new Vector3(2f, 3f, 4f);
            grid.transform.position = new Vector3(7f, 11f, 300f);
            player.position = Vector3.zero;
            grid.BuildInitialGrid(15f);
            Check(grid.EndPrefabInstance == null, "End prefab must wait until generation reaches the endpoint.");
            player.position = Vector3.forward * 90f;
            grid.UpdateStreaming();
            GameObject firstEnd = grid.EndPrefabInstance;
            Check(firstEnd != null && firstEnd.transform.position == new Vector3(7f, 17f, 120f),
                "End prefab must use the middle cell center, nonuniform height, and endpoint-relative cell offset.");
            Check(Quaternion.Angle(firstEnd.transform.rotation, template.transform.rotation) < 0.01f
                && firstEnd.transform.localScale == template.transform.localScale,
                "End prefab must preserve authored rotation and scale.");
            grid.GenerateNextSlice();
            grid.GenerateToSliceCount(100);
            Check(grid.EndPrefabInstance == firstEnd, "Repeated generation must not duplicate the end prefab.");
            player.position = Vector3.forward * 10000f;
            grid.UpdateStreaming();
            Check(grid.EndPrefabInstance == firstEnd, "End prefab must survive ordinary slice recycling.");
            Set(profile, "endPrefabCellsAfterEnd", 0);
            grid.BuildInitialGrid(90f);
            Check(firstEnd == null && grid.EndPrefabInstance != null
                && grid.EndPrefabInstance.transform.position == new Vector3(7f, 17f, 100f),
                "Reset must remove the old prefab and place offset zero at the first cell center.");
            grid.ClearGeneratedWorld();
            Check(grid.EndPrefabInstance == null, "Clear must remove the end prefab.");
            Set(profile, "endPrefab", null);
            grid.BuildInitialGrid(90f);
            Check(grid.EndPrefabInstance == null, "Missing optional prefab must not affect generation.");
            Debug.Log("PASS: Level generation distance — block/grid boundaries, shared endpoint, offsets, resets, catch-up, recycling and unlimited mode.");
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
            if (profile != null) Object.DestroyImmediate(profile);
            if (block != null) Object.DestroyImmediate(block);
        }
    }
}
