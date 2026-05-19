# MushroomRunner System Wiring

Use [MushroomRunner-Overview.md](/d:/Work/FunGuy/Docs/MushroomRunner-Overview.md) for the fast mental model.

Use this document when you need the exact scene references, prefab wiring, runtime sequence, and event map for the `Funguy.MushroomRunner` module.

## Scope

This doc covers the production runner module under [Assets/_Game/Funguy.MushroomRunner](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner).

It does not describe the older gameplay loop under [Assets/_Game/Scripts/Gameplay](/d:/Work/FunGuy/Assets/_Game/Scripts/Gameplay). In particular, [GameManager.cs](/d:/Work/FunGuy/Assets/_Game/Scripts/Gameplay/GameManager.cs) is not part of MushroomRunner flow and is used by older `MushroomSpawner_XAxis` / `CreatureBouncer_XAxis` gameplay code.

## Scene Layout

The gameplay scene is [MushroomRunnerGameplay.unity](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Scenes/MushroomRunnerGameplay.unity).

Important hierarchy:

| Group | Key objects |
| --- | --- |
| `_Systems` | `RunnerInputSource`, `RunScoreService`, `RunnerCourseStreamer`, `LegacyEnvironmentResetAdapter`, `RunFlowCoordinator` |
| `_Runtime` | `GeneratedMushrooms`, `GeneratedEnvironment`, `MushroomRunnerPlayer` prefab instance, `DeathPlaneResetVolume` |
| `_Presentation` | `Main Camera` |
| `HUD` | `JoystickArea`, `DashButton`, `ScoreText`, `MomentumText`, `SpeedMeter` |

The split is intentional:

- `_Systems` owns orchestration and services.
- `_Runtime` owns live actors and generated content.
- `_Presentation` owns the camera and scene view.
- `HUD` owns authored UI bindings.

## Player Prefab Wiring

The player prefab is [MushroomRunnerPlayer.prefab](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Prefabs/MushroomRunnerPlayer.prefab).

| Prefab object | Script/component | Assigned references | Used by | Purpose |
| --- | --- | --- | --- | --- |
| `MushroomRunnerPlayer` root | `MushroomRunnerPlayer` | `inputHandler ->` scene `RunnerInputSource`<br>`movementMotor ->` local `RunnerMovementMotor`<br>`tuningProfile ->` movement tuning asset<br>`cameraFollowTarget ->` child `CameraFollowTarget` | `RunFlowCoordinator`, `RunScoreService`, `RunMultiplierService`, `DeathPlaneResetVolume`, event payloads | Main player brain; owns state, dash lifecycle, input forwarding, and reset behavior. |
| `MushroomRunnerPlayer` root | `RunnerMovementMotor` | local `Rigidbody`<br>tuning is pushed from `MushroomRunnerPlayer` during `Awake()` | `MushroomRunnerPlayer`, `RunMultiplierService`, `PlayerSpeedHudPresenter`, `RunnerCameraRig` | Executes physics, bounce movement, dash impulse, drag, and grounded state. |
| `MushroomRunnerPlayer` root | `RunMultiplierService` | `player ->` local `MushroomRunnerPlayer`<br>`movementMotor ->` local `RunnerMovementMotor` | `RunScoreService` | Tracks combo state, multiplier state, and airtime qualification. |
| `CameraFollowTarget` child | `Transform` | none | `RunnerCameraRig` | Follow anchor for the camera. |

### Runtime Authority Note

The scene can show values on both `MushroomRunnerPlayer.tuningProfile` and `RunnerMovementMotor.tuningProfile`. At runtime, `MushroomRunnerPlayer.Awake()` calls `movementMotor.SetTuningProfile(tuningProfile)`, so the player's `tuningProfile` is the one that wins.

## Scene Wiring Inventory

These are the authored references in the gameplay scene.

| Scene object | Script/component | Assigned references | Used by | Purpose |
| --- | --- | --- | --- | --- |
| `RunnerInputSource` | `RunnerInputSource` | `movementJoystick ->` `FloatingJoystick` on `JoystickArea`<br>`dashButton ->` `TouchDashButton` on `DashButton`<br>`movementCamera ->` `Camera` on `Main Camera` | `MushroomRunnerPlayer` | Converts UI and keyboard input into `MovementInputFrame`. |
| `RunScoreService` | `RunScoreService` | `trackedPlayer ->` player `MushroomRunnerPlayer`<br>`trackedTarget ->` player root `Transform`<br>`multiplierService ->` player `RunMultiplierService` | `RunnerCourseStreamer`, `RunScoreHud`, global score event consumers | Converts forward progress and airtime into `RunScoreSnapshot`. |
| `RunnerCourseStreamer` | `RunnerCourseStreamer` | `player ->` player root `Transform`<br>`mushroomRoot ->` `GeneratedMushrooms`<br>`decorationRoot ->` `GeneratedEnvironment`<br>`generationProfile ->` bounce area generation asset<br>`tuningProfile ->` world tuning asset<br>`scoreTracker ->` scene `RunScoreService`<br>`startSpawnDefinition ->` start route spawn asset | `RunFlowCoordinator` | Builds the route, generates ahead, and recycles behind the player. |
| `LegacyEnvironmentResetAdapter` | `LegacyEnvironmentResetAdapter` | no serialized refs | `RunFlowCoordinator` | Resets legacy `BlockSpawner` systems through reflection. |
| `RunFlowCoordinator` | `RunFlowCoordinator` | `player ->` player `MushroomRunnerPlayer`<br>`areaStreamer ->` scene `RunnerCourseStreamer`<br>`legacyEnvironmentResetAdapter ->` scene `LegacyEnvironmentResetAdapter`<br>`spawnPoint ->` none in current scene<br>`captureInitialPlayerPose ->` enabled<br>`initializeRunOnStart ->` enabled | `DeathPlaneResetVolume` | Owns run start and reset order. |
| `DeathPlaneResetVolume` | `DeathPlaneResetVolume` | `resetCoordinator ->` scene `RunFlowCoordinator`<br>`trackedPlayer ->` player `MushroomRunnerPlayer`<br>`trackedTarget ->` player root `Transform`<br>`useAutomaticDeathHeight ->` enabled<br>`postResetGraceTime ->` `0.25` | Failure path | Detects death and reports it to run flow. |
| `Main Camera` | `RunnerCameraRig` | `target ->` player `CameraFollowTarget`<br>`velocitySource ->` none, so it resolves from target hierarchy | View only | Follows the player and adjusts FOV from speed. |
| `ScoreText` | `RunScoreHud` | `scoreTracker ->` scene `RunScoreService`<br>`scoreText ->` local score text<br>`statusText ->` `MomentumText` | HUD | Displays score and momentum/airtime state from the current snapshot. |
| `SpeedMeter` | `PlayerSpeedHudPresenter` | `movementMotor ->` player `RunnerMovementMotor`<br>`fillImage ->` authored fill image<br>`labelText ->` authored label text<br>`valueText ->` authored value text | HUD | Displays live player speed from the movement motor. |

## Runtime Sequence

This is the exact order the core loop follows.

1. Scene load
   The scene already contains one player instance, one input source, one score service, one course streamer, one run coordinator, one death volume, one camera rig, and authored HUD objects. Nothing in the production path creates those systems at runtime.
2. Player init
   `MushroomRunnerPlayer.Awake()` validates refs, pushes its `tuningProfile` into `RunnerMovementMotor`, and wires dash resources into the motor. `OnEnable()` subscribes to `Bounced` and `Dashed`. `Start()` raises `MushroomRunnerEvents.PlayerRegistered`.
3. Run start
   `RunFlowCoordinator.Start()` calls `StartRun()` because `initializeRunOnStart` is enabled. `RunInternal(...)` decides the spawn pose, calls `player.ResetRun(...)`, calls `areaStreamer.BuildInitialWorld()`, calls `legacyEnvironmentResetAdapter.ResetEnvironment()`, then raises `RunStarted`.
4. Player reset
   `MushroomRunnerPlayer.ResetRun(...)` sets reset state, stops and teleports the motor, restores dash availability, then returns the player to active state.
5. World rebuild
   `RunnerCourseStreamer.BuildInitialWorld()` clears generated content, resets its generator state, builds the start route from `startSpawnDefinition`, points `RunScoreService` at the player transform, and resets score using the player's current `z`.
6. Legacy reset
   `LegacyEnvironmentResetAdapter.ResetEnvironment()` finds legacy `BlockSpawner` mono behaviours by type name and invokes `ResetSpawner()` on them.
7. Per-frame input
   `RunnerInputSource.Update()` reads joystick and dash input, optionally blends keyboard fallback, converts direction relative to `movementCamera`, and writes a new `MovementInputFrame` into `CurrentFrame`.
8. Per-frame player orchestration
   `MushroomRunnerPlayer.Update()` reads `inputHandler.CurrentFrame`, stores it as `CurrentInputFrame`, passes it into `movementMotor.SetInput(...)`, and requests dash if the frame contains dash input.
9. Fixed-step movement
   `RunnerMovementMotor.FixedUpdate()` handles gravity shaping, bounce consumption, air acceleration, drag, speed floor logic, buffered dash execution, and the final `Rigidbody.linearVelocity` write. It raises `Bounced` and `Dashed` when those actions occur.
10. Gameplay reaction
   `MushroomRunnerPlayer` responds to `Bounced` by restoring dash and raising `PlayerBounced`. It responds to `Dashed` by raising `PlayerDashed`.
11. Multiplier update
   `RunMultiplierService.Update()` reads `player.CurrentInputFrame`, `movementMotor.IsGrounded`, and airtime state. It updates combo hits, combo break timing, multiplier, and airtime qualification. It also listens to `movementMotor.Bounced` and only counts valid mushroom bounces.
12. Score update
   `RunScoreService.Update()` reads `trackedTarget.position.z` plus state from `RunMultiplierService`, updates forward progress and airtime score, and publishes the new `RunScoreSnapshot` through `SnapshotChanged` and `MushroomRunnerEvents.RunScoreUpdated`.
13. Presentation update
   `RunScoreHud` reads the current score snapshot and updates score and status text. `PlayerSpeedHudPresenter.Update()` samples motor speed and updates the meter. `RunnerCameraRig.LateUpdate()` follows `CameraFollowTarget` and adjusts FOV from movement speed.
14. Failure and reset
   `DeathPlaneResetVolume` detects failure through trigger overlap or by the player falling below the death height. It calls `RunFlowCoordinator.ReportFailure(...)`, which raises `RunFailed` and immediately performs `ResetRun()`. Reset repeats the same order: player reset, world rebuild, legacy reset, then `RunReset`.

## Event Map

The cross-system event hub is [MushroomRunnerEvents.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Core/MushroomRunnerEvents.cs).

### Publishers

| Publisher | Events |
| --- | --- |
| `MushroomRunnerPlayer` | `PlayerRegistered`, `PlayerStateChanged`, `PlayerBounced`, `PlayerDashed` |
| `RunFlowCoordinator` | `RunStarted`, `RunReset`, `RunFailed` |
| `RunScoreService` | `RunScoreUpdated` |

### Current Subscribers

| Subscriber | Events | Why |
| --- | --- | --- |
| `DeathPlaneResetVolume` | `RunStarted`, `RunReset` | Re-arms its grace timer after spawn and reset. |
| `RunScoreHud` | local `RunScoreService.SnapshotChanged` only | It is a direct presenter for one score source, so it listens to the service instead of the global hub. |

## Legacy Boundary

The repository currently contains both the new MushroomRunner module and older gameplay systems under `Assets/_Game/Scripts/Gameplay`.

The important boundary is:

- MushroomRunner uses `RunFlowCoordinator`, `MushroomRunnerPlayer`, `RunnerCourseStreamer`, and `RunScoreService`.
- MushroomRunner does not use `GameManager`.

If you are debugging the current runner loop, start inside [Assets/_Game/Funguy.MushroomRunner](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner), then use [MushroomRunner-Overview.md](/d:/Work/FunGuy/Docs/MushroomRunner-Overview.md) for the mental model and this file for the exact wiring.
