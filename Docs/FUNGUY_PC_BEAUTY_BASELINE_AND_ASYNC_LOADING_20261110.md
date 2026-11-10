# FunGuy â€” PC Beauty Baseline, Async Scene Loading and Profiling
Date: 2026-11-10 (Asia/Amman)
Project: D:/Work/FunGuy, branch Analysis, Unity 6000.3.9f1.

## Constraints
Keep Windows native render scale at 1.0, retain beauty-first graphics, profile unlocked gameplay FPS with VSync off. Do not overwrite old builds or saved scene changes. The 120 FPS cap is menu-only to prevent redundant CPU load and live-Profiler buffer overflow.

## Windows PC Quality Configuration
The previous QualitySettings excluded PC from Standalone and selected Mobile for Windows. This was the major reason earlier Windows capture used Mobile quality.
Updated `ProjectSettings/QualitySettings.asset`:
- Include PC in Standalone quality; default Standalone and active Editor tier PC (index 1).
- PC VeryHigh shadow resolution, 4 cascades, 120-unit shadow distance, LOD Bias 2.5; PC VSync=0.
- Android and iOS still default to Mobile; Mobile assets unchanged.
Updated `Assets/Settings/PC_RPAsset.asset`:
- Render scale remains 1.0; HDR and PC_Renderer SSAO already enabled; Forward+ and SRP Batcher remain on.
- Four-sample MSAA, 4096 main-light shadow resolution, 4 cascades, 120 shadow distance.
- Additional light shadows on; per-object light limit 8.
- GPU Resident Drawer Instanced Drawing enabled (Forward+ compatible).
- GPU occlusion culling not yet enabled, to avoid unverified visual/occlusion regressions.
- PC quality is a starting beauty baseline to profile, not proven globally optimal.

## Async persistent gameplay transition
Menu previously used blocking `SceneManager.LoadScene`. New:
- `MainMenu.Load` invokes `FunGuySceneLoadingTransition.Begin`; no saved scene changes.
- New `Assets/_Game/Funguy.MushroomRunner/Core/FunGuySceneLoadingTransition.cs`: root persistent runner + async loading coroutine; loading cover appears while menu is still rendered, then runs `SceneManager.LoadSceneAsync` with activation after progress reaches 0.9 and a frame of ready-cover rendering. The runner survives menu scene destruction.
- `FunGuyStartupLoadingOverlay.ShowDuringSceneLoad/AcquireForGameplay` shares one persistent full-screen canvas between menu load and gameplay staged warmup, with progress 0-30% scene load and 30-100% world prewarm.
- `RunFlowCoordinator` keeps the cover until after the first ready frame and then closes it. Existing warming of blocks/grid and its memory budget are retained.
- Menu's authored logo can be assigned via Inspector or inferred from an Image named Logo. No duplicate/changed menu prefab.
- In scenes without RunFlowCoordinator (e.g. infinite mode), temporary driver closes the cover after async activation.
- Imported D3D12 PSO trace is Mobile quality and should skip on PC until a PC trace is captured.

## Validation plan
Windows Development Build using PC tier, native 1920x1080, render scale 1.0, VSync disabled and gameplay uncapped.
Check: PC tier selected in build and in player, menu scene transit and loading cover visually correct, first-run warmup timings and pool counts, DX12 PSO trace, per-frame CPU/GPU/GC, and crash/warnings.
PresentMon measures median, P95, P99 CPU busy, GPU busy and frame presents. Start with opening gameplay then move through more environments to identify visual-quality outliers.
Prior 1080p Mobile-tier results (starting area): median CPU busy 4.03 ms, GPU busy 2.32 ms. Not an apples-to-apples equivalent of PC graphics.
No further player build after the approved PC Development Build without permission.

See `Docs/FUNGUY_WINDOWS_PLAYER_1080P_PROFILING_20261110.md` for earlier profiling.

## Player validation results — 2026-11-10 09:50 PC build

The authorized PC DevBuild succeeded: `Builds/Windows-Development-Profiler/20261110-092921/FunGuy.exe` (Unity 6000.3.9f1, IL2CPP Windows DX12, quality=PC, renderScale=1.0, 1920x1080, VSync=0, gameplay target uncapped). Build result: zero errors and 14 warnings. Original builds were not overwritten.

The clean run's player-side probe is under:
`C:/Users/USER/AppData/LocalLow/One More Game/FunGuy/FunGuyProfiling/20261110-095043/`
Files: `player-frames.csv`, `hardware-and-events.log`, `capture-finished.txt`, and `windows-dx12-graphics-states.graphicsstate`.

Measured steady playable phase (5,559 frames) from `analyze_windows_player.py`:
- Unity CPU/total frame time median **7.303 ms**, P95 **8.669 ms**, P99 **9.332 ms**, mean **7.408 ms**, longest frame **17.374 ms** (about 135 FPS equivalent from mean frame time).
- CPU Main Thread Frame Time median **7.286 ms**, P95 **8.649 ms**, P99 **9.305 ms**; GPU busy time is **NOT measured**, so do not declare CPU-bound versus GPU-bound based only on these counters.
- Median draw calls **372**, median batches **370**, median SetPass calls **155**, median GC allocated **1.842 KiB/frame**.
- `Loading.ReadObject` and `AsyncReadManager.SyncRequest` were zero on recorded playable frames; first-use work was moved to the staged-loading period.
- First 320 menu frames mostly ~8.33 ms per frame (120 FPS cap), independently confirming idle menu isn't rendering at hundreds of FPS anymore.
- Staged-world phase 79 frames, median **4.307 ms**, P95 **18.281 ms**, longest **49.983 ms** (cost falls behind loading cover).

Prior Mobile quality DevBuild opening camera had median ~4.167 ms (P95 4.76, P99 5.763), 207 median draw calls. These are DIFFERENT quality settings and sessions, not an A/B isolation of any one graphics feature. The PC visual settings are more expensive but remain substantially above 60 FPS in this measured opening view. There is no reliable GPU busy reading because PresentMon recorded no frame rows on this machine despite successfully starting its trace; it warned about lack of elevated permissions. Do not invent GPU metrics or change Windows permissions without user approval.

In the clean run, actual logs confirmed `sceneLoaded=MushroomRunnerGameplay`, `RunFlowCoordinator` warmup prepared Z0-2260 with **14** environment blocks and **100** grid objects, **519 ms** staging, and `[FunGuy SceneLoad] MushroomRunnerGameplay visible after **0.76 seconds**; activated=True`. An earlier run with cold/other cache took 5.03 seconds total transition and 2.63 seconds warmup; startup speed varies across runs. Screenshots `work/windows-profiling-2026-11-10/pc-beauty-run/01-loading-immediate.png` show the persistent logo/progress cover at 3% with no black screen; `02-loading-progress.png` shows the game after transition, `04-gameplay-stable.png` and `05-gameplay-moving.png` show intact PC forest rendering. The file named *moving* should NOT be interpreted as proof of actual movement: score and speed remain 0 despite the automated W key test; a second W+Space focus test still showed 0. Therefore only opening-camera costs have been benchmarked; late biome/dense-moving visuals are unverified. Scene `RunnerInputSource.enableKeyboardFallback=1`, and the player prefab has a source reference, so further runtime input/physics debugging is required before full traversal claims.

No new DOTween missing-target warning was observed in the clean PC `Player.log` after the source `UIButtonAnimator.OnDisable` tween cleanup. Convex MeshCollider warnings remain for `Cap` and `Mushroom_9.1_Mushrooms_0.002` source meshes; fix with low-poly collider-only proxies without removing visual detail.

Captured Windows/DX12/PC graphics-state collection: **51 shader variants, 69 PSO graphics states**. It was copied without modification to `Assets/StreamingAssets/FunGuy/PSO_D3D12_Windows_PC_20261110.graphicsstate`, with SHA-256 2023CCCF1395392832EBF01A4054FD84969D22879B2F44FCFAC50B772919E067. `RunFlowCoordinator` post-build source selects either `PC` or `Mobile` prewarm file by active quality tier. The existing PC build was compiled BEFORE that PC-specific PSO integration; another build would be needed to validate actual prewarm benefit and must be approved separately. Collection is partial (starting environments only).

### Recommended next steps
1. Debug why simulated W/Space input did not move the player in the Windows build, and get a repeatable real movement/camera traversal to the late biomes.
2. Obtain valid per-frame GPU busy time via properly configured graphics profiling, rather than assume the source of the 7.3ms frame time. Compare GPU-heavy PC features one at a time only when the measured cost justifies the trade-off.
3. Expand the PC DX12 graphics-state trace by playing through all biomes; then validate staged PSO warm-up with a separately authorized build.
4. Preserve 1.0 native render scale and visual quality until there is evidence for a specific change. Keep shadow/SSAO/MSAA quality comparisons optional with user approval.
