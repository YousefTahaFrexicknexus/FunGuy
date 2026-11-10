# FunGuy Windows CPU Profiling — Unity Editor Playback Audit
**Captured:** 2026-11-09, 22:55 local (Asia/Amman)
**Workspace:** `D:/Work/FunGuy` · branch `Analysis` · Unity 6000.3.9f1 / DX12
**Player builds:** NONE (no Windows or Android build started)

## What was profiled
One controlled, automated Editor Play Mode session in `MushroomRunnerGameplay`:
- 6 seconds baseline with player at approximately Z=-0.44
- 13 seconds of synthetic environment streaming at 28 world units/second, using a temporary stream anchor (the player itself did not move). Reached about Z=363.6.
- `RunFlowCoordinator.ResetRun()`, then 5 seconds of recovery.
- `Application.runInBackground` was temporarily enabled so Play Mode advanced when Unity did not have focus; original false value restored.
- ProfilerDriver recording was used; normal profiler enabled/disabled flags restored and scene was left in Edit Mode.
- The first attempt produced only 2 frames while the Editor was background-throttled. This **invalid** preliminary attempt is retained under `funguy-cpu-20261109-224821.*` for investigation only; do not use it for benchmark numbers.
- **Successful capture:** `work/windows-profiling-2026-11-09/cpu-profile/funguy-cpu-20261109-225516.data` (native Unity CPU Timeline, ~588 MB), with `.frames.csv` (~2,165 frames), `.profiler-frames.csv`, `.markers.csv`, and `.events.txt`.
- Follow-up extractor exported 2,000 recorded Timeline frames, including earlier streaming spikes: `trace-inspection-events.txt`, `trace-slowest-frame-markers.csv`, `trace-slowest-frame-summary.csv`.
- The native Unity Timeline is saved and can be opened in the Profiler window for further inspection.

## Per-phase performance from frame records
| Metric | Baseline | Synthetic streaming | Recovery after reset |
|---|---:|---:|---:|
| Count | 548 frames (543 after warmup exclusions) | 1,188 frames | 429 frames |
| Derived average frame rate | 94.9 FPS | 91.4 FPS | 85.8 FPS |
| Mean wall frame duration | 10.53 ms | 10.94 ms | 11.66 ms |
| Median wall frame duration | 10.32 ms | 10.46 ms | 10.65 ms |
| P95 wall frame duration | 12.13 ms | 13.29 ms | 12.80 ms |
| P99 wall frame duration | 16.56 ms | 16.54 ms | 43.28 ms |
| Worst wall frame | 51.33 ms | 178.22 ms | 156.19 ms |
| Median CPU Main Thread Frame Time counter | 3.14 ms | 3.04 ms | 3.14 ms |
| P95 CPU Main Thread Frame Time counter | 4.18 ms | 4.32 ms | 4.15 ms |
| Median BehaviourUpdate | 0.200 ms | 0.197 ms | 0.197 ms |
| P95 BehaviourUpdate | 0.291 ms | 0.363 ms | 0.336 ms |
| Median Physics.Simulate | 0.221 ms | 0.219 ms | 0.226 ms |
| P95 Physics.Simulate | 0.346 ms | 0.412 ms | 0.385 ms |
| Median GC Allocated In Frame | 28.3 KB | 28.3 KB | 28.3 KB |
| Max GC allocation in one frame | 1.22 MB | 0.93 MB | 0.073 MB |
| Frames exceeding 33.3 ms | 1 | 5 | 6 |

Note: Editor wall-frame timings and internal CPU Main Thread Frame Time counters are different measures; do not equate or subtract them. These numbers are **not standalone Windows FPS**. The camera remained near the initial player location during synthetic streaming, so render visibility differed from real forward gameplay.

## Main CPU outlier — GPU Resident Drawer, asset loading, ShaderGUI
The worst streaming frame occurred around target Z=290:
- **178.17 ms main-thread frame** (RawFrameDataView frame ~1501)
- ~101.45 ms nested **EditorLoop**, including Editor-specific work.
- ~67.67 ms **PlayerLoop / UpdateScene**.
- ~62.95 ms inside **GPUResidentDrawer**, overwhelmingly `GPUResidentDrawer.ProcessRenderers`.
- ~62.50 ms `GPUDrivenProcessor.EnableGPUDrivenRenderingAndDispatchRendererData` (nested, do NOT add to parent).
- ~57.24 ms `InstanceCullingBatcher.BuildBatch` (nested).
- ~33.18 ms `UnityEditor.ShaderGUIUtility.ValidateMaterial` (Editor-specific).
- ~30.33 ms `Loading.ReadObject` and ~29.47 ms `AsyncReadManager.SyncRequest` (nested); underlying `File.Read` ~16.60 ms and `File.Open` ~12.84 ms were visible.
- ~3.48 ms `Update.ScriptRunBehaviourUpdate`.
The overlapping nested samples should **not** be summed.

Several independent streaming spikes had the same signature:
- Target Z≈122, frame ~946: 57.04 ms total, 47.62 ms PlayerLoop, 43.35 ms GPUResidentDrawer, 27.21 ms `Loading.ReadObject`.
- Target Z≈142, frame ~1003: 47.96 ms total, 39.19 ms PlayerLoop, 38.00 ms GPUResidentDrawer.
- Target Z≈197, frame ~1190: 54.89 ms total, 46.47 ms PlayerLoop, 44.54 ms GPUResidentDrawer, 43.49 ms `Loading.ReadObject`.

**Interpretation:** The CPU hotspot in this Editor synthetic-streaming test is GPU-driven renderer batch/instance processing triggered around content activation, in conjunction with synchronous asset reads/material validation. It is **NOT simply GPU shading load**. Editor asset database and `ShaderGUIUtility.ValidateMaterial` work may not reproduce in the Windows player; a new Windows player profile is needed before concluding that GPU Resident Drawer should be disabled.

## Separate Editor-only outlier
Near recovery frame ~2054:
- 156.13 ms main thread
- 143.50 ms under `EditorLoop`
- ~1.74 ms for camera-render PlayerLoop and ~1.64 ms for update PlayerLoop.
Another frame near 1996 was ~82.35 ms with ~72.29 ms EditorLoop.
**Interpretation:** These are predominantly Editor-side stalls and must not be treated as equivalent Windows-player gameplay CPU spikes.

## Rendering, scripts, physics and allocation
- `Update.ScriptRunBehaviourUpdate` and `Physics.Simulate` were typically below 0.5 ms in this test. Neither was the dominant CPU hotspot.
- GC allocation median remained ~28.3 KB/frame in all phases, consistent with significant fixed Editor/profiling overhead; this alone does not identify a gameplay allocation.
- Short bursts up to ~0.93 MB happened during synthetic streaming. Without allocation-callstack attribution, specific allocations must not be blamed on `BlockSpawner` or GridCourseStreamer.
- Median batches were ~207 during baseline and ~93 during streaming, because the camera stayed fixed while the stream anchor moved: **not a real in-game draw-call improvement**.

## Pooling validation status
`BlockSpawner` held:
- 6 active, 0 pooled initially.
- 5 active, 1 pooled around synthetic Z=181.
- 4 active, 2 pooled around synthetic Z=340.
- After `RunFlowCoordinator.ResetRun()`, 6 active, 0 pooled.
This proves **recycle to the pool and reuse on reset are consistent with the counter readings**, but no proof of individual instance-ID reuse yet, and the stream reached only Z≈364. A longer synthetic traversal must be used to verify that new chunks re-acquire pooled instances of matching prefabs. The recorded reset frame was ~17.9 ms; the 156 ms recovery outlier was unrelated EditorLoop overhead.

## Recommendations, prioritized, without reducing looks
1. **Keep the current visual settings unchanged** until we can profile a **new Windows player**, ideally a Development Build with Unity Profiler attached, traversing an equivalent section. Player profiling is the only way to decide whether `GPUResidentDrawer.ProcessRenderers` is also expensive in the shipping build. Building requires explicit user approval.
2. Inspect environment and generated mushroom/foliage prefab activation: how many MeshRenderers, materials, LODGroups, MeshColliders and shaders get introduced per chunk. Reduce first-use synchronous loading and material validation by warming up shared assets during a loading window, not in the middle of the run. Verify memory tradeoffs before preloading everything.
3. A/B test **GPU Resident Drawer ON vs OFF** only in a controlled experiment, while keeping identical camera, quality level, scene and other settings; do not disable it by assumption.
4. Use Profiler Timeline plus GC Alloc / allocation-callstack capture on a *player development build* to attribute streaming spikes and short allocation bursts to exact C# versus engine functions.
5. Test longer stream (>1,000 world Z) to prove pool reacquisition and watch for repeated material or asset loads.
6. Investigate `AudioManager` root-object `DontDestroyOnLoad` warning separately; it is a correctness warning, not a demonstrated CPU FPS offender.

## Project modifications from this profiling task
- Added **Editor-only** `Assets/Editor/FunGuyCpuProfilingAudit.cs` and `Assets/Editor/FunGuyCpuTraceInspect.cs`.
- Created profiling output files and the present audit document.
- **No runtime gameplay code, scenes, assets, graphics settings, or build profiles modified by this profiling pass.**
- Editor restored to Edit Mode, no persistent profiling/Play Mode requests pending.

## Existing Windows executable / player profiling readiness
The existing `Builds/New folder/FunGuy.exe` and its Data files were produced on 2026-11-09 around 19:44-20:27 local, before the fixes in this audit. The present `Builds/New folder/FunGuy_Data/boot.config` only lists graphics thread, HDR, GC slice and build GUID settings; it does not declare profiling/debug connection flags. A player-attached per-function CPU Timeline cannot be verified for this existing build, so **no new build was produced**. A Windows Development Build with profiling enabled will need explicit user approval for player-side confirmation.

The Edit/Play synthetic stream test's pooled block counters prove 2 blocks were placed into the inactive pool and the pool returned to 0 after reset. They do *not* prove that specific instance IDs were actually reused rather than replaced; full recycle/re-acquire testing requires further stream traversal and ID-level checks.
