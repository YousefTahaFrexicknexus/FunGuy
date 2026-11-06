# FunGuy Android profiling — running findings and recovery

## Latest — real-phone diagnosis, approximately17:36, 6 November 2026

Owner connected Redmi and authorized physical-phone testing. Correction: Redmi approximately25FPS; real Blackview tablet reportedly10FPS; emulator60FPS. Installed Redmi APK hash matches PhaseD Combined exactly; actual Mali-G68 MC4 Vulkan1.1 and Combined settings verified. No APK replacement, source edit or build occurred.

Native -> quarter display pixels -> native, recording OFF:22.53 ->49.51 ->22.09FPS. Real phone Unity capture shows main45.48ms/frame, about30.13ms rendering/present wait and Vulkan render-thread present33.02ms; physics2.15ms, representative28bytesGC. GPU query timings unavailable. Strong evidence for GPU/pixel-rendering pressure in this idle phone scene, not an isolated shader/pass identification. Phone recordingON22.07FPS and disconnected/OFF22.00FPS; do not apply the emulator's7FPS recording behavior to this phone.

Full evidence/limitations: **D:/Work/FunGuy/Docs/Android-Phone-Diagnostics-2026-11-06.md**, output `FunGuy-Phone-Diagnostics-2026-11-06.md`. Original phone resolution and USB stay-awake0 restored, recording OFF, profiler target Editor. Same Combined APK/data retained. Earlier emulator-only scope and phone-deferred headings below are historical; phone testing is now authorized by the owner.

## Status audit — 6 November 2026, approximately 17:16 (Asia/Amman)

The latest work is PhaseD shadow/foliage-shadow/GPU-occlusion diagnostics, after targeted Vulkan and GRD ON/OFF tests. Older first-pass/installed-APK/current-profile headings below are historical; use the verified checkpoint at the top of the master plan.

- Live Unity is idle and responsive, profile **FunGuy Vulkan PhaseD Combined**, Vulkan API, recording OFF; gameplay scene open/clean. Original Mobile quality URP currently scale0.75/shadow160/GRD1. Emulator stopped; saved4GB/90Hz.
- Combined APK is PRESENT now,106,507,559bytes, SHA256 A59CD1DEF4DFD9E80485A29325A143FEDC42C17D27E01944C67D4F2A895A2BC1. Earlier missing-file claims are stale. Installed package was not queried while emulator is stopped; PhaseD notes say older GRD-ON APK restored.
- PhaseD Combined59.17/59.43FPS and older GRD-ON warm59.78FPS do not prove benefit or regression: different code/world phases and60Hz ceiling. Production graphics should remain unchanged pending a proper comparison.
- Vulkan development profiling already established severe recording overhead:55.49FPS OFF ->7.24FPS ON ->56.81FPS OFF. Android GRD ON counters205draws/132SetPass valid; OFF Android capture failed. Editor draw reductions remain Editor-only evidence.
- This was a status/documentation audit, not a new benchmark. Refreshed stale output copies from newer project Docs, preserving full PhaseB/C/D history. See master verified checkpoint for next steps and exact live-state corrections.

Date: 6 November 2026 (Asia/Amman)
Status: Emulator first-pass measurements completed; no game optimization changes applied. The owner explicitly requested continued emulator focus and declined phone testing for now. This is not a phone-performance diagnosis.

## Latest result — matching GLES baseline tested, API gap reproduced
Completed at approximately06:09 local. Tested the already-built GLES baseline APK; hash remained 8F79AEF578D871BDF7D89F3038E6CA7F24062755695A9AF2DD3CA13F418FFBD5. Both comparison APKs are non-development, ARM64 IL2CPP Release/OptimizeSpeed; Unity recording remained OFF. No new build or game optimization was performed.

| Condition | Measured FPS | Frame evidence |
| --- | --- | --- |
| GLES90Hz, idle |23.52|358frames/15.18sec; p95 interval48.29ms |
| GLES90Hz, moving run |26.64|149frames/5.56sec; ending screenshot score92/17kmh |
| GLES60Hz, warmed idle |24.16|369frames/15.23sec; p95 52.28ms |
| Vulkan60Hz, same-boot recheck |59.80|920frames/15.37sec; p95 19.53ms; maximum54.46ms |
| GLES60Hz, switched back |27.10|416frames/15.32sec; p95 51.34ms |
| GLES60Hz, additional launch/reset sample |26.38|107frames/4.02sec; starts score17/15kmh, ends reset at score0; MIXED movement/reset/idle |

- The60Hz sequence was GLES -> Vulkan -> GLES without rebooting or changing emulator host graphics flags. Same4GB guest,2160x1600, -gpu host -feature Vulkan, matching non-development profile settings. Host available physical memory was roughly5.0–5.7GiB during the relevant samples. World seed and every scene phase were not held constant, so this is strong API-path evidence rather than a precise universal speedup factor.
- The Vulkan recheck file is named vulkan-60hz-recheck-idle.json, but its boundary screenshots show movement (score9/17kmh) before timing and idle/reset afterward. Treat it as a mixed scene-phase sample, NOT pure idle. Previous Vulkan90Hz samples remain approximately45FPS.
- GLES runtime verified on fresh boots using Android GPU stats: createdGlesContext=1, createdVulkanDevice=0, createdVulkanSwapchain=0, for com.omg.funguy. Actual display app overrides were45 on90Hz and60 on60Hz. After same-boot APK switching, GPU stats accumulated both GLES and Vulkan entries; do not interpret those accumulated booleans as both APIs rendering simultaneously. The new Vulkan-device/swapchain entries appeared after installing/launching the previously verified Vulkan-only APK.
- Strong conclusion: graphics API/rendering path is a major factor in THIS translated emulator. Disabling Development/Script Debugging alone did not lift GLES to Vulkan performance. General Unity recording overhead remains a separate, previously demonstrated issue. No experiment has isolated the individual development/debug settings' costs or recording overhead on Vulkan.
- GLES again logs GPUResidentDrawer unsupported BatchBufferTarget. Vulkan may avoid this fallback and uses a different host-guest graphics path, but drawer activity and its share of the improvement remain unmeasured. Do not claim the exact cause is already a specific shader/script or the drawer alone.
- An initial GLES60Hz collection (gles-60hz-idle.json) began before the scene was ready: its initial screenshot is blank;14.33FPS average includes startup/3.36sec gap. Exclude it from steady-state comparison while retaining it as startup evidence. gles-60hz-run.json measured27.94FPS after an input attempt, but the ending screenshot is idle score0; exact phase is unclassified, so do not label all frames as moving gameplay.
- New measurement helper gles-baseline/measure.ps1 logs poll timing, latency command duration, overlap and potential history gaps. Accepted samples above reported0 potential coverage gaps; latency commands were approximately42–70ms at worst. This check does not establish zero measurement overhead or exact procedural-world equivalence.
- Visual checks showed normal rendering and actual gameplay; no crash was observed in this short test. Final GLES memory snapshot via Android meminfo: PSS637,846KiB/RSS846,112KiB; not a Unity Memory Profiler snapshot or a leak test.
- Evidence folder: C:/Users/USER/Documents/Codex/2026-11-06/do-you-remember-when-we-profiled/work/funguy-profiling/gles-baseline/ contains JSON/raw latency/poll metadata, screenshots, runtime GPU/display dumps, logs, meminfo and config backup. Preserve phase labels/corrections when comparing files.
- Cleanup VERIFIED: emulator stopped, ADB list empty, config restored to4GB/90Hz. App data preserved; installed APK is now GLES baseline. Active Unity profile remains GLES3 Baseline; recording OFF; no build running.
- NEXT: prepare a separate Vulkan development profiling profile with Script Debugging/Deep/Wait OFF, measure recording OFF/ON/OFF, and investigate rendering/TMP/physics leads. The owner builds manually. No additional GLES baseline build is needed. No phone diagnosis is established.

## Handoff update — master plan and GLES APK now available
- Full continuation plan/checklists: D:/Work/FunGuy/Docs/Android-Profiling-Master-Plan-2026-11-06.md, with a matching workspace output FunGuy-Android-Profiling-Master-Plan.md. Its current-state/next-action sections supersede older chronological next-step paragraphs here.
- During handoff preparation at approximately05:57, discovered the owner already built D:/Work/FunGuy/Builds/gles3-baseline.apk:68,866,894bytes, modified05:56:14local. SHA256 8F79AEF578D871BDF7D89F3038E6CA7F24062755695A9AF2DD3CA13F418FFBD5. aapt confirms com.omg.funguy,ARM64,minSDK25/targetSDK36,no application-debuggable flag. It is NOT yet installed/tested. Do not ask for another build; next action is the matched GLES baseline test.
- No further emulator/game tests were run while writing the master plan. Saved AVD config rechecked4GB/90Hz; emulator processes absent. Latest Editor state resource was stale after compilation/reload, so re-verify live connection/profile before operating. Last successfully selected profile remains GLES3 Baseline; last installed APK remains Vulkan baseline.
- Original Android profile hash rechecked unchanged against backup: E982CD5B4FE23FDB4581D1230B36477D0B1B98EF27ECA6FF7FE653E0B2AC90A5.

## Latest update — Vulkan baseline installed and tested
- Owner built D:/Work/FunGuy/Builds/vulkan-baseline.apk, 69,825,014 bytes, modified 05:42:55 local. SHA256 AE0CD642ADF5F6197F9EEE6EF16D9228C8418219F0B04D1C917FCC4525A11952.
- aapt confirms com.omg.funguy, ARM64, min SDK25/target SDK36, Vulkan feature declaration, and no application-debuggable flag. Installed with adb install -r; existing app data preserved.
- The latest live Unity BuildReport was for a DIFFERENT output (Builds/debug2.apk), Cancelled, 5 errors/93 warnings. Do not use that report to claim this Vulkan APK failed or built error-free. The APK above was directly inspected, installed and exercised successfully.
- Launched 4 GB emulator using -gpu host -feature Vulkan, with snapshots disabled. Host Vulkan device explicitly NVIDIA GeForce RTX3060; Vulkan API1.3 advertised by the guest. This replaces the earlier -feature -Vulkan override for these tests.
- Actual runtime API verified by Android dumpsys gpu --gpustats for com.omg.funguy: createdVulkanDevice=1, createdVulkanSwapchain=1, createdGlesContext=0, cpuVulkanInUse=0, engine Unity. This is genuine Vulkan, not a silent GLES fallback or CPU Vulkan renderer.
- Unity Profiler recording stayed OFF; this baseline APK is non-development and cannot supply a normal remote Unity Profiler capture. These are Android SurfaceFlinger presented-frame measurements and visual gameplay checks, not a new Unity CPU/GPU trace.

| Same Vulkan APK, 2160x1600, 4 GB | Presented FPS | Notes |
| --- | --- | --- |
| 90 Hz, three idle samples | 44.99 / 45.01 / 44.98 | Each history sample contains 127 presented frames |
| 90 Hz, slingshot gameplay sample | 44.69 | Moving gameplay screenshot shows score33, 15km/h |
| Temporary 60 Hz, 5.28-second moving run | 59.83 | 317 presented frames; p95 interval18.88ms, maximum34.25ms; screenshot score88, 15km/h |
| Temporary 60 Hz, 15.83-second follow-up | 59.56 | 944 presented frames; p95 interval18.91ms, maximum90.62ms; scene phase after the run not visually classified |

- Android dumpsys display at90Hz explicitly reported renderFrameRate45 and frameRateOverride for this app UID45. At60Hz it reported app override60. This supports a refresh-divisor ceiling at45 for the game's requested60 on the90Hz display, rather than a demonstrated45FPS rendering limit in this build.
- An initial longer60Hz idle collection yielded45.91FPS overall (1033frames/22.48sec), median16.73ms, p95 20.68ms and one3.15sec gap. It must not be presented as steady60 throughout. The collector polls finite127-frame histories; a long host/ADB scheduling gap can lose intervening frames, and an actual stall is also possible. The precise cause was not isolated. The subsequent full follow-up and short run independently reproduced approximately60FPS with much smaller gaps.
- The new build clearly performs better than the earlier GLES development APK's approximately25–27FPS, but API and development/debug instrumentation BOTH changed. Do not attribute the entire gain to Vulkan, RAM, or GPU Resident Drawer. No Vulkan development recording test or matching GLES non-development comparison has been completed yet. GPU Resident Drawer activity itself was not directly measured in this release build.
- Visuals rendered correctly in the inspected screens; no Vulkan crash or VK_ERROR was observed during this short test. Existing startup warnings remain: convex collider hull polygon limit, DOTween infinite child loops, DontDestroyOnLoad root requirement and kinematic rigidbody velocity assignments. This is not proof of long-session stability.
- Idle Android PSS652,338KiB/RSS867,784KiB; process/emulator memory are distinct measurements. Emulator host working set around5.2GiB despite4GiB guest setting. Do not equate a guest RAM cap with a host process cap.
- Evidence: work/funguy-profiling/vulkan-baseline/ contains screenshots, GPU statistics, display dumps, filtered logcat, memory summary,60Hz measurements and continuous collector. The90Hz JSON/latency files are under work/funguy-profiling/ram-comparison/vulkan-*.json and matching -latency.txt files.
- Cleanup verified: emulator stopped, ADB device list empty, saved config RESTORED to hw.lcd.vsync=90 and hw.ramSize=4096. Installed Vulkan APK/app data retained. No gameplay or production render settings changed.
- CURRENT ACTIVE BUILD PROFILE: Assets/Settings/Build Profiles/FunGuy GLES3 Baseline.asset. Re-read effective settings: OpenGLES3 only; Development/Autoconnect/ScriptDebugging OFF; Unity recording OFF.
- NEXT OWNER ACTION: build this selected profile normally to Builds/gles3-baseline.apk. Agent must not start the build. Then install/test on the same Vulkan-enabled emulator host configuration, first90Hz and then the same temporary60Hz trial. This isolates API differences between otherwise matching non-development profiles. Preserve existing APKs for subsequent targeted development comparisons.

## Previous update — 4 GB comparison and prepared API builds
The owner requested a lower-RAM comparison and an audit of graphics API/build/texture settings. The original installed development APK was used for these RAM tests; no new APK has been built yet.

| Emulator RAM | Unity recording OFF | Unity recording ON | Emulator host working set |
| --- | --- | --- | --- |
| 4 GB | 25.66 FPS / 38.97 ms | 8.16 FPS / 122.59 ms | about 4.84 GiB |
| 6 GB | 19.42 FPS / 51.50 ms | 6.84 FPS / 146.24 ms | about 6.16 GiB |

- These are short SurfaceFlinger presentation samples, one live session per RAM size. Cold starts, procedural scene contents and changing host load were not held constant. The 4 GB session was faster, but this is not proof that RAM capacity caused the FPS difference. The earlier 6 GB baseline was 27.16 FPS.
- 4 GB saved approximately 1.3–1.5 GB of host working set. Host available physical memory remained about 6–8.5 GiB during these live tests; a snapshot does not establish whether earlier paging ever occurred. Lowering guest RAM did not remove the large recording overhead.
- Persistent AVD config now uses hw.ramSize=4096 and display name Blackview Link 8 (4 GB test, Android 15). This deliberately departs from the requested tablet's 6 GB configuration for a host-memory experiment.
- Emulator was stopped after testing; no qemu/emulator process remained at verification. Recording is OFF. App data and captures are retained.
- Evidence: work/funguy-profiling/ram-comparison/4gb-off-live.json, 4gb-recording-live.json, 6gb-off-live.json, 6gb-recording-live.json and corresponding latency files. Earlier files without the -live suffix are INVALID as recording-state comparisons: the Profiler window had been closed and the selected player connection was stale, so recording frames did not advance. Do not use those earlier files to compare overhead.
- Repaired the Android player connection using adb forward tcp:55000 tcp:55000 and ProfilerDriver.DirectIPConnect("127.0.0.1:55000"). Verified advancing frame indices and valid raw frame data before accepting the live recording measurements. Re-query the port after app restarts; do not assume a stale advertised NAT player ID is connected.

Texture audit:
- All 52 audited game-owned texture importers used automatic compressed settings. A wider scene-dependency audit found 76 texture importers: 63 ASTC_6x6, 3 ASTC_4x4, 1 ETC2_RGBA8, 7 RGBA32 and 2 RGB24. Dependency results include Editor icons and are not an APK texture inventory.
- The uncompressed game textures inspected were small or intentional: ModifierRing 128x128, LUT 1024x32 and noise 64x64. There is no evidence of a broad uncompressed-texture problem. The Android memory snapshot contained only 24.40 MiB of native Texture2D objects.
- Keep existing ASTC settings for this comparison. ASTC is Android's recommended RGB/RGBA texture format, with ETC2 as a compatibility alternative: https://developer.android.com/games/engines/unity/start-in-unity . Do not apply broad texture reimports without a specific measured need.

Prepared two matching non-development build profiles, copied through Unity's AssetDatabase and configured through live Editor APIs:
- Assets/Settings/Build Profiles/FunGuy GLES3 Baseline.asset — OpenGLES3 only.
- Assets/Settings/Build Profiles/FunGuy Vulkan Baseline.asset — Vulkan only; active at preparation time. GLES3 Baseline is now active after Vulkan testing.
- Both: ARM64, IL2CPP Release, OptimizeSpeed, APK, same gameplay scene/mobile quality/package/signing as the diagnostic profile. Development Build, Autoconnect Profiler, Script Debugging, Wait for Managed Debugger, Deep Profiling Support, frame timing stats and OpenGL profiler GPU recorders are OFF. Optimized Frame Pacing remains OFF in both to keep the comparison controlled.
- Vulkan-only is deliberate for attribution: no silent GLES fallback in this test. It is not the final distribution recommendation. Vulkan-first with GLES3 fallback is a reasonable shipping candidate after device validation; no universal best API has been established here.
- Existing Android™.asset and FunGuy Blackview Link 8 Profiling.asset were preserved. No gameplay scripts, scenes, material settings or URP quality settings were changed. No build was started; the owner builds manually.
- Next: owner builds the active Vulkan Baseline to Builds/vulkan-baseline.apk. Relaunch the 4 GB emulator with guest Vulkan ENABLED (remove the previous -feature -Vulkan override), install with adb install -r, verify actual runtime API from logs, and measure presented FPS without Unity recording. Then build/install GLES3 Baseline with otherwise identical settings. Measure the same scene phase for longer/repeated samples. A Vulkan failure on this translated emulator is a compatibility result, not a physical-phone verdict.
- For a clean FPS comparison, these baseline APKs intentionally cannot attach the Unity Profiler. Retain the separate development profiling profile for short targeted captures.
- API rationale: Android documents lower CPU overhead for Vulkan than GLES. Unity's GPU Resident Drawer requires Forward+ and a compute-capable API excluding OpenGL ES; our Mobile renderer requests it, but the current GLES3 APK logs that it cannot use it. Vulkan could change that path, subject to device/material compatibility. Sources: https://developer.android.com/games/engines/unity/start-in-unity and https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer .

## Latest findings — native sampling without Unity recording
- Both requested Unity tools were used: normal Unity Profiler captured Android frame data (.raw), and Memory Profiler captured a real Android memory snapshot. The snapshot is now imported and loaded in the official Memory Profiler window at D:/Work/FunGuy/MemoryCaptures/idle-memory.snap; Valid=true, Platform=Android, Product=FunGuy, IsEditorCapture=false were verified.
- The user explicitly said to focus on the emulator; do not ask again for the Redmi during this phase.
- Android thread sampling with Unity recording OFF showed UnityGfxDeviceW using about 71–77% of one virtual core, UnityMain about 63–65%, and Job.Worker 0 about5–8%. Neither individual thread was proven fully saturated; do not claim the main thread alone is maxed out.
- Android simpleperf works via the supported debuggable-app mode: simpleperf record --app com.omg.funguy -e cpu-clock -f 99 -g --duration 8 -o /data/local/tmp/funguy-native.data.
- Recorded1119 samples,0 lost, over7.95sec,11.303CPU-seconds across threads. This is sampled CPU work, NOT wall-frame-time percentages.
- Native callgraph:41.47% inclusive sampled CPU work under QemuPipeStream::commitBufferAndReadFully;40.66% under GL2Encoder::s_glUnmapBuffer / glUnmapBufferAEMU_enc. These overlap: DO NOT add them. The stack passes through libndk_translation_proxy_libGLESv2.so into libGLESv2_enc.so and qemu_pipe_read/write.
- By thread/library,48.61% of sampled CPU work is UnityGfxDeviceW in kernel,34.14% UnityMain in unresolved translated code,9.56% UnityMain in kernel. Kernel symbols are restricted; the named user-space parents identify graphics buffer transfer, but no exact kernel function name is established.
- This strongly implicates the emulator graphics buffer/host-guest transport path as a major bottleneck, amplified by the scene's high draw-submission workload. It does NOT prove that this emulator-specific path exists on a real Blackview or Redmi.
- SurfaceFlinger history after native sampling measured24.43FPS /40.93ms. Compare the earlier recording-off baseline27.16FPS /36.82ms; the native callgraph sampler also has overhead/normal variation and must not be described as zero-cost. It was far less disruptive than Unity recording at about5FPS.
- The plain shell simpleperf stat attempt was permission-denied. App-identity stat worked. Direct app-identity record could not set a sample-rate debug property; --app mode handled the supported setup. No rooting or kernel symbol/security restriction changes were made. A first failed native-cpu.perf.data was0bytes and was overwritten by the later valid1,693,913-byte capture.
- Native evidence: native-cpu.perf.data, native-cpu-report.txt, native-cpu-by-library.txt, native-cpu-callgraph.txt, native-sampling-surface-latency.txt, android-threads-recording-off.txt in the capture folder below.
- The normal build uses IL2CPP C++ compiler Release / OptimizeSpeed; a Debug C++ compiler setting is not established as the cause.
- Additional recording-state correction: setting ProfilerDriver.enabled alone can disagree with ProfilerWindow.m_Recording and was observed to resume. Stop through ProfilerWindow.SetRecordingEnabled(false), then verify both the window field and ProfilerDriver.enabled=false. This was done and remained false on later checks.
- No evidence supports calling this a GC or texture-memory bottleneck. There is a significant rendering submission workload; precise per-script uninstrumented timing remains unresolved because ARM translation obscures native user-space symbols and Unity recording changes the workload.

## User request and boundaries
Investigate why FunGuy is around 30 FPS on phones including the user's Redmi Note 12 Pro. First use an emulator configured to approximate the Blackview Link 8 tablet. Prepare profiling settings; the USER builds the APK; install and test it afterward. Keep documenting results for context recovery. Do not start another Player/APK/AAB build without the user's explicit instruction. No gameplay/performance fixes have been made in this investigation.

## Project / connection
- Unity root: D:/Work/FunGuy
- Unity 6000.3.9f1, Android target.
- MCP: http://127.0.0.1:8090/mcp
- Instance: FunGuy@4c74fdf52ee2a695
- Connected unityMCP resources and tools are working. The separately connected Unity plugin workspace_context returned an internal error, so live unityMCP plus scoped local reads were used.
- For reconnect: read project/info and editor/state first and verify the root. Enable scripting_ext, profiling and docs tool groups if necessary. Disabled groups initially caused execute_code to return unhelpful failures.
- Scene: Assets/_Game/Funguy.MushroomRunner/Scenes/MushroomRunnerGameplay.unity
- User updated the scene list through a pull; it now contains this scene.
- Original Android profile: Assets/Settings/Build Profiles/Android™.asset
- Initially added and activated separate profile: Assets/Settings/Build Profiles/FunGuy Blackview Link 8 Profiling.asset. Current active profile is GLES3 Baseline, as recorded above.
- Original profile was hash-verified unchanged immediately after setup.
- Before setup there was no custom active BuildProfile. Effective settings used the template package, whereas the pulled Android profile contained com.omg.funguy.
- Preserve all existing source, authored scene/prefab edits and unrelated Google Play Games resolver/package changes.

## Prepared diagnostic profile
- Product FunGuy; package com.omg.funguy.
- ARM64 IL2CPP; APK rather than AAB; local debug signing.
- Development Build ON, Autoconnect Profiler ON, Script Debugging ON.
- Deep Profiling Support OFF; Wait for Managed Debugger OFF.
- GLES3 only for this emulator profile, optimized frame pacing OFF; frame timing stats and OpenGL GPU profiler recorders ON.
- Original Android profile has Vulkan then GLES3 and ARMv7+ARM64. Physical-phone behavior with Vulkan is not established by this GLES3 emulator run.
- Mobile quality remains the original baseline. No quality/performance tuning applied.
- Memory Profiler package 1.1.12 is installed; normal Unity Profiler is built in. Both were used. Window visibility can change; re-query rather than assuming they remain open.
- Script debugging was enabled in the binary, but no managed IDE debugger session was actually attached.

## APK and installation
- APK: D:/Work/FunGuy/Builds/debug.apk
- File size at discovery: 130,148,558 bytes.
- SHA-256: 8A2B0E4DD4BC9995CC4690AC1A97F1F19AD65B6E13CD0C5083E4B659EDA9175E
- Latest Unity BuildReport: Succeeded, Android, Development / ConnectWithProfiler / AllowDebugging / IL2CPP / LZ4; build GUID 9b1761cdf31748769f22f402834d887a.
- BuildReport also records 4 errors and 93 warnings despite Succeeded. Console includes GLES3 DOTS shader variant errors; do not describe this build as error-free.
- aapt confirmed package com.omg.funguy, versionCode 1/versionName 0.1.0, debuggable, arm64-v8a, min SDK25/target SDK36.
- Activity: com.unity3d.player.UnityPlayerGameActivity.
- adb install -r completed successfully. No data wipe/uninstall was performed.
- App launched and slingshot interaction produced a genuine run (screenshots show scores 25/27 and moving character).
- Autoconnect selected AndroidPlayer(11,Google_sdk_gphone64_x86_64@10.0.2.16), NOT the Editor.

## Emulator / RAM
- AVD internal ID remains NeonChambers_API35_Play; its original tablet approximation was Blackview Link 8 (6 GB, Android 15). It is now saved as Blackview Link 8 (4 GB test, Android 15).
- AVD folder: C:/Users/USER/.android/avd/NeonChambers_API35_Play.avd
- 2160x1600, 213 dpi (approximation), 90 Hz mode, originally 6144 MB RAM and now 4096 MB for the requested comparison, landscape; Pixel 7 cutout/system UI overlays disabled.
- This is a Google Android 15/API35 x86_64 Play Store image translating the ARM64 Unity player. It is NOT Blackview DokeOS, a Helio G100 CPU or a Mali GPU.
- Host renderer was verified as NVIDIA GeForce RTX 3060 through the Android Emulator OpenGL ES translator, not SwiftShader.
- User noted the emulator consumes RAM; qemu working set was approximately 5857 MiB. It was closed during the build, then restarted for testing.
- On completion of testing, close the emulator to release RAM unless the user asks to keep it open. Minimizing/docking does not free it.
- Temporary Android wm size 1080x800 was used for one comparison and RESTORED with wm size reset. Last verification: physical size 2160x1600.
- Emulator is now STOPPED after testing; ADB shows no devices and no qemu-system-x86_64 process remains. Installed app/data are retained. Last test app PID was3701; rediscover after restart.
- No current emulator launcher PID: it is stopped. Rediscover process IDs after restart.
- Our adb logcat collector PID40716 was stopped during cleanup.

## Confirmed correction: profiling itself produces the ~5 FPS drop
The user reported around 27 FPS, dropping to 5 when testing starts. Initially I suggested warm-up; that was unverified and was corrected. The controlled recording-off test confirms the user's observation.
- Recording OFF: Android SurfaceFlinger presentation timestamps gave 27.1606 FPS / 36.818 ms average (127 presented frames), and on-screen FPS showed 27.
- CPU tracing ON, Deep Profiling OFF: on-screen FPS approximately 5; 80 recorded frame times averaged 201.986 ms. These are INSTRUMENTED timings.
- Rendering+Memory recording with CPU and GPU areas OFF: 5.63 FPS / 177.50 ms by SurfaceFlinger over 40 frames. Disabling CPU/GPU areas alone did not eliminate the recording overhead.
- Driver deepProfiling=false and allocation callstacks=false were verified.
- DO NOT claim only the GPU or only the CPU module caused the overhead. Global recording in this setup is intrusive.
- DO NOT label the instrumented 5 FPS as normal emulator performance, or use its millisecond costs as phone timings.
- Exact mechanism of the recording overhead (emulator translation, development instrumentation/transport/other recording cost) is NOT isolated.
- Profiler recording is currently OFF (ProfilerDriver.enabled=false after extraction).

## Uninstrumented resolution comparison
Same running app, recording off:
- Native 2160x1600: 27.16 FPS / 36.82 ms.
- Half dimensions 1080x800 (quarter pixels): 27.04 FPS / 36.98 ms.
- This did not materially improve the sampled idle performance. It argues against pixel fill cost being the dominant limit in THIS emulator scene. It does not rule out CPU-side rendering, driver overhead, physics, or GPU limitations on the real phone.
- Native resolution restored.

## Instrumented workload leads (counts useful; durations distorted)
Saved profiler captures show:
- Idle: about 755 draw calls, 468 SetPass calls, 320k triangles/frame.
- Mixed gameplay/current capture: about 786 draw calls, 490 SetPass calls, 329k triangles/frame.
- Later static counters capture: about 716 draw calls, 447 SetPass calls, 313k triangles/frame.
- Render thread spends a large proportion waiting for main-thread commands (not evidence the GPU is the dominant limit).
- Main thread shows repeated TMP text mesh generation (~13 idle/~15-16 in gameplay per recorded frame), rendering/draw scheduling, shader application, and physics catch-up.
- Slow instrumented frames performed ~9-10 fixed steps/frame, with ~785-919 MushroomLauncher.FixedUpdate invocations/frame. These catch-up counts are amplified by recording slowdown; they are NOT proven to occur normally at 27 FPS.
- TimeManager fixed step 0.02 sec; maximum allowed step 0.33333334 sec.
- At idle, sampled GC allocated/frame was 28 bytes. Some gameplay frames allocate more; no evidence yet that GC is the primary bottleneck.
- Do not sum inclusive profiler timings: many nested samples overlap.

## Source and graphics checks
- GameplayManager.Init calls Set_FrameRate(60); the active scene has enabled GameplayManager.
- GlobalManager also requests 60, but was not present in the inspected scene. No explicit 30 FPS cap was found in project scripts.
- Android target 60 on a 90 Hz screen can be quantized by refresh divisors; 27 measured FPS is not explained merely by the 60 request. Reference: Unity Application.targetFrameRate documentation.
- Two cameras exist in authored scene. GameplayManager.ResetSlingShot disables playerCamera and enables slingshot camera; OnSlingShotLaunched reverses them. Do not claim both render simultaneously from the static enabled flags alone.
- Mobile_RPAsset: render scale 0.75, HDR off, MSAA1/off, shadow distance160, shadowmap512, one cascade, GPU Resident Drawer requested.
- Mobile_Renderer rendering mode2 (Forward+), no renderer features.
- Runtime warning: GPUResidentDrawer current platform does not support BatchBufferTarget. GLES3 emulator cannot use this path.
- Runtime warning: Additional Lights Cookie GrayscaleHigh format falls back to RGBA8.
- Console shader build errors reference unsupported GLES3 DOTS_INSTANCING_ON variants for Toon Fantasy Nature shaders. Successful app visuals show fallback/non-DOTS paths; this is not proof all shader variants are healthy.
- Source leads: MushroomLauncher.FixedUpdate runs on all enabled launchers to clear contact-tracking entries; investigate consolidating/sleeping empty trackers only if measured on target phone.
- HUD_UI event-driven updates; SpeedMeter_HUDUI polls speed each LateUpdate; TextAnimator animates values/scales. No confirmed root cause yet for all repeated TMP mesh rebuilding. Do not blame simple unchanged .text assignments without tracing dirty state.

## Memory evidence
- Valid remote memory snapshot: idle-memory.snap (~26.1 MB file), success flag true.
- Parsed metadata explicitly confirms Platform Android, IsEditorCapture false, ProductName FunGuy, Unity6000.3.9f1. It is NOT an Editor capture.
- Captured flags: ManagedObjects, NativeObjects, NativeAllocations.
- Native-object inventory from snapshot:
  - Texture2D: 138 objects / 24.40 MiB.
  - RenderTexture: 33 / 71.15 MiB, INCLUDING ProfilerScreenshotFull ~26.37 MiB (instrumentation object; do not assign all 71 MiB to game rendering).
  - Shader: 70 / 14.69 MiB.
  - Mesh: 100 / 13.05 MiB.
  - AudioClip: 10 / 9.14 MiB, one Funky Chill 2 loop ~8.24 MiB.
  - Loaded GameObjects5315, MeshRenderers3044, LODGroups826; includes loaded/inactive objects, NOT all visible draws.
  - Loaded colliders: Box435, Mesh362, Capsule378, Sphere47. Counts alone do not establish per-frame physics cost.
  - Camera render targets at1620x1200 are consistent with 0.75 render scale.
- First Android dumpsys meminfo PSS was 1,012,021 KiB (~988 MiB), RSS1,338,760 KiB. Includes Android/ARM translation/native mappings; do not add this to Unity counters or call it a leak.
- No repeated controlled-cycle leak test has been completed.
- A reflection-based CachedSnapshot inventory extraction wrote idle-memory-inventory.json successfully, then hit a package Dispose NullReference because RootAndImpactInfo is uninitialized before crawling. Most native caches had already been disposed; some reader/sorted caches may remain until GC/editor cleanup. Avoid repeating this parser as-is. No source changes were made to the Memory Profiler package.

## Evidence locations
Session capture folder:
C:/Users/USER/Documents/Codex/2026-11-06/do-you-remember-when-we-profiled/work/funguy-profiling/capture/
- idle-baseline.json/.raw; warm-current.json/.raw; cpu-only.json/.raw.
- counters-only.json; idle-memory.snap and .snap.status; idle-memory-inventory.json.
- recording-off-surface-latency.txt; half-resolution-surface-latency.txt; counters-only-surface-latency.txt.
- startup.png, gameplay.png, recording-off.png, unprofiled-current.png, cpu-only.png, half-resolution.png, counters-only.png.
- startup-meminfo.txt, warm-meminfo.txt, unity-logcat.txt; emulator stdout/stderr.
- Raw captures are roughly96MB,146MB,29MB; they contain intrusive recording. Keep provenance when sharing.
Prior configuration backups:
.../work/funguy-profiling/before/
.../work/blackview-link8/backup-20261106-035734/

## Current task state / next steps
1. Keep profiler recording off by default; do not repeatedly impose 5 FPS tracing on the user. ProfilerWindow.SetRecordingEnabled(false) was used for stable UI/driver state.
2. Finish a concise tablet findings report, retaining the overhead correction and resolution A/B.
3. Investigate actionable source/rendering workload leads without claiming precise phone attribution.
4. The owner explicitly declined phone testing in this phase. Continue only with emulator work unless the owner changes scope. Any future phone diagnosis needs its own measurements; do not transfer emulator glUnmapBuffer/QEMU attribution to a physical phone.
5. No phone is connected in the last ADB inventory; only emulator-5554 was present.
6. Cleanup completed: verified2160x1600/213dpi restored, stopped our logcat collector and closed the emulator; qemu exited and ADB device list is empty. Captures and installed app data are preserved. The official Memory Profiler snapshot remains loaded for review, and the CPU-only saved trace has been loaded into the normal Profiler. The trace records intrusive timing and must retain that label.
7. Do not silently alter gameplay, UI or production render settings, or start a new build.

## 6 November 2026 follow-up — targeted Vulkan development profile prepared

Read the newer master plan for the authoritative next step; the earlier chronological notes above are retained as evidence.

- Live Unity Editor connection verified: FunGuy 6000.3.9f1, Android platform, gameplay scene idle. Profiler recording OFF and editor not compiling.
- Added a **separate** `Assets/Settings/Build Profiles/FunGuy Vulkan Targeted Profiling.asset` using Unity `AssetDatabase.CopyAsset` from the existing Vulkan baseline, not by editing the original profile. Generated a distinct Unity `.meta` GUID.
- The new profile has Vulkan only; Development Build ON; Autoconnect Profiler ON; Script Debugging, Deep Profiling, Wait For Managed Debugger OFF; Android BuildType Development. ARM64 IL2CPP Release/OptimizeSpeed, graphics configuration, scenes and quality are inherited unchanged from the tested Vulkan baseline.
- A 1,038-line before/after comparison shows exactly four serialized differences from Vulkan Baseline: asset name, `m_Development`, `m_ConnectProfiler`, `m_BuildType`. The original baseline hash matches its pre-operation backup under `work/funguy-profiling/targeted-vulkan`.
- Selected the new profile as active; `ProfilerDriver.enabled=false` afterward. No build started, no emulator launched and no game code, materials, scene content or production URP settings intentionally modified. Existing tested GLES APK remains installed while the emulator is stopped. ADB reported no connected devices.
- A full Git-workspace checkpoint could not be made because the dirty workspace exceeded the 256 MiB snapshot limit. Narrow baseline asset and `.meta` backups were made instead. Rollback of this preparation is simply to reselect the GLES3 baseline profile and remove the newly created targeted profile if needed; baseline assets remain intact.
- **Next:** owner manually builds `D:/Work/FunGuy/Builds/vulkan-targeted-profiling.apk` using the active diagnostic profile. Only after it exists: verify SHA256 and `aapt` flags, launch the existing 4 GB emulator without erasing data, install with `adb install -r`, confirm runtime Vulkan, capture non-recorded frame timing, then brief verified Unity Android-player profiler ON/OFF samples. Ensure allocation callstacks disabled and do not interpret intrusive profiler timings as normal frame rate. Close emulator when done.

### Read-only source inspection to guide the upcoming attribution

- `Assets/_Game/Scripts/Gameplay/SpeedMeter_HUDUI.cs`: `LateUpdate` runs `UpdateSpeedMeter` each frame; line 63 assigns the rounded speed as TMP text every frame. This is a concrete label to isolate, **not** proof that every assignment rebuilds a mesh. Compare its frame markers/UI rebuilds with speed frozen and changing in a future controlled diagnostic.
- `Assets/_Game/Scripts/UI/HUD_UI.cs`: score, coins, momentum and multipliers subscribe to gameplay events. No ordinary `Update` loop was found in this class; avoid incorrectly blaming it for every-frame HUD work.
- `Assets/_Game/Funguy.MushroomRunner/World/MushroomLauncher.cs`: `FixedUpdate` is present on each enabled launcher and traverses the launched-player contact set to remove exited contacts. Empty-set updates remain scheduled; do not optimize blindly without comparable profiler evidence or validating trigger/teleport behavior.
- Rendering attribution remains the first priority because the clean API comparison changed FPS dramatically. Inspect actual pass/draw sources, shader batching and GPUResidentDrawer compatibility before changing URP/material quality. **No gameplay/UI/physics code was modified in this read-only inspection.**

## 6 November 2026 follow-up — Phase B Vulkan profiling actually completed

This entry supersedes the earlier *pre-build* next-step text. See Section13 of `Docs/Android-Profiling-Master-Plan-2026-11-06.md` for the full results, evidence index and interpretation boundaries.

- The owner manually built `D:/Work/FunGuy/Builds/vulkan-targeted-profiling.apk` (130,148,558 bytes, SHA256 `4387ABE9D8721D825B0DA9564FC384BB1C3392CC18F651A1858507FC221D475D`). Android aapt/boot.config/logcat verified `com.omg.funguy`, ARM64, Android development/profiler enabled, script connection debug OFF, Vulkan API1.1 on host NVIDIA. No agent-initiated build.
- Retained the4GB Blackview-style Android15 emulator, backed up its90Hz AVD config, temporarily used60Hz at2160x1600, installed using `adb install -r`, and retained app data. A working launch by ADB touch was confirmed by screenshot score18/speed15 km/h, followed by a later reset screenshot score0.
- With recording OFF, SurfaceFlinger measured58.06FPS for a short4.10s check,55.49FPS over12.20s idle, then56.81FPS over10.68s after recording OFF again; a separate10.68s post-launch run/reset **mixed** sample measured58.88FPS. The recording-ON interval measured7.24FPS/138.07ms mean. Every accepted sample reported0potential history-coverage gaps under the existing poll diagnostic; that does not rule out individual slow frames.
- Unity Profiler attached to **actual Android player PID3168** through verified TCP55000, not Editor or stale NAT identity. Created a real ProfilerWindow, recorded briefly with Deep Profiling OFF, then used ProfilerWindow `SetRecordingEnabled(false)`, reverified driver/window OFF and saved `vulkan-targeted-cpu-rendering-01.raw` (35,065,840bytes). The file contains valid raw Android frames (0..187, frame187 has2951 sample records). Recording itself was intrusive; raw CPU costs are not normal phone or unrecorded timings. The recording window was longer than the independent4sec SurfaceFlinger ON interval because of tool/inspection delays.
- Captured Vulkan Rendering counters across frames25,80,140,180: **217 draws/batches,142 SetPass calls,305.31k triangles,325.72k vertices,52 shadow casters,22 RenderTextures**. This is notably below older GLES development captures (~716–786 draws /447–490 SetPass), but mismatched scene phase/build/API prevents causal attribution. Cannot yet prove GPU Resident Drawer is responsible; do not equate zero static/dynamic batching counters with no GPU instancing.
- Main thread in those intrusive frames: **13 TMP.GenerateText** and **109 SRPBatcher.Flush** markers/frame; **480–560 MushroomLauncher.FixedUpdate** calls under approximately6–8 catch-up fixed steps/frame. Repeated nested TMP markers are not independent meshes. `frameGpuTimeMs=0` means GPU timing unavailable. No GC/texture/physics/TMP root cause has been established from counts alone.
- After test: removed ADB forward, confirmed profiler recording OFF, stopped emulator gracefully, verified QEMU exited and no ADB devices attached, restored90Hz/4GB/2160x1600 original config byte-identically, retained the installed development APK, saved logcat/meminfo/screenshots/measurements under `work/funguy-profiling/targeted-vulkan`, and deleted only temporary thumbnails in project `Temp`. No gameplay/shader/scene/prefab/production URP change was applied.
- Next: Phase C rendering pass/material attribution and a separate controlled Vulkan GPU Resident Drawer ON/OFF diagnostic, with manual owner build and recording-OFF scene-matched FPS tests. Then isolate which text component regenerates TMP meshes at idle and evaluate empty launcher FixedUpdate cost without violating trigger/reset behavior. Do not infer physical-phone performance from this translated Android emulator.

## 6 November 2026 follow-up — Phase C structural rendering audit and isolated diagnostic setup

Current and authoritative continuation instructions are in **Section14** and the updated Section11 of the master plan. This entry is chronological evidence, not the final performance verdict.

- The effective Mobile quality pipeline is `Assets/Settings/Mobile_RPAsset.asset`, not the different Graphics project default. The Mobile URP has SRP Batcher ON, render scale0.75, Forward+ renderer, shadows160 and GPU Resident Drawer mode1. Global `m_BrgStripping=2` means KeepAll. Original Mobile URP asset was hash-verified unchanged.
- Editor Play generated1209 MeshRenderers,1186 enabled and active,310 LODGroups and15 unique referenced materials. Approx1180 active renderer components were under `_Runtime`. The main shader families are Toon Fantasy Nature vegetation/outline/grass; shader files contain instancing and DOTS directives. Zero active renderers had nonempty MaterialPropertyBlocks or nondefault sorting in the sample. Loaded/enabled renderer counts include unrendered LOD children; they are not draw-call counts. All15 observed material assets had `enableInstancing=false`, which does not alone establish BRG inactivity.
- Attempted Unity Editor Frame Debugger through its internal API and MCP's supported pause/enable/get-events path with GameView and Frame Debugger windows. The tool returned0 frame events despite enabling, so **per-pass draw attribution and direct Hybrid Batch Group evidence remain unavailable**. The Frame Debugger was disabled, Play was stopped, temporary windows were closed and scene dirty state remained false.
- Using Unity AssetDatabase, created `Assets/_Diagnostics/FunGuyAndroidProfiling/Resources/FunGuy_GRD_On.asset` and `FunGuy_GRD_Off.asset` from the original Mobile asset. The copied assets differ exactly in name and GPU Resident Drawer mode1 versus0. Created two corresponding Vulkan development BuildProfiles: `FunGuy Vulkan GRD ON Diagnostic.asset` and `FunGuy Vulkan GRD OFF Diagnostic.asset`, each copied from the already tested Targeted Profiling profile, with only the additional diagnostic scripting define(s). The original asset/profile hashes remain unchanged.
- Added diagnostic-only `Assets/_Diagnostics/FunGuyAndroidProfiling/FunGuyGpuResidentDrawerAB.cs`, excluded from ordinary profiles via `#if FUNG_GPU_RESIDENT_DRAWER_AB`. BeforeSceneLoad it selects the copied URP based on profile define, seeds `UnityEngine.Random` with20261106 and logs the chosen ON/OFF mode. Both ON and OFF variants compiled (after an explicit clean script recompilation for ON, and subsequent recompilations for OFF/ON) with zero new Console errors and passed Editor Play checks: `QualitySettings.renderPipeline` pointed to the correct copied asset, serialized drawer mode1/0 matched, and both expected logs contained the seed. When Play ended, the editor reverted to the original Mobile quality pipeline.
- Reproducibility limitation: sorted environment mesh/position fingerprints from seeded Editor runs differed. OFF:1142 qualifying environment renderers/hash `D307A19CF7FD5C9E733AEA19D74F62F29B638B32684C3E4E76D93B6A4FA9F6C0`; ON:1126/hash `64F7A678620967C27AF67D91DC17C48E66E6629E79FC770891BD6330CF552256`. Observations occurred at different elapsed times, so exact phase matching was not proven. Use repeated same-boot ON/OFF/ON samples and screenshots rather than trusting a lone FPS difference.
- **No Player/APK/AAB build started.** Emulator remains stopped after Phase B, config4GB/90Hz/2160x1600 restored, installed app/data preserved, Profiler recording OFF, original Mobile RP active, scene clean. Current selected Editor build profile: **FunGuy Vulkan GRD ON Diagnostic**. Next action belongs to the owner: manually build `Builds/vulkan-grd-on-diagnostic.apk`, then switch to the OFF profile and manually build `Builds/vulkan-grd-off-diagnostic.apk`; after that the agent installs and tests on the emulator. Compare runtime logs/API, draw calls, SetPass, FPS intervals and visuals with recording OFF. No phone testing requested.
- Temporary Resources URP copies would be bundled into other APKs if left in the project; **remove all `Assets/_Diagnostics/FunGuyAndroidProfiling` assets and the two temporary GRD profiles before any shipping build**. Preserve the original Mobile URP, prior profiles, game code, scene and owner changes.

## 6 November 2026 follow-up — Phase C measured A/B, visual tradeoff review and probe recovery

For the full decision matrix, numerical samples and limitations see **Section15** of the master plan (latest). This is a chronological completion record, not a claim of physical-device performance.

- Two owner-built diagnostic APKs validated: GRD ON `vulkan-grd-on-diagnostic.apk` SHA256 `819F6FB91399C871BB4433E8E343B7928CBCA6DD62608E1C1202703EF4C160DF`, GRD OFF `vulkan-grd-off-diagnostic.apk` SHA256 `727D11F14B7E64048EE34EAD5B839EF3978B0937171B324820D2209A99E2712B`. Android runtime verified Vulkan1.1, correct ON/OFF diagnostic startup logs and unchanged app data using `adb install -r`.
- At emulator60Hz, profiler recording OFF, same boot/config: ON idle58.63FPS/median16.68ms/p9518.53ms; OFF idle58.74FPS/median16.70ms/p9518.91ms; ON second idle59.80FPS/median16.70ms/p9519.52ms. ON and OFF after launch/run/reset each presented approximately60.00FPS over10.47s. These tests are ceiling-limited and phases not exactly matched. No meaningful *presented FPS* winner. SurfaceFlinger measurement JSON and screenshots retained under `work/funguy-profiling/grd-ab/runtime-2026-11-06`.
- Valid intrusive **Android ON** raw profile (saved `grd-on-profile.raw`) reported205 draws,132 SetPass,331.36k triangles,48 shadow casters on frames40–310. Android OFF profiler connection disconnected with `lastFrameIndex=-1`; no valid OFF Android draw count. Never substitute an Editor DX12 or older GLES value as a matched OFF Android count.
- Separate Editor DX12 lightweight diagnostic-only `ProfilerRecorder` reported ON430 draw calls/282 SetPass vs OFF744 draw calls/462–465 SetPass, i.e. ~42% fewer draws and~39% fewer SetPass with GRD ON. Editor scenes/geometry were not identical (ON~605k vs OFF~322k triangles), and GPU frame times were variable; these are workload leads, NOT assured mobile FPS improvements.
- Rendering recommendations: retain Vulkan (for compatible Android devices), GPU Resident Drawer ON, Forward+, SRP Batcher and current Mobile URP render scale0.75, HDR/MSAA/soft-shadow-off baseline. Main visual-impact choices to offer user: shorten shadow distance160→120/80, disable tiny-plant shadow casting, test distant decor outlines and foliage LOD/culling, and only as last resort reduce render scale. For potential pixel-identical optimization, cache rounded speed in `SpeedMeter_HUDUI` before assigning TMP every `LateUpdate`. A highly intrusive7FPS profile exaggerated the `MushroomLauncher.FixedUpdate` counts because of physics catch-up; normal 60FPS physics cost is not yet proven. No permanent changes made to gameplay, character/mushroom outlines, lighting, shadows or resolution.
- Agent was newly authorized to build. A temporary *diagnostic-only* runtime `ProfilerRecorder` was added and tested successfully in Editor, then an Android APK built with Unity MCP `manage_build` to `Builds/vulkan-grd-on-probe.apk`. APK bytes105623288, SHA256 `D834A58AA40EDAE84F145B0B9A5DDBF352424F908DA654C484B653BD0D5BCDF9`; MCP build-status polling timed out. On Android this probe repeatedly hung on a black screen before diagnostics; the original owner-built APK temporarily also black-screened in that emulator session, but recovered after restarting the emulator. The probe still failed afterward; root cause unknown. **No data from probe APK accepted.** Backed up failed probe source to `work/funguy-profiling/grd-ab/probe-runtime/failed-probe-source.cs.txt`, moved failed APK out of Builds to `probe-runtime/FAILED_vulkan-grd-on-probe.apk`, and **reverted diagnostic source** to the simple ON/OFF/seed bootstrap that worked earlier. The Unity Editor MCP bridge was not answering ping after the build and this source rollback was not recompiled. Inspect Unity Editor window (`FunGuy - Untitled`) and validate scene/compile state before another build.
- At end installed prior known-good ON APK with `adb install -r`, stopped emulator, restored 4GB/90Hz/2160x1600 AVD config byte-identically, retained app data and all valid measurement/evidence files. Do not ship experimental Resources URP assets, diagnostic profiles, test bootstrap or the failed APK. No permanent visual/gameplay quality changes applied.

## 6 November 2026 continuation — Phase D: three reversible visual optimizations

**Task:** User approved retaining Vulkan, GPU Resident Drawer, SRP Batcher, Forward+, and render scale 0.75, while testing shorter shadows (160 -> 120), removal of small grass/flower shadow casting, and GPU occlusion culling. Complete details and exact experiment caveats are in **Section 16 of the master plan**.

- Added `Assets/_Diagnostics/FunGuyAndroidProfiling/PreparePhaseDVariants.ps1` and `FunGuyPhaseDVisualAB.cs`; generated five isolated URP assets under `Resources/FunGuy_PhaseD_*.asset` and five corresponding `FunGuy Vulkan PhaseD *.asset` build profiles: **Baseline** (160, no occlusion, original foliage), **Shadow120** (120 only), **OcclusionOn** (occlusion only), **GrassFlowerShadowOff** (small grass/flower casting shadows OFF only), and **Combined** (all three). All continue using GRD=1, SRP Batcher, Forward+, render scale 0.75, Vulkan profile and diagnostic seed20261106. Original `Mobile_RPAsset.asset`, renderer asset, source foliage prefabs and gameplay scene were not edited.
- Imported and inspected all five assets in Unity. Each selected its intended URP asset in Editor Play. No new console errors or scene dirty state. Baseline scanner matched228 grass/flower MeshRenderers and modified0; GrassFlowerShadowOff and Combined each matched228 and disabled casting on132, leaving0 still casting in those captures. The scanner checks streamed instances every4s; source prefabs and the plants themselves remain unchanged. Using a scene-wide scanner has test overhead and must not be shipped as a permanent optimization.
- Agent built and installed the **Combined** Vulkan IL2CPP development APK; package at build/test time `Builds/phase-d-combined.apk`, 106,507,559 bytes, SHA256 `A59CD1DEF4DFD9E80485A29325A143FEDC42C17D27E01944C67D4F2A895A2BC1`. First install started black; without clearing app data, an emulator restart allowed Vulkan startup and correct `[FunGuy PhaseD]` logs, including 132 foliage caster removals. Gameplay screenshot showed forest, foliage, mushrooms and UI. Visual states were not precisely matched to baseline.
- At emulator 4GB,2160x1600, temporary60Hz, profiler recording OFF: Combined idle12.016s **59.169 presented FPS**, median16.7325ms/p9518.6874ms, worst84.2616ms; Combined post-input10.399s **59.430FPS**, median16.6614ms/p9518.7225ms, worst69.3107ms. No SurfaceFlinger history gaps. Do not conflate presentation rate with a stable real-game FPS on an actual mobile GPU.
- New matched PhaseD Baseline APK was **not built**, because Unity bridge stopped responding after the combined build. Instead installed the older manually built GRD-ON diagnostic APK (same core rendering baseline but without PhaseD scanner): first sample12.004s **57.566 presented FPS**, median16.6387ms/p9519.1754ms, worst216.5233ms; warmed second sample12.729s **59.784FPS**, median16.637ms/p9519.0284ms, worst34.6173ms. **Not a true A/B**: different code, warmup and scene phases. No demonstrated meaningful performance win or regression from any of the three changes.
- Early screenshots showed transient rolling in-game FPS overlay readings of23 baseline and26 combined; later three idle and two post-input baseline shots displayed **60FPS / 16.7ms**. A single screenshot is not a stable frame-time measurement. Avoid claiming the game necessarily runs at23 or60 on all scenes/devices. Other known limitation: seeded procedural environments were not proved identical.
- Last checkpoint at documentation update: emulator **stopped**, AVD config byte-identically restored to its original **90Hz**; Unity window showed `MushroomRunnerGameplay`. `Builds/phase-d-combined.apk` was **missing** from the latest directory listing, despite having been built/tested; the build's backup folder and measurement PNG/JSON remain. Original production Mobile URP asset and scene hashes remained unchanged. Recheck the Unity bridge/compiler state and build output location before continuing. The temporary variants in Resources and separate profiles must be cleaned up before shipping.
- **Next decisions:** benchmark all isolated variants with actual rendered game frame timing, controlled camera/score phase, screenshot visual QA, and ideally physical Android hardware. Keep Vulkan+GRD ON+SRP Batcher+Forward+ and 0.75 scale; do not change production shadow distance, plant shadows or GPU occlusion until benefit and image quality are verified. See work folder `C:/Users/USER/Documents/Codex/2026-11-06/do-you-remember-when-we-profiled/work/funguy-profiling/phase-d-visual-ab` for backups/variant manifest/runtime evidence.

