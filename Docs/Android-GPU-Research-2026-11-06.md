# FunGuy mobile GPU research and event priorities

6 November 2026. Research requested after the real Redmi capture. No game settings, source, phone state or APK were changed by this research. Recommendations below are test candidates, not measured fixes.

## What the 30 ms wait means

`Gfx.WaitForPresentOnGfxThread` marks time when the CPU main thread is ready to continue but the render/presentation side has not finished. Unity says it can include GPU pressure or synchronization with the display. It is not a30ms C# calculation that can simply be moved onto another CPU thread, and removing a wait does not remove its underlying dependency. Check the simultaneous render thread and independent workload experiments. [Unity profiler marker reference](https://docs.unity3d.com/6000.0/Documentation/Manual/profiler-markers.html)

Our phone evidence:

- Exact installed PhaseD Combined APK verified by SHA256; real Mali-G68 MC4 using Vulkan1.1.
-120 completed phone frames: mean45.48ms; main rendering/present wait30.13ms; render-thread Vulkan Present33.02ms. These overlap and must not be added.
- Native1080x2400 -> half dimensions540x1200 -> native, recording OFF:22.53 ->49.51 ->22.09FPS. Same session and APK; fixed random/camera equivalence was not fully established.
- Physics approximately2.15ms/frame, tiny representative GC allocation28bytes, awake main CPU thread approximately25–26% of one core. No direct GPU timing/counter breakdown was available.

Inference: a significant resolution-sensitive rendering limit is strongly supported. The wait marker by itself would not prove that. We cannot say every30ms is shader execution, or name the dominant GPU pass yet.

## Which high-end-game techniques fit this evidence?

| Technique | What it addresses | Fit for FunGuy now |
| --- | --- | --- |
| CPU multithreading / Jobs | Parallel CPU calculations and graphics command preparation | Lower priority for this measured idle bottleneck; the CPU is waiting and rendering is already threaded |
| Preloading / pooling / shader prewarming | Asset creation, loading and first-use stalls | Useful for separate spikes; no evidence it explains sustained idle22–25FPS |
| Reduced overdraw and cheaper shaders | Repeated/expensive pixel shading | Strong candidate for this dense forest |
| Cheaper post-processing | Screen-wide effects and intermediate-buffer work | Concrete new lead: Bloom is present in the actual phone capture |
| Render scale plus upscaling | Fewer scene pixels while presenting at full display resolution | Strongest directly corroborated direction; quality and overhead still require phone validation |
| LODs / impostors / culling | Unnecessary visible/hidden geometry and distant detail | Useful content-budget tools; avoid duplicate cost from ineffective culling |
| Frame pacing | Refresh matching, queueing and timing consistency | Worth an isolated test; not a replacement for reducing an overloaded frame |
| Async compute | Overlap compatible GPU workloads when hardware has spare capacity | Advanced/content-specific, not a generic “use spare CPU cores” fix |

### Multithreading is already active

Phone startup logged `kGfxThreadingModeThreaded`, and the actual capture contains separate main/render/worker threads. The selected Combined profile stores `m_MTRendering:1`. Do not claim Graphics Jobs specifically are enabled just because multithreaded rendering is enabled; those are distinct modes.

Unity Graphics Jobs splits rendering CPU work across cores. It can help a CPU command-generation bottleneck, but does not make fragment shaders cheaper. Given this capture and resolution test, rewriting gameplay into Jobs is a poor first event-deadline experiment. [Unity Graphics Jobs](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/PlayerSettings-graphicsJobs.html)

### Preloading is about stalls, not avoiding every-frame drawing

The project's BlockSpawner creates environment prefabs during play. Pooling/preloading can reduce creation spikes, but loaded trees still need to be shaded whenever visible. Shader prewarming targets first-use preparation. Unity specifically notes that Vulkan may still need driver preparation when vertex layout/render-target state differs from the warmup setup. Do not promise that warming every shader eliminates all hitches or raises steady25FPS. [Unity shader prewarming](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/ShaderVariantCollection.WarmUp.html)

### The forest needs a pixel-work budget

Arm identifies excess fragments, complex shaders and memory stalls as different causes of heavy fragment work. Overdraw means multiple surfaces shade the same screen pixels; a dense forest can be expensive despite a modest triangle count. The relevant strategies are fewer overlapping layers, cheaper distant shading, correct depth/culling behavior and reduced texture/shader work—not simply loading the same content earlier. [Arm fragment-analysis guide](https://developer.arm.com/community/arm-community-blogs/b/mobile-graphics-and-gaming-blog/posts/mali-gpu-tools-a-case-study-part-2-frame-analysis-with-mali-graphics-debugger), [Arm overdraw analysis](https://developer.arm.com/community/arm-community-blogs/b/mobile-graphics-and-gaming-blog/posts/accelerating-mali-gpu-analysis-using-arm-mobile-studio)

Project-specific candidates include dense grass/vegetation, wind calculations, toon-outline extra passes and shadows. Their individual GPU costs are NOT measured. Do not convert cutout foliage to alpha blending blindly; that can change appearance and overdraw. Preserve the player, foreground mushroom silhouettes and key foliage while testing cheaper distant decoration.

### New concrete lead: Bloom

The actual phone render-thread trace includes Bloom, RG_BloomDownsample and RG_BloomUpsample. The0.83ms Bloom marker is CPU-side recording/command time, NOT its GPU cost. HDR being OFF does not establish Bloom is absent.

Read-only Editor inspection found the gameplay global Volume referencing `Assets/Toon Fantasy Nature/Post Processing/Used profiles/Mobile_PP_Profile.asset`, weight0.436. Its Bloom is active, intensity0.85, High Quality Filtering OFF, Downscale Half, MaxIterations6, dirtIntensity0. These are source asset values, not proof that blending produces those exact final runtime values.

Unity recommends High Quality Filtering OFF for mobile Bloom; ours already is OFF. Its Bloom reference identifies Quarter downscale and fewer iterations as performance options. Test a copied profile with Quarter and a lower iteration cap (for example4), plus a Bloom-OFF diagnostic to determine whether it matters. Retaining the glow at lower cost may preserve the look better than disabling all post-processing. No FPS gain is established yet. [Unity Bloom controls](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/post-processing-bloom.html), [Mobile post-processing guidance](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/integration-with-post-processing.html)

### Use scene render scale, not permanent phone-resolution overrides

URP render scale reduces the3D target resolution; Unity documents UI rendering remaining at device-native resolution. Upscaling filters include Bilinear and FSR1.0; FSR has compatibility requirements and its own GPU cost. This is a practical way to reduce3D pixel work while keeping UI sharper than the whole-display override used in diagnosis. FSR does not guarantee a net performance win. [Unity URP quality/upscaling reference](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/universalrp-asset.html)

Current source scale is0.75. A candidate0.60 uses `(0.60/0.75)^2 = 0.64`, or36% fewer3D pixels than the current scale. That is arithmetic, not a36% frame-time or FPS promise. Start with simple filtering and compare an upscaler only if its image/performance tradeoff warrants it. The whole-display half-dimensions phone test effectively reduced scene dimensions much further; it is not proof0.60 reaches30 or60FPS.

### Frame pacing is worth checking separately

Optimized Frame Pacing was OFF in the existing diagnostic profiles. Android documents how queue back-pressure, refresh mismatch and buffer stuffing can cause waits, uneven delivery and latency. Its frame-pacing library coordinates presentation and handles refresh changes. Test Unity's built-in option on the real phone, keeping other settings fixed. It may improve consistency/refresh selection; it does not guarantee an overloaded45ms render fits a16.7ms budget. [Android Frame Pacing](https://developer.android.com/games/sdk/frame-pacing)

Do not treat larger frame queues as free performance: they can add input latency while hiding where the wait occurs. Likewise, changing the target to30FPS is not sufficient when measured frames currently take45ms; rendering cost must fall first.

### Async compute is real, but not the immediate fix

Arm demonstrates overlapping suitable GPU work across queues on Mali, but stresses workload and hardware dependence. Its particular Mali-G77 sample gained about5%; that is not a prediction for our Mali-G68. Compute/graphics share shader resources, and dependencies can serialize work. Reworking URP/Vulkan queue scheduling before tomorrow would be a much larger experiment than reducing the verified expensive workload. [Arm async-compute sample](https://developer.arm.com/community/arm-community-blogs/b/mobile-graphics-and-gaming-blog/posts/using-asynchronous-compute-on-arm-mali-gpus)

## Priority for the event deadline

1. Keep the known-working Vulkan/GRD/SRPBatcher/Forward+ baseline. Preserve original materials, profiles and the last working APK.
2. First isolated image-preserving candidate: cheaper Bloom (Quarter/lower iteration cap), comparing with Bloom OFF to measure its contribution. Keep color grading and unrelated effects unchanged.
3. If needed, test a separate mobile event quality profile at render scale0.60, native UI, simple upscale. Compare visuals and moving gameplay; don't apply the phone's temporary display override permanently.
4. Separately test Optimized Frame Pacing and a30FPS event target once rendering can meet33.3ms. Do not change pacing, scale, shaders and occlusion all at once when attributing a result.
5. If native sharpness must be retained, prioritize foliage/outline/shadow pass isolation and GPU occlusion OFF/ON on the actual phone. This needs matched variants; previous60FPS emulator tests hid headroom and did not prove wins.

Measured45.48ms/frame requires about27% lower frame duration to reach33.33ms/30FPS, and about63% lower duration to reach16.67ms/60FPS. CPU wait time is not directly subtractable from that budget. A stable30FPS event build is a smaller target than60, but neither has been validated by these proposed settings.

## Acceptance and remaining uncertainty

- Use actual event devices, including the real tablet if available under the owner's scope. The Redmi result does not establish the tablet result; emulator RTX3060 is not representative.
- Fixed camera/scene/score phase, native phone display, same battery/thermal conditions, recording OFF, several steady samples; inspect sustained movement, reset and spawning separately.
- Verify median/p95 frame intervals, image quality and actual target delivery. A brief screenshot reading60 is insufficient.
- Retain only a change with measured benefit and acceptable visuals. Keep diagnostic resources/scripts out of the event production APK unless deliberately needed.
- No new shader, quality, phone setting or build change occurred in this research. Owner's current build authorization remains in force. Exact GPU pass timings and final event-device performance remain unmeasured.
