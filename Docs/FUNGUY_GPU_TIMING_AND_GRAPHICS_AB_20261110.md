# FunGuy — Windows FrameTimingManager + Graphics A/B Lab (2026-11-10)
## What was built
The Development Player has an independent GPU/CPU timing logger and an on-screen diagnostic panel, added in `Assets/_Game/Scripts/Profiling/FunGuyGpuTimingLab.cs`. It is attached to the existing development-only `FunGuyWindowsDevelopmentProbe` under `[RuntimeInitializeOnLoadMethod]` and does not ship in a release player.

**F8** toggles the PC graphics panel (right side of game). Panel has:
- CPU/GPU live frame timings and count of valid GPU frames.
- Recording toggle for detailed per-frame CSV.
- **RESTORE ORIGINAL PC GRAPHICS** (returns all baseline URP settings).
- Shadow distance slider (20–200 units), quick presets 40/80/120/160.
- Main shadow atlas presets 1024/2048/4096.
- Shadow cascades 1/2/4.
- Main directional light shadows toggle (for visual-cost test only).
- SSAO on/off (via active URP Renderer Feature).
- HDR on/off.
- MSAA off/2x/4x.
- LOD bias 1.5/2.0/2.5.
- **RUN ONE-AT-A-TIME A/B SUITE** and **CANCEL**.

**Native render scale is deliberately locked at 1.0.** GPU Resident Drawer remains enabled; the panel doesn't modify it. VSync remains disabled and gameplay FPS stays unlocked. Settings are changed in runtime only and restored after automated tests, on scene changes, and on exit; URP project assets are not written or saved by the player.

## FrameTimingManager
The Development Build enables `FrameTimingManager` by default on DX12, and it is queried once each `LateUpdate`, deduplicating delayed `frameStartTimestamp` where available. Unity reports GPU timing about four frames after submission; transitions between graphics settings are excluded from each A/B test by a **1.75-second settling period**.

`frame-timing-gpu.csv` logs:
- Scene, player Z, realtime elapsed, frame ID and FrameTiming timestamp.
- CPU frame time, main-thread CPU, render-thread CPU, GPU frame time, GPU validity, Unity delta.
- Exact active shadow distance, atlas size, cascade count, MSAA, SSAO, HDR, main sun shadow mode, LOD bias and scale 1.0.
- A/B case number and settle vs measure phase.

A GPU sample of zero/nonfinite/unavailable is **NOT recorded as 0ms GPU work**; it remains empty with `gpu_valid=0`. The user-visible panel displays unavailable GPU time and the valid sample count.

## A/B testing methodology
The automatic suite has 12 cases, each for 7 seconds: the first 1.75 seconds allow shaders/driver/hardware changes to stabilize, then 5.25 seconds are measured. One graphics property is changed at a time versus the ORIGINAL PC baseline; all other settings are restored between test cases:
1. Original PC baseline.
2. Shadow distance 80.
3. Shadow distance 40.
4. Two cascades.
5. Main shadow atlas 2048.
6. Main shadow atlas 1024.
7. SSAO off.
8. MSAA 2x.
9. MSAA off.
10. HDR off.
11. Main sunlight shadows off.
12. LOD bias 1.5.

The suite is most meaningful from the **same scene and exact camera/player position** (static A/B). A moving view introduces large geometry and lighting variation and invalidates per-feature attribution; record additional free movement traces separately. Small deltas should be retested in a second run to rule out noise. UI logging/Development Player itself adds overhead, so results are *comparisons*, not shipping release FPS.

To start the suite automatically for testing, pass `-funguy-run-ab` on the executable command line. It queues the suite until the regular `MushroomRunnerGameplay` scene has completed its staged loading. The menu and infinite mode do not automatically trigger the suite.

## Files and usage
All timing files live under the player data directory:
`%USERPROFILE%\AppData\LocalLow\One More Game\FunGuy\FunGuyProfiling\<timestamp>\`

- `frame-timing-gpu.csv` — per-frame CPU+GPU and current graphics settings.
- `graphics-ab-results.csv` — one summary row per test case (CPU/GPU average, P50/P95/P99, GPU validity count, scene and player Z).
- `player-frames.csv` — existing Unity profiler marker/draw/GC counters (shorter separate capture duration).
- `hardware-and-events.log` — hardware, quality, shader graphics-state trace.
- `windows-dx12-graphics-states.graphicsstate` — captured graphics states for later PSO prewarm.

Use `work/windows-profiling-2026-11-10/analyze_gpu_ab.py <run-directory>` for readable side-by-side CPU/GPU differences and their signs. Negative delta means a setting was faster; it is only meaningful if it has enough valid GPU samples.

## Remaining limits
- URP shadow/SSAO effects may have weak or no effect at the starting camera position; repeat after moving the player into dense environments.
- Runtime toggles for a particular feature may be limited by shader variant stripping or runtime pass construction; confirm visual change on screen and report any unsupported setting.
- Dynamic changes to GPU settings can cause temporary shader compilation or texture reallocations; the first 1.75 seconds are discarded per case.
- This suite cannot by itself prove actual GPU utilization or VRAM residency; it measures Unity GPU frame timing as supported by DX12.
- For a release comparison, use an instrumentation-disabled release benchmark later; don't cut the graphics before running tests on the dense biomes.

**Build:** new Windows PC Development Build, approved by user; render scale 1.0; previous builds preserved.
## Completed Windows Development Build and controlled measurements
Build succeeded (0 errors, 13 warnings):
`D:/Work/FunGuy/Builds/Windows-Development-Profiler/20261110-102545/FunGuy.exe`
Quality=PC, RenderScale=1.0, GPU Resident Drawer enabled, DirectX12, IL2CPP.
The executable includes the F8 controls and development FrameTimingManager.

Live player confirmed `FrameTimingManager.IsFeatureEnabled() == true` and valid nonzero GPU samples.
Recorded run:
`C:/Users/USER/AppData/LocalLow/One More Game/FunGuy/FunGuyProfiling/20261110-103448/`
- `frame-timing-gpu.csv` (per frame, including settings and valid GPU timing)
- `graphics-ab-results.csv` (two suites of 12 cases)
- `hardware-and-events.log` (GPU RTX3060 DX12)
- `windows-dx12-graphics-states.graphicsstate` (83 variants, 183 states in this test)
- A/B interpreter: `work/windows-profiling-2026-11-10/analyze_gpu_ab_sessions.py`.

The first suite traversed several Z positions (33.29 to 169.28 to ~0), so its cross-case comparisons were **INVALID due to movement/reset**. A second suite was run at constant player Z=-0.40 (range zero), with the same opening camera pose for all 12 cases. That second suite is the one used below. All changes are one-at-a-time relative to ORIGINAL PC settings, not cumulative.

| Independent setting | CPU median ms | GPU median ms | GPU improvement vs default |
|---|---:|---:|---:|
| PC default: 120 shadow distance, 4 cascades, 4096 atlas, SSAO ON, MSAA4, HDR ON, sun shadows ON, LOD 2.5 | 7.6215 | 7.5745 | baseline |
| Shadow distance 80 | 7.1778 | 7.0687 | 6.7% |
| Shadow distance 40 | 6.4118 | 6.1379 | 19.0% |
| 2 shadow cascades | 6.0854 | 5.8051 | 23.4% |
| Shadow atlas 2048 | 6.1263 | 5.8368 | 22.9% |
| Shadow atlas 1024 | 5.6988 | 5.0975 | 32.7% |
| SSAO OFF | 5.3810 | 5.0309 | 33.6% |
| MSAA 2x | 7.4317 | 7.3728 | 2.7% |
| MSAA OFF | 7.2429 | 7.1782 | 5.2% |
| HDR OFF | 7.6101 | 7.5622 | 0.2% |
| Main sunlight shadows OFF | 5.2056 | 3.3966 | 55.2% |
| LOD bias 1.5 | 7.4776 | 7.4250 | 2.0% |

GPU valid sample counts ranged from **677 to 931 per case** in stationary suite; there is no need to infer GPU timings. The original baseline restored after both suites and was measured again using the last 8 seconds of samples: CPU median 7.606ms, GPU median 7.552ms, extremely close to initial stationary CPU 7.6215ms/GPU 7.5745ms. That argues against simple order/cold-cache explanation for these large deltas.

**Interpretation for beauty-first PC:**
- Do **not** disable main sunlight shadows purely for performance. It gains ~4.18ms of GPU time but severely damages scene depth.
- SSAO is costly (~2.54ms GPU in this view); compare lower-cost SSAO source/downsample/sampling quality against visual difference before disabling.
- Shadow atlas 2048 or two cascades independently save ~1.74-1.77ms GPU. Verify contact/detail and cascade transitions at several camera locations before committing to a new default.
- Changing shadow distance from 120 to 80 saves ~0.51ms GPU, potentially a good compromise if distant tree shadows are not obvious.
- HDR ON and 4x MSAA are comparatively inexpensive here; preserve them pending heavier scene tests.
- LOD bias 1.5 does little at the stationary starting view and may hurt background geometry silhouettes; leave default 2.5.
- CPU and GPU frame times overlap; `cpuFrameTime` can include waits. Reductions in CPU time during graphics toggles do NOT imply game-logic CPU work was eliminated.
- Do not add the percentages or extrapolate across all biomes; this is **one view on RTX 3060 at 1920×1080**. Need moving camera and later course capture to generalize.
- The frame time overlay and CSV instrumentation themselves have Development Build overhead. Use paired comparisons to assess deltas, not exact shipping-release FPS.

**Posttest:** The suite returned the PC settings to 120/4096/4/4x/SSAO on/HDR on/soft sun shadows/LOD2.5. The output frame rows explicitly confirm `render_scale=1.000` and those baseline settings.
