# FunGuy real-phone diagnostics — 6 November 2026

Status: first real-phone diagnosis completed at approximately17:36 (Asia/Amman). The owner explicitly connected the phone and authorized real-phone testing, superseding the earlier emulator-only phase. No APK replacement, game source change or new build was performed. Major limiting category identified; exact GPU pass/shader attribution remains open.

## Owner clarification and main conclusion

The owner corrected the initial report: **Redmi is about25FPS with no10FPS drop. The10FPS was on the real Blackview tablet; the PC emulator reached60.** Do not document a reproduced10FPS phone regression. The actual tablet is not connected/tested here. An RTX3060-hosted emulator does not reproduce either mobile GPU.

The same installed Combined APK is running Vulkan correctly on the Redmi's Mali-G68 MC4. Same-session native -> half display dimensions -> native measurements were **22.53 ->49.51 ->22.09FPS**, with recording OFF. A valid phone Unity Profiler capture additionally shows the main thread waiting about30.13ms for rendering/presentation during a45.48ms frame. These independent results strongly support **GPU/pixel-rendering pressure at native phone resolution**, rather than game CPU scripts, a silent GLES fallback, or recording overhead as the dominant cause of this idle25FPS-class result.

This is not direct GPU pass timing: GPU frame-time queries returned0/unavailable. `Gfx.WaitForPresentOnGfxThread` alone would not prove GPU limitation; the reversible resolution response corroborates it. Exact contributions from foliage fragment shading/overdraw, toon outlines, shadows, GPU occlusion and driver behavior have not been isolated.

## Accepted measurements

All measurements use the same installed APK, phone portrait aspect ratio and existing Combined settings. Unity recording OFF except the explicitly marked sample. SurfaceFlinger helper reported zero potential coverage gaps; this is presented-frame timing, not a direct GPU timestamp.

| Sample | FPS | Frames / seconds | Median / p95 interval | Interpretation |
| --- | ---: | --- | --- | --- |
| native awake03 |22.53|338/14.96|41.33/49.59ms| confirmed foreground score0 idle |
| native input-attempt04 |22.56|188/8.29|41.33/49.60ms| input attempt; screenshot score0, NOT proven moving gameplay |
| temporary540x1200,05 |49.51|618/12.46|16.53/24.80ms| quarter display pixels; screenshot confirms game rendered smaller |
| restored1080x2400,06 |22.09|275/12.41|41.33/49.60ms| original portrait resolution, same-session confirmation |
| phone recordingON07 |22.07|92/4.12|41.35/49.60ms| Unity capture valid; much less intrusive here than the translated emulator |
| disconnected/recordingOFF08 |22.00|237/10.73|41.34/49.60ms| confirms native result after profiler disconnected |

Do not claim a universal2.2x speedup: world state was not fingerprinted for all boundaries, Android resolution changes affect viewport/UI as well as render targets, and thermal/frequency states can vary. This is a strong directional pixel-load experiment, not a final quality-setting recommendation. Original resolution was restored; do not ship540x1200 as the proposed fix merely from this test.

## Actual phone Unity Profiler evidence

- Connected target **AndroidPlayer Xiaomi22101316G@192.168.100.13**, not Editor or emulator. A real Profiler window was opened; advancing valid Android frames verified; Deep Profiling OFF. Recording stopped through window and driver state afterward.
- Saved `phone-native-cpu-gpu.raw` (83,639,732bytes), plus `native-profile-summary.json`;120 completed frames558–677 analyzed. Null/unnamed samples were skipped by the parser. No game/package source was modified.
- Main-thread mean frame45.48ms; `Gfx.WaitForPresentOnGfxThread`30.13ms; FinishFrameRendering38.03ms; URP CPU render loop7.57ms. These inclusive markers overlap; do not sum them.
- Render-thread `Gfx.PresentFrame`35.11ms, `GfxDeviceVK.Present`33.02ms; command wait4.93ms. GPU timestamp mean0 means unavailable, not zero GPU work.
- Main-thread physics fixed-update2.15ms/frame including about2.28fixed steps/frame; nested physics simulation1.77ms. This is not the earlier emulator's inflated9–10fixed-step catch-up condition and is not dominant in this idle capture.
- Representative sampled frame counters:240draw calls,118SetPass,92,030triangles,93,810vertices,30shadow casters,28bytesGC allocated. These are this phone's idle camera sample, not a paired count comparison with earlier landscape captures.
- Unrecorded awake top samples: UnityMain25–26% of one CPU core, UnityGfxDeviceW17–20%, Mali driver backend14–15%, substantial aggregate CPU idle. This corroborates no single main/render CPU thread being fully busy; it is not direct GPU utilization.
- Direct reads of /proc/net/tcp,/proc/net/unix and the MediaTek GED sysfs GPU interface were denied even using app identity where appropriate. No rooting, permission weakening or security changes attempted. Unity's advertised player connection worked without those reads.

## Temperature, cleanup and next isolation tests

- Android thermal status remained0 at initial/restored/final checks. Live battery29.7 ->33.4 ->34.7C; final HAL CPU/GPU53.07C. Cached older temperatures must not be confused with current HAL data. No reported thermal-severity event establishes heat as the primary cause here; frequency/power limits were not independently measured.
- Original display override was absent; restored `wm size reset` and verified1080x2400 with no override. Original stay_on_while_plugged_in was0; restored and verified0. Density, refresh settings, battery/game mode, phone security, installed APK and save data were not changed.
- Profiler recording verified OFF and connection returned to Editor. Existing phone app was left installed/running normally; no force-stop or APK replacement performed in this phone phase.
- Next diagnostic priority: native-resolution matched phone baseline versus isolated PhaseD variants, particularly GPU occlusion OFF/ON and foliage/outline/shadow pixel cost. Compare actual same-camera/scene phase, device temperature, median/p95 frame times and visuals. A Combined-vs-older-baseline difference alone cannot identify which of its three changes helped/hurt.
- A lower render scale is a reversible fallback tradeoff, but the preferred first investigation is reducing expensive shader/overdraw/pass work while preserving looks. Retain tested Vulkan/GRD compatibility and do not treat the emulator's60FPS as a phone acceptance test.

## Verified phone and build

- USB device Xiaomi/Redmi22101316G, codename ruby, Android14/API34, SoC MT6877V/TTZA. Device-reported GPU **ARM Mali-G68 MC4**.
- Installed `com.omg.funguy` base.apk SHA256 **A59CD1DEF4DFD9E80485A29325A143FEDC42C17D27E01944C67D4F2A895A2BC1**, byte-identical to `D:/Work/FunGuy/Builds/phase-d-combined.apk` (106,507,559bytes). This is not a similarly named older build.
- Fresh process log confirms Unity6000.3.9f1, Development, ARM64 IL2CPP and **Vulkan1.1** on the actual Mali GPU.
- Fresh PhaseD log confirms Combined, seed20261106, shadowDistance120, small-foliage-shadow-off=True. Scanner matched228 renderers and disabled132 foliage casters. Occlusion remains the configured Combined variant.
- Physical phone display1080x2400, portrait; initial active refresh120Hz. Emulator tests were landscape on a host RTX3060 and do not establish this phone's performance.
- Unity Editor recording was already OFF, connected target Editor, zero ProfilerWindows. No remote recording was responsible for the initial observed slowdown.
- Initial battery64%, chargingUSB, battery29.7C; Android thermal status0. Cached thermal readings differed from live HAL; do not mistake old cached values for current temperatures or rule out later throttling.

## Initial measurements and interruption

- Startup screenshot showed26FPS, score0. First external sample `phone-native-idle-off-01` measured24.89presentedFPS/40.17ms over14.94sec (373frames), zero helper coverage gaps.
- The phone subsequently entered its clock screensaver and locked; the following top snapshot showed sleeping game threads and is NOT representative game CPU load. Do not use it as evidence the app is CPU-idle during normal rendering.
- First awake retry had no game SurfaceView because lock screen was still showing. No valid sample was produced for that retry.
- Asked owner to unlock; owner confirmed FunGuy open. Temporarily enabled staying awake while USB-connected to prevent this recurring. Original setting saved under `work/funguy-profiling/phone-phase-d/original-stay-awake.txt`; MUST restore at cleanup. No lock/security setting changed or lock bypass attempted.
- This interruption was resolved by the owner. The subsequent accepted foreground measurements and capture are reported above; the corrected phone complaint is approximately25FPS, not10FPS.

## Evidence

`C:/Users/USER/Documents/Codex/2026-11-06/do-you-remember-when-we-profiled/work/funguy-profiling/phone-phase-d/`: installed APK path/hash evidence, startup log, screenshots, thermal/display/GPU/memory snapshots, original stay-awake setting, presentation collector and per-sample JSON/raw/poll data. Lock/screensaver screenshots are interruption evidence, not game screenshots.
