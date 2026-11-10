# FunGuy — Staged Gameplay Loading and Resource Warm-up
**Date:** 2026-11-10 (Asia/Amman)
**Project:** `D:/Work/FunGuy` · branch `Analysis` · Unity 6.3 LTS (6000.3.9f1)
**Constraint:** Render scale 1.0 and visual fidelity unchanged. **NO player builds performed.**

## Goal
Move first-use CPU work (loading meshes/materials, constructing prefab instances,
registering renderers with the GPU Resident Drawer, and filling reusable pools)
from moving gameplay into a visible pre-run loading step. Use RAM deliberately
but bound additional Unity memory use.

## Actual current startup path
- The main-menu script `Assets/_Game/Art/Logo/MainMenu.cs` still calls
  `SceneManager.LoadScene(sceneName)` directly. It does not currently use the
  separate `GlobalManager` loading-screen flow.
- Therefore, this implementation starts **inside the activated
  `MushroomRunnerGameplay` scene**, before the first playable run.
- The first frame displays a cover using the game’s actual FunGuy logo, rather
  than exposing gameplay while expensive prefabs are instantiated.

## Implementation
1. `Assets/_Game/Funguy.MushroomRunner/Core/RunFlowCoordinator.cs`
   - New **Startup Loading / Staged Warm-up** Inspector fields:
     - `stageWorldBeforeFirstRun` (default true, switch off to bypass)
     - `warmupMaxStepsPerFrame` = 6 grid-cell advances/frame
     - `warmupMillisecondsPerFrame` = 3 ms soft CPU budget/frame
     - `maximumWarmupSeconds` = 18 seconds safety limit
     - `maximumExtraReservedMemoryMiB` = 768 MiB soft additional
       reserved/allocated Unity memory budget, further limited by RAM / 12
     - `gameplayShaderVariants` = optional assigned
       `ShaderVariantCollection`; **none recorded/assigned yet**
     - `shaderVariantsPerFrame` = 16 when the collection is assigned
   - Disables the player before the staged work.
   - Displays the loading overlay, then yields a frame to make it visible.
   - If a shader collection is assigned, calls
     `ShaderVariantCollection.WarmUpProgressively` in bounded steps.
   - Initializes the exact existing BlockSpawner and GridCourseStreamer without
     firing gameplay lifecycle events. A temporary non-saved stream cursor
     advances from 0 to the minimum of the two configured endpoints: **Z2260**.
   - Calls `EnsureSpawnedThrough`, `BlockSpawner.UpdateStreaming` and
     `GridCourseStreamer.UpdateStreaming` for each staged step. All the work
     uses **production pools**, so warm instances are reused after the final
     reset rather than creating throwaway duplicate prefabs.
   - Restores the player stream targets, destroys the temporary cursor and
     cover, restores `Application.runInBackground`, and calls `RunInternal(true)`
     **once**, producing a single genuine first `RunStarted` event.
   - Exposes state/timings: `IsStartupReady`, `IsStartupWarming`,
     `StartupProgress`, `StartupStage`,
     `StartupDurationMilliseconds`, `StartupHitMemoryBudget`.
   - Handles external StartRun/ResetRun and object disabling during warm-up by
     stopping the coroutine and restoring original targets/UI.
2. `Assets/_Game/Funguy.MushroomRunner/Core/FunGuyStartupLoadingOverlay.cs`
   - Runtime-only UGUI/TMP overlay (no saved scene/prefab changes).
   - Reuses the authored FunGuy logo and forest-green/orange/cream palette.
   - Displays warm-up status and a working percentage bar. The image fill is
     controlled by the progress rect’s anchors (rather than
     `Image.Type.Filled` with no sprite, which displayed a full bar at 31%).
3. `Assets/_Game/Funguy.MushroomRunner/Core/FunGuyDeathTransition.cs`
   - Exposes `StartupLogo` for reuse without duplicating artwork.
4. `Assets/_Game/Funguy.MushroomRunner/World/GridCourseStreamer.cs`
   - Exposes the configured `GridCellLength` to advance the temporary cursor
     exactly one real grid slice at a time.
5. `Assets/_Game/Scripts/Gameplay/GameplayManager.cs`
   - In `OnDisable` and `BindMovementMomentum`, replace
     `runnerMovementMotor?.SetMomentumSystem(...)` with
     `if (runnerMovementMotor != null)` using Unity’s overloaded null check.
     Prevents `MissingReferenceException` when leaving Play Mode and the
     motor has already been destroyed.

## Validation in Unity Editor (no build)
One-shot harness:
`Assets/Editor/FunGuyStartupWarmupAudit.cs`.
Request path:
`work/windows-profiling-2026-11-09/startup-prewarm/test-startup.request`.
QA result:
`work/windows-profiling-2026-11-09/startup-prewarm/test-startup.final.txt`.
Screenshot of progress screen:
`work/windows-profiling-2026-11-09/startup-prewarm/loading-cover-preview.png`.

- Two successful Play Mode sessions, one after fixing the progress bar.
- Final automated check **PASS=True**, including:
  `warmupObserved=True`, `disabledDuringWarmup=True`,
  `coverObserved=True`, `coverRemoved=True`.
- **14** distinct large environment instances were created and retained.
- Final test warmed **93 grid instances**, with **370 cumulative grid pool
  acquisitions** after warm-up and final reset. Previous run warmed 99 grid
  instances and had 349 pool acquisitions. Variation arises from randomized
  grid course contents.
- Player remained Disabled until `IsStartupReady=True`, then Active.
- Full Z0–2260 stream preparation completed without triggering the soft
  Unity-memory budget. The final run reported ~**5.86 seconds** using a
  `System.Diagnostics.Stopwatch` in this Editor session; this is not
  a standalone Windows-player load-time benchmark.
- The progress bar screenshot was inspected visually and shows a properly
  sized orange fraction (about 31% fill when the text reads 31%).
- No new compile errors. The new movement null guard removed the prior
  Play-exit `RunnerMovementMotor` destroyed-object exception in the final QA.
- Scene returned to Edit Mode. No request/active marker remained.

## RAM and GPU caveats
- This warms prefab construction, Unity asset loading and renderer registration
  through the regular production stream code. It holds the resulting inactive
  object hierarchies in RAM; this is intentional and trades memory/loading
  duration for fewer gameplay hitches. The extra-memory budget is a **soft
  Unity-reserved/allocated-memory delta**, not proof of system-wide free RAM
  or GPU VRAM available.
- Renderer activation touches GPU Resident Drawer state but does **not**
  guarantee that all textures are uploaded or all DX12 pipeline state objects
  (PSOs) are compiled. A camera kept near the start may not render all distant
  material permutations.
- The optional `ShaderVariantCollection` field is currently unassigned.
  Broad `Shader.WarmupAllShaders()` is deliberately **not** used because it
  can compile many unused variants, increase load time and memory.
- Unity 6.3 PSO tracing (`GraphicsStateCollection`) requires a Development
  Build. The user has explicitly disallowed creating a build without asking,
  so the PSO collection cannot truthfully be reported as captured/prewarmed.
  Future path: with explicit permission, trace DX12 PSOs in a development
  build, save the collection and warm it in controlled loading batches.
- A standalone Windows Development Build and profiler validation still require
  explicit user permission; none made in this task.
- For a seamless menu->game transition (rather than a cover after scene
  activation), eventually change the main-menu path to `LoadSceneAsync`
  and keep a persistent loading overlay across the transition. This was
  **not** changed now to avoid disrupting scene/menu UX.

## Visual constraints
Both active Mobile and PC URP assets still have `m_RenderScale: 1`. No
graphics, lighting, shadow, material, texture or quality-setting changes were
made during the staged-loading implementation. GPU Resident Drawer was not
disabled.

## Existing related work
- `Docs/FUNGUY_FULL_COURSE_OPTIMIZATION_20261110.md`
- `Docs/FUNGUY_RENDERER_PRELOAD_AB_TEST_20261109.md`
- `Docs/FUNGUY_CPU_PROFILING_20261109.md`
