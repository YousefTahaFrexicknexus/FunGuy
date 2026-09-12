# Mushroom launcher prototype

Drag `Assets/_Game/Funguy.MushroomRunner/Prefabs/MushroomLauncher.prefab` into the runner scene to test it. Existing mushrooms and spawner selections are unchanged.

Select **Triggers > NormalLanding (Launcher)** to edit the launcher. Each **Normal / Bad**, **Good**, and **Perfect** section has an ordered list of speed rules. Add or remove entries with the list controls and drag them into the order you want. Each entry has a mode and a value:

| Rule | Incoming forward speed | Outgoing forward speed |
| --- | --- | --- |
| Add 10 | 50 | 60 |
| Add -10 | 50 | 40 |
| Multiply 0.5 | 50 | 25 |
| Set 30 | Any | 30 |

Rules run from top to bottom: at incoming speed 50, Add 10 then Multiply 2 gives 120, while Multiply 2 then Add 10 gives 110. Set 30 then Add 5 gives 35. Effectiveness applies once before the list; clamping to zero happens only after all rules. An empty list keeps the effective incoming speed.

New components start with Add 0 / Add 5 / Add 10. The existing prototype prefab retains its tuned Multiply 1.1 / Multiply 1.3 / Multiply 1.5 as one-entry lists. Negative final results clamp to zero; there is no minimum launch speed. A stationary player needs an Add or Set rule greater than zero to get moving.

On the active **MovementTuningProfile**, **Mushroom Bounce Effectiveness** scales incoming speed before Add or Multiply (default 1, range 0–2). For example, incoming 50 at effectiveness 0.8 becomes 40, then Add 10 produces 50. Set ignores incoming speed and effectiveness.

**Allow Air Acceleration** retains the existing movement styles. Forward acceleration uses `Move Acceleration * max(0, joystick Y)`; backward braking uses `Air Brake Acceleration * max(0, -joystick Y)`. For example, Move Acceleration 19 and Y +0.1 gives a forward acceleration rate of 1.9 before reaching the existing control-speed limit. Sideways steering retains its existing calculations. Brake values are flat speed-per-second rates, not percentages. Existing gravity, drag, speed limits, and joystick dead zones still apply (input exactly at the dead-zone boundary is accepted).

Rotate the **LaunchDirection** transform: its blue Z arrow specifies the whole launch direction. It starts tilted 45 degrees upward and forward. The launch is scaled so world-Z velocity equals the resulting speed, matching the current HUD. The arrow must keep a normalized world-Z component of at least 0.1; the Inspector warns about missing or invalid settings and invalid configurations do not launch.

At entry into the main trigger, the launcher checks actual overlap with the quality colliders. Perfect takes priority over Good; everything else is Normal/Bad. Multiple colliders on one player still produce only one launch and momentum reward. The player must fully leave the main trigger before it can launch again.

The launch replaces velocity directly. It does not add the old base bounce bonus, upward impulse, impact recovery, flight shaper, or retained-speed floor. Ordinary gravity, braking, drag, extra jumps, and x1–x4 soft limits continue afterward, so an overspeed launch can gradually slow down.

The optional squash component changes only the visuals. All three trigger colliders are outside its animated hierarchy. The new prefab has no bounce-profile dependency or modifier scripts; it is not yet used by procedural generation.
