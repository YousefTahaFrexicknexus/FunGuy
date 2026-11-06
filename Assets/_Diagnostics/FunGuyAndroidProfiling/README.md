# FunGuy — temporary GPU Resident Drawer A/B experiment

**Diagnostic files, not production changes. Do not leave these assets in the final shipping game.**

The two URP assets under `Resources` were duplicated from `Assets/Settings/Mobile_RPAsset.asset`. They retain exactly the same renderer, quality, lighting, render scale, and other settings. `FunGuy_GRD_On` has GPU Resident Drawer = Instanced Drawing (1); `FunGuy_GRD_Off` has GPU Resident Drawer = Disabled (0).

The two Vulkan profiles under `Assets/Settings/Build Profiles` were duplicated from `FunGuy Vulkan Targeted Profiling`. Both are Development builds with Script Debugging/Deep Profiling OFF. Both compile this bootstrap; the only additional define in the OFF profile is `FUNG_GPU_RESIDENT_DRAWER_OFF`.

At runtime, the bootstrap selects the appropriate copied URP asset using `Resources.Load` before the first scene loads. It sets **only** `QualitySettings.renderPipeline`; it does not mutate the production `Mobile_RPAsset.asset` or the global `ProjectSettings/QualitySettings.asset`. A success log line starts `[FunGuy Profiling] GPU Resident Drawer` and explicitly names ON or OFF. Validate that log line in every test APK before trusting results.

Both diagnostic builds additionally initialize `UnityEngine.Random` to the same fixed seed, `20261106`, before the scene begins so the Unity-random procedural spawners are more reproducible. This seed is confined to the diagnostic profiles. It does not guarantee exact world equality if gameplay uses other randomness, time, or saved state; inspect the scene/score at sample boundaries and describe any differences.

An additional diagnostic-only `ProfilerRecorder` probe was trialled on 6 November 2026. It worked in Editor Play but the Unity-MCP-built Android probe APK repeatedly remained black before diagnostic startup logging; the probe is therefore **not validated on Android**, and has been removed from this source. The failed probe source and APK are retained under `work/funguy-profiling/grd-ab/probe-runtime` for investigation only. **Do not use `vulkan-grd-on-probe.apk` for performance comparisons or releases.** The original manually built A/B APKs and these profiles use the simpler bootstrap above.

The owner builds manually. Suggested outputs are `Builds/vulkan-grd-on-diagnostic.apk` and `Builds/vulkan-grd-off-diagnostic.apk`. Compare the same emulator boot/config, refresh rate, input, seed/world phase, recording state and screen resolution. Install each with `adb install -r`; never wipe app data. Capture runtime renderer API and GPU/BRG behavior, rendering counters, FPS distribution and screenshots in each run. If gameplay is not deterministic, report that limitation rather than claiming a perfect A/B.

**After experiments:** restore the regular Vulkan profile and delete the two diagnostic build profiles plus this entire diagnostic folder, including Resources assets, before any production release. Both copied Resources assets otherwise become included in non-diagnostic builds even when the diagnostic script is compiled out.
