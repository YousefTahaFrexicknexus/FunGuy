# FunGuy — No-Build Renderer & Asset-Preloading Experiments
Date: 2026-11-09 · Project: `D:/Work/FunGuy` · Git branch: `Analysis`.

## Scope
The user explicitly requested **no build**. All measurements came from Editor Play Mode in `MushroomRunnerGameplay`, using a temporary stream-anchor at 28 world units/s for about 336 units per phase. The original player position and saved scene were untouched; the test returns to Edit Mode.

The active Standalone quality tier was **Mobile** (`QualitySettings.GetQualityLevel()=0`), active asset `Mobile_RPAsset`, render scale 1.0, GPU Resident Drawer `InstancedDrawing`. We used an **in-memory clone** to test GPU Resident Drawer OFF, and restored `QualitySettings.renderPipeline` after each run. No persistent change to quality or URP asset settings. Verified SHA-256:
- `ProjectSettings/QualitySettings.asset` = `38668B630F4AF365646BF991C380FCAAF6258D546A7FE559132EC6525222BDCF`.
- `Assets/Settings/Mobile_RPAsset.asset` = `F4C675496B643C6C1CF147900CC3DDA06A950EE9C8CA92E87B1D33CC87B6BB6C`.

The phases in each test were: ON first pass, ON after explicit prefab reference preloading, OFF with the same reference preloading, then ON again. Each phase reset the course to its original state and traveled the same distance; an Editor background-play override was temporary. Randomized generated content, cache warming, time-varying Editor overhead and a stationary gameplay camera mean this is **not a controlled Windows-player A/B**.

## Baseline experiments before shipping source changes
`work/windows-profiling-2026-11-09/cpu-profile/renderer-ab-20261109-231525.csv`
- ON first: 86.1 average FPS, worst 34.5 ms, ~136 average draw calls, max `GPUResidentDrawer` sample 9.39 ms.
- ON after preloading: 97.9 FPS, worst 29.9 ms, ~131 draw calls, max Drawer sample 1.04 ms.
- OFF after preloading: 97.6 FPS, worst 39.6 ms, ~318 draw calls, zero GPU Resident Drawer CPU marker.
- ON again: 98.4 FPS, worst 45.4 ms, ~137 draw calls.
- Explicit one-time Editor resource access: 14 unique environment prefabs, 2,546 Renderers, 10 shared materials, 64 shared meshes and 301 MeshColliders. About 95 ms while some assets were cold in Editor.

These phases do not isolate explicit preloading from the natural warming caused by the first pass. They indicate that content first-use is important and turning the Drawer OFF does **not** reliably eliminate stutters.

## Added optional startup asset preloading (keeps visual quality)
`Assets/_Game/Scripts/Blocks/BlockSpawner.cs`
- New `preloadFutureBlockRenderAssets` Inspector checkbox, default true.
- In `Awake()`, before streaming, touches shared materials/shaders and mesh metadata referenced by *later* distinct environment-block prefab assets. Skips the first six pre-spawned prefabs, already created during normal initial-world generation.
- No additional prefab instances, material clones, mesh CPU vertex-array reads, shader variant compilation or GPU Resident Drawer toggles.
- Adds a Unity ProfilerMarker and nonserialized metrics `PreloadedFuturePrefabCount`, `PreloadedFutureMeshCount`, `PreloadedFutureMaterialCount`, `PreloadDurationMilliseconds`.
- Observed 8 future prefabs, 10 distinct shared materials, 64 meshes; ~4.0–4.8 ms in a **warm Editor session** (not a cold launch benchmark).

`Assets/_Game/Funguy.MushroomRunner/World/GridCourseStreamer.cs`
- New `preloadUpcomingGridRenderAssets` checkbox default true and `preloadGridThroughScore` default 500.
- In `Awake()`, touches shared render assets for mushroom/coin/obstacle prefab entries eligible by score 500. Does not instantiate, duplicate, modify or change their random selection.
- Adds startup profiler marker and metrics properties.
- Observed **3 grid prefabs, 5 shared materials, 6 meshes** preloaded in **0.53 ms** in the warm Editor session.
- This specifically targets early streaming: the first six authored environment blocks are each **160 world units**, occupying ~Z0–960. Spikes at Z122–290 therefore arise before any **later** large block prefab is required and likely involve the GridCourseStreamer/renderer activation.

**Important:** Touching referenced asset metadata moves a portion of lazy asset resolution to startup, but it does **not** precompile shaders, guarantee all textures are resident on the GPU, or eliminate the GPU Resident Drawer re-batching cost when renderers become active.

## Regression session with only BlockSpawner preload
`work/windows-profiling-2026-11-09/cpu-profile/renderer-ab-20261109-232222.csv`
- ON first: 99.79 FPS, p99 frame time ~13.98 ms, worst 32.54 ms, ~133 draw calls, max Drawer 1.17 ms.
- ON after explicit preload: 100.76 FPS, worst 17.58 ms, ~133 draw calls.
- OFF after preload: 102.16 FPS, worst 36.22 ms, ~326 draw calls.
- ON repeat: 95.62 FPS, worst 52.58 ms, ~136 draw calls.
The differences are variable across phases and cannot establish a consistent FPS gain due solely to asset preloading. Startup confirmed 8 future prefabs / 10 materials / 64 meshes.

## Final regression with BlockSpawner + GridCourseStreamer preload
`work/windows-profiling-2026-11-09/cpu-profile/renderer-ab-20261109-232814.csv`

| Phase | Average Editor FPS | P99 frame time | Worst frame | Average draw calls | Max GPUResidentDrawer |
|---|---:|---:|---:|---:|---:|
| ON first pass | 95.68 | 15.19 ms | 33.16 ms | 140 | 5.83 ms |
| ON after explicit preload | 98.40 | 13.96 ms | 16.96 ms | 137 | 1.44 ms |
| OFF after preload | 103.75 | 14.67 ms | 48.54 ms | 316 | 0 ms |
| ON repeated | 96.83 | 14.49 ms | 50.00 ms | 131 | 1.24 ms |

All four phases reached approximately Z336. The in-memory OFF test did disable GPU Resident Drawer (marker zero), but **average draw calls were roughly 2.3× higher** than ON. OFF occasionally had a slightly better mean frame time, but its worst frame was larger than the warmed ON pass. With normal Editor variability and a stationary camera, this is not enough to change the project's current rendering settings.

### Interpretation and decision
1. **Leave GPU Resident Drawer enabled** and the current URP quality parameters as they were. No quality setting was saved or modified.
2. **Keep optional render-asset preloading enabled** in `BlockSpawner` and `GridCourseStreamer`, shifting shared mesh/material metadata touches before streaming. Both compile and the automated Editor runs completed. This is a reasonable low-risk source change, not a demonstrated Windows player FPS improvement.
3. Draw-call count ~131–140 ON versus ~316–326 OFF in repeat passes indicates materially greater submission work with Drawer disabled. Profile actual Windows player if/when the user separately authorizes a future build.
4. Synthetic traversal to Z336 does not exercise the later distinct authored block prefabs (first 6 blocks occupy Z0–960); deeper traversal and instance-ID reuse remain untested.
5. Profiling with real player movement/camera and a cold Editor cache may yield different results; do not claim that the ON vs OFF delta is portable to the shipping Windows executable.

## Validation
- Unity 6000.3.9f1 compiled the new scripts, no new compile error.
- All 3 Editor A/B experiment sessions were completed and returned to Edit Mode.
- The original URP and quality file hashes remain unchanged.
- No Windows or Android build produced.
- No scene or prefab edits were made for these tests.
- Reusable diagnostic tool: `Assets/Editor/FunGuyRendererExperiment.cs`; one-shot request `work/windows-profiling-2026-11-09/cpu-profile/compare-renderers.request`.
