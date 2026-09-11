# MushroomRunner System Wiring

Use [MushroomRunner-Overview.md](MushroomRunner-Overview.md) for the component overview.

## Player and movement

The player prefab is `Assets/_Game/Funguy.MushroomRunner/Prefabs/MushroomRunnerPlayer.prefab`. The current gameplay scene is `Assets/_Game/Funguy.MushroomRunner/Scenes/MushroomRunnerGameplay.unity`.

| Component | References | Responsibility |
| --- | --- | --- |
| `MushroomRunnerPlayer` | `inputHandler`, local `movementMotor`, `tuningProfile`, child `cameraFollowTarget` | Player state, input forwarding, dash charges, and resetting the body. |
| `RunnerMovementMotor` | Local `Rigidbody`; profile supplied by the player; momentum source bound by `GameplayManager` | Gravity, air steering, bouncing, braking, soft speed limits, and dash execution. |
| `RunnerInputSource` | Joystick, dash button, movement camera | Builds camera-relative `MovementInputFrame` values. |
| `GameplayManager` | `defaultMovementTuningProfile`, `momentumSystem`, `runnerMovementMotor`, `distanceScoreManager` | Applies the selected profile, starts scoring and momentum, feeds forward speed into momentum, and handles gameplay reset. |

`MushroomRunnerPlayer.Awake()` first pushes its profile into the motor. `GameplayManager.Start()` applies the selected default through `MushroomRunnerPlayer.SetTuningProfile(...)` and then notifies the HUD. Later profile changes use the same path. Runtime tier changes never write to shared profile assets.

## Scoring and tier speeds

| Component | References | Responsibility |
| --- | --- | --- |
| `MomentumSystem` | Existing speed thresholds, tier thresholds, landing rewards and penalties | Owns Low/x1, Medium/x2, High/x3, Maximum/x4. |
| `DistanceScoreManager` | `playerTransform`, `momentumSystem`, points per unit, four score multipliers | Scores positive forward distance and publishes score/multiplier updates. |
| `HUD_UI` | Score label, multiplier label, four momentum bars, dash indicators | Presents `GameplayEvents` updates. |
| `PlayerSpeedHudPresenter` | Motor, fill image, label and value text | Samples velocity and uses `CurrentMaxSpeed` as the profile speed reference, while retaining the authored display range. |

`GameplayManager` uses `DistanceScoreManager.MomentumSource` when a score manager is assigned, ensuring movement and scoring share one tier source. The motor subscribes to `OnTierChanged`, synchronizes on binding/start/re-enable, and unsubscribes on disable. A missing source selects x1.

Each `MovementTuningProfile` exposes **Max Speed x1**, **x2**, **x3**, and **x4**, in world units per second. Existing assets initialize all four to their previous effective maximum. New assets initialize all four to 18. Values can be edited independently and cannot be negative.

`GetMaxSpeed(MomentumTier)` resolves a profile value; `MaxSpeed` remains an x1 accessor. The motor's `CurrentMaxSpeed` drives overspeed drag, retained bounce speed, and speed reporting. Steering targets the smaller of `MaxControllableSpeed` and the active tier limit. A tier increase does not add velocity; a decrease lets overspeed drag remove excess horizontal speed. Vertical bounce and dash velocity are preserved.

## World, camera, and reset

| Component | References | Responsibility |
| --- | --- | --- |
| `RunnerCourseStreamer` | Player transform, mushroom/decoration roots, generation profile, movement profile, start spawn definition | Builds and streams the course. Refreshes tuning from the motor and supplies `CurrentMaxSpeed` to reach predictions. |
| `RunFlowCoordinator` | Player, area streamer, optional legacy reset adapter and spawn point | Resets player, rebuilds world, resets legacy environment, then raises the run lifecycle event. |
| `DeathPlaneResetVolume` | Run coordinator, tracked player/target | Detects failure, invokes the run reset, then raises `GameplayEvents.GameplayReset`. |
| `LegacyEnvironmentResetAdapter` | No authored references | Resets older `BlockSpawner` components through reflection. |
| `RunnerCameraRig` | Player's `CameraFollowTarget`, optional velocity source | Follows the player and adjusts FOV from movement speed. |

`BounceReachRequest.MaximumSpeed` passes the resolved soft limit into the shared steering and overspeed math. Standalone prediction requests default to the profile's x1 value.

`GameplayManager` starts momentum before distance scoring. On gameplay reset it resets momentum and distance score; the resulting tier event updates the movement limit. `DistanceScoreManager` resynchronizes the tracked position when resetting, so teleporting does not add score.

The alternate `MushroomRunnerGameplay 1.unity` scene also has obsolete scoring components removed. It has no authored new scoring manager; standalone movement there defaults to x1.

## Events

| Publisher | Events and consumers |
| --- | --- |
| `MushroomRunnerPlayer` | `MushroomRunnerEvents.PlayerRegistered`, `PlayerStateChanged`, `PlayerBounced`, `PlayerDashed`. |
| `RunFlowCoordinator` | `MushroomRunnerEvents.RunStarted`, `RunReset`, `RunFailed`; the death volume listens to start/reset to re-arm grace timing. |
| `MomentumSystem` | `OnTierChanged` for scoring and movement; `GameplayEvents` momentum/multiplier updates for the HUD. |
| `DistanceScoreManager` | `GameplayEvents.OnScoreChanged` and `OnMultiplierChanged` for the HUD. |
| `RunnerMovementMotor` | Local `Bounced` and `Dashed` events for the player; `GameplayEvents.OnSpeedChanged` for speed presentation. |

The runner uses `GameplayManager`, `MomentumSystem`, and `DistanceScoreManager` alongside the MushroomRunner module. The older `GameManager`, `MushroomSpawner_XAxis`, and `CreatureBouncer_XAxis` loop remains separate.
