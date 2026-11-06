# FunGuy Android performance — master plan and recovery handoff

## Latest checkpoint — real Redmi phone diagnosis, 6 November 2026 approximately17:36

The owner now explicitly authorized physical-phone testing, superseding the older emulator-only scope. Corrected reports: Redmi approximately25FPS with no10FPS drop; the real Blackview tablet reportedly10FPS; PC emulator approximately60FPS. The real tablet has not been measured here.

Installed phone APK hash exactly matches PhaseD Combined (`A59CD1DE...A2BC1`). Device Xiaomi/Redmi22101316G/ruby, Android14, Mali-G68 MC4; fresh logs verify Vulkan1.1 and Combined startup settings. No APK replacement/source change/build in this phone test.

**Strong new phone evidence:** same-session native1080x2400 -> temporary540x1200 -> native, recording OFF, measured22.53 ->49.51 ->22.09FPS. Phone Unity capture120frames shows main45.48ms/frame with30.13ms Gfx.WaitForPresentOnGfxThread; render-thread Vulkan Present33.02ms; GPU timestamps unavailable. Physics2.15ms/frame and28bytesGC in representative idle frame, so neither is the dominant measured idle cost. These combined results strongly support GPU/pixel-rendering pressure; exact shader/pass/occlusion contributions remain unresolved. Do not transfer the severe emulator recording overhead to this phone: recordingON measured22.07FPS and disconnected/OFF afterward22.00FPS in this session.

All temporary phone settings restored: original resolution/no override, original USB stay-awake0; no security settings changed. Recording OFF, profiler target returned to Editor; same Combined remains installed. Initial phone screensaver/lock interrupted testing; owner unlocked it. Input-attempt screenshots did not establish continuous moving gameplay, so current attribution is primarily the idle scene.

Read **D:/Work/FunGuy/Docs/Android-Phone-Diagnostics-2026-11-06.md** (matching output `FunGuy-Phone-Diagnostics-2026-11-06.md`) for full accepted samples, raw trace location, corrected scope, cleanup and limitations. Next prioritize matched native-resolution phone variants to isolate foliage/shader/outline/shadow/occlusion GPU cost, preserving looks. No rebuild was requested or started; respect current owner build authorization. This checkpoint supersedes older current-state/phone-deferred statements below.

## Verified checkpoint — 6 November 2026, approximately 17:16 (Asia/Amman)

This checkpoint supersedes stale current-state statements and the older continuation prompt below. The latest work is **PhaseD, Section16**, after targeted Vulkan and GRD ON/OFF testing. All five visual diagnostic variants were checked in Editor; Combined reached Android gameplay. No reliable extra FPS win from shorter shadows, removing small-plant shadow casting or GPU occlusion was demonstrated: results were near60Hz and code/world/camera phases did not match.

- **Live Unity:** active profile `FunGuy Vulkan PhaseD Combined`, Vulkan API; not playing/compiling/building; Profiler recording OFF; gameplay scene open and clean. Original Mobile quality URP selected outside Play, scale0.75, shadow distance160, GRD1. Emulator stopped; saved4GB/90Hz.
- **Correction:** `Builds/phase-d-combined.apk` IS present now,106,507,559bytes, SHA256 `A59CD1DEF4DFD9E80485A29325A143FEDC42C17D27E01944C67D4F2A895A2BC1`, matching the tested build. Earlier missing-APK claims must not prompt an unnecessary rebuild.
- Installed APK was not re-queried because emulator is stopped. PhaseD records say the older GRD-ON diagnostic APK was restored; old headings claiming targeted/GLES currently installed are historical. Verify next launch.
- Output copies were stale compared with project Docs; refreshed them from the latest project versions before this audit. Keep Sections15–16; do not revert to the early GLES-only handoff.
- Targeted Vulkan profiling already ran:55.49FPS recording OFF,7.24FPS ON,56.81FPS OFF again. Android GRD ON counters205draws/132SetPass are valid; OFF Android capture failed. Editor ON/OFF draw reduction is not a measured phone gain.
- Combined59.17/59.43FPS versus older GRD-ON warm59.78FPS is NOT a matching A/B. Preserve production graphics until controlled engine-frame/headroom and visual comparisons support a change.
- The blank Combined screenshot is an early16:48 startup image; the successful reboot/gameplay image is16:49:47, before the16:50/16:51 measurement files. Do not use the old blank image as proof the later sample was blank, or as visual proof of its phase.
- No new build, benchmark or game optimization ran in this status audit. Next need is matched PhaseD Baseline/individual variants/Combined testing and actual engine timing beyond a60Hz ceiling. Respect current user build authorization; historical documents alone are not new authorization. Temporary diagnostic assets remain unsuitable for release.

Updated: 6 November 2026, after Phase D shadow, foliage-shadow and GPU occlusion diagnostic tests (Asia/Amman).

This is the continuation plan. The companion **FunGuy-Android-Profiling-Findings.md** contains the detailed measurement history. The current-state and next-action sections here supersede older “next step” paragraphs in that chronological record.

## 1. Start here after context loss

**Phases A/B and Phase C are documented below; Phase D is the latest work (Section 16). Five isolated visual test variants were created and verified in Unity Play mode; one Combined APK was built and reached Android gameplay after an emulator restart. No reliable FPS improvement was established over the earlier baseline, because performance was near the 60 Hz ceiling and scenes differed. Keep production graphics unchanged until a better-controlled comparison is available. The Combined APK was missing from Builds at the last check, despite the surviving logs/screenshots and measurements.**

The previously tested `D:\Work\FunGuy\Builds\gles3-baseline.apk` is retained: 68,866,894 bytes, SHA256 `8F79AEF578D871BDF7D89F3038E6CA7F24062755695A9AF2DD3CA13F418FFBD5`. It does not need rebuilding. The latest installed app in the stopped emulator is now **vulkan-targeted-profiling.apk**, not the GLES baseline.

The matching GLES NON-development baseline measured23.52FPS idle/26.64FPS moving at90Hz,24.16FPS warmed idle at60Hz and27.10FPS in a60Hz repeat. In the SAME60Hz emulator boot, switching GLES -> Vulkan -> GLES produced24.16 ->59.80 ->27.10FPS. World contents/phases varied, but the repeated large gap strongly supports the API/rendering path as a major factor in this emulator. Disabling development/debug features alone did not make GLES perform like Vulkan. The exact contributions of driver translation, submission workload and GPUResidentDrawer remain unresolved.

In the 4GB 60Hz emulator, the targeted Vulkan development APK measured **55.49FPS** unrecorded idle, **7.24FPS** with profiler recording, **56.81FPS** after recording stopped, and **58.88FPS** post-launch/mixed-reset. Recorded Vulkan main-thread frames showed **217 draw calls,142 SetPass calls**, and ~305k triangles, versus ~716–786 draws/~447–490 SetPass in earlier GLES development captures (not exact scene/build matches). Profiler traces were intrusive; GPU times unavailable. The emulator remains STOPPED and its original4GB/90Hz/2160x1600 configuration restored byte-for-byte; targeted development APK remains installed. Unity Editor is NOT playing, recording OFF, original Mobile URP restored, source scene clean. **Current selected profile is `FunGuy Vulkan GRD ON Diagnostic`**, ready for the first manual A/B build. See Section14 for the two diagnostic profiles, fixed seed, non-identical scene fingerprints, and required release cleanup.

## 2. Objective, scope and working rules

Find the causes of low FunGuy frame rates, distinguish emulator overhead from game workload, and make measured game-side improvements while preserving appearance and gameplay.

- [x] Start with a Blackview Link8-style tablet emulator; owner originally selected6GB.
- [x] Owner subsequently requested a4GB experiment to reduce PC RAM use.
- [x] Owner requested both Unity Profiler and Memory Profiler; both were actually used.
- [x] Owner explicitly declined physical-phone testing in this phase. Focus on emulator; do not ask again for the Redmi unless the owner changes scope.
- [x] Owner builds APKs manually. Agent prepares settings, installs and tests. Do not start a Player/APK/AAB build without a new explicit instruction to do so.
- [x] Preserve installed app data: use `adb install -r`; do not wipe the AVD or uninstall the package to switch APKs.
- [x] Preserve unrelated local changes, scene/prefab edits and Google Play Games resolver/package work. No broad cleanup, package upgrades or project resets.
- [x] Keep measurement notes updated after each experiment. Record APK hash, actual API, development state, refresh rate, RAM, scene phase, recording state, method, results and limitations.
- [ ] Any optimization must have a baseline, one isolated change, a visual/gameplay check and an equivalent after measurement. Keep a rollback path.
- [ ] Close the emulator between builds and after testing to release PC RAM. Docking/minimizing does not release its working set.

This AVD is Android15/API35 x86_64 translating the ARM64 Unity player on an NVIDIA host GPU. It does not reproduce Blackview firmware, a Helio CPU, a Mali GPU, phone thermals or real-device driver behavior. Emulator conclusions are not a diagnosis of the Redmi.

## 3. Current configuration and artifacts

| Item | Recorded state / location |
| --- | --- |
| Project | `D:\Work\FunGuy` |
| Unity version | 6000.3.9f1; Android target |
| Gameplay scene | `Assets/_Game/Funguy.MushroomRunner/Scenes/MushroomRunnerGameplay.unity` |
| Unity MCP endpoint | `http://127.0.0.1:8090/mcp` |
| Unity MCP instance | `FunGuy@4c74fdf52ee2a695` |
| Package / activity | `com.omg.funguy` / `com.unity3d.player.UnityPlayerGameActivity` |
| AVD internal ID | `NeonChambers_API35_Play` — historical name; this is the tablet test AVD |
| AVD config | `C:\Users\USER\.android\avd\NeonChambers_API35_Play.avd\config.ini` |
| Saved display name | Blackview Link8 (4GB test, Android15) |
| Saved display / RAM | 2160x1600,213dpi,90Hz,4096MB |
| Host GPU verified | NVIDIA GeForce RTX3060; not SwiftShader |
| Memory snapshot | `D:\Work\FunGuy\MemoryCaptures\idle-memory.snap`,26,098,923bytes |
| Workspace evidence root | `C:\Users\USER\Documents\Codex\2026-11-06\do-you-remember-when-we-profiled\work\funguy-profiling` |

### Build profiles

All paths below are under `D:\Work\FunGuy\Assets\Settings\Build Profiles`.

| Profile | Role | Important settings |
| --- | --- | --- |
| `Android™.asset` | Owner's original profile | Vulkan then GLES3, ARMv7+ARM64; preserved |
| `FunGuy Blackview Link 8 Profiling.asset` | Existing diagnostic GLES build | Development/Autoconnect/Script Debugging ON; Deep Profiling/Wait for Debugger OFF; ARM64 IL2CPP Release/OptimizeSpeed; timing/GLES GPU recorders ON |
| `FunGuy GLES3 Baseline.asset` | Clean GLES FPS comparison; last selected | GLES3 only; Development/Autoconnect/Script Debugging/Deep/Wait OFF |
| `FunGuy Vulkan Baseline.asset` | Clean Vulkan FPS comparison | Vulkan only; same baseline settings otherwise |
| `FunGuy Vulkan Targeted Profiling.asset` | Previously built and measured in Phase B; retained | Vulkan only; Development/Autoconnect ON; Script Debugging/Deep/Wait OFF; copied from Vulkan Baseline |
| `FunGuy Vulkan GRD ON Diagnostic.asset` | Phase C manual build first; active now | Same Vulkan development settings; diagnostic bootstrap selects duplicate Mobile URP with GPU Resident Drawer ON and seed20261106 |
| `FunGuy Vulkan GRD OFF Diagnostic.asset` | Phase C manual build second | Identical to ON diagnostic except one extra scripting define; bootstrap selects duplicate Mobile URP with GPU Resident Drawer OFF, same seed |

Both baseline profiles use ARM64 IL2CPP Release/OptimizeSpeed, APK, same scene and mobile quality. Frame timing stats and OpenGL profiler GPU recorders are OFF. Optimized Frame Pacing is OFF in both to keep this comparison controlled. The saved baseline assets were compared and differed only in profile name and Android graphics API.

Vulkan-only is deliberate for the test, so API selection cannot silently fall back. It is not a final shipping decision. Keep the original profile intact. Its SHA256 was rechecked at handoff and still matches the pre-investigation backup: `E982CD5B4FE23FDB4581D1230B36477D0B1B98EF27ECA6FF7FE653E0B2AC90A5`.

### APK inventory

| APK under `D:\Work\FunGuy\Builds` | Size | Status / hash |
| --- | --- | --- |
| `debug.apk` |130,148,558bytes| Earlier GLES development APK, installed/profiled. SHA256 `8A2B0E4DD4BC9995CC4690AC1A97F1F19AD65B6E13CD0C5083E4B659EDA9175E` |
| `vulkan-baseline.apk` |69,825,014bytes| Tested and retested; retained for reinstall. SHA256 `AE0CD642ADF5F6197F9EEE6EF16D9228C8418219F0B04D1C917FCC4525A11952` |
| `gles3-baseline.apk` |68,866,894bytes| Installed and tested; CURRENTLY INSTALLED. SHA256 `8F79AEF578D871BDF7D89F3038E6CA7F24062755695A9AF2DD3CA13F418FFBD5` |

Rehash if files change. The most recent live BuildReport read was Cancelled for a different output, debug2.apk; it is not the provenance of the successfully exercised Vulkan APK. The old debug.apk report said Succeeded but also contained4errors/93warnings. Do not describe either build as error-free based solely on those reports.

## 4. Completed work checklist and what it established

### Setup, tools and preservation

- [x] Recovered prior profiling documentation/workflow and identified emulator/ADB/Unity Profiler/Memory Profiler tools.
- [x] Configured the existing AVD to approximate the tablet display and originally6GB RAM, without wiping app data.
- [x] Disabled Pixel7 cutout/system UI overlays in the guest.
- [x] Verified host hardware graphics acceleration.
- [x] Read project context and pulled build settings; scene list contains the gameplay scene.
- [x] Created separate diagnostic and baseline profiles through live Unity APIs; preserved original Android profile.
- [x] Installed the owner's debug APK and exercised actual slingshot gameplay.
- [x] Used normal Unity Profiler to capture real Android CPU/rendering/memory data and save `.raw` traces.
- [x] Took a real remote Android Memory Profiler snapshot, imported it into the official Memory Profiler UI, and verified Valid=true, Android, IsEditorCapture=false, FunGuy.
- [x] Documented that Script Debugging support was enabled but no IDE managed debugger session was attached.
- [x] Installed/tested Vulkan baseline and verified actual runtime Vulkan use.
- [x] Installed/tested matching non-development GLES baseline at90/60Hz and repeated GLES/Vulkan/GLES in one60Hz emulator boot.
- [x] Closed emulator after tests; restored temporary display changes; retained app data/captures.

### Measurements already completed

| Experiment | Result | Interpretation boundary |
| --- | --- | --- |
| Old GLES development, recording OFF |27.16FPS/36.82ms| Initial emulator baseline |
| Same running app at half dimensions1080x800 |27.04FPS/36.98ms| Quarter pixels did not help that idle sample; pixel fill alone is unlikely to explain this emulator baseline |
| Unity CPU recording, Deep OFF |Approximately5FPS; recorded mean approximately202ms| Recording was severely intrusive; these are not normal game/phone timings |
| Rendering+Memory recording with CPU/GPU areas OFF |5.63FPS| Disabling CPU/GPU areas alone did not remove global recording overhead |
|4GB guest, old APK |25.66FPS off /8.16FPS recording| Short live session |
|6GB guest, old APK |19.42FPS off /6.84FPS recording| Different short session; cold start/world/host load not held constant |
|4GB host memory saving |Approximately1.3–1.5GB less emulator working set| Helpful for the PC, not proof RAM caused the FPS gap; overhead remained |
| Vulkan baseline,90Hz |44.99/45.01/44.98FPS idle;44.69 moving sample| Android explicitly reported app45FPS override |
| Same Vulkan APK, temporary60Hz |59.83FPS over5.28sec moving run;59.56FPS over15.83sec follow-up| Demonstrates approximately60FPS capability in this emulator configuration; not long-session phone proof |
| Matching GLES non-development90Hz |23.52FPS idle;26.64FPS moving| Development OFF does not by itself remove the GLES slowdown |
| Same-boot60Hz GLES/Vulkan/GLES |24.16 /59.80 /27.10FPS| Strong graphics-path evidence; procedural world/scene phases not exactly fixed |

The new GLES60Hz startup sample14.33FPS began with a blank screen and must be excluded from steady-state comparisons. The Vulkan recheck file's name includes “idle”, but boundary screenshots show movement followed by reset/idle; classify that15sec sample as mixed. Additional GLES60Hz launch/reset sample26.38FPS also contains movement followed by reset. Do not present these as pure moving or pure idle throughout. Full sample details/corrections are at the top of the companion findings file.

Important anomalous sample: an initial60Hz idle collection averaged45.91FPS with a3.15sec gap despite median16.73ms. A finite127-frame history can lose frames if polling is delayed; a real stall is also possible. Do not omit this anomaly or claim all samples were uninterrupted60FPS. Later samples reproduced approximately60 with much smaller gaps.

### Game workload findings

**Strong rendering lead, not an isolated game-side root cause:**

- Recorded approximately716–786 draw calls,447–490 SetPass calls and313k–329k triangles/frame.
- Native CPU sampling of the old GLES APK found41.47% inclusive sampled CPU work under `QemuPipeStream::commitBufferAndReadFully` and overlapping40.66% under GL buffer-unmap transport. This is substantial emulator graphics/translation work. These percentages overlap and are not wall-frame-time percentages.
- Normal unrecorded thread samples showed graphics worker71–77% of one virtual core and main thread63–65%; neither was proven individually saturated.
- GPU frame time returned0 in the captured API, meaning unavailable data, not zero GPU cost.

**Text/UI lead:**

- Approximately13–16 TMP mesh regeneration calls per instrumented frame.
- HUD_UI is largely event driven; SpeedMeter_HUDUI polls in LateUpdate; TextAnimator effects are present.
- Exact labels and dirty-state causes have not been identified. Do not assume an unchanged `.text` assignment causes every rebuild.

**Physics lead:**

- Many MushroomLauncher components receive FixedUpdate and clear contact-tracking entries.
- Recording caused approximately9–10 fixed steps per slow frame and785–919 launcher calls/frame. Catch-up inflated those counts; they are not established uninstrumented rates.
- Loaded collider counts:435Box,362Mesh,378Capsule,47Sphere. Loaded is not equivalent to simultaneously active or expensive.

**Memory/texture findings:**

- Snapshot Texture2D138objects/24.40MiB; RenderTexture33/71.15MiB including a26.37MiB profiler screenshot object; Shader14.69MiB; Mesh13.05MiB; Audio9.14MiB.
- Loaded GameObjects5315/MeshRenderers3044/LODGroups826 include inactive/loaded content, not all visible draws.
- All52 audited game-owned textures were automatically compressed. Wider dependency audit:63ASTC6x6,3ASTC4x4,1ETC2,7RGBA32,2RGB24. Dependency list includes Editor icons.
- Remaining inspected game uncompressed textures were small/intentional:128x128 ring,1024x32 LUT,64x64 noise.
- No demonstrated GC, texture-memory or memory-leak root cause. One snapshot is not a leak test. Idle recorded GC was28bytes/frame.
- Android PSS/RSS and Unity native-object sizes are different views; do not add them together. Old development PSS approximately988MiB; Vulkan baseline idle PSS approximately637MiB. Builds/conditions differ, so this is not a controlled memory optimization claim.

### Render settings/source facts

- Mobile_RPAsset: render scale0.75, HDRoff, MSAAoff/1, shadow distance160, shadowmap512, one cascade, SRPBatcher ON, GPUResidentDrawer requested.
- Mobile_Renderer: Forward+, no renderer features.
- Old GLES runtime warned GPUResidentDrawer platform/BatchBufferTarget unsupported. Unity excludes GLES from GPUResidentDrawer support. Vulkan is compatible in principle; actual drawer activity/draw reduction in the release APK has not been measured.
- GameplayManager explicitly requests60FPS. No explicit30FPS cap was found. The90Hz emulator's app override45 was directly observed, then60 at60Hz.
- Two authored cameras exist, but ResetSlingShot and OnSlingShotLaunched switch between them. Do not claim double rendering from static enabled flags.
- Existing startup warnings include convex mesh hull polygon limit, DOTween infinite child loops inside sequences, DontDestroyOnLoad root requirement and setting velocity on kinematic bodies. They deserve correction, but are not proven steady-state FPS causes.
- No gameplay, shader, UI, physics or production URP optimization has been applied yet.

## 5. Ordered next steps and decision points

### Phase A — clean API comparison completed; optional broader repetition remains

- [x] Recheck GLES APK size/hash and timestamp. It already exists; no rebuild request needed unless it has become invalid or changed unexpectedly.
- [x] Start4GB AVD at saved90Hz with `-gpu host -feature Vulkan`, no snapshots. Keep host/emulator graphics configuration identical for both APIs. Do not disable emulator Vulkan only for GLES in this comparison.
- [x] Confirm Android boot completion, physical2160x1600 and guest RAM configuration.
- [x] Install GLES APK with `adb install -r`, launch correct activity, verify runtime GLES from Android GPU stats/logs. GPU stats may accumulate per-package use across relaunches; account for history/reboot and do not infer current API from a previous Vulkan entry alone.
- [x] Verify release/non-development APK state and Unity recording OFF. Do not attach heavy profiling during FPS baseline tests.
- [x] Warm up, verify a changing rendered scene, then collect idle and moving-gameplay presentation timing. Record visual state/score at sample boundaries.
- [x] Repeat the60Hz experiment by stopping emulator, backing up config and changing only `hw.lcd.vsync` to60. Restart with identical host graphics flags and repeat samples.
- [x] Reinstall/retest Vulkan and switch back to GLES in the same60Hz emulator boot. The large gap reproduced. Exact world/phase matching and a larger statistical study remain limitations rather than reasons to repeat baseline builds now.
- [ ] Use a fixed/reproducible seed if the existing game supports one; first inspect rather than editing gameplay just to add determinism. Otherwise document world variation.
- [x] Record mean FPS/frame interval, median,p95,max, total captured frames/duration, host free RAM and scene phase. Separate startup, idle, launch, movement, spawning and reset; mark mixed/unclassified samples honestly.
- [x] Restore90Hz configuration and stop emulator after testing.

Decision:

Observed outcome: clean GLES remains materially slower, while Vulkan repeatedly approaches60FPS at60Hz. Proceed with PhaseB on Vulkan and prioritize the render-submission/API compatibility path. This does not isolate a particular script/shader, prove GPUResidentDrawer activity, establish universal API superiority or diagnose a physical phone.

- If both clean APIs approach60 at60Hz, development/debug/recording and display pacing account for much of the observed gap; do not declare Vulkan the sole fix.
- If clean GLES remains materially slower under matched conditions, prioritize API/render-submission/driver path investigation and verify Vulkan stability.
- If both remain slow in certain sections, investigate that scene phase with targeted captures.
- If both hit the60FPS ceiling, equal FPS does not prove equal CPU/GPU cost. Obtain workload/timing data or a separate explicitly labeled diagnostic configuration before comparing headroom. Do not silently raise the shipping frame target.

### Phase B — targeted game profiling with controlled overhead (initial capture complete)

- [x] Preserve baseline profiles/APKs and prepare a separate Vulkan development profile with ARM64 IL2CPP Release/OptimizeSpeed, Development/Autoconnect ON, Script Debugging/Wait/Deep OFF and identical gameplay/render quality. Confirm allocation callstacks OFF when profiling.
- [x] Owner manually built it; agent did not start a build.
- [x] Measured the development APK with recording OFF before profiling: 58.06FPS short warm sample; 55.49FPS 12-second idle sample.
- [x] Attached to the Android player (PID 3168, port 55000 via ADB forward; direct Unity Profiler connection), not the Editor; verified valid advancing raw frame indices and names.
- [x] Recorded one short idle CPU/rendering burst; stopped through the actual ProfilerWindow, verified driver OFF, saved `.raw` and SurfaceFlinger JSON. This completes the first bounded burst, not all possible scene phases.
- [x] Extracted coarse counts; held Deep Profiling OFF. Allocation callstack state was not independently rechecked during this specific burst, so do not assert it was verified.
- [x] Compared recording OFF/ON/OFF: 55.49/7.24/56.81FPS; added a distinct post-launch/mixed-reset 58.88FPS sample. Recording overhead remains severe, so trace durations are *not* normal runtime costs.
- [x] Confirmed GPU frame timing returned 0/unavailable in sampled frames. Do not claim GPU-bound or CPU-bound from these data.
- [ ] Native simpleperf is an optional corroborating path for debuggable development APKs. It also has overhead and translated symbols can be unresolved. It is not currently available through `run-as` on the non-development baselines.

### Phase C — rendering investigation, highest-priority game lead

- [ ] Attribute draws by forest foliage, mushrooms, characters, UI, shadows and other passes. Inspect an appropriate Frame Debugger capture and runtime counters; Editor captures are structural evidence, not phone timings.
- [ ] Verify SRPBatcher/GPU instancing compatibility for actual Toon Fantasy Nature materials/shaders; check why batches break. Do not assume GPUResidentDrawer is active merely because its checkbox is on.
- [ ] Compare GPUResidentDrawer actually enabled/disabled on a supported API in an isolated diagnostic setup. Verify compatibility, variants, visual output and draw count.
- [ ] Audit shared versus instantiated materials, renderer/material variation and repeated meshes. Avoid merging distinct artistic materials without reviewing appearance.
- [ ] Inspect camera culling, active cameras, render layers, LOD behavior and spawning/culling distances. Loaded3044 renderers are not3044 visible draws.
- [ ] Test shadow distance/casters as one reversible diagnostic variable. The current160 distance is a lead, not a mandate to reduce it. Compare visuals and real frame-time improvement before retaining changes.
- [ ] Only compare Forward versus Forward+ as an isolated renderer variant after API/instancing behavior is understood. Changing several render settings together prevents attribution.
- [ ] Retain the smallest change with repeatable performance benefit and acceptable visuals; revert experiments that show no measurable benefit.

### Phase D — TMP/UI investigation

- [ ] Identify specific TMP objects rebuilding through profiler context, object hierarchy and dirty events; separate intentional TextAnimator animation from unchanged labels.
- [ ] Check speed/score formatting, repeated SetText/property updates, font/material changes, layout rebuilds and Canvas partitioning.
- [ ] Temporarily disable one effect or group in a diagnostic comparison, keeping the rest fixed. Measure counts and unrecorded frame performance.
- [ ] Cache displayed/rounded values and avoid unnecessary updates only where the source actually repeats work. Preserve score correctness, animation timing and readable UI.
- [ ] Verify whether mesh regeneration decreases and whether that reduction materially helps frame time. Do not optimize a counter merely because it is visible.

### Phase E — physics and spawning investigation

- [ ] Count active launchers, contacts and FixedUpdate calls per fixed step in normal-speed gameplay, separately from calls per rendered frame.
- [ ] Inspect contact-tracking cleanup in MushroomLauncher. Consider skipping empty work or consolidating updates only if contact lifecycle/removal remains correct.
- [ ] Inspect active collider complexity, layer collision matrix, trigger interactions, rigidbody state transitions and procedural block lifetime.
- [ ] Attribute spawn/reset spikes to allocation, instantiation, mesh/collider setup or pooling before selecting a fix.
- [ ] Check high-density moving sections and repeated resets. Keep launch power, bounce behavior, score, contacts and speed identical after any change.
- [ ] Do not globally increase fixed timestep or reduce physics accuracy to hide catch-up generated by intrusive profiling.

### Phase F — memory, warnings and longer stability

- [ ] Capture comparable memory snapshots after warm-up and after repeated run/reset cycles, outside timed FPS samples. Compare retained objects, not just process working set.
- [ ] Investigate retained blocks, textures, materials, tweens and event listeners only if the trend/call references show accumulation.
- [ ] Correct DOTween sequence-loop usage and rigidbody/reset warnings with behavior checks; measure whether logging continues during steady gameplay.
- [ ] Inspect convex collider warnings and scene ownership of DontDestroyOnLoad objects; fix concrete behavior issues without claiming automatic FPS gains.
- [ ] Run a longer moving-gameplay/reset session on the winning configuration. Record stutters, crashes, visual defects and memory trend. Short60FPS samples are not a stability pass.

### Phase G — final configuration and delivery

- [ ] Select API order from measured compatibility/performance. Vulkan-first/GLES3 fallback is a candidate, not a universal answer.
- [ ] Separately evaluate Optimized Frame Pacing and the60FPS target across60/90Hz modes; preserve consistent target and record changes. Do not mix this into the initial API comparison.
- [ ] Keep existing ASTC compression unless an identified texture/device issue justifies a change. Avoid broad texture reimports as an FPS experiment without evidence.
- [ ] Keep non-development shipping builds separate from diagnostic builds.
- [ ] Summarize each accepted change: cause/evidence, change, validation, before/after and remaining limitations. Keep baseline APK hashes and rollback notes.
- [ ] Physical-phone validation remains deferred until the owner changes scope. Do not claim emulator success has fixed the Redmi.

## 6. Tooling runbook and known failure modes

### Live Unity connection

Use connected `unityMCP` purpose-built tools. The separately tagged Unity plugin's workspace_context previously returned an internal error; live Unity MCP and scoped local tools were the working fallback. No need to reinstall packages to solve that plugin error.

1. Read `mcpforunity://project/info`, `mcpforunity://editor/state` and instances as needed; confirm root and Unity version.
2. If stale/not ready after reload, retry a bounded state read and verify actual Editor liveness. Do not treat stale status as a verified current configuration.
3. Enable `scripting_ext`, `profiling` or `docs` groups with manage_tools if necessary. Disabled groups previously caused execute_code to fail without useful details.
4. Use live Editor APIs to copy/configure assets. Read relevant skills and any current repository instructions before code changes. No applicable AGENTS.md was found earlier; recheck if the project changed.
5. After source edits, wait for compilation and inspect Console errors. No broad test suite is needed for documentation-only work; gameplay/optimization changes require focused behavior validation.

### Emulator/ADB commands

Paths:

```powershell
$adbPath='C:\Users\USER\AppData\Local\Android\Sdk\platform-tools\adb.exe'
$emulatorPath='C:\Users\USER\AppData\Local\Android\Sdk\emulator\emulator.exe'
$avdConfig='C:\Users\USER\.android\avd\NeonChambers_API35_Play.avd\config.ini'
```

Start with a hidden launcher and log redirection to a new experiment folder:

```powershell
Start-Process -FilePath $emulatorPath -ArgumentList @(
 '-avd','NeonChambers_API35_Play','-memory','4096',
 '-no-snapshot-load','-no-snapshot-save','-no-boot-anim','-no-skin',
 '-gpu','host','-feature','Vulkan'
) -WindowStyle Hidden
```

Wait for `adb devices` to show device and `shell getprop sys.boot_completed` to return1. Then:

```powershell
& $adbPath -s emulator-5554 install -r 'D:\Work\FunGuy\Builds\gles3-baseline.apk'
& $adbPath -s emulator-5554 shell am start -W -n com.omg.funguy/com.unity3d.player.UnityPlayerGameActivity
& $adbPath -s emulator-5554 shell dumpsys gpu --gpustats
& $adbPath -s emulator-5554 shell dumpsys display
& $adbPath -s emulator-5554 shell wm size
```

For gameplay, the prior successful input was `shell input swipe 1080 650 1080 1250 600`; verify launch visually rather than assuming input worked. Screenshots showed score33/88 and15km/h during Vulkan runs.

For60Hz tests: stop emulator with `emu kill`; verify qemu exits; back up the exact config; change only `hw.lcd.vsync=90` to60; restart and verify actual display mode. Restore the backup after stopping. The existing backup `vulkan-baseline\config-before-60hz.ini` contains4GB/90Hz. Prefer a fresh backup for a new experiment so later user changes are not lost.

Do not reuse the early `-feature -Vulkan` launch override for Vulkan tests. It was used for compatibility in the first GLES session; Vulkan is now verified to launch successfully in this project.

ADB/server/process access required scoped elevated execution in this environment; ordinary sandbox calls reported daemon startup failure. Use the tool's permission mechanism when needed. Do not weaken Android security or root the AVD to obtain symbols.

### Presentation timing collectors

Existing helpers under the evidence root:

- `ram-comparison\measure.ps1 -Label <unique-name> -Seconds 10`: dynamically finds the FunGuy SurfaceView, clears latency, samples the finite recent127-frame history, writes JSON/raw text plus host RAM/working set. It does NOT measure the full10sec if history wraps.
- `vulkan-baseline\measure-continuous.ps1 -Label <unique-name> -Seconds 15`: polls latency histories and deduplicates timestamps, writing a longer JSON/raw record in that script's folder.

Before relying on long-session results, improve the continuous collector to log poll start/end times, latency-command duration, raw frame ranges and possible history overflow. The initial3.15sec gap is unresolved; finite-history loss must not be mislabeled as a proven game stall. Preserve outliers and their provenance. Do not silently filter large intervals to make averages look better.

Update: `gles-baseline\measure.ps1` now logs poll start/end, command duration, window frame count, overlap and potential coverage gaps. Accepted API comparison samples reported0potential coverage gaps with worst command times approximately42–70ms. Reuse this improved helper with unique labels. It still cannot identify exact game phase automatically or guarantee zero observer cost.

Use unique labels; helpers overwrite same-label files. Avoid other heavy captures or screenshot commands during timed baseline intervals. The benchmark method itself must remain the same between APKs.

### Unity Profiler connection and recording control

- A name in connectedProfiler is insufficient: stale advertised NAT IDs were observed after app restarts. Re-query available players/ports and verify advancing frames.
- Working route on the previous development APK: `adb forward tcp:55000 tcp:55000`, then `UnityEditorInternal.ProfilerDriver.DirectIPConnect("127.0.0.1:55000")`. Verify the player's listening port each run instead of assuming55000 indefinitely.
- ProfilerDriver.enabled alone previously disagreed with ProfilerWindow recording state and resumed. Use `ProfilerWindow.SetRecordingEnabled(false)` through reflection, then verify driver/window state.
- If no Profiler window exists, a loop over windows does nothing. Reopen the normal Profiler before using window control; assert the window count is nonzero.
- Save raw profiles through ProfilerDriver.SaveProfile with a unique absolute path. Inspect valid raw frame data and completed frames. Nested inclusive samples overlap; never add them as independent costs.
- No normal remote Profiler connection should be expected from the current non-development baseline APKs.

### Memory Profiler

- Package1.1.12, real remote snapshot already captured and imported. Window visibility may change; file validity was verified, not perpetual UI state.
- Prefer official UI/SnapshotDataService import/load flow for crawling/postprocessing.
- Do not repeat the earlier direct `new CachedSnapshot(reader)` extraction unchanged: inventory wrote successfully, but disposal hit an uninitialized RootAndImpactInfo NullReference before proper postprocessing. No package source was modified.
- A memory snapshot pauses/disturbs execution; keep it outside timed FPS measurements and label instrumentation objects such as ProfilerScreenshotFull.

### Optional native sampling

The working debuggable-app command was:

```powershell
& $adbPath -s emulator-5554 shell simpleperf record --app com.omg.funguy -e cpu-clock -f 99 -g --duration 8 -o /data/local/tmp/funguy-native.data
```

Pull the capture and use simpleperf report for thread/library/symbol/callgraph views. Plain shell stat failed permissions; direct run-as record failed a debug sample-rate property; supported `--app` mode worked. No rooting or security changes were made. Current release baselines require a different supported profiling route or a separate development build; do not expect this same access to work unchanged.

## 7. Source investigation map

These are verified paths, not files already optimized:

| Area | File |
| --- | --- |
| Launcher physics/contact work | `D:\Work\FunGuy\Assets\_Game\Funguy.MushroomRunner\World\MushroomLauncher.cs` |
| HUD updates | `D:\Work\FunGuy\Assets\_Game\Scripts\UI\HUD_UI.cs` |
| Speed display | `D:\Work\FunGuy\Assets\_Game\Scripts\Gameplay\SpeedMeter_HUDUI.cs` |
| Frame target / camera switching | `D:\Work\FunGuy\Assets\_Game\Scripts\Gameplay\GameplayManager.cs` |
| Run/reset orchestration | `D:\Work\FunGuy\Assets\_Game\Funguy.MushroomRunner\Core\RunFlowCoordinator.cs` |
| Movement/reset rigidbodies | `D:\Work\FunGuy\Assets\_Game\Funguy.MushroomRunner\Movement\RunnerMovementMotor.cs` |
| Procedural block lifetime | `D:\Work\FunGuy\Assets\_Game\Scripts\Blocks\BlockSpawner.cs` |
| Mobile pipeline | `D:\Work\FunGuy\Assets\Settings\Mobile_RPAsset.asset` |
| Mobile renderer | `D:\Work\FunGuy\Assets\Settings\Mobile_Renderer.asset` |

Read fresh source before editing because the owner may continue working. Use live Unity APIs for asset/scene edits; avoid blind YAML edits.

## 8. Evidence index and invalid results

Evidence root: `C:\Users\USER\Documents\Codex\2026-11-06\do-you-remember-when-we-profiled\work\funguy-profiling`.

| Folder | Contents / purpose |
| --- | --- |
| `before` | Original Android profile, ProjectSettings, EditorBuildSettings, QualitySettings backups |
| `capture` | `.raw`/JSON Unity captures, remote memory snapshot/inventory, screenshots, logcat, SurfaceFlinger resolution/recording comparisons, simpleperf data/reports |
| `ram-comparison` | RAM config backup/helper, texture audit, valid RAM comparisons ending `-live`, Vulkan90Hz FPS JSON/latency files |
| `vulkan-baseline` | Host Vulkan startup logs, screenshots, Android GPU/display dumps,60Hz results, continuous collector,60Hz config backup |
| `gles-baseline` | Matching GLES90/60Hz results, same-boot Vulkan/GLES rechecks, improved polling diagnostics, phase-boundary screenshots, logs, display/GPU dumps, meminfo and restored config backup |

Important valid capture names: `idle-baseline.raw`, `warm-current.raw`, `cpu-only.raw`, matching JSON, `counters-only.json`, `idle-memory.snap`, `idle-memory-inventory.json`, `native-cpu.perf.data`, `native-cpu-report.txt`, `native-cpu-by-library.txt`, `native-cpu-callgraph.txt`.

**Invalid RAM recording comparisons:** early6gb-off/6gb-recording and `-verified` labels without `-live` were made while the Profiler window/connection was stale and frame indices did not advance. Keep them for audit history, but do not use their numbers as recording ON/OFF evidence. Use4gb-off-live,4gb-recording-live,6gb-off-live,6gb-recording-live.

Raw Unity trace timings are intrusive. A raw capture is proof the tool ran, not proof those durations represent the ordinary game.

## 9. Documentation and completion checklist

Authoritative human-readable outputs:

- `C:\Users\USER\Documents\Codex\2026-11-06\do-you-remember-when-we-profiled\outputs\FunGuy-Android-Profiling-Master-Plan.md` — this plan.
- Same folder: `FunGuy-Android-Profiling-Findings.md` — detailed chronological evidence.

Project copies:

- `D:\Work\FunGuy\Docs\Android-Profiling-Master-Plan-2026-11-06.md`.
- `D:\Work\FunGuy\Docs\Android-Profiling-2026-11-06.md`.

After every test or fix:

- [ ] Add a compact result with exact conditions and evidence path.
- [ ] Update completed/remaining checklist and current active/installed profiles/APKs.
- [ ] Mark disproven hypotheses and invalid measurements explicitly.
- [ ] Preserve baseline files and owner changes.
- [ ] Restore temporary settings, stop recording and close emulator when finished.
- [ ] Sync project/output documentation copies and verify matching hashes.

Overall investigation is complete only when clean API comparison, targeted attribution, any retained fixes, equivalent before/after verification and scope limitations are documented. We are not there yet. This handoff is complete; the optimization investigation continues.

## 10. References already used

- [Android Unity rendering, graphics APIs, texture compression and frame pacing](https://developer.android.com/games/engines/unity/start-in-unity).
- [Unity GPU Resident Drawer requirements and GLES exclusion](https://docs.unity.com/en-us/engine/6000.0/manual/analysis/graphics-performance-profiling/in-urp/reduce-draw-calls-urp/reduce-rendering-work-on-cpu/gpu-resident-drawer).
- [Unity targetFrameRate behavior](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/application/targetframerate).
- [Android Emulator graphics acceleration](https://developer.android.com/studio/run/emulator-acceleration).

## 11. Copyable continuation instruction

Continue FunGuy Android performance from Section16 (most recent) of `D:\Work\FunGuy\Docs\Android-Profiling-Master-Plan-2026-11-06.md` and companion findings. The original clean GLES/Vulkan comparison and Phase B profiling are complete. Owner-built Vulkan GRD ON/OFF APKs were both verified as Vulkan 1.1, with expected diagnostic ON/OFF logs. Same-boot 4GB/2160x1600/60Hz external FPS tests with profiler recording OFF showed ON idle58.63 and59.80FPS vs OFF58.74FPS; mixed launched/reset approximately60FPS for both. A 60Hz FPS ceiling hides workload differences. Valid Android ON raw profile shows205draws/132SetPass; Android OFF raw capture failed (no valid OFF draw counter). Windows Editor DX12 `ProfilerRecorder` comparison showed GRD ON430draws/282SetPass versus OFF744draws/~463SetPass (42.2% fewer draws /39.1% fewer SetPass), with unmatched triangle counts and scene phases. Recommended KEEP Vulkan+GRD ON+SRP Batcher+Forward+ and original 0.75 render scale. Potential visual A/B choices: shadow distance160 vs120/80, small-plant shadows, farther foliage outlines and LOD/culling, before any resolution decrease. Possible no-visible-change fixes: avoid unchanged TMP speed text assignments, investigate FixedUpdate physics callbacks. No visual downgrade or gameplay code optimization applied.

User authorized more builds, but a diagnostic-only `ProfilerRecorder` Android APK built via MCP `manage_build` failed to reach gameplay and is quarantined at `work/funguy-profiling/grd-ab/probe-runtime/FAILED_vulkan-grd-on-probe.apk`. The probe source was removed and simple bootstrap restored on disk; Editor bridge lost ping following that build, so recompilation of the restoration is PENDING. Unity Editor main window was `FunGuy - Untitled`; inspect/reopen the scene carefully, preserving owner changes, before building again. Original ON APK was restored with `adb install -r`, user data retained; emulator STOPPED and AVD restored byte-identically to4GB/90Hz/2160x1600. Production Mobile_RPAsset and gameplay source were not deliberately edited. Probe and temporary Resources diagnostic assets MUST NOT ship. Master/Findings documents are synced to the user's Codex November6 outputs. Evidence and full caveats Section15. No real Android-phone FPS demonstrated; obtain phone representative benchmark before shipping setting changes.
**LATEST PHASE D (Section 16):** User approved isolated tests for shadow distance160 ->120, small grass/flower shadows OFF, and GPU occlusion ON, keeping Vulkan, GRD ON, SRP Batcher, Forward+ and render scale0.75. Five duplicate URP assets and five paired Vulkan diagnostic profiles were created, all imported, all passed Editor Play mode asset-selection tests. Grass/flower shadow-off variants changed132 of228 matching MeshRenderers and changed no permanent prefab, material, production URP or scene files. Agent-built Combined APK reached Vulkan gameplay after reboot; FPS external presentation ~59.17 idle and59.43 post-input at60Hz. Older original GRD ON APK ~57.57 first/~59.78 warm at60Hz, but not a matched baseline: code, world, scene phase and warmup differ. No proven FPS win from any requested setting; do not edit production just yet. A single in-game FPS overlay screenshot (23 vs26) is a transient0.5s value; later original baseline captures were60FPS; stable game FPS on devices is unproven. Combined APK was absent from Builds at latest recheck though JSON and PNG evidence survive. Unity bridge disconnected after build, but window again showed MushroomRunnerGameplay. Emulator stopped and AVD90Hz config restored byte-for-byte. Maintain user app data. Next: restore bridge and build matched PhaseD variants, benchmark game frame times/screenshots, then user can choose visual compromises. Delete temporary Resources/assets/profiles before any release. Full details Section16 and phase-d-visual-ab work folder.
## 12. Phase B profile preparation — 6 November 2026

- Connected to the live Unity 6000.3.9f1 editor and verified `D:/Work/FunGuy`, Android target, gameplay scene idle and no compilation in progress. Confirmed `ProfilerDriver.enabled=false` before and after changing the selected profile.
- Created `Assets/Settings/Build Profiles/FunGuy Vulkan Targeted Profiling.asset` and a new `.meta` via Unity `AssetDatabase.CopyAsset` from the existing Vulkan baseline. Used live Unity platform-build-settings properties to set Development ON, Autoconnect Profiler ON, Script Debugging OFF, Deep Profiling OFF, Wait For Managed Debugger OFF and Android build type Development. Kept Vulkan-only, the build scene, IL2CPP/player configuration and quality settings unchanged.
- Compared the saved baseline and target line by line: both have 1,038 serialized lines and **only four lines differ**: name, `m_Development`, `m_ConnectProfiler`, and `m_BuildType`. The original Vulkan baseline SHA256 matches its backup. The new profile is active in the Editor.
- A whole-workspace checkpoint was unavailable because the existing dirty state exceeded the 256 MiB limit. Instead, backed up the Vulkan baseline `.asset` and `.meta` under `work/funguy-profiling/targeted-vulkan` before any write. No original baseline, gameplay, URP profile, or production player settings were intentionally changed.
- No APK/AAB was built. Existing GLES baseline remains installed in the stopped emulator; ADB device list was empty on recheck. **Next handoff to owner:** manually build `Builds/vulkan-targeted-profiling.apk` using the active targeted profile. After that, validate package hash, actual Vulkan runtime/API, development state, FPS with recording OFF and short verified Android-player recording ON/OFF. Close the emulator afterward. Do not infer real-device performance from emulator evidence.

## 13. Phase B targeted Vulkan development run — completed 6 November 2026

The entry in Section12 above was a *pre-build* handoff. The owner has now supplied the APK and the following test has actually run.

### Build, emulator, and capture provenance

- Owner-built `D:/Work/FunGuy/Builds/vulkan-targeted-profiling.apk`, **130,148,558 bytes**; SHA256 `4387ABE9D8721D825B0DA9564FC384BB1C3392CC18F651A1858507FC221D475D`. Do not confuse it with older `debug.apk`: same length but a different SHA256 and `boot.config` build GUID.
- `aapt` confirmed `com.omg.funguy` version0.1.0, minSDK25, targetSDK36 and Android `debuggable=true`, appropriate for a development profiling APK. Embedded `boot.config` has `profiler-enable=1` and `player-connection-debug=0`, supporting Script Debugging OFF. Android log confirmed `Build type 'Development'`, ARM64 IL2CPP, Vulkan API1.1 on NVIDIA RTX3060 host GPU. No development build was started by the agent.
- Existing Android15/API35 x86_64 translator emulator was started with `-gpu host -feature Vulkan -no-snapshot`, on the saved4GB/2160x1600/213dpi profile with ONLY vsync temporarily changed90Hz to60Hz. Saved the exact original config first. Installed the owner's APK with `adb install -r`, preserving the package data; launched `UnityPlayerGameActivity`, PID3168. Verified `sys.boot_completed=1`, mode60Hz and active Vulkan renderer in Android logs.
- SurfaceFlinger sample helper copied from `gles-baseline/measure.ps1`, polling roughly every0.75sec with overlap/coverage diagnostic. Every accepted sample had **zero potential SurfaceFlinger history coverage gaps** under that helper's heuristic. This does not exclude real frame stalls or eliminate all observer overhead.

| Sample | Presented frames / duration | FPS | Median, p95, max frame interval | Phase / recording |
| --- | --- | --- | --- | --- |
| `vulkan-targeted-shortcheck` | 239 /4.10s |58.06|16.74 /20.66 /49.22ms| short initial check, recording OFF |
| `vulkan-targeted-60hz-unrecorded-idle-01` |678 /12.20s|55.49|16.81 /32.49 /101.14ms| idle score0, recording OFF |
| `vulkan-targeted-60hz-recording-on-01` |29 /3.87s|7.24|133.56 /217.77 /217.77ms| idle, Unity recording ON; **intrusive** |
| `vulkan-targeted-60hz-unrecorded-after-01` |608 /10.68s|56.81|16.81 /30.17 /65.06ms| idle, recording OFF again |
| `vulkan-targeted-60hz-unrecorded-after-launch-mixed-01` |630 /10.68s|58.88|16.74 /19.33 /49.73ms| after a working slingshot launch, **mixed movement and reset**, recording OFF |

- The slingshot `adb input swipe` launched real gameplay: screenshot after launch showed score18 and speed15km/h. Screenshot at the later sample boundary showed the reset to score0. This validates the interaction, but the 10.68sec sample is NOT pure movement throughout.
- Live Unity Profiler was routed to the **Android player** on actual open TCP55000 via `adb forward tcp:55000 tcp:55000` and `ProfilerDriver.DirectIPConnect("127.0.0.1:55000")`. Opened a real ProfilerWindow and controlled `SetRecordingEnabled`. Started with cleared frames, observed advancing valid Android-player raw frames, stopped through ProfilerWindow and verified `m_Recording=false` and `ProfilerDriver.enabled=false`. Saved valid `vulkan-targeted-cpu-rendering-01.raw` (35,065,840bytes); sampled raw frame187 had `valid=true` and2951 raw sample records. The recording lasted longer than just the nominal4sec FPS measurement because of live inspection/tool response intervals; do not present all the trace as only4sec long.

### CPU/rendering counters and interpretation

- In recorded Main Thread frames25,80,140,180, Unity Rendering counters were stable at **217 Draw Calls / Batches,142 SetPass calls,305.31k triangles,325.72k vertices,52 shadow casters and22 RenderTextures**. Earlier GLES development traces showed716–786draws and447–490SetPass calls. Vulkan has notably fewer recorded submissions in these captures, but world phases, API, build settings and profiler context were not controlled to isolate the cause. GPU Resident Drawer is a leading hypothesis, not a verified attribution; zero static/dynamic batching counters alone cannot identify the active SRP/drawer path.
- The same intrusive frames showed **13 `TMP.GenerateText` markers/frame**, **109 `SRPBatcher.Flush` markers/frame** and **480–560 `MushroomLauncher.FixedUpdate` invocations/frame**. Physics reported approximately6–8 fixed-step passes per slow frame; each launcher is revisited on catch-up. The nested low-level TMP markers and SRP markers are not independent costs; do not add inclusive timings. These counts are diagnostic leads, not an uninstrumented60FPS workload measurement.
- Raw frame CPU times were approximately131–135ms when recording. `frameGpuTimeMs` read0, meaning unsupported/unavailable GPU timing, NOT zero GPU rendering cost. A real camera/gameplay screenshot also showed intermittent43FPS/40FPS momentary overlay values, while SurfaceFlinger samples averaged55–59FPS over longer windows; instantaneous overlay and multi-second presentation average need not agree.
- Android meminfo at the end of testing showed approximately786,543KiB app PSS, which is a different view from earlier release/build measurements and not a controlled memory optimization result.

### Cleanup, artifacts, and next controlled experiment

- Full artifacts: `C:/Users/USER/Documents/Codex/2026-11-06/do-you-remember-when-we-profiled/work/funguy-profiling/targeted-vulkan/` contains original AVD config backup, measurement JSON/polls/latency, `vulkan-targeted-cpu-rendering-01.raw`, screenshots (`vulkan-targeted-idle.png`, `vulkan-after-launch.png`, `vulkan-after-sample.png`), saved Android logcat/meminfo and emulator startup logs. Preserve original raw traces and hashes.
- After testing, verified Unity Profiler window OFF and driver OFF, removed the ADB forward, gracefully stopped the emulator (`emu kill`), verified QEMU exited and ADB devices empty, restored exact original AVD config (byte-identical hash) to4GB/90Hz/2160x1600. Removed only temporary project `Temp` thumbnails; kept screenshots in the external evidence folder. Installed APK/app data retained.
- **Priority next:** use a structurally grounded Frame Debugger investigation to attribute the remaining217 Vulkan draws to forest, mushrooms, shadows, UI and characters; confirm GPU Resident Drawer runtime activity and material/instancing eligibility. Then prepare a **separate** Vulkan profile with only drawer mode changed, retain production URP, and ask the owner to build manually; perform reproducible scene-phase A/B with recording OFF and visual/gameplay checks before keeping any optimization.
- Secondary: identify which TMP text component(s) cause13 full GenerateText invocations even at idle; inspect dirty text state rather than assuming unchanged `.text` assignment causes it. Profile per-launcher physics at normal frame cadence and consider event-driven contact cleanup only after equivalent behavior and frame timing can be validated. No gameplay, shader, scene, prefab, UI or production pipeline optimization was applied during this run.

## 14. Phase C structural audit + Vulkan GPU Resident Drawer A/B preparation — 6 November 2026

### Runtime/editor findings (structural, **not Android timings**)

- Verified actual Mobile quality pipeline `Assets/Settings/Mobile_RPAsset.asset`; the Graphics project default is `Custom_Mobile_RPAssett.asset` but is overridden by the Mobile quality level. The effective Mobile asset has SRP Batcher ON, render scale0.75, shadow distance160 and `m_GPUResidentDrawerMode=1`. `Mobile_Renderer.asset` uses Forward+ (`m_RenderingMode=2`). The original Mobile asset remained byte-identical (SHA256 `C1C67460F7D9960EF0F7D1D6AB71378DAB7658039BC992D3F07F88C58CD1FE6A`).
- `ProjectSettings/GraphicsSettings.asset` retains `m_BrgStripping=2`, which reflection confirmed is **KeepAll**; this matches the requirement to retain BatchRendererGroup shader variants. No production graphics/quality/renderer asset was edited.
- In Editor Play, the procedural scene generated **1,209 MeshRenderer components,1,186 enabled and active,310 LODGroups,15 distinct referenced materials**; approximately1,180 active renderers were under `_Runtime`. Counts include LOD children not necessarily visible or drawn, and they are NOT render passes/draw calls.
- Frequent meshes include mushrooms, grass, aspen trees, rocks and foliage; the most frequently referenced shader families were `Toon/TFF_CustomToonOutline`, `Toon/TFF_CustomToonVegetation` and `Toon/TFF_CustomGrass`. Their source shaders include instancing and `DOTS.hlsl` pragmas; shader support/actual BRG activity remains to be verified in the Android runtime. The referenced materials had `enableInstancing=false`; this does **not** by itself prove GPU Resident Drawer inactive. None of the1,186 active MeshRenderers had a nonempty MaterialPropertyBlock or nondefault sorting property in that sample.
- Attempted Unity Editor Frame Debugger with GameView and Frame Debugger windows, including paused Play mode and the supported `manage_profiler frame_debugger_enable` flow. Although enabled, it returned **0 frame events**. This does not mean0 draw calls. **No reliable per-pass or Hybrid Batch Group attribution exists yet.** The next capture can attempt remote Android-player Frame Debugger after the owner builds; avoid claiming GPU Resident Drawer is active merely from a checkbox or reduced draw counters.

### Isolated diagnostic setup created (no APK builds)

- Duplicated `Mobile_RPAsset.asset` via Unity AssetDatabase into `Assets/_Diagnostics/FunGuyAndroidProfiling/Resources/FunGuy_GRD_On.asset` and `FunGuy_GRD_Off.asset`. A saved line-by-line diff found exactly **two differences between the copies:** name and `m_GPUResidentDrawerMode` (ON=1, OFF=0). The ON copy differs from original only in its name. Renderer reference, lighting, quality, shaders, shadow settings and all other serialized fields match.
- Duplicated `FunGuy Vulkan Targeted Profiling.asset` into `FunGuy Vulkan GRD ON Diagnostic.asset` and `FunGuy Vulkan GRD OFF Diagnostic.asset`. Verified both profiles have otherwise identical serialized content to the targeted Vulkan development profile after removing only their names and scripting-define lines. ON defines `FUNG_GPU_RESIDENT_DRAWER_AB`; OFF defines that plus `FUNG_GPU_RESIDENT_DRAWER_OFF`. Development/Autoconnect ON; Script Debugging, Deep, Wait OFF; Vulkan-only. No production profile/player settings were changed.
- Created `Assets/_Diagnostics/FunGuyAndroidProfiling/FunGuyGpuResidentDrawerAB.cs`, compiled **only** with the above diagnostic define(s). At `RuntimeInitializeOnLoadMethod(BeforeSceneLoad)`, it loads the copied URP asset from Resources and applies it through `QualitySettings.renderPipeline`. Both diagnostics also call `UnityEngine.Random.InitState(20261106)` for procedural-world control. Logs must contain `[FunGuy Profiling] GPU Resident Drawer ON/OFF diagnostic URP selected: ...; fixed Unity random seed=20261106` in the running game before results are accepted. Ordinary profiles do not compile the startup override.
- A forced Unity script-only compilation of the ON profile and a subsequent compile of the OFF profile completed without new compiler errors; after the seed addition both profiles compiled correctly. Editor Play verified actual `QualitySettings.renderPipeline` is the appropriate diagnostic asset with drawer mode1/0 and each correct console log. After stopping Play mode, effective quality pipeline returned to the original Mobile asset, original scene stayed clean, and Profiler/Frame Debugger were OFF.
- **Important control limitation:** fingerprints of sorted environment mesh names and rounded positions differed even with the fixed Unity seed: OFF had1,142 qualifying environment mesh renderers, hash `D307A19CF7FD5C9E733AEA19D74F62F29B638B32684C3E4E76D93B6A4FA9F6C0`; ON had1,126, hash `64F7A678620967C27AF67D91DC17C48E66E6629E79FC770891BD6330CF552256`. Captures occurred at different elapsed phases, so this is NOT proof random seeding failed, but it proves exact world equivalence was **not** established. Use repeated measurements, screenshots, fixed time/score phase and if practical same-boot ON/OFF/ON. Do not infer causality from one FPS comparison.
- Backup hashes of original Mobile URP and targeted profile assets/metas are under `C:/Users/USER/Documents/Codex/2026-11-06/do-you-remember-when-we-profiled/work/funguy-profiling/grd-ab/`. No checkpoint of entire dirty project was made. No game, scene, shader, prefab, production renderer, ProjectSettings, or existing build file was intentionally changed for Phase C.

### NEXT — owner manually builds the paired diagnostic APKs

1. Unity editor currently has **`FunGuy Vulkan GRD ON Diagnostic`** active. The OWNER manually builds to `D:/Work/FunGuy/Builds/vulkan-grd-on-diagnostic.apk`. Do not auto-build or overwrite an existing APK.
2. Switch the build profile to **`FunGuy Vulkan GRD OFF Diagnostic`**, allow script recompilation to finish, then the OWNER manually builds to `D:/Work/FunGuy/Builds/vulkan-grd-off-diagnostic.apk`. Leave original Vulkan profiles/APKs unchanged.
3. After both exist, check SHA256, `aapt` flags, manifest, `boot.config` diagnostics, actual runtime Vulkan and diagnostic ON/OFF log in `adb logcat`. Use the same saved 4GB emulator, host Vulkan enabled, physical2160x1600 and temporary60Hz, and `adb install -r` so app data survives. Start with profiler recording **OFF**.
4. Warm up and capture matched idle and launched movement/reset phases with score/camera and visual screenshots; use the existing polling helper and several samples. Where possible use same-boot ON/OFF/ON order, note host free RAM, actual world contents, refresh rate, sample intervals and coverage gaps. Measure Draw Calls, SetPass, FPS median/p95/max and shadow/LOD behavior. Short intrusive profiler/Frame Debugger captures should be separate from FPS windows and labeled accordingly.
5. Interpret the result conditionally: fewer draws with GRD ON would strengthen the drawer-attribution hypothesis only if runtime mode and scene phase match; unchanged FPS at60Hz can hide headroom. Test visual/shader correctness and any regression before deciding whether ON is useful for shipping. Vulkan performance on emulator does not establish physical-phone performance.

**Release hygiene:** Both copied URP assets live in a `Resources` folder and therefore can enter ordinary APKs while retained. They are for this experiment only. After A/B results, revert to the normal production build profile and delete the two temporary GRD profiles and `Assets/_Diagnostics/FunGuyAndroidProfiling/` directory before releasing any game build. Do not remove the original `Mobile_RPAsset`, target profile or prior evidence.

## 15. Phase C A/B results and performance decision guide — 6 November 2026

### Two original owner-built Vulkan diagnostic APKs

- ON `Builds/vulkan-grd-on-diagnostic.apk`: 80,630,466 bytes, SHA256 `819F6FB91399C871BB4433E8E343B7928CBCA6DD62608E1C1202703EF4C160DF`.
- OFF `Builds/vulkan-grd-off-diagnostic.apk`: 105,621,671 bytes, SHA256 `727D11F14B7E64048EE34EAD5B839EF3978B0937171B324820D2209A99E2712B`.
- Both include the same 456 named ZIP archive entries and nearly identical compressed entry sizes; APK file-size difference is not proof of different game content. `adb install -r` retained data. Android player logcat verified Vulkan1.1 and the correct diagnostic ON/OFF URP logs, both with seed20261106.
- Same-boot emulator, 2160x1600, 4GB RAM, temporary60Hz, recording OFF. The external SurfaceFlinger helper reported no potential history coverage gaps:

| Run | Frames / sample seconds | Presented FPS | Median ms | p95 ms | Worst ms | Notes |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| ON idle1 |858 /14.62|58.63|16.68|18.53|182.60|One slow outlier, sample context differs |
| OFF idle1|846 /14.39|58.74|16.70|18.91|183.86|Similar single slow outlier |
| ON idle2|864 /14.43|59.80|16.70|19.52|33.52|Warm repeated pass |
| ON mixed run/reset|629 /10.47|60.00|16.70|17.93|20.87|Not continuous motion |
| OFF mixed run/reset|629 /10.47|60.00|16.61|18.54|27.94|Not continuous motion |

**Interpretation:** There is no meaningful *presented FPS* difference on the 60Hz emulator; both are near the ceiling. These runs do not establish equal CPU/GPU cost and are not physical-device benchmarks. Captures/screenshots and measurement JSON are retained under `work/funguy-profiling/grd-ab/runtime-2026-11-06`.

### Measured rendering workload (separate from FPS)

- Valid intrusive **Android-player ON** capture: `runtime-2026-11-06/grd-on-profile.raw`, recorded with Android player connected and recording stopped afterward. Sampled frames40–310: **205 draw calls,132 SetPass calls,331.36k triangles,48 shadow casters,22 render textures**. Recording-time CPU frame durations are not uninstrumented runtime costs.
- The subsequent Android **OFF** capture failed because the Profiler disconnected (`lastFrameIndex=-1`), so there is **no valid OFF Android draw-call figure**. Do not invent one or compare the earlier GLES counts as though they were the OFF Vulkan run.
- A short-lived *diagnostic-only* `ProfilerRecorder` component logged in **Windows Editor DX12 Play**, with run-in-background and GameView active. GRD **OFF**: five5sec windows, roughly **744 draw calls,462–465 SetPass,322k triangles**, FPS around59–60, GPU frame time about4.77–5.05ms. GRD **ON**: six5sec windows, **430 draw calls,282 SetPass,605k triangles**, FPS around59–60, GPU time typically2.2–3.74ms (variable; one1.06ms sample). This represents **42.2% fewer draw calls and39.1% fewer SetPass** with ON in these *Editor* captures, but geometry/phase differed materially (321k vs605k triangles), so neither the GPU-time change nor the amount of speedup can be generalized to Android phones. GRD ON is nevertheless a good default to retain on supported Vulkan hardware.
- The Android emulator uses host NVIDIA RTX3060/Android x86_64 translation. GPU bottlenecks, mobile thermals, memory bandwidth and fallback shader support are not proven by these tests. At60Hz, presented FPS is insufficient to compare headroom.

### Outliers and highest-value choices, preserving looks

| Priority | Current state / evidence | Recommendation | Visual impact / decision |
| --- | --- | --- | --- |
| **Keep now** | Vulkan markedly faster than GLES in repeated clean 60Hz A/B (roughly60 vs24–27 FPS); GRD supported on Vulkan, Editor draw calls down42% with ON | **Keep Vulkan + GPU Resident Drawer ON**, Forward+ and SRP Batcher. Verify device/API support and maintain tested fallback rather than assuming every phone supports the same path | No deliberate downgrade; correctness/fallback must be validated |
| **Keep now** | Mobile URP render scale0.75; HDR off, MSAA off, shadows512px, one cascade, soft shadows off; 2 additional lights/object, no additional shadows | **Do not lower scale or lighting quality yet.** Existing settings are already relatively mobile-minded | Preserves current sharpness, toon palette and scene mood |
| **Investigate first** | 310 LOD groups,1186 active renderers (includes unrendered LOD children) and many dense plants/grass/trees/mushrooms; shader outline material frequently used | Audit actual per-pass/far-field visible foliage and render layers, LOD transitions and culling, and test camera/frustum/GRD occlusion culling on copied diagnostic URP | Correct culling should have little/no visual change; overly short cull distances can reduce lushness |
| **Try visual A/B** | Mobile URP main light shadow distance160, 512 shadow map and48–52 recorded shadow casters; no precise shadow cost attribution yet | Compare shadows at160 (baseline) vs120 and80 with identical camera/visual screenshots; also test disabling cast shadows on tiny grass/flower decorations *only* | Shadows fade sooner; plants may look flatter without tiny contact shadows; preserve player/mushroom/large-tree shadows initially |
| **Try visual A/B** | Toon/TFF_CustomToonOutline has a front-culled ExtraPrePass plus forward and shadow/depth passes; hundreds of materials reference it | Test outline omission **on distant, small decorative meshes only**, not player or hero mushrooms, in a reversible material/LOD variant | Distant outlines may disappear; can preserve foreground silhouette/identity |
| **No-regret code candidate** | `SpeedMeter_HUDUI.UpdateSpeedMeter` assigns rounded TMP speed text in `LateUpdate` every frame even when rounded value unchanged | Cache last displayed integer; update TMP text only when rounded speed changes, with resets handled | Intended pixel-identical, fewer redundant text sets; validate existing text-animation/reset behavior before retaining |
| **Conditional code candidate** | Intrusive 7FPS profiler had480–560 `MushroomLauncher.FixedUpdate` calls per recorded frame during6–8 physics catch-up steps | Measure number of normally scheduled FixedUpdate callbacks at60FPS; then, if material, skip empty-set cleanup or move to event-driven lifecycle *with teleport/disabled-collider regression tests* | No intended look change; gameplay-risk if contact rearm breaks |
| **Last resort** | Render scale is already0.75 | Test0.70/0.65 only if real phones fail target FPS after other steps | Lower sharpness / more aliasing, particularly foliage and UI edge detail |

- Shader compatibility remains a separate blocker to investigate: Unity Console contained GLES3 variant compilation errors for `Toon/TFF_CustomGrass`, `TFF_CustomToonVegetation`, `TFF_CustomToonOutline`, `TFF_ToonWater` (DOTS/target4.5 vs GLES3 requirement3.5). This is not proof that all GLES rendering is broken; do not modify vendor-generated shaders without isolated variants and visual regression testing.
- Outlier priority is **rendering submission/CPU work and fallback compatibility**, then selective shadows/foliage/LOD, then TMP and physics. The existing FPS ceiling means none of these should be claimed as a proven Android-device FPS fix yet. Main objective is preserve artwork while reducing headroom cost on *real phones*.

### Failed lightweight Android probe and safe cleanup

- After the owner authorized additional builds, agent added an extra diagnostic-only `ProfilerRecorder` reader and built `vulkan-grd-on-probe.apk` through Unity MCP `manage_build` (IL2CPP + Gradle produced APK 105,623,288 bytes, SHA256 `D834A58AA40EDAE84F145B0B9A5DDBF352424F908DA654C484B653BD0D5BCDF9`). The MCP build-status response could not be retrieved because the Editor bridge became unresponsive; do not claim the managed build job itself completed cleanly.
- That experimental APK repeatedly hung with a black screen before its diagnostic startup log. The original manual ON APK also temporarily black-screened in the same emulator session, but a full emulator restart restored normal gameplay for the original; the new probe continued failing afterward. **Cause unproven; do not use probe APK as performance evidence.** Its failed source, Android logcat, screenshots and APK are quarantined under `work/funguy-profiling/grd-ab/probe-runtime/`. The bad APK was moved out of `Builds` to `probe-runtime/FAILED_vulkan-grd-on-probe.apk`.
- Restored `Assets/_Diagnostics/FunGuyAndroidProfiling/FunGuyGpuResidentDrawerAB.cs` to the prior **simple known-working mode/seed bootstrap** and updated its README. Did not edit production gameplay or Mobile URP. This source rollback has **not yet been recompiled in Unity** because the Editor bridge was not answering its ping after the MCP build; prior simple version had compiled and passed Editor Play tests, but confirm again in the Editor before a future build.
- Last emulator cleanup: installed original working ON APK with `adb install -r` (data retained), stopped emulator, restored original 4GB/90Hz/2160x1600 AVD config **byte-for-byte**, no emulator running. The Unity Editor process remained responsive at OS layer but its window title changed to `FunGuy - Untitled` during the build; the original gameplay scene file was not intentionally edited. **Inspect/reopen the gameplay scene carefully if needed after validating any unsaved Editor state.**
- **Shipping hygiene:** GRD diagnostic profiles, copied Resources URP assets and bootstrap are TEMPORARY. Remove them before production APKs; they are still present intentionally for follow-up. Do not use the failed probe APK or accidentally ship the temporary Resources copies. Neither code-optimization candidates nor visual compromises were deployed.

## 16. Phase D — shadow distance, small foliage shadows and GPU occlusion (6 November 2026)

**User-approved scope:** preserve Vulkan on supported hardware, GPU Resident Drawer ON, SRP Batcher ON, Forward+ renderer, render scale 0.75; independently test shadow distance 160 vs 120, disabling small grass/flower shadow casting, and enabling GPU occlusion culling. Prioritize visual fidelity; do not silently commit compromises to the production settings.

### Files and reversible variants created

- Added `Assets/_Diagnostics/FunGuyAndroidProfiling/PreparePhaseDVariants.ps1` and `FunGuyPhaseDVisualAB.cs`, and **five duplicate URP assets** under `Assets/_Diagnostics/FunGuyAndroidProfiling/Resources/FunGuy_PhaseD_*.asset` with corresponding **five development Vulkan profiles** under `Assets/Settings/Build Profiles/FunGuy Vulkan PhaseD *.asset`. Source definitions were based on the existing targeted Vulkan profiling profile and effective `Assets/Settings/Mobile_RPAsset.asset` (the production asset was **not edited**).
- Temporary shader/scene instrumentation compiles only when the profile has `FUNG_PHASED_TEST`. At `BeforeSceneLoad` it sets the copied URP asset and `UnityEngine.Random.InitState(20261106)`. A runtime-only scanner, present in every variant, checks for streamed MeshRenderers with `TFF_Grass_Patch_*` or `TFF_Flower_Patch_*` names every 4s. In two variants it disables **shadow casting** on those instance renderers, not the plants, meshes, colliders, LODs or shadow reception. This scan adds overhead to all variants; seeding does not guarantee identical procedural scenes.

| Phase D profile suffix | Shadow distance | GPU occlusion | Small grass/flower shadow casting |
| --- | ---: | --- | --- |
| `Baseline` | 160 | Disabled | Original |
| `Shadow120` | 120 | Disabled | Original |
| `OcclusionOn` | 160 | Enabled | Original |
| `GrassFlowerShadowOff` | 160 | Disabled | Disabled by runtime scanner |
| `Combined` | 120 | Enabled | Disabled by runtime scanner |

- Validated that **all five URP assets and profiles import** and report the expected shadow/occlusion fields, GPU Resident Drawer mode 1, render scale 0.75 and diagnostic scripting defines. Unity Play mode verified each variant selects its intended asset. `Baseline` scanner matched 228 MeshRenderers and changed zero; `GrassFlowerShadowOff` matched 228 and disabled shadow casting on 132; `Combined` matched 228 and had zero remaining grass/flower shadow casters. The other variants loaded their respective shadow/occlusion values correctly. No new Unity console errors during these checks, and the scene reported `isDirty=False`.
- The project uses URP's `RenderGraphSettings.m_EnableRenderCompatibilityMode: 0`; the old `m_EnableRenderGraph: 0` serialized value is **deprecated** and not the authoritative compatibility setting. GPU occlusion is still only a candidate: enabling it is not a demonstrated performance win.

### Android runtime evidence (60 Hz emulator)

- An **agent-initiated, user-authorized** IL2CPP/Vulkan Development build `Builds/phase-d-combined.apk` was generated by Unity's build pipeline. Build-time package was 106,507,559 bytes, SHA256 `A59CD1DEF4DFD9E80485A29325A143FEDC42C17D27E01944C67D4F2A895A2BC1`. `aapt` reported package `com.omg.funguy`; `boot.config` still enabled the development player connection/profiler capability, although external FPS windows had profiler *recording* OFF. It installed with `adb install -r`, preserving app data.
- Initial launch stayed black before the diagnostic startup log. A **full emulator restart without clearing data** resolved it; subsequent logcat verified Vulkan renderer on host RTX 3060, `[FunGuy PhaseD] Selected FunGuy_PhaseD_Combined; seed=20261106; small-foliage-shadow-off=True; shadowDistance=120`, plus a successful foliage scan matching 228 renderers/132 casting changes. Gameplay screenshot showed the forest/mushrooms/foreground and gameplay UI rendering; there was no obvious missing foliage, but a pixel-matched visual regression test was *not* performed.
- Combined, external SurfaceFlinger presentation samples: idle **59.169 FPS**, median **16.7325 ms**, p95 **18.6874 ms**, worst **84.2616 ms** over 12.016 s; post-input/mixed **59.430 FPS**, median **16.6614 ms**, p95 **18.7225 ms**, worst **69.3107 ms** over 10.399 s. Both samples reported zero potential history coverage gaps.
- Unity's bridge stopped responding after the build, preventing a matching new `PhaseD Baseline` APK build at that time. An *older, previously validated* Vulkan GRD-ON diagnostic APK (without the Phase D scanner) was installed in the same emulator session instead: first idle external FPS **57.566**, median **16.6387 ms**, p95 **19.1754 ms**, worst **216.5233 ms** over 12.004 s; subsequent warm sample **59.784 FPS**, median **16.637 ms**, p95 **19.0284 ms**, worst **34.6173 ms** over 12.729 s. This is an **imperfect comparison** due to different compiled code, warmup and procedural/camera phases. Do not claim combined FPS improved or regressed.
- Screenshots initially caught transient in-game FPS overlay values of **26 FPS** (combined) and **23 FPS** (older baseline), but the overlay is a rolling 0.5s average. Later **three idle and two post-input baseline screenshots** displayed **60 FPS / 16.7 ms**. The transient overlays are *not* stable baseline results. Externally presented FPS is also not a reliable substitute for full engine frame-time distribution on a real phone.
- The active diagnostic PNGs and FPS JSON/poll traces are under `C:/Users/USER/Documents/Codex/2026-11-06/do-you-remember-when-we-profiled/work/funguy-profiling/phase-d-visual-ab/runtime/`. Script, variant manifest and backups of the original URP/profile plus metadata are under that work folder. Screenshots are **not precisely camera/position matched**, so visual equivalence has not been proved.

### Current checkpoint, caveats, next steps

- Latest filesystem check: `Builds/phase-d-combined.apk` is **not currently present** (it had been built and run earlier). A `phase-d-combined_BackUpThisFolder_ButDontShipItWithYourGame` directory was present. Preserve existing runtime evidence and recheck build outputs before any new benchmark rather than assuming the APK is available.
- Latest tool state check: emulator not running; AVD 4 GB / original 90 Hz configuration restored and byte-matched to saved `runtime/config-original-90hz.ini`; no user-data wipe. Unity window again displayed the `MushroomRunnerGameplay` scene. The Unity bridge had previously stopped responding, so recheck live connectivity and compiler state before further builds. Source gameplay scene SHA256 `6C1470A2CBB6991CFD5F320D5F3D0E2AAC48B2A11A87593E22CF57AEF78A71E7` and original Mobile URP SHA256 beginning `C1C67460F7D9960EF0F7D1D6AB7137` were verified unchanged by this work. A small diagnostic-only source comment was modified after the combined build; do not assume the current on-disk source exactly corresponds to that old binary.
- **Next:** restore/recheck Unity connection; run *matched* PhaseD Baseline vs Shadow120 vs OcclusionOn vs SmallShadowOff vs Combined development APKs using multiple comparable gameplay camera/score phases, verified Vulkan and per-variant startup logs, stable per-engine frame times, optional unobtrusive counters, and visual before/after screenshots. Capture real Android-device performance before committing a win. Treat GPU occlusion as conditional: it may add processing overhead where little geometry is hidden.
- **Do not ship** these ten temporary diagnostic profiles/Resources assets across Phase C and D, scripts, or the old failed `vulkan-grd-on-probe.apk`. The production URP render scale, shader/foliage prefabs and main gameplay scene were left unchanged; any final choice to shorten shadows or remove decorative shadow casters awaits a valid benchmark and visual review.
