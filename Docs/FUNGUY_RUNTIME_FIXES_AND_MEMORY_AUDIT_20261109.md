# FunGuy — Windows Fix Validation & Unity Memory Profiler Audit
Date: 2026-11-09 · Workspace: `D:/Work/FunGuy` · Git branch: `Analysis`

## Scope and safeguards
- The actual Unity Editor **recompiled successfully** at 22:12 and 22:27; the source assemblies contain the pooling, tween, and Rigidbody changes.
- The Editor entered `MushroomRunnerGameplay` Play Mode twice for controlled startup captures, then returned to Edit Mode automatically.
- No Windows or Android player build was run. Existing user modifications to scene, graphics, packages, and unrelated source were preserved.
- These are Unity **Editor** snapshots (both Edit Mode and Editor-hosted Play Mode), not standalone Windows-player memory/VRAM profiles. Editor caches and GUI surfaces are included.
- Repeatable player FPS benefits have **not** been measured because the existing Windows executable predates these fixes.

## Implemented and compiled source fixes

1. `Assets/_Game/Scripts/Blocks/BlockSpawner.cs`
   - Adds bounded, prefab-keyed pooling, configurable in Inspector; default max 6 retained blocks total and max 2 of each prefab.
   - Reused blocks are repositioned, rescaled, and parented before activation. Old colliders immediately deactivate during recycling.
   - `ActiveBlockCount` and new `PooledBlockCount` are observable.
   - Play Mode startup verified **6 active environment blocks and 0 pooled**, as expected before any blocks pass their despawn threshold.
   - **Streaming/restart reuse not yet exercised** in the short automated startup runs; do not claim a performance gain until tested while actually travelling past chunks and restarting.

2. `Assets/_Game/Scripts/LaunchPad/LilypadMotion.cs`
   - Replaces invalid infinitely repeating nested tween with standalone looping `rotationTween`.
   - Recomputes movement endpoints on each enable, so reuse at a new Z does not target the prior block's position.
   - Cancels delayed landing reaction and kills movement/rotation/dip tweens on disable/destroy.
   - Nested-loop warnings were **absent in the new Editor Play Mode logs**, although individual lily-pad animation states were not visually QA-checked.

3. `Assets/_Game/Funguy.MushroomRunner/Movement/RunnerMovementMotor.cs`
   - Guards velocity resets and `WakeUp` for kinematic bodies.
   - Follow-up source audit found the old Windows-player log also had repeated kinematic warnings from `FixedUpdate()`, not merely reset.
   - Therefore additional guards now prevent motor ticks, speed-multiplier velocity writes, mushroom launches, force launches and ground bounces while kinematic.
   - **Zero kinematic velocity warnings** during two new Editor Play Mode startups.

4. Destroyed-target `DOShakeScale` warning
   - This was present **once** in the older Windows player log. Source and all current script assemblies contain no direct application call to `DOShakeScale`; the older player's DOTween stack only identifies the internal tween setter, not its owner.
   - It was **not reproduced** in either newly compiled Editor Play Mode startup. No speculative unrelated tween removal was made. Recheck after an approved new player build / death flow test.

## New issue independently observed
`Assets/_Game/Scripts/Global/AudioManager.cs:36` calls `DontDestroyOnLoad(gameObject)` on a **non-root GameObject**. Unity warns each Play session. As-is, it may not persist across scene transitions as intended; inspect the manager's hierarchy and desired lifetime before modifying. This is outside the requested four changes.

## Unity Memory Profiler captures
The Memory Profiler package `com.unity.memoryprofiler@1.1.12` was already installed. The reusable Editor capture helper is:
`Assets/Editor/FunGuyMemoryAuditCapture.cs`.
It writes `.snap` and `.metrics.json` to `MemoryCaptures/`. A bounded two-second request poll enables one-shot captures; no build is needed. Menu: **Tools > FunGuy > Memory Audit > Capture Snapshot Now**.

| Snapshot (2026-11-09 local time) | Runtime state | Tracked native Unity object memory | Native Unity objects | RenderTextures |
|---|---|---:|---:|---:|
| 22:12:32 | Edit, before startup tests | 435.0 MiB | 26,949 | 14 / 84.83 MiB |
| 22:21:06 | Play run 1, ~5 seconds | 492.7 MiB | 34,097 | 59 / 106.23 MiB |
| 22:27:57 | Edit, after run 1 | 466.1 MiB | 27,131 | 14 / 84.83 MiB |
| 22:29:04 | Play run 2, ~5 seconds | 492.7 MiB | 34,261 | 59 / 106.23 MiB |
| 22:31:54 | Edit, after run 2, settled | 488.7 MiB | 27,276 | 59 / 106.23 MiB |

The data are object-native-memory categories, not total process RAM, GPU VRAM or independent native allocator totals.

### What changed on entering Play Mode
Compared to first Edit snapshot:
- +1,883 GameObjects, +1,883 Transforms, +1,190 MeshRenderers, +316 LODGroups and +144 MeshColliders. Many of these are likely associated with streamed world activation and merit CPU/GC profiling.
- +23.23 MiB `Texture2D`, primarily `TFF_Water_Normal_1` (+10.71 MiB), `Tex_Common_Atlas` (+5.36 MiB), and character textures.
- +21.40 MiB `RenderTexture`: `GameView RT`, `SceneView RT`, depth/color attachments, bloom mip chain and a 512×512 shadowmap. These are Editor-hosted views and render targets, not all memory that a standalone player would allocate.
- +8.24 MiB `AudioClip`: `Funky Chill 2 loop`, imported as **Decompress On Load** (`loadType: 0`) and `preloadAudioData: 1`. Switching long BGM to Streaming is an option to test, subject to playback and IO quality.
- GameObject and MeshRenderer counts returned to their prior baseline after both Play runs, indicating runtime scene objects were cleaned up on leaving Play Mode.

### Stability and remaining ambiguity
- **Play run 1 vs Play run 2:** tracked native Unity object memory remained ~492.7 MiB; no substantial cumulative object-memory growth in this limited two-cycle startup test. Some small Editor-only scriptable assets/materials accumulated.
- In Edit Mode, ~30 MiB of textures/music remained resident after run 1. This is consistent with normal asset caching, but not proof of leak-free scene transitions.
- After run 2, ~21.4 MiB more render targets remained in Edit Mode even after a settling interval. Names include `GameView RT`, `SceneView RT`, camera attachments, bloom. This may be Editor window/render-target caching. No evidence that this same retention occurs in a standalone Windows player. Investigate Editor viewport lifecycle if it continues growing with many cycles.
- The Mono managed heap reported ~1.3 GiB reserved in the **Editor** and native object snapshots were ~1.4 GiB each on disk. Those numbers should not be interpreted as the game's shipped memory footprint.
- In current Editor the biggest `Texture2D` allocations include `Atlas_01_MASK` (~42.71 MiB), `Atlas_01_Normal` (~42.71 MiB) and `Atlas_01_Base_Color` (~21.36 MiB), sourced from project TGA atlases. Editor-imported texture storage can be far larger than platform-compressed player textures; do not lower resolution without inspecting the actual Windows build.
- For reference, the older `MemoryCaptures/idle-memory.snap` from Nov 6 contained ~151.5 MiB native Unity object memory, with a 26.37 MiB **ProfilerScreenshotFull** contribution to RenderTextures. It was captured under different conditions and **cannot** be subtracted from the Editor snapshots.

## Reusable files and one-shot requests
- `work/windows-profiling-2026-11-09/analyze_unity_memory_snap.py` — reads raw Unity `.snap` sections and sums native objects.
- `work/windows-profiling-2026-11-09/memory_top_objects.py` — lists largest native objects.
- `work/windows-profiling-2026-11-09/compare_editor_play_memory.py` — compares names/types across any two snapshots.
- `capture-editor-after-fixes.request` — save Edit Mode snapshot at next Editor request poll.
- `capture-play-after-fixes.request` + `enter-play-for-memory-audit.request` — one-shot Play capture and automatic return to Edit Mode if the active scene has no unsaved changes.
- Every one-shot request was consumed and no automatic Play session remains queued.

## Priority next steps
1. **Exercise actual streaming and restart** in Play Mode while observing `PooledBlockCount`, GC Alloc, CPU Timeline, pooled instance IDs and lily-pad motion; the current startup samples show 6 active / 0 pooled only.
2. Investigate `AudioManager` root/persistence warning as a separate correctness fix.
3. For meaningful Windows performance conclusions, profile a **fresh** development Windows player and compare against the old Windows PresentMon capture, especially CPU p99 frame spikes, world instantiation costs and GC. A player build requires explicit user authorization.
4. Use Windows Memory Profiler player snapshots at idle, sustained movement, after death/restart and after scene return to distinguish resource caching from leaks. Editor-hosted snapshots alone cannot measure actual player VRAM.
5. Consider BGM Streaming and texture/RenderTexture tuning only after validating player impact and visual/audio quality.

## Validation limits
- Unity compilation succeeded, `git diff --check` passed, and Editor Play snapshots were obtained.
- This does **not** constitute a passing gameplay regression suite, streaming pool reuse QA, Windows-player regression test or approved new player build.
