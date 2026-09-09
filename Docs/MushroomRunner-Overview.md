# Mushroom Runner overview

The player owns movement tuning, the motor owns physics, and the existing momentum and distance systems own scoring.

## Handling: forward carry and lateral grip

Movement is designed around correcting a landing between mushrooms. Camera-relative left/right input requests a bounded sideways speed; it no longer rotates all forward velocity into a sideways heading. Forward carry remains a separate component. Releasing the stick or key requests zero sideways speed, so grip catches the drift. Countersteering changes the requested sideways speed immediately. Forward input adds propulsion only below its independent forward limit.

BalancedMomentum starts with:

| Setting | Value | Effect |
| --- | --- | --- |
| Sideways Speed | 12 units/second | Full-input lateral correction, independent of gear |
| Steering Response | 0.1 seconds | Close 63% of the lateral velocity gap in this time; applies on release too |
| Air Resistance | 0.8 units/second squared | Gentle horizontal speed loss that is never silently restored |
| Air Acceleration | 28.5 units/second squared | Forward propulsion, with the existing control-window modifiers |
| Brake Response | 0.25 seconds | Full backward input removes about 63% of horizontal speed in this time |
| Dive Pull | 40 units/second squared | Extra downward acceleration while pulling back |

At full lateral speed, release sheds about 95% of sideways motion in 0.3 seconds. Its remaining coasting distance is approximately 1.2 world units before other resistance, independent of forward speed. These are starting design values, not a claim that the final feel matches another game.

Backward input brakes and dives without steering backward. Backward diagonals still allow lateral aiming. Releasing stops added dive acceleration immediately. The motor remembers the downward velocity supplied by diving and excludes it from the next bounce's impact recovery. Bounce, air jump, reset, and disable clear that history. Buffered air jumps retain their upward launch priority.

The former retained-speed floor has been removed. It could restore speed immediately after drag, cancelling air resistance and carrying a correction past its target. Bounces and input now explicitly add speed; braking, grip, drag, and overspeed slowdown explicitly remove it.

## Speed gears and bounces

BalancedMomentum uses x1 = 15, x2 = 25, x3 = 40, and x4 = 60 world units/second. The list is expandable, fractional multipliers use the completed integer gear, above-range values use the last entry, and missing scoring uses x1. An empty list uses 15/25/40/60.

Gears change horizontal speed capacity rather than granting velocity. Temporary bounce overspeed remains possible and uses the existing proportional slowdown. Upshifts cannot restore speed lost earlier because there is no stored speed floor.

The standard mushroom multiplies horizontal velocity by 1, then adds its existing 1.35 gain plus BalancedMomentum's 3. Special boost/slow profiles, camera settings, input bindings, and scoring rules remain unchanged.

## Designer tuning

Use Speed Gears, Steering, Bounce, and Air Jump for everyday tuning. Advanced contains the forward propulsion speed limit, overspeed slowdown, gravity/flight shaping, temporary propulsion windows, and contact forgiveness. The obsolete Air Turn Speed setting is replaced in existing assets by Sideways Speed and Steering Response. Existing presets use 80% of their former directional propulsion limit as sideways speed, with a 0.1-second response. BalancedMomentum and the matching default profile use 0.8 air resistance; other presets keep their authored drag values. Asset GUIDs and all gear tables are preserved.

## Validation

Run **Tools > Mushroom Runner > Validate Movement and Gears**. The original fixture remains unchanged; forward/coast and no-input launch references run with the original 0.18 drag to isolate unchanged launch/gravity math. The new live resistance is checked separately, including after a bounce. Old diagonal/reversal/braking traces describe superseded behavior and are replaced by lateral response, release distance, countersteering, brake/dive, and reach checks.

Real physics checks cover trigger/collision bounces, collision restoration, downshifts, air jumps, small-target landings at 40/60, and released-input drift while preserving forward carry. They use equivalent keyboard/full-stick input frames, not physical device input or a human assessment.

Playtest the same mushroom sequence with keyboard and touch: tap sideways, release over the target, reverse a late correction, then commit to descent. Judge both landing accuracy and the ability to build speed through consecutive bounces. Camera tuning stays fixed for that comparison.

See [System wiring](MushroomRunner-System-Wiring.md) for lifecycle and prediction details.
