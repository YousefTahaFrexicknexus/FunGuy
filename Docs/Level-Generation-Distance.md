# Level generation distance

Set **Maximum Distance** on the scene's `BlockSpawner` and on its `GridCourseGenerationProfile` asset. Use the same positive value for both to share one world-Z endpoint. For example, 1000 means Z = 1000, regardless of either generator's starting offset. This is world distance, not multiplied score or distance relative to the player spawn. **0 means unlimited**, preserving existing scenes and profiles.

Blocks whose start is before the endpoint are kept whole, so the final terrain block may extend past it. No further block starts at or beyond the endpoint. Grid slices and spawned object origins stop before the same endpoint, including randomized positions and entry offsets. Prefab geometry and the contents of a coin bundle may extend beyond their origin.

The limit applies to initial generation, streaming, manual generation, catch-up after teleports, and resets. Older slices and blocks continue recycling normally at the end.

Under **Level End** on the grid profile, assign **End Prefab** and set **End Prefab Cells After End**. This is the number of empty cells between the endpoint and the prefab's cell: 0 selects the first cell immediately after the endpoint, 1 skips one cell, and so on. The prefab root is placed exactly at the center of the middle column and middle height cell of the 3x3 grid, without random offsets. Z is `Maximum Distance + (Cells After End + 0.5) * Cell Length`; X is the streamer's X and Y is its Y plus one Cell Height. For an endpoint of 1000, cell length 10 and offset 2, the prefab spawns at Z = 1025.

The prefab spawns once when the generated course reaches its endpoint, stays after ordinary slices recycle, and is removed and recreated on a rebuild. Leaving the prefab empty or using unlimited distance disables it. The prefab retains its authored rotation and scale. Its root pivot determines placement; center the artwork around that pivot to center it visually. Spawning this prefab does not itself end the run or show results; those behaviors can be attached to the prefab.

Run **Tools > Funguy > Verify Level Generation Distance** outside Play Mode for focused checks in a temporary scene. The verification restores the previous scene and does not save it.
