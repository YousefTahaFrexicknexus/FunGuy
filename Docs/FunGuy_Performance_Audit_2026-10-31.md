# FunGuy — Editor-First Performance Audit

**UPDATE — SUCCESSFUL EDITOR CAPTURE:** The earlier startup issues have been resolved. A 600-frame CPU Profiler capture and both early/late Memory Profiler snapshots are complete, and the Editor has returned to non-Play mode. Full verified results and current caveats are in `Docs/FunGuy_Performance_Findings_Editor_2026-10-31.md`. Previous notes below describing pending snapshots are historical and superseded.

**Date:** 2026-10-31 (Asia/Amman)  
**Project:** \`C:\Users\PC\Documents\GitHub\FunGuy\`  
**Unity version:** 6000.3.9f1  
**Target:** FunGuy / \`MushroomRunnerGameplay\`  
**Scope:** Read-only diagnosis first. **No Android build was started** and no gameplay or rendering settings were intentionally changed.

## Live MCP connection (2026-10-31)

- Confirmed `http://127.0.0.1:8080/mcp`: MCP for Unity v3.4.11.
- Active instance: `FunGuy@5ff58e1bb849e9cd`; gameplay scene loaded, Editor idle, not playing.
- Verified open windows: Profiler, Memory Profiler, Profile Analyzer, MCP for Unity.
- Idle rendering counters: zero draw calls; 79 render textures occupying 314,749,912 reported bytes. Not gameplay measurements.
- The two Editor profiling attempts aborted during Play-mode startup. A false-positive callback was removed; the updated helper is waiting for Unity recompilation. No valid new CPU or memory snapshots yet.
- No Android build or graphics-configuration change was made.

## Executive summary

FunGuy's existing Android audit shows an average **39.02 ms/frame (~25.6 FPS)** on a Xiaomi 22101316G (Mali-G68 MC4, Vulkan, 1080×2400), against a 60 FPS target (16.67 ms/frame). Of 1,200 measured frames, 1,189 exceeded 16.7 ms and 820 exceeded 33.4 ms. This is an **on-device** result from an earlier profiling APK, not a new build.

Earlier Editor captures were far faster (roughly 10–11 ms/frame). **Do not extrapolate Editor FPS to Android FPS.** The most promising investigation is graphics workload, rendering synchronization, and main-thread stalls; this is a hypothesis rather than proof of a GPU bottleneck because Android GPU frame-time data was unavailable.

### Current live-capture status

- **Editor is running:** \`FunGuy - MushroomRunnerGameplay - Android - Unity 6.3 LTS (6000.3.9f1) <DX12>\` (verified by Windows process inspection).
- **Installed tools verified:** Unity built-in Profiler; Memory Profiler \`1.1.12\`; Profile Analyzer \`1.3.4\`. Both packages are present in \`Packages/manifest.json\` and \`Packages/packages-lock.json\`.
- **Unity MCP:** verified the running **MCP for Unity v3.4.11 server** at `http://127.0.0.1:8080/mcp` and active FunGuy instance. This supports connected resource reads. Hack's separate local operator bridge on port 3847 is not active; profiler-control requests were not completed.
- **Editor capture attempts:** Unity entered Play mode but the helper aborted during a domain reload. The false-positive callback has since been removed and a **retry request is pending** at `.utmp/profiling/editor-2026-10-31/run.request`. The latest source has **not yet recompiled**, and **no valid new CPU or memory snapshots have been obtained**. Partial raw files from aborted runs are not valid profiling captures.
- **Profile Analyzer:** package is installed and the audit will save a CPU \`.raw\` capture for analysis. **A new comparison has not yet been run**.
- **No player build** was created in this phase.

## 1. Historical Android measurement (saved audit)

Source: \`.utmp/profiling/android/device-summary.json\`, \`android/meminfo.txt\`, \`android/cpu-response.json\`, and \`android/threads.json\`.

| Metric | Measured | Interpretation |
|---|---:|---|
| Average frame time | 39.02 ms | ~25.6 FPS |
| Median frame time | 41.32 ms | Frequent frame pacing near 24 FPS |
| P95 frame time | 41.33 ms | 95% of frames at or below this time |
| P99 frame time | 49.59 ms | Occasional slower frames |
| Worst measured frame | 99.18 ms | Visible stutter candidate |
| Mean draw calls | 728.77/frame | Significant render submission workload |
| Mean SetPass calls | 444.61/frame | Investigate render pass/material and shadow costs |
| Mean triangles | 259,391/frame | Not, by itself, proof of a GPU bottleneck |
| Mean shadow casters | 190.7/frame | Significant shadow workload |
| Mean instanced batches | 0 | Investigate suitability; SRP batching and GPU instancing are different |
| Physics.Simulate mean | ~1.80 ms (per recorder sample) | Not primary evidence of an overall CPU bottleneck |
| LandingAssist.Search mean | ~0.30 ms (per recorder sample) | Profile before optimizing |
| GC Allocated in Frame mean | ~993 bytes | Allocation spikes still need inspection |
| Android app total PSS | ~727 MiB | From \`dumpsys meminfo\`, includes system-accounted app memory |

**Caution on memory accounting:** Unity "Total Used Memory," Android PSS, OS working set, GPU dedicated memory, and private committed memory are different accounting systems. Do **not** add them together or equate any one of them with game-only RAM consumption.

### Historical CPU markers (300-frame on-device sample)

| Marker | Captured time | Notes |
|---|---:|---|
| Main Thread | 38.71 ms/frame | Whole-frame marker, not necessarily busy CPU time |
| PostLateUpdate.FinishFrameRendering | 31.40 ms/frame | Contains blocking/presentation |
| Gfx.WaitForPresentOnGfxThread | 23.37 ms/frame | Waiting for presentation; not direct GPU execution time |
| URP render loop (Inl_UniversalRenderTotal) | 7.68 ms/frame | Rendering-side CPU cost |
| FixedUpdate.PhysicsFixedUpdate | 2.62 ms per displayed frame on average | May include multiple fixed ticks |
| GameplayManager.FixedUpdate | 0.49 ms average, 72.55 ms maximum | **Investigate the outlier** |
| RunnerMovementMotor.FixedUpdate | 0.44 ms average | Secondary candidate |
| MushroomLauncher.FixedUpdate | 50,123 calls across 300 frames | ~167 calls per rendered frame; investigate architecture, not yet proven costly |

**Important:** \`Gfx.WaitForPresentOnGfxThread\` is primarily waiting time and is not valid proof that the GPU itself executed for 23.37 ms. The historical Android CPU capture contains **no usable GPU frame-time samples**.

## 2. Historical Unity Editor A/B tests

Source: \`.utmp/profiling/editor-comparison.json\` and \`.utmp/profiling/moving/sample-*.json\`.

| Test | Editor static (ms/frame) | Editor moving (ms/frame) |
|---|---:|---:|
| Baseline | 10.66 | 11.24 |
| No debug | 10.37 | 10.91 |
| Short shadows | 10.43 | **10.48** |
| No post-processing | 11.28 | 11.41 |

Shortening shadows was the best measured moving-frame variant (~6.8% faster than the moving baseline) and reduced measured moving shadow casters from ~418 to ~174, but **the scene workload varied between tests**, so this is exploratory evidence, not a controlled Android optimization result. Disabling post-processing did not produce a reliable improvement in those Editor captures.

The prior runtime inventory (source: \`.utmp/profiling/prepare-result.json\`) included approximately 2,317 enabled renderers, 770 within the camera frustum, 1,452 objects configured as shadow casters, 620 LODGroups, and 1,228 colliders. These are a moment-in-time inventory, not necessarily the exact set of renderers drawn in a frame.

The current Mobile URP asset has render scale 1.0, HDR enabled, MSAA 1, and shadow distance 160. A previously recorded before/after hash mismatch exists for the URP asset; verify its intended settings before making optimizations.

## 3. New Windows OS snapshot (2026-10-31, before a build)

Source: \`.utmp/profiling/editor-2026-10-31/os-metrics.json\`  
Unity Editor PID: \`102536\`. This is a **single short Windows-process sample**, and Editor Play-state/focus was not verified.

| Metric | Observed | Meaning |
|---|---:|---|
| Unity process working set | 3,424 MiB (~3.34 GiB) | Resident RAM for entire Editor process |
| Unity process private commit | 8,093 MiB (~7.90 GiB) | Committed private virtual memory; NOT physical RAM |
| Process CPU over ~2 seconds | 122.8% of one logical core | Roughly 3.8% of 32-logical-processor system capacity |
| Process dedicated GPU memory | 898,789,376 bytes (~857 MiB) | Windows GPU Process Memory counter |
| NVIDIA shared GPU memory | 443,174,912 bytes (~423 MiB) | Shared memory assigned in Windows GPU accounting |
| Intel GPU shared memory | 101,289,984 bytes (~97 MiB) | Different adapter; do not conflate with NVIDIA dedicated VRAM |
| GPU Engine % reading | 0% in one sample | **INCONCLUSIVE** while Editor may be idle/unfocused; not evidence that graphics cost is zero |

**This is not game-only usage.** The Editor includes editor UI, asset databases, tooling, imported resources, and Play-mode state; multiple Unity projects are open concurrently. Full memory snapshots of Editor Play mode still include the Editor process. To measure game-only memory accurately, a later on-device Player capture will be necessary (after the user-approved Editor-first phase).

## 4. Prepared Editor-first capture workflow

New Editor-only source: \`Assets/Editor/FunGuyEditorSnapshotSession.cs\`.

The script is deliberately **trigger-once** and does not edit scenes, materials, quality settings, URP settings, or gameplay source. A request is already staged in \`.utmp/profiling/editor-2026-10-31/run.request\`.

Once Unity imports the script, and provided the active \`MushroomRunnerGameplay\` scene has no unsaved edits:

1. Enter Play mode only if not already playing.
2. Warm up 120 frames; record 600 Editor frames using Unity Profiler counters.
3. Save a Unity CPU \`.raw\` profile plus JSON frame-time/counter and CPU marker summaries.
4. Take an **early** Memory Profiler snapshot of the **entire Editor process**, then let 600 more game frames run.
5. Take a **late** memory snapshot to compare retained allocations; record memory and graphics-driver memory counters for both.
6. Return to the original Play mode state and open the Profile Analyzer window. No Android build or graphics experiment is performed.

**Expected files** (only after a successful run):

- \`.utmp/profiling/editor-2026-10-31/status.json\`
- \`.utmp/profiling/editor-2026-10-31/environment.json\`
- \`.utmp/profiling/editor-2026-10-31/cpu-aggregate.json\`
- \`.utmp/profiling/editor-2026-10-31/editor-cpu-profile.raw\`
- \`.utmp/profiling/editor-2026-10-31/memory-early.snap\`
- \`.utmp/profiling/editor-2026-10-31/memory-late.snap\`
- \`.utmp/profiling/editor-2026-10-31/memory-counters-early.json\`
- \`.utmp/profiling/editor-2026-10-31/memory-counters-late.json\`
- \`.utmp/profiling/editor-2026-10-31/result.json\`

Snapshot files should be opened in **Window > Analysis > Memory Profiler**. The CPU profile should be loaded in the Unity Profiler and examined with **Window > Analysis > Profile Analyzer**. Opening the analyzer window does not, on its own, perform a marker comparison.

### Safety/blockers

- Node.js is installed and accessible to Hack. **The supplied port 8080 Unity MCP server is reachable and connected**, and direct read-only MCP resource calls succeed. The separate optional operator bridge (port 3847) is absent; Editor profiler-control actions were not completed.
- Unity imported the first version, but the initial run aborted during an expected Play-mode domain reload. The updated version is awaiting a Unity asset refresh/recompile. A focus/refresh of the FunGuy Editor is needed before the retry can run.
- The scene must be \`MushroomRunnerGameplay\` and saved. If it contains unsaved changes, the tool will **stop rather than discard them**.
- If the Memory Profiler cannot capture, the run records a failure instead of claiming success.
- A valid GPU frame-time capture may not be supported by the Editor/graphics API on this machine. GPU *utilization %*, GPU *frame time*, GPU *total VRAM*, and GPU *memory allocated* are distinct quantities.
- Git CLI was unavailable from Hack's host PATH; no Git-state verification was possible.

## 5. Priority investigations once snapshots become available

1. **Rendering / frame pacing:** Profile the frame and render threads, distinguish CPU rendering from wait-for-present, and obtain real GPU timing on a supported platform.
2. **Memory ownership:** Compare early/late \`.snap\` files for textures, meshes, render textures, native objects, managed heap, and retained asset references. Treat Editor overhead separately.
3. **Gameplay spikes:** Identify the conditions causing the ~72.55 ms \`GameplayManager.FixedUpdate\` outlier; inspect \`MushroomLauncher.FixedUpdate\` call volume and physics-transform synchronization.
4. **Controlled optimizations:** Only after establishing the baseline, compare shorter shadows, HDR off, appropriate instancing/SRP Batcher changes, and post-processing individually under the same scene, resolution, movement, and capture duration.
5. **Later platform validation:** Once Editor profiling is complete, run a matched Android Player capture to verify actual gameplay RAM, CPU frame time, GPU frame time, thermal behavior, and FPS.

## References

- Unity Memory Profiler snapshot API: https://docs.unity.com/en-us/engine/6000.0/script-reference/unity/profiling/memory/memoryprofiler/takesnapshot
- Profile Analyzer guide: https://unity.com/how-to/optimize-your-game-unity-profile-analyzer
- Existing audit source code: \`Assets/Editor/FunGuyPerformanceAudit.cs\`
- Existing audit raw data: \`.utmp/profiling/\`

---

**Status at latest verification:** Unity MCP port 8080 connected and FunGuy Editor resources verified; historical audit reviewed; new Windows OS snapshot captured; Editor profiling attempt(s) aborted by a helper startup callback; fixed helper/retry pending Unity recompilation; **fresh CPU and Memory Profiler snapshots are still not available**. No build performed.
