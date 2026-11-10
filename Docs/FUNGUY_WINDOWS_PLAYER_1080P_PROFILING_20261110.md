# FunGuy — Windows Development Player Profiling, Native 1080p
**Date:** 2026-11-10 (Asia/Amman)
**Project:** `D:/Work/FunGuy` · Unity 6000.3.9f1 · branch `Analysis`
**Build:** `Builds/Windows-Development-Profiler/20261110-011321/FunGuy.exe`

## Scope and configuration
- User explicitly authorized this one Windows Development Build. Unity returned **Succeeded**, **0 build errors**, **15 warnings**, 3,099,758,314 bytes (~2.89 GiB), build time 13m28s (first IL2CPP native compilation).
- Build flags: `Development | ConnectWithProfiler | AllowDebugging | DetailedBuildReport`.
- Windows Build Profile scene order: `MainMenuPGC`, `MushroomRunnerGameplay`, `MushroomRunnerGameplay_Infinite`.
- Windows standalone active quality tier was **Mobile**; graphics API forced to **Direct3D 12**, URP renderer scale **1.0**, **GPU Resident Drawer ON** (`InstancedDrawing`). This is **not** the separate PC URP quality tier; do not accidentally describe the numbers as the PC tier.
- Test hardware: NVIDIA RTX 3060 12 GiB, ~32 GiB system RAM. Measurements via player-side Unity `ProfilerRecorder` and PresentMon 2.6.
- Test resolutions: **1600x900** and **1920x1080** in windowed mode. Real runner/camera remained in the opening part of the course at very low score/speed. No claim about late environment or intense gameplay render loads.
- The original older executable `Builds/New folder/FunGuy.exe` was preserved. All graphics/quality file hashes were left unchanged:
  - `Assets/Settings/Mobile_RPAsset.asset`: `F4C675496B643C6C1CF147900CC3DDA06A950EE9C8CA92E87B1D33CC87B6BB6C`
  - `ProjectSettings/QualitySettings.asset`: `38668B630F4AF365646BF991C380FCAAF6258D546A7FE559132EC6525222BDCF`

## Measured gameplay frame times (players)
The first frame after transition is excluded from the steady player interpretation. Playable-phase Unity counters:

| Metric | 1600x900 | 1920x1080 |
|---|---:|---:|
| Playable frames in player CSV | 2,246 | 9,188 |
| Median Unity total frame time | 4.17 ms | 4.17 ms |
| P95 Unity total frame time | 4.39 ms | 4.76 ms |
| P99 Unity total frame time | 4.92 ms | 5.76 ms |
| Max Unity total frame time (transition tail included) | 34.68 ms | 35.77 ms |
| Median draw calls | 205 | 207 |
| Median GC allocated in frame | ~1.84 KiB | ~1.84 KiB |
| Streaming `Loading.ReadObject` during playable frames | 0 ms | 0 ms |
| Additional large-block instances created during initial warm-up | 14 | 14 |

Player CSV locations:
- `C:/Users/USER/AppData/LocalLow/One More Game/FunGuy/FunGuyProfiling/20261110-015131/player-frames.csv` (1600)
- `C:/Users/USER/AppData/LocalLow/One More Game/FunGuy/FunGuyProfiling/20261110-015447/player-frames.csv` (1080)

A separate 15-second PresentMon capture of the *stationary opening gameplay area* confirms CPU greater than GPU work:

| PresentMon metric | 1600x900 median / P95 / P99 | 1920x1080 median / P95 / P99 |
|---|---|---|
| Between presents | 4.158 / 5.134 / 5.714 ms | 4.195 / 5.557 / 6.310 ms |
| CPU busy | 4.018 / 5.003 / 5.560 ms | 4.033 / 5.372 / 6.139 ms |
| GPU busy | 1.796 / 2.361 / 3.182 ms | 2.319 / 2.994 / 3.736 ms |
| GPU time | 1.806 / 2.591 / 3.248 ms | 2.462 / 3.751 / 4.215 ms |

PresentMon files in `work/windows-profiling-2026-11-10`:
`presentmon-third-gameplay.csv`, `presentmon-1080p-gameplay.csv`.
Reusable analysis scripts:
`analyze_windows_player.py`, `analyze_pm.py`.

**Conclusion:** The opening camera is **more CPU-limited than GPU-limited**, even at 1920x1080 render scale 1.0. Don't reduce quality without measuring a later heavy camera angle or busy movement. Data is from a *Development* build with live profiler and native instrumentation, not a shipping release FPS target.

## Staged preparation: validated in the actual Windows player
- 1600x900 capture: streamed Z0-2260 and populated **14** environment-block prefab instances and **90** grid prefab instances. First-run preparation reported ~**2.03 seconds**, with an expensive scene-load transition not fully covered by the overlay; subsequent playable frames had no synchronous asset file-loading sample.
- 1920x1080 capture: 14 blocks, 78 grid prefabs (randomized distribution), **~444 ms** authored staging after scene activation, with 78 staged frames recorded in the player. Different cold/warm cache states produce variable loading times; no repeatable absolute startup-time claim.
- In both runs, the recorded player progressed from `runWarming=True` to `runReady=True`. The production pool supplies the warmed objects after scene reset.
- **Important remaining UX issue:** `Assets/_Game/Art/Logo/MainMenu.cs` still uses synchronous `SceneManager.LoadScene`. This can freeze the menu before the in-game warm-up overlay appears. For a seamless launch, use `LoadSceneAsync` with a persistent/loading canvas and coordinate scene activation with prewarming.

## DirectX 12 PSO capture: now available
Unity `GraphicsStateCollection.BeginTrace` / `EndTrace` / `SaveToFile` succeeded in the Windows player:
- Menu-only capture: 5 variants / 6 states; not useful for forest.
- 1600x900 forest-opening capture: **38 shader variants / 53 graphics states**.
- 1920x1080 forest-opening capture: **42 shader variants / 60 graphics states**.
- The 1080p collection was copied *exactly* to
  `Assets/StreamingAssets/FunGuy/PSO_D3D12_Windows_Mobile_20261110.graphicsstate` (353,969 bytes). JSON metadata: `m_DeviceRenderer=18` (D3D12), `m_RuntimePlatform=2` (Windows Player), `m_QualityLevelName=Mobile`, 42 variant records. It is a partial collection, because the camera did not traverse all environments.
- After the successful build, `RunFlowCoordinator` source was updated to optionally load this file on Windows/DX12 with a matching quality tier and prewarm in batches of 12 graphics states using Unity's experimental `GraphicsStateCollection.WarmUpProgressively`. It safely skips other APIs/quality tiers and its batch loading happens behind the gameplay cover, before world streaming. This NEW GPU-prewarm source change is **not part of the already-built executable**. It must be tested in another Windows build, only after further user approval. Shader-variant-only fallback remains optional.
- Source version builds and scene data were not modified further for another executable. No second Windows player build occurred.

## Runtime issues found and source remedies
1. **Uncapped main menu:** The initial menu repeatedly rendered at hundreds of FPS (first run ~56,500 frames/90s). The Unity Profiler connection buffer overflowed its **128 MiB** configured limit. In post-build source, `Assets/_Game/Art/Logo/MainMenu.cs` now caps menu FPS at **120** only when VSync is disabled, restoring the former target on leaving the menu. It does not cap gameplay.
2. **Destroyed DOTween target during menu-to-game transition:** Reproducible `Transform.DOScale` safe-mode warning. The menu `UIButtonAnimator` schedules a two-stage scale tween, but the `SceneManager.LoadScene` callback can destroy the button while the second tween still runs. Post-build source adds `transform.DOKill(false)` in `UIButtonAnimator.OnDisable` to cancel tweens before destruction. Await player verification on the next approved build.
3. **Convex collider warnings:** Mushroom meshes `Cap` and `Mushroom_9.1_Mushrooms_0.002` exceed PhysX's 256-polygon convex hull limit. Unity uses partial hulls. Consider simplified collider-only proxy mesh or low-poly collider to improve collision accuracy, *without changing visible mesh quality*. Not yet edited.
4. **D3D12 device crash in one extended menu/profiler run:** Crash log under
   `C:/Users/USER/AppData/Local/Temp/One More Game/FunGuy/Crashes/Crash_2026-11-09_223546482/Player.log` states `Unrecoverable D3D12 device error` (HRESULT 0x80004005). It specifically says no local or nonlocal VRAM exhaustion and reports GPU local usage ~182 MB against an ~11.9 GB budget. Separately, the Unity live Profiler showed a buffer overflow. Causality **not established**; do not blame GPU Resident Drawer/graphics quality. Shorter gameplay captures completed and produced valid graphics-state files.
5. **Missing custom markers in standalone player-side recorder:** Most relevant `FunGuy.*` profiler markers and `Other|GPUResidentDrawer` were not discoverable via `ProfilerRecorderHandle.GetAvailable` at probe initialization, likely registration timing/category. Frame timings and render counters were captured, so measurements remain useful. For new builds, bind to markers after the gameplay scene has initialized or capture Unity Timeline directly.
6. **The Windows build is using the Mobile quality tier.** This is the user's current setup, preserved intentionally. For a beauty-first PC game, eventually benchmark the authored PC tier side-by-side. Do not silently enable PC tier until user chooses the quality/performance trade-off.

## Priority order going forward
1. **Scene transition quality:** Change synchronous main-menu scene load to an async loading flow with a persistent visible cover (no black/hard hang). Preserve current animation/layout.
2. **Profile actual moving player and camera** across dense foliage and all biomes, at 1920x1080, with render scale 1.0. The current starting-area GPU margin is not proof of late-course capacity.
3. **Expand graphics-state trace** during real movement into later environments, then revalidate progressive PSO warm-up in a subsequent authorized Development Build.
4. **Separate A/B of GPU Resident Drawer** in Windows player only under identical visual and movement conditions, with user approval for another build if needed.
5. **Clean collider proxies**, validate button tween destruction fix, cap UI FPS, and monitor system/Unity/VRAM allocations over multiple restarts. A prior cold trace included first-use renderer activation spikes; preloading fixed repeated pool construction but cold driver/PSO effects still need player verification.

**No further build** after the code edits above; remember the user's rule to ask before every new build. All current URP settings and render scale remain unchanged.
