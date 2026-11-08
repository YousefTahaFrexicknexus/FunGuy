# FunGuy death spin transition (2026-11-08)

## Purpose
Short original Batman-1966-inspired cartoon death wipe using the correct FunGuy logo.

## Authored in
- Assets/_Game/Scenes/MushroomRunnerGameplay.unity (active build scene)
- Assets/_Game/Scenes/MushroomRunnerGameplay_Infinite.unity (parity)
- GameObject: RunFlowCoordinator
- Component: FunGuyDeathTransition

## How it works
- Death-plane and ground/water deaths enter RunFlowCoordinator.ReportFailure.
- Player motor is disabled, time temporarily freezes, and cream/orange/green
  radial rays spin behind the real FunGuy logo.
- At ~0.42 seconds the screen is fully covered. The regular GameplayReset
  callback rebuilds the world exactly once.
- After ~0.08 seconds covered, the logo shrinks and rays fade in ~0.42 seconds.
- Optional Inspector fields for sound, logo, ray colors, spin rate and durations.
- Updated rotating rays (2026-11-08): 8 broad curled ribbons in saturated
  red-orange, cream, and green. Cream separates orange and green so the palette
  never turns muddy brown. A smooth blend affects only the color boundaries,
  leaving each band center vivid rather than desaturating the entire pattern.
- Both gameplay scenes set Ray Softness = 0.7, Ray Curl = 11, Ray Count = 8,
  and background spin = 220 degrees/sec (reduced from 580 for readability).
  All values remain tuneable in the transition Inspector.
- Original SFX: FunGuy_Death_Spin_Whoosh.wav and FunGuy_Death_Reveal_Pop.wav.
- Uses unscaled time and restores the previous time scale.
- Repeated failure notifications during playback are ignored.
- Manual UI restarts and the castle To be continued ending remain unchanged.

## Preview / visual QA
1. Open FunGuy in Unity 6 and open MushroomRunnerGameplay.
2. Enter Play Mode and select RunFlowCoordinator.
3. In the FunGuyDeathTransition component context menu, select:
   Preview Death Wipe (Play Mode).
4. For actual end-to-end QA, fall below the death plane. Ensure the world
   rebuild is hidden, occurs once, and controls work after reveal.
5. Repeat for environmental deaths and for different aspect ratios.

## QA state
- Scene/script/sprite/sound reference validation was performed.
- Unity connector currently points to Casual-Puzzle-Games, not FunGuy.
- No FunGuy Play Mode visual check, Unity compilation, or player build was performed.
- Other game movement and world-generation settings were not modified.