# Mushroom Runner performance audit — 30 October 2026

Target: Android phones, 60 FPS (16.67 ms per displayed frame). Unity 6000.3.9f1, URP 17.3.0. Scene: `Assets/_Game/Funguy.MushroomRunner/Scenes/MushroomRunnerGameplay.unity`.

## Result on the connected Android phone

**The current runner did not reach 60 FPS in the measured run.** On the connected Xiaomi 22101316G, Mali-G68 MC4, Vulkan, at 1080×2400, the 1,200-frame gameplay sample averaged **39.02 ms per frame, approximately 25.6 FPS**. Rendering/presentation pressure and occasional world/reset hitches are the main leads. Landing assistance is not the dominant steady cost.

I built and installed a separate Development IL2CPP app, `com.funguy.performanceaudit` / **FunGuy Performance Audit**, using the runner scene directly. The existing installed game was preserved. Temporary instrumentation set the frame target to 60, waited 180 frames, and collected 1,200 frames over 46.83 seconds before writing the results. The first sample was taken before attaching the native Profiler, avoiding its streaming overhead. It used neutral input/automatic gameplay rather than a replay of a high-score session. Then I attached the Unity Profiler through MCP and an ADB USB tunnel for a separate 300-frame CPU capture.

| Phone metric | Mean | p95 / other useful detail |
| --- | ---: | ---: |
| Rendered frame interval | 39.02 ms | p95 41.33 ms; p99 49.59 ms; worst 99.18 ms |
| Frames over 20 ms | — | 1,189 / 1,200 (99.1%) |
| Draw calls | 729 | p95 795 |
| SetPass calls | 445 | p95 498 |
| Triangles | 259,391 | p95 295,199 |
| Shadow casters | 191 | p95 207 |
| Instanced batches | 0 | 0 throughout |
| Physics.Simulate | 1.80 ms | p95 3.10 ms per rendered frame |
| Landing-assist search | 0.300 ms | p95 0.934 ms; worst 4.277 ms |
| Landing-assist correction | 0.0051 ms | Most frames zero |
| Managed allocations | 993 bytes/frame | median 154 B; p95 2,546 B; worst 97,452 B |
| Unity total used memory counter | 328.6 MiB | This is not total Android process memory |
| Managed memory counter | 6.96 MiB | Short sample, not a leak test |

The attached 300-frame capture averaged 38.71 ms. The main thread spent **23.37 ms in `Gfx.WaitForPresentOnGfxThread`**. In the final 120 frames, the render thread averaged **28.97 ms in `Gfx.PresentFrame`**, including 26.93 ms in `GfxDeviceVK.Present`. CPU rendering through URP was about 7.76 ms on the main thread; opaque drawing was 3.16 ms inclusive, with shader-application self time 1.78 ms. Physics ran roughly 1.94 fixed steps per rendered frame at this low frame rate, so its per-frame cost includes catch-up work.

**Interpretation:** there is substantial graphics/presentation waiting as well as real CPU submission cost. This is consistent with GPU or presentation/driver pressure, but it is not a measured GPU breakdown. The native capture provided **no GPU timestamp samples**. Bloom's roughly 0.61 ms render-thread marker is CPU command work, not a measurement of its GPU shading time. Obtain GPU/system tracing before deciding whether bloom, outlines, overdraw, resolution, or presentation pacing dominates.

There is also an independently measured hitch: `GameplayManager.FixedUpdate()` reached **72.55 ms**, and an individual `Instantiate` reached 17.16 ms. The player log contains reset/world-instantiation stacks and 114 DOTween warnings about infinite child loops in a Sequence. `LilypadMotion.cs:103` constructs exactly that pattern. This makes block reuse, reset work, and correcting the tween configuration high-priority fixes that can preserve the appearance. The native capture contains a 95.35 ms frame. A single capture does not prove how often a long session will hitch.

Android `dumpsys meminfo` after the captures reported **727.4 MiB PSS**, including about 242.7 MiB in its Graphics category. This broader process reading differs from Unity's used-memory counter and includes Development/profiling overhead. Do not call the gap a leak. The phone was charging; battery temperature rose from 30.6°C before testing to 32.2°C, and Android reported thermal status 0 at the checks. This was a short test, not a sustained unplugged thermal qualification. Cold-start initialization, network services, other game scenes, high-speed play, and a broad device range remain outside the sample.

Evidence: `.utmp/profiling/android/device-sample.json`, `device-summary.json`, `device-cpu.raw`, `threads.json`, `player-log.txt`, `meminfo.txt`, and `phone.png`; main-thread analysis is `.utmp/profiling/cpu-android.json`. The profiling APK is 73,155,176 bytes. The build completed with warnings and logged errors in its report; it must not be described as a clean production build. The temporary Android source was copied beside the APK as `Instrumentation-source.cs`, then removed from Assets. The audit app is installed but has been stopped after capture.

## Findings from the Editor

Rendering is the first area to investigate. Gameplay physics and landing assistance were relatively small CPU costs in the sampled desktop run. The most promising approach is to draw fewer copies/passes of the same scenery and avoid work outside the useful view, while retaining the frog, nearby mushrooms, outlines, colors, and lighting.

These are measurements from this project, not generic mobile limits. Editor timings on the RTX 4080 Laptop GPU do **not** establish Android frame rate. The course changes while running, and Editor render counters can include other views, gizmos, and repaint activity. Device results and limitations are recorded separately below.

### Running gameplay

One 600-frame moving sample, after a 40-frame warmup:

| Metric | Result |
| --- | ---: |
| Observed Editor frame interval, mean / p95 / worst | 11.24 / 14.78 / 34.61 ms |
| Draw calls, mean / p95 | 1,513 / 1,709 |
| SetPass calls, mean | 833 |
| Triangles, mean | 831,627 |
| Physics.Simulate, mean / p95 | 0.126 / 0.264 ms |
| Landing-assist search, mean / p95 | 0.092 / 0.566 ms |
| Landing-assist correction, mean | 0.00023 ms |
| Managed allocations, median / p95 | 38,537 / 62,285 bytes per Editor frame |

The final 120 recorded CPU frames from that run averaged 10.86 ms. `EditorLoop` accounted for approximately 6.47 ms; aggregated `PlayerLoop` samples were 4.10 ms, including 2.38 ms in `RenderPlayModeViewCameras`. These are inclusive, nested markers: do not add them together. Notable self times were shader application (0.509 ms), DOTween updates (0.261 ms), and SRP-batcher flushing (0.177 ms). This points toward submission/rendering work before a physics rewrite. Editor allocations include Editor/profiling activity; they are not a measured allocation rate for the shipped app.

The prediction preview added earlier is guarded by `UNITY_EDITOR`. Its desktop cost must not be charged to the Android player. In a separate rendering comparison it contributed two draw calls; the actual assist remained enabled throughout.

### Controlled rendering comparison

I held simulation time at zero without pausing rendering, kept the view and geometry fixed, and captured 300 frames for each temporary variant. All variants were restored afterward. The recorded Game-view size was 482×797; this is not the phone resolution. The Profiler and Editor remained running, so absolute timings include their overhead. The camera and post-processing still render while simulation is frozen.

| Variant | Mean interval | p95 | Draw calls | SetPass | Shadow casters | Triangles |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Current settings | 10.66 ms | 11.32 ms | 2,101 | 1,207 | 443 | 1,113,632 |
| Prediction debug preview off | 10.37 ms | 11.09 ms | 2,099 | 1,206 | 443 | 1,113,308 |
| Shadow distance 40 instead of 160 | 10.43 ms | 11.22 ms | 1,873 | 1,205 | 215 | 1,049,624 |
| All camera post-processing off | 11.28 ms | 12.44 ms | 2,083 | 1,189 | 443 | 1,113,614 |

The shadow experiment removed 228 shadow casters (51.5%) and 228 draw calls (10.9%) in that view. That is a clear reduction in submitted work, **not** a demonstrated 51% frame-rate improvement. The small timing differences are sensitive to Editor noise. Disabling all post-processing did not improve elapsed time in this capture; it is not evidence that post-processing is free on a phone. Removing the game's look on that evidence would be premature.

### Actual rendering structure

The MCP Frame Debugger captured 1,215 events from one paused frame. Of these, 1,160 were named `DrawOpaqueObjects/RenderLoop.DrawSRPBatcher`, 21 were UI overlay events, and bloom had 10 downsample plus 5 upsample events, with a prefilter as well. There were also a color-grading LUT pass, an UberPost pass, CopyDepth, transparent drawing, and a final blit. Frame Debugger events are not all individual mesh draw calls: an SRP batch event can contain multiple draws. The MCP version exposed event names but did not provide reliable per-event mesh/material/batch-break details for this Unity version.

The scene inventory found:

- One enabled directional light, Mixed, shadow strength 0.35. No active point/spot-light swarm.
- Active **Mobile_RPAsset**: render scale 1, HDR on, MSAA 1, main shadow atlas 1024, one cascade, **shadow distance 160**, soft shadows off at pipeline level. The generic Quality setting of 40 is not the effective URP distance.
- Camera far plane 200. One active rendering camera.
- 2,317 enabled active-hierarchy renderer components, 17 distinct scene mesh/particle materials, 620 LODGroup components, and 1,228 colliders at inventory time. Renderer and bounds counts include LOD members and do not mean all were visible or drawn.
- Materials predominantly use the Toon Fantasy Nature custom grass, vegetation, and outlined shaders. All 17 inventoried mesh/particle materials had instancing disabled; recorded instanced batches were zero. SRP batching was already being used, which reduces CPU setup but does not turn every object into one combined draw.
- Two global volumes blend together. The sampled profiles have effective bloom, tonemapping, vignette, chromatic aberration, color lookup, and color curves. Many other components are present but have neutral settings; their presence does not mean they generate expensive passes. The inactive MotionBlur component is also not an active blur pass.
- The grid maintains 48 slices at length 5: 240 units of gameplay lookahead. Environment blocks contain enabled objects hundreds of units away; inventory examples were over 700 units from the camera. Culling can prevent drawing them, but their transforms, LODs, colliders, and lifetime still deserve measurement.

## Recommendations, preserving the art

### 1. Make 60 FPS explicit on every gameplay entry path

The directly opened runner reported `Application.targetFrameRate = -1`, with vSync off. `GlobalManager.InitApplicationSettings()` sets 60, but that manager belongs to another startup path. The runner is also absent from the current enabled build scenes (Loader and Main). Ensure the production bootstrap includes the runner and applies the desired frame target when entering it directly. On Android, Unity documents the default target of -1 as 30 FPS; desktop uncapped Editor behavior can hide this problem. [Unity targetFrameRate documentation](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Application-targetFrameRate.html).

`androidUseSwappy` is currently 0. Test **Optimized Frame Pacing** on the phone. It changes presentation pacing rather than art quality, but still needs a real-device comparison; it cannot make an overloaded GPU finish faster. [Android frame-pacing guidance](https://developer.android.com/games/sdk/frame-pacing).

### 2. Reduce repeated scenery submission while retaining its appearance

Start with the mushrooms and repeated vegetation. Test instancing with their existing shared meshes/materials, then verify actual instanced draws and CPU render-thread cost. Simply checking Enable GPU Instancing is not a guaranteed win: shader-pass support, material variation, batching priority, and LOD state matter.

The outlined shader contains an extra front-culled outline pass as well as its forward pass. The forward and shadow passes declare ordinary instancing variants; the extra outline pass does not declare `multi_compile_instancing`. Investigate that path before expecting complete mushroom instancing. A shader having nine available passes does not mean all nine execute each frame. Keep the frog and near mushroom silhouettes unchanged; if restructuring outline submission, compare overlapping objects and depth edges carefully.

For environment pieces that repeat within a block, combine only sensible spatial groups with the same material, or use an explicit instanced renderer. Keep groups small enough for culling and retain the existing LOD transitions. Combining the whole course into one huge mesh would sacrifice useful culling. Avoid per-object material copies and unnecessary shader-keyword variants. Verify the batch-break reason in the Frame Debugger UI before changing materials.

**Success measure:** fewer draw/SetPass calls and less render submission CPU time on the same scene, with identical near-field screenshots. Prioritize this over blindly lowering mesh detail everywhere.

### 3. Keep nearby shadows; stop paying for distant detail

Trial an effective URP shadow distance of 40, 60, and 80 with the camera's normal FOV and highest jumps. Keep the player and landing mushrooms shadowed. Use shadowless distant foliage, simpler shadow meshes, or baked/blob shadows for decorative scenery where appropriate. Fade before the boundary so shadows do not pop visibly. Static lighting can be authored within reusable streamed blocks, while moving gameplay objects continue using realtime shadows and probes.

The measured caster reduction makes this a strong experiment. Choose the shortest distance that survives visual comparison during motion, rather than committing 40 blindly. The current scene has one light and one cascade; reducing a nonexistent collection of additional lights or more cascades would accomplish nothing here. [Unity URP performance settings](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/optimize-for-better-performance.html).

### 4. Preserve color grading; tune bloom and unnecessary copies individually

Keep tonemapping, the palette, and vignette while testing bloom's resolution/iteration quality and threshold. The captured bloom chain is a worthwhile phone GPU experiment. Two volumes are not automatically two complete post-processing chains: Unity blends volume settings. Consolidating profiles can simplify control, but is not itself a proven draw-call saving.

The frame contains CopyDepth and an intermediate/final output path. Check which custom shaders, water, fog, and effects actually sample camera depth or color before disabling depth/opaque texture generation. Remove a copy only when its consumers no longer need it. Do not switch HDR off without checking highlights and bloom: that can visibly alter this art style.

Use render-scale reduction only if a device capture identifies pixel/GPU pressure. For example, 0.9 scale shades roughly 81% as many internal pixels, while retaining full-resolution screen-space UI when configured correctly. This is a small quality tradeoff, not a zero-loss optimization; assess moving outlines and thin grass at phone size. Prefer removing redundant passes and invisible work first.

### 5. Reuse environment blocks and smooth generation spikes

This is a measured hitch priority on the phone, alongside the steady graphics/presentation problem. Correct the infinite child tween in `LilypadMotion` by owning repetition at the appropriate tween/Sequence level and preserving its rotation behavior. Avoid hiding the warnings as a substitute for fixing their cause.

The grid already uses pools and cached arrays, which is useful. The environment `BlockSpawner` still instantiates blocks and destroys them on despawn/reset. `RunFlowCoordinator` rebuilds the environment before the grid on each failure/reset. Pool whole environment blocks and prewarm the few required variants, resetting coins, colliders, animation, and random state on reuse. Bound pool size; pooling unlimited blocks trades spikes for excessive memory.

Split purely visual population across frames ahead of the camera, while guaranteeing that gameplay collision surfaces exist before the player's trajectory reaches them. Keep simulation lookahead separate from visual detail activation distance. Preserve high-detail foreground objects, use the LODs already authored, and cull tiny distant decorations based on their projected size. Measure block boundaries, deaths, and resets separately from steady running; an average can hide all of them.

### 6. Handle small scripting/UI costs after the larger work

`SpeedMeter_HUDUI` formats a new speed string every LateUpdate. Update only when the displayed integer changes and use TMP's formatted `SetText` path where suitable. Separate frequently changing UI from static canvases if CPU captures show costly canvas rebuilds. Do not split every UI object into a separate canvas: that can increase drawing.

Many `MushroomLauncher` instances run FixedUpdate to examine their launched-player set. An empty-set fast path is small; a central collection of only mushrooms with tracked overlaps could avoid hundreds of callbacks if device profiling shows it matters. Preserve teleport/disabled-collider cleanup semantics. Keep the landing search's current nonallocating physics and cached-target approach. Its recorded desktop cost does not justify rewriting physics or weakening assistance.

## Tools and a repeatable workflow

1. Use a **Development Android player**, with Deep Profiling off initially. Capture the real phone over USB with the Unity Profiler. The Editor capture is useful for locating code, not signing off Android performance. [Unity Android profiling procedure](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/developing/testing-and-debugging/profile-on-an-android-device).
2. In Profiler **Highlights**, set the target to 60 FPS. Inspect representative slow frames in CPU Timeline and compare the main thread, render thread, workers, and GPU. `WaitForTargetFPS` can mean the cap is working; `Gfx.WaitForPresent...` can be GPU/presentation pacing; `WaitForJobGroupID` requires following the worker dependency. A wait marker alone does not identify the fix. CPU and GPU overlap, so do not add their frame times as if they ran serially. [Highlights module](https://docs.unity3d.com/6000.3/Documentation/Manual/ProfilerHighlights.html).
3. Use **Profile Analyzer** to compare the same deterministic run before/after over many frames, including distributions and worst markers. It is not currently installed. Test one change at a time and use an A–B–A sequence to expose drift. [Profile Analyzer](https://docs.unity3d.com/Packages/com.unity.performance.profile-analyzer@1.2/manual/index.html).
4. Use **Frame Debugger** for actual passes, draw ordering, shader variants, instancing, and batch breaks. Disable it before timing: its captured-frame mode is diagnostic, not normal gameplay. Use **Rendering Debugger** for overdraw/material/lighting views. Water, fog, particles, and full-screen layers can be expensive even with few triangles. [Rendering Debugger](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/features/rendering-debugger.html).
5. In Unity 6, use **Window > Analysis > Render Graph Viewer** to inspect intermediate textures, resource lifetimes, pass merging, and copies. The current captures contain Render Graph markers. This is particularly useful before changing the depth/color-copy path. [Render Graph Viewer](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/render-graph-view.html).
6. Use **Memory Profiler** player snapshots after warmup, then after repeated resets/streaming loops. Compare live textures, meshes, materials, pools, and managed objects. A rising high-water mark can be normal pooling; sustained live-object growth after equivalent resets needs investigation. Editor multi-gigabyte memory readings are not the Android app's memory footprint. Memory Profiler is not currently installed.
7. If Unity cannot supply trustworthy mobile GPU timings, use a compatible device GPU tool and Android system tracing/Perfetto to distinguish CPU scheduling, GPU workload, frame presentation, and thermal throttling. Device support must be verified; an unavailable/zero GPU counter does not mean a free GPU frame.

**Unity 6 feature to evaluate carefully:** GPU Resident Drawer can help repeated mesh renderers, but requires Forward+ and a supported compute-capable API (not OpenGL ES). This project currently uses Forward. Test shader compatibility, memory, and GPU cost before migrating; it is not a universal checkbox optimization for this mobile project. [GPU Resident Drawer requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/urp/gpu-resident-drawer.html).

For acceptance, record the same seed/input route at startup, steady running, a busy environment boundary, particle bursts, repeated deaths, and high speed. Measure cold and warmed gameplay, then a 10–15 minute thermal run unplugged, at fixed brightness. As an initial engineering target, aim for roughly 12–13 ms of busy CPU and GPU work independently, leaving room within 16.67 ms for variability. Report p95/p99, missed refreshes, and longest gameplay frames, not just average FPS. The exact headroom and memory budget must be chosen for the weakest supported phone.

## Evidence and restoration

Editor measurements, inventories, captures, and Frame Debugger JSON are under `.utmp/profiling/` (local, ignored by Git). Editor raw captures are compressed in `editor-captures.zip`; extract the selected `.raw` file before loading it in the Profiler. The Android `android/device-cpu.raw` is retained directly for inspection. The moving summaries are in `moving/`; root comparison samples are explicitly marked `simulationFrozen: true`. CPU analyses cover the final 120 captured frames; counter summaries cover the stated 300 or 600 frames, so their averages need not match exactly. The separate Android main-thread analysis covers 300 frames, with the all-thread breakdown covering the final 120.

The reusable Editor-only MCP tool is `Assets/Editor/FunGuyPerformanceAudit.cs`. It creates temporary rendering variants and restores them after measurement or on domain reload/Play-mode exit. Measurements are opt-in. No permanent graphics-quality reduction is part of this audit.

The original product name and Android package ID were restored and saved after the separate app build. The build did serialize URP shader-prefilter/runtime metadata and Android build data; these are build-generated changes, separate from the temporary shadow/post experiments. Existing unrelated project changes were preserved. QualitySettings was verified against its pre-experiment hash. The native Profiler is stopped with Android capture data available for inspection.

