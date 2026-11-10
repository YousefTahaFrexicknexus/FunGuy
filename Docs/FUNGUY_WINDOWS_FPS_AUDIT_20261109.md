# FunGuy Windows FPS / frame pacing investigation — 2026-11-09

## Scope and constraints
- Existing Windows player only: `D:/Work/FunGuy/Builds/New folder/FunGuy.exe`.
- Branch `Analysis`, HEAD `453e564` at inspection time.
- No builds, scene modifications, quality changes, or gameplay-code edits were made.
- Windows 11, Unity 6000.3.9f1 player, Direct3D 12, NVIDIA RTX 3060.
- Targeted visible FunGuy player PID 1468, launched at 1280 x 720 with a separate player log.
- **Not a controlled gameplay benchmark:** League of Legends was the foreground process during measurement, and a second FunGuy.exe (PID 75288) was running without a visible window. Treat values below as exploratory.

## PresentMon capture and recovery
- Intel-signed PresentMon 2.6.0 console binary was already under `work/windows-profiling-2026-11-09/`.
- An orphaned ETW trace session named `PresentMon` initially blocked capture; running a second session produced substantial lost ETW events and no CSV.
- `logman stop PresentMon -ets` successfully cleared that stale session. Subsequent captures produced CSV frame data with no lost-event warning.
- Reusable analyzer: `work/windows-profiling-2026-11-09/analyze_windows_capture.py`
- Capture A: `windows-after-stale-session-cleanup.csv` (~10 s, 970 presents).
- Capture B: `windows-confirmation-20s.csv` (~20 s, 1990 presents).

| Measurement | Capture A | Capture B |
|---|---:|---:|
| Mean app present FPS (1000 / mean present frame time) | 97.36 | 99.90 |
| Median instantaneous present FPS | 124.35 | 125.58 |
| Mean present frame duration | 10.27 ms | 10.01 ms |
| P95 present frame duration | 24.75 ms | 25.33 ms |
| P99 present frame duration | 62.28 ms | 57.90 ms |
| 1% low FPS (1000 / mean of slowest 1% durations) | 11.49 | 12.61 |
| Frames above 16.67 ms | 131 / 970 | 254 / 1990 |
| Frames above 33.33 ms | 29 / 970 | 70 / 1990 |
| Average CPU busy | 10.08 ms | 9.81 ms |
| Average GPU busy | 3.04 ms | 3.18 ms |
| P99 CPU busy | 62.11 ms | 57.68 ms |
| P99 GPU busy | 5.39 ms | 6.74 ms |

Interpretation:
- App-side frame pacing is CPU-dominated in these two samples: CPU busy duration is roughly 3x GPU busy duration.
- Big CPU-side spikes are present; they cannot be attributed to any specific Unity C# method from PresentMon alone.
- Not all app presents were displayed to the foreground screen (capture B had only 796 rows with display-change timing out of 1990 presents). The window was not foreground; these presented-FPS numbers are **not** a verified user-visible gameplay FPS baseline.
- Repeat in an isolated, actively played build with the other game and duplicate FunGuy instance closed before making production optimization decisions.

## Render configuration discrepancy
`ProjectSettings/QualitySettings.asset` sets `Standalone: 0` (Mobile tier), while `PC` is excluded for `Standalone`. No code override via `QualitySettings.SetQualityLevel` was identified in the project scripts scanned. Check the built player's runtime quality tier with development instrumentation before drawing conclusions about its actual asset selection.

| Feature | Mobile RPAsset | PC RPAsset |
|---|---|---|
| URP render scale | 1.0 | 1.0 |
| Main-light shadow distance | 160 | 50 |
| Shadow cascades | 1 | 4 |
| Main-light shadowmap size | 512 | 2048 |
| GPU Resident Drawer mode | 1 (on) | 0 (off) |
| GPU Resident Drawer camera occlusion | off | off |
| SRP Batcher | on | on |
| HDR | off | on |
| SSAO renderer feature | none | enabled |

Do not blindly switch tiers to gain FPS: PC enables several additional GPU-heavy features and disables GPU Resident Drawer, although its shadow distance is shorter.

## Specific runtime findings
1. **48 DOTween infinite nested-loop warnings**, from `Assets/_Game/Scripts/LaunchPad/LilypadMotion.cs::StartRotating`. It puts `DORotate(...).SetLoops(-1)` *inside* a DOTween Sequence via `Append`. DOTween warns and changes the nested loop to `int.MaxValue`. Correct by looping the outer Sequence or using a standalone looping Tweener. Verify original movement / animation semantics before editing.
2. **Environment block instantiation/destruction:** `Assets/_Game/Scripts/Blocks/BlockSpawner.cs` uses `Instantiate` in `TrySpawnNext` and `Destroy` in `RemoveBlock`. Streaming transitions and resets may create CPU / GC spikes. Confirm with Unity Profiler Timeline and GC Alloc; compare to `GridCourseStreamer` which already has pools.
3. **Kinematic Rigidbody warnings:** `RunnerMovementMotor.ResetMotion` writes `linearVelocity` and `angularVelocity` even when the Rigidbody is kinematic. One warning of each type appears in the player log. Guard according to actual kinematic state and intended reset behavior.
4. **DOTween null-target warning:** `DOShakeScale` attempted to update a missing/destroyed Transform. Find its owner and kill it on disable/destroy or link it to the GameObject lifetime. One warning in player log.
5. **Convex Mesh warnings:** two sources exceed Unity's 256-vertex convex hull limit (`Mushroom_9.1_Mushrooms_0.002`, `Cap`); simplified collision meshes merit investigation for collider correctness and CPU efficiency.
6. **URP reflection-probe array-size warnings:** 0 `urp_ReflProbes` occurrences in this Windows player log. Previously reported in the Editor, not proven to cause Windows player FPS issues. Reproduce separately and restart Unity per the warning after configuration changes.

## Next experiments, in priority order
1. Repeat a controlled 60-second active-gameplay benchmark on the Windows player, with only one FunGuy instance and no unrelated foreground game; capture a consistent segment while traveling through streamed areas. Include D3D12 baseline and Windows quality-tier instrumentation.
2. Use a Development Build + Unity Profiler (CPU Timeline, GC Alloc, Rendering, GPU module if supported) to attribute CPU spikes to individual systems. **Request build authorization first.**
3. Profile block streaming / respawn / lily-pad activation and pooling improvements, not just GPU switches; fix the known DOTween and Rigidbody warnings separately.
4. A/B test shadow distance 160 vs 120 vs 80 on the **same** mobile pipeline, and verify visible shadow popping. Then independently compare PC quality with HDR/SSAO and GPU Resident Drawer configurations.
5. Keep existing SRP Batcher and visual features until measured data supports a change; do not reduce render scale by default on desktop.

## File-change policy
- No builds executed.
- No original settings, scene, prefab or code file edited.
- This report and the capture/analyzer files are the only added investigation artifacts.
