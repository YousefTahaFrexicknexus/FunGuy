# Mushroom Runner system wiring

## Player and profile ownership

`MushroomRunnerGameplay.unity` binds the player to BalancedMomentum and its scene `DistanceScoreManager`. `GameplayManager.Start` also binds that manager before beginning the run. The older `MushroomRunnerGameplay 1.unity` has no new score source and therefore uses x1.

| Owner | Responsibility |
| --- | --- |
| MushroomRunnerPlayer | Input forwarding, active/dead state, air-jump charges/cooldown, profile selection, read-only scoring reference |
| RunnerMovementMotor | Rigidbody velocity, bounce/air-jump execution, buffering, dive history, control windows, current gear ceiling |
| RunnerBounceContacts | Collision candidates, consumed-surface tracking, contact retention, temporary collision ignore and restoration |
| BounceMovementMath | Lateral grip, forward propulsion, braking, dive, gravity/flight shaping, launch response and soft speed limit |
| MomentumSystem | Existing speed/landing rewards and penalties; existing four tiers |
| DistanceScoreManager | Existing distance score and tier-based multiplier |
| GameplayManager | Existing scoring lifecycle and profile-change routing |
| RunFlowCoordinator | Player pose reset, course rebuild, run lifecycle events |

The player assigns its tuning profile to the motor during Awake. The motor no longer serializes an independent profile. `GameplayManager.SetActiveTuningProfile` calls the player's `SetTuningProfile`, which applies the profile, restores jump charges, and publishes the existing profile event for the HUD.

The player binds `ResolveSpeedLimit` through `RunnerMovementMotor.SetSpeedLimitProvider`. This reads `DistanceScoreManager.CurrentMultiplier` and calls `MovementTuningProfile.GetMaxSpeed`. It does not subscribe to `GameplayEvents.OnMultiplierChanged`, which has two existing publishers, and it never modifies scoring or momentum.

## Physics order

Each motor physics step resolves one ceiling, restores expired collision ignores, evaluates contact state, applies gravity and airborne dive, consumes a collision bounce, applies lateral grip/braking and eligible forward propulsion, applies drag and the soft ceiling, consumes a buffered air jump, and writes velocity. `CurrentMaxSpeed` reports the resolved ceiling for that physics step.

Trigger mushrooms call `TryBounce`, preserving the immediate velocity-change impulse. Collision bounces retain their candidate/grace behavior and temporary collision ignore. Both paths share launch math and completion bookkeeping while retaining their original event timing. The existing landing detector still reports quality after an accepted trigger bounce; the resulting tier is observed at the next physics step.

`GameplayEvents.OnSpeedChanged` continues to report forward Z speed; its maximum argument is now the current gear. `PlayerSpeedHudPresenter` uses the same ceiling. Camera effects continue to use their authored absolute-speed ranges.

## Course prediction

`RunnerCourseStreamer` resolves the player's motor and tuning profile. Every `BounceReachRequest` includes an explicit `MaxSpeed`, captured from the current motor ceiling. `BounceReachEvaluator` passes it to the same soft-limit math used by live movement. Predictions assume that gear remains constant for the projected hop.

The streamer's old score target/reset responsibilities are removed. It still retains its existing steering-speed-based spacing heuristics. The evaluator remains approximate: surface drag handling and contact timing are not a full replay of the motor. Validate authored and generated courses at every gear, including high-speed arrivals and downshifts.

## Scoring and reset

The new `MomentumSystem`, `DistanceScoreManager`, landing detection, and scoring settings are unchanged. Existing lifecycle entry points continue to begin/reset scoring. Movement does not award points, change momentum thresholds, or introduce another multiplier event. A run reset clears movement state; the score reset determines the next resolved gear.

The retired combo/airtime service, legacy score service/HUD, their snapshot/event payloads, and their prefab/scene components are removed. The entirely commented-out scene bootstrapper and its obsolete editor assembly definition are retired as well. The custom tuning Inspector and validation checks live in the predefined editor assembly alongside the project's Assembly-CSharp runtime.

## Automated checks

Use the Unity menu **Tools → Mushroom Runner → Validate Movement and Gears**, or batch mode with `-executeMethod MushroomRunnerMovementChecks.Run`. The reference fixture is `Assets/Editor/MovementTests/BalancedMomentumBaseline.json`, captured from the saved pre-refactor source and original BalancedMomentum values with 0.02-second steps and a 48 ceiling.

The checks cover formula parity, all gear boundaries, invalid/empty lists, soft overspeed, vertical/directional preservation, effective air resistance after a bounce, disabled/reset states, air-jump consumption, both bounce entry paths, collision restoration, score binding, profile swaps, reachability, and missing script components in both runner scenes. A separate in-game keyboard/touch playtest is still required to assess feel.

For actual physics callbacks, run Unity with `-batchmode -nographics -executeMethod MushroomRunnerPlayChecks.Start` and the project path, without `-quit`. This check enters Play Mode in an empty unsaved scene, tests trigger/collision landings at speed 60, collision restoration, a downshift to 15, a falling air jump, and four small-target controlled landings at 40/60 using equivalent keyboard/full-stick frames, plus released-input drift and forward carry, then exits Unity with a success/failure code.

## Landing control and air resistance

`BounceMovementMath.ApplyAirMovement` coordinates `ApplyLateralControl`, braking, and forward propulsion. Lateral control resolves camera-relative axes from the existing input frame and exponentially approaches the requested sideways speed. Neutral input requests zero sideways speed, so release removes drift without turning that lost speed into forward acceleration. There is no total-velocity rotation controller or retained-speed floor.

The motor applies lateral grip/braking on the collision-bounce step too, while preserving the existing forward-propulsion delay. Dive is applied with gravity before bounce resolution; buffered air jumps still execute last and launch upward. Both bounce paths pass accumulated dive-added downward speed into the shared response calculation, then clear it on success. Release retains that history; air jump, reset, and motor/component disable clear it.

Reach prediction uses the same lateral control, braking, and dive math. The course progresses along world Z; prediction converts lateral target error into bounded stick input in that frame, slowing the correction as it approaches the target. It no longer rotates a virtual reference camera toward each target, which would bypass the lateral speed limit. Brake intent supplies backward input alongside that lateral correction. No automatic target aiming is added to the live player.

`BounceReachResult.DiveAddedDownSpeed` flows through route nodes into `BounceReachRequest.IncomingDiveAddedDownSpeed` and launch estimates, preventing artificial rebound recovery across predicted hops. Prediction still approximates contacts, first-step behavior and drag overrides, and assumes the current gear remains constant.
