# FunGuy — Verified Editor Profiling Findings

Date: 2026-10-31. Unity 6000.3.9f1. Scene: MushroomRunnerGameplay. Mobile quality, Direct3D 12, NVIDIA GeForce RTX 4080 Laptop GPU. Viewport: 718 × 1367. No Android build performed.

## Capture status

**COMPLETE** — status.json reports 600 captured frames and no error. The profiler run warmed up for 120 frames, recorded 600 frames, then captured two Memory Profiler snapshots 600 game frames apart. Unity MCP was verified at http://127.0.0.1:8080/mcp (mcp-for-unity-server 3.4.11, FunGuy instance).

Files are in `.utmp/profiling/editor-2026-10-31/`:
- `editor-cpu-profile.raw`: Unity CPU Profiler capture for Profile Analyzer.
- `cpu-aggregate.json`: frame, CPU, GPU, graphics and allocation counters.
- `memory-early.snap`, `memory-late.snap`: Unity Memory Profiler snapshots (~1.94 GB each on disk).
- `memory-counters-early.json`, `memory-counters-late.json`, snapshot results, `environment.json` and `result.json`.

The two snapshots are **Editor-process snapshots**, not isolated game memory. Their file sizes do not indicate RAM used.

## CPU and GPU frame timing

| Metric | Editor measurement |
|---|---:|
| Mean frame time | **7.774 ms** |
| P95 frame time | **10.345 ms** |
| Worst frame | **32.778 ms** |
| FPS equivalent from mean time | **~128.6 FPS** |
| Mean main-thread counter | **7.752 ms** |
| GPU Frame Time recorder mean | **3.140 ms** |
| GPU Frame Time recorder P95 | **4.137 ms** |
| GPU Frame Time recorder max | **5.345 ms** |
| GPU time from raw-frame API | **No samples** |

The independent GPU Frame Time ProfilerRecorder returned valid samples even though Unity's raw-frame GPU-time API did not. **GPU time per frame is not GPU utilization percentage.** The GPU recorder's minimum sample was zero, so its reported average may contain missing/idle samples; verify timing validity in the GPU Profiler before treating this as a definitive workload measurement. These numbers apply to Editor Play mode on a powerful laptop GPU, not mobile.

The last-120-frame CPU marker summary: Main Thread 7.09 ms, EditorLoop 4.51 ms, PlayerLoop 2.27 ms, RenderPlayModeViewCameras 1.37 ms, URP render loop 0.97 ms, WaitForJobGroupID 0.55 ms. These are nested inclusive markers and must not be added together. The EditorLoop includes tooling overhead.

## Rendering and GC recorder statistics

| Metric | Mean | P95 |
|---|---:|---:|
| Draw calls | 710 | 1,397 |
| SetPass calls | 434 | 846 |
| Batches | 709 | 1,394 |
| Triangles | 246,461 | 488,844 |
| Shadow casters | 189 | 376 |
| Instanced batches | **0** | 0 |
| Physics.Simulate | 0.067 ms | 0.197 ms |
| LandingAssist.Search | 0.028 ms | 0.198 ms |
| GC Allocated in Frame | 42.7 KiB | 61.8 KiB |

The Editor rendering counters include zero readings and vary considerably; do not interpret their means as a stable on-device draw workload. Zero instanced batches does not imply the SRP Batcher is disabled.

## Two Memory Profiler snapshots

Both captures refer to the **entire Unity Editor process**. Values in MiB (binary megabytes).

| Memory category | Early | Late | Change |
|---|---:|---:|---:|
| Total allocated | 4,004.909 | 4,006.364 | **+1.455** |
| Total reserved | 4,577.234 | 4,578.234 | **+1.000** |
| Managed memory used | 1,028.598 | 1,045.086 | **+16.488** |
| Managed heap capacity | 1,553.242 | 1,553.242 | 0 |
| Graphics-driver allocated | 640.404 | 640.405 | ~0 |

Snapshot timestamps were ~8.35 seconds apart, at Unity game frames 721 and 1321. The +16.49 MiB managed-memory increase **does not prove a leak**; this short interval includes Editor activity and GC behavior. Use Memory Profiler's comparison to find persistent object owners before diagnosing leaks.

These memory categories overlap. Do not add Unity allocated, managed used, graphics memory and operating-system usage together.

Earlier Windows-process OS counters recorded about **3.34 GiB resident RAM**, **7.90 GiB private commit**, and **857 MiB dedicated GPU memory** attributed to the Editor. Those are not game-only figures, were sampled at another time, and are not additive with Unity's counters. Reported GPU capacity (11,995 MB) is not actual use.

## Historical Android comparison (separate saved audit)

On Xiaomi 22101316G, Mali-G68 MC4, Vulkan, 1080 × 2400, an earlier Development Player averaged **39.02 ms/frame (~25.6 FPS)** and 729 draw calls/frame. App PSS was around 727 MiB. The Android trace included approximately 23.4 ms/frame of `Gfx.WaitForPresentOnGfxThread`; this is waiting time and not GPU execution time. **Android GPU timing remains unavailable.**

The new Editor timing cannot confirm Android performance. Differences in hardware, resolution, captures and workloads prevent a controlled FPS comparison.

## Tools and remaining investigation

- **Unity Profiler:** CPU raw capture saved successfully.
- **Memory Profiler 1.1.12:** early and late `.snap` files saved successfully; open in Window > Analysis > Memory Profiler and use Compare to identify textures, meshes, render textures, managed/native objects and retained references. Detailed object ownership is **not yet extracted**.
- **Profile Analyzer 1.3.4:** package installed and window open; `editor-cpu-profile.raw` is ready for marker and spike analysis. No matched second capture or formal A/B comparison has been completed.

Priorities: (1) Memory Profiler retained-object comparison; (2) Profile Analyzer frame spikes, especially previously recorded `GameplayManager.FixedUpdate` outlier and `MushroomLauncher.FixedUpdate` call volume; (3) rendering/shadow and batching evidence; (4) later, only after Editor review, matched on-device GPU and CPU profiling.

**No build was created or game optimization settings changed.**

Raw source: `.utmp/profiling/editor-2026-10-31/`. Historical context: `Docs/FunGuy_Performance_Audit_2026-10-31.md`.
