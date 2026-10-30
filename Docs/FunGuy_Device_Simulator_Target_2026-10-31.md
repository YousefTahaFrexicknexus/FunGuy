# FunGuy — Redmi Note 12 Pro 5G Device Simulator Plan

Date: 2026-10-31.

## What is configured

A custom Unity Device Simulator definition was added at:

`Assets/Editor/Profiling/RedmiNote12Pro5G_22101316G.device`

Display name: **FunGuy Target - Redmi Note 12 Pro 5G (22101316G)**.

Simulation configuration:
- Android operating system
- 1080 x 2400 portrait screen (the measured device resolution)
- Approximate display density: 395 dpi

The phone's *measured* graphics device in the historical audit was ARM Mali-G68 MC4. **This profile does not and cannot emulate its GPU, CPU, memory bandwidth, RAM size, or Android native graphics driver.**

An Editor inspection helper was added at `Assets/Editor/FunGuySimulatorProbe.cs`; one-time diagnostic request at `.utmp/profiling/simulator/probe.request`. The helper has not yet imported/compiled while the Editor is unfocused. Its resulting `probe.json` will identify the current Simulator window/device selection after import. This is an inspection-only probe, not a performance capture.

## Historical phone measurements (actual Android build, already captured)

- Device: Xiaomi 22101316G / Redmi Note 12 Pro 5G.
- GPU: Mali-G68 MC4; Vulkan.
- Screen: 1080 x 2400.
- Requested frame cap: 60 FPS; desired frame budget: 16.67 ms.
- Measured average: 39.02 ms/frame, about 25.6 FPS.
- Draw calls: about 729/frame.
- SetPass calls: about 445/frame.
- Shadow casters: about 191/frame.
- Instanced batches: zero.
- App PSS: about 727 MiB. This is not a memory limit.
- Mobile GPU frame-time was not obtained.

Draw calls, SetPass count, memory and frame time are **reference measurements, not hard hardware capacities**. Their values cannot be directly imposed as CPU/GPU throttles in the Editor.

## What can be compared in Unity

Use the newly defined profile in the Device Simulator and test the game at an equivalent screen resolution and graphics configuration. Collect:

1. Draw calls, SetPass calls, shadow casters, triangles, post-processing and frame-buffer/render-texture usage.
2. Main-thread/PlayerLoop spikes and scene/gameplay scripts with Unity Profiler / Profile Analyzer.
3. Managed allocations and snapshot differences with Memory Profiler.
4. Screen layout, safe area, and portrait UI.

Run at 1080 x 2400 and maintain consistent scene/camera/movement across captures. Game, Scene and Simulator window focus can change which view is rendered.

## What cannot be verified with Simulator

- Actual Mali-G68 MC4 utilization, GPU execution time or bandwidth.
- CPU time as it would run on Android ARM processor cores.
- Real Android process PSS, native allocations, GC behavior, GPU memory pressure.
- Thermal throttling, power consumption, Vulkan driver performance or reliable Android FPS.

Unity's documentation explicitly states that Device Simulator **does not simulate performance characteristics or rendering capabilities**. Setting `Application.targetFrameRate = 60` caps FPS at most; it **does not simulate weak hardware**. Forcing a low Windows process priority, arbitrary CPU affinity, Editor heap limit or artificial sleeps would make an uncalibrated and misleading test, not a Redmi Note 12 Pro benchmark.

## Current state

The new `.device` file and Simulator probe have been written to the project. At the latest check, Unity had **not imported/compiled** `FunGuySimulatorProbe.cs` and `probe.request` remained pending. No new 1080 x 2400 Simulator performance capture was performed yet. Existing Editor results in `Docs/FunGuy_Performance_Findings_Editor_2026-10-31.md` were captured at 718 x 1367 and are not a matched Simulator experiment.

To activate the profile, bring the **FunGuy Unity Editor** window to the foreground, choose **Assets > Refresh** if necessary, open its **Simulator** tab, and select **FunGuy Target - Redmi Note 12 Pro 5G (22101316G)** from the device list.

Unity documentation:
- https://docs.unity.com/en-us/engine/6000.7/manual/platform-specific/cross-platform-features/device-simulator/introduction
- https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/cross-platform-features/device-simulator/adding-a-device
