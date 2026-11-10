# FunGuy — Full Course Performance Fixes (Unity Editor, no player build)
Captured: 2026-11-09 23:49–2026-11-10 00:09 Asia/Amman
Workspace: `D:/Work/FunGuy` · Git branch `Analysis`

## User constraints respected
- **Render scale stays 1.0**. Both `Mobile_RPAsset.asset` and `PC_RPAsset.asset` contain `m_RenderScale: 1`.
- **No Windows/Android player builds**. Testing exclusively used existing Unity Editor Play Mode.
- No modifications to the scene, shader materials, LOD assets, texture sizes, lighting/shadows or quality settings in this task.
- Original quality and active Mobile URP asset SHA-256 hashes unchanged from earlier:
  - `ProjectSettings/QualitySettings.asset`: `38668B630F4AF365646BF991C380FCAAF6258D546A7FE559132EC6525222BDCF`
  - `Assets/Settings/Mobile_RPAsset.asset`: `F4C675496B643C6C1CF147900CC3DDA06A950EE9C8CA92E87B1D33CC87B6BB6C`
- Profiling returned Unity to Edit Mode and did not save serialized scene overrides.

## The first issue: repeated large environment chunk instantiation (fixed)

### Observed before change
`work/windows-profiling-2026-11-09/cpu-profile/full-course-20261109-234901.csv` and `full-course-20261109-235516.csv`:
- The large-block pool was configured for **6 retained inactive blocks**.
- The current 15-entry authored sequence contains **14 distinct prefabs**; the first six 160-unit entries cover approximately 960 world units.
- Early blocks occupied all six pool slots, so the later nine blocks were repeatedly created/destroyed as gameplay proceeded.
- With pool cap 6, created blocks grew from **15 to 24** across two long traversals. Destructions grew from **7 to 16**.
- Pool hit count increased by 6 across the reset but did not cover later prefab types.
- This behavior causes avoidable managed/native object churn during repeat runs.

### Proof from controlled runtime override
`full-course-20261110-000000.csv`:
- In Play Mode only, temporarily set pool cap to 14 without changing/saving the scene.
- Created blocks stayed **14 → 14** across two traversals.
- Destroyed blocks stayed **0 → 0**.
- Pool acquisitions increased **7 → 22**.
- Pool contained **12 inactive blocks** at the end of each traversal, with 2 still active.
- The cached native editor allocation count was similar to the six-slot runs, but session-to-session Editor caching prevents treating this as a precise memory overhead measurement.

### Final production code fix, independently validated
`Assets/_Game/Scripts/Blocks/BlockSpawner.cs`:
- Added **Automatically Cover Block Sequence**, default true.
- The effective pool capacity at Awake and Reset is `max(manualPoolCapacity, distinctValidPrefabCount)` when automatic coverage is enabled. This addresses the existing scene's previously serialized six-slot value, which persisted despite a C# initializer default change.
- Manual inspector limit remains configurable: disable auto coverage to enforce a lower limit where memory is more important than repeat-run CPU stutter.
- Kept bounds per prefab, pooling by exact prefab key, correct deactivation/reactivation lifecycle.
- Added observable counts: `EffectivePoolCapacity`, `TotalInstancesCreated`, `TotalPoolHits`, `TotalPoolStores`, `TotalInstancesDestroyed`.
- Added profiler markers for `FunGuy.BlockSpawner.InstantiatePrefab` and `FunGuy.BlockSpawner.ReactivatePrefab`.

`full-course-20261110-000415.csv` verifies the **real code without an override**:
- `EffectiveBlockPoolCapacity=14`.
- Created **14 on first pass; still 14 on second pass**.
- Destroyed **0** across both passes.
- **22 cumulative pool acquisitions**.
- Second pass max `FunGuy.BlockSpawner.InstantiatePrefab` profiler counter **0.000 ms**, confirming no new large-block instance was made during the traversal.
- In this warmed Editor run, first-pass p99 frame time ~9.35 ms and worst ~13.55 ms; second-pass p99 ~9.56 ms and worst ~14.83 ms. These are *not* comparable to uncached, standalone Windows-player FPS and do not prove first-use stutter is fixed.

**Tradeoff:** The automatic policy retains more heavy inactive prefab hierarchies (12 inactive at the end). Monitor actual player memory and GPU-resident resource residency. The per-prefab pool remains capped at 2; manual cap can be restored through the Inspector if required.

## The second issue: grid objects (confirmed reuse; no major lifecycle bug)
`Assets/_Game/Funguy.MushroomRunner/World/GridCourseStreamer.cs`:
- Exposed lightweight allocation/reuse/active slice/cached count counters and profiler markers (generated slices, prefab instantiate, activation) to distinguish real reuse.
- The grid streamer reused **775 pooled instances cumulatively** during the final two-pass stress test, and created approximately 90–94 instances total.
- The grid's pooled instance and cache counts stabilized; no evidence of repeated mass instantiation over subsequent passes.
- Existing managed caches and reuse policies were preserved. No unnecessary rewrite.

## The third issue: AudioManager non-root persistence warning (fixed and verified)
`Assets/_Game/Scripts/Global/AudioManager.cs`:
- Previously called `DontDestroyOnLoad(gameObject)` on an object nested beneath scene hierarchy, causing a Unity warning every startup and potentially unreliable persistence.
- On Awake, duplicate AudioManager **GameObject** hierarchies are destroyed rather than only the duplicate component. For the singleton, the manager is detached to the scene root (preserving world-space transform) before calling `DontDestroyOnLoad`.
- OnDestroy clears the static instance only if it is the singleton.
- One-shot QA helper `Assets/Editor/FunGuyAudioLifetimeTest.cs` entered Play Mode, inspected the hierarchy and exited.
- `work/windows-profiling-2026-11-09/cpu-profile/audio-lifetime-verification.txt`: **PASS=True**; manager count 1, singleton present, root transform true, scene `DontDestroyOnLoad`. No new non-root persistence warning in the QA run.

## Unresolved biggest cold-first-use performance risk
`full-course-20261109-234901.csv` (early, cold Editor pass) captured:
- **123.39 ms** GPU Resident Drawer sample around stream Z735.
- **74.38 ms** GPU Resident Drawer sample around stream Z772.
- **174.95 ms** `Loading.ReadObject` sample around Z772.
- Worst recorded frame **328.08 ms** around Z774.
- Block-prefab instantiate markers reached **23.88 ms** around Z735 and **33.76 ms** around Z1203.
- Second traversal in the same Editor process had no comparable cold shader/asset load stalls.
- Subsequent warmed runs, even before the pool capacity fix, no longer reproduced the initial massive cold load sample.

These are Editor-only findings involving GPU-driven renderer registration, asset read and material initialization; they may include Editor asset validation work unavailable in a standalone player. The startup shared-material/mesh metadata preload does not precompile shaders or guarantee GPU residency, so it **cannot** be claimed to solve this first-use spike.

**Next safe technical experiment:** profile the costly 7th and later environment block prefab activations in a truly cold launch and evaluate staging renderer initialization or asynchronous prefab instantiation away from the active movement phase, with memory and startup-time budgets. Do not disable GPU Resident Drawer, lower resolution, or merge/destroy art without proving a benefit. Actual Windows-player validation requires explicit permission for a new build; none was made.

## Test methodology and limits
- A temporary Editor-only stream-target object traveled from Z0 to **Z2260** twice at **112 units/s**, using existing RunFlowCoordinator streaming. The grid's configured end is Z2260 and the block spawner maximum is Z2360. The nearest effective shared route end was used.
- The actual player and camera remained near their start; this is a stress test of **streaming and pooling**, not a real-play visibility/GPU-cost benchmark.
- Unity Editor ProfilerRecorder tracked per-frame CPU frame duration, GPU Resident Drawer, asset loading, grid and block spawn markers, GC allocations, draw calls, and pool counters.
- Render scale remained 1.0; no build, no scene serialization.
- Unity recompiled source and tests executed; `git diff --check` passed.

## Primary file locations
- Source: `Assets/_Game/Scripts/Blocks/BlockSpawner.cs`, `Assets/_Game/Funguy.MushroomRunner/World/GridCourseStreamer.cs`, `Assets/_Game/Scripts/Global/AudioManager.cs`
- Diagnostic harness: `Assets/Editor/FunGuyFullCourseAudit.cs` (one-shot `full-course-audit.request`)
- Audio verification: `Assets/Editor/FunGuyAudioLifetimeTest.cs`
- Reports: `work/windows-profiling-2026-11-09/cpu-profile/full-course-20261109-234901.*`, `full-course-20261109-235516.*`, `full-course-20261110-000000.*`, `full-course-20261110-000415.*`, `audio-lifetime-verification.txt`
- Earlier work: `Docs/FUNGUY_CPU_PROFILING_20261109.md`, `Docs/FUNGUY_RENDERER_PRELOAD_AB_TEST_20261109.md`
