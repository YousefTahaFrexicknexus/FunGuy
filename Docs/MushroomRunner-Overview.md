# MushroomRunner Overview

Use [MushroomRunner-System-Wiring.md](/d:/Work/FunGuy/Docs/MushroomRunner-System-Wiring.md) when you need the exact scene references and serialized wiring.

Use this document first when you want the fast mental model: who owns what, what talks to what, and how a run moves from input to score to reset.

## 30-Second Mental Model

- `MushroomRunnerPlayer` is the gameplay brain on the player.
- `RunnerMovementMotor` is the body that actually moves.
- `RunnerCameraRig` follows the player's `CameraFollowTarget` child, not the root transform.
- `RunMultiplierService` and `RunScoreService` turn movement and airtime into score.
- `RunFlowCoordinator` owns run start, run reset, and failure recovery.

The scene that wires this all together is [MushroomRunnerGameplay.unity](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Scenes/MushroomRunnerGameplay.unity), and the player root is [MushroomRunnerPlayer.prefab](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Prefabs/MushroomRunnerPlayer.prefab).

## Component Map

```mermaid
flowchart LR
    Input[RunnerInputSource]
    Player[MushroomRunnerPlayer]
    Motor[RunnerMovementMotor]
    Camera[RunnerCameraRig]
    Multiplier[RunMultiplierService]
    Score[RunScoreService]
    World[RunnerCourseStreamer]
    Flow[RunFlowCoordinator]
    Death[DeathPlaneResetVolume]
    HUD[RunScoreHud / PlayerSpeedHudPresenter]
    Legacy[LegacyEnvironmentResetAdapter]
    Events[MushroomRunnerEvents]

    Input --> Player
    Player --> Motor
    Motor --> Player
    Player --> Multiplier
    Motor --> Multiplier
    Multiplier --> Score
    Flow --> Player
    Flow --> World
    Flow --> Legacy
    Death --> Flow
    Player --> Camera
    Score --> HUD
    Motor --> HUD
    Player --> Events
    Flow --> Events
    Score --> Events
```

If Mermaid does not render, read it like this:

- input feeds the player
- the player commands the motor
- the motor reports movement results back to the player and multiplier service
- multiplier feeds score
- run flow resets player, world, and legacy environment
- HUD reads score and motor state
- camera follows the player's follow target

## Runtime Story

```mermaid
flowchart TD
    A[Scene loads] --> B[RunFlowCoordinator starts run]
    B --> C[MushroomRunnerPlayer resets]
    C --> D[RunnerCourseStreamer builds world]
    D --> E[RunnerInputSource samples input]
    E --> F[MushroomRunnerPlayer forwards input]
    F --> G[RunnerMovementMotor moves body]
    G --> H[RunMultiplierService updates combo and airtime]
    H --> I[RunScoreService updates score]
    I --> J[HUD and camera update]
    J --> K{Player failed?}
    K -- No --> E
    K -- Yes --> L[DeathPlaneResetVolume reports failure]
    L --> M[RunFlowCoordinator resets run]
    M --> C
```

If Mermaid does not render, the loop is:

1. The run coordinator starts the run.
2. The player resets to a clean state.
3. The course streamer rebuilds the route.
4. Input is sampled each frame.
5. The player passes input into the motor.
6. The motor moves and bounces.
7. Multiplier and score update from movement.
8. HUD and camera reflect the latest state.
9. Death detection reports failure and the coordinator resets the run.

## Player

### What it owns

- player state
- dash lifecycle
- current input frame
- reset entry point for the player body

### Scripts/components

- [MushroomRunnerPlayer.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Player/MushroomRunnerPlayer.cs)
- [RunnerMovementMotor.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Movement/RunnerMovementMotor.cs)
- [RunMultiplierService.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/World/RunMultiplierService.cs)
- `Rigidbody`
- `SphereCollider`
- `CameraFollowTarget`

### Talks to

- `RunnerInputSource`
- `RunFlowCoordinator`
- `RunScoreService`
- `RunnerCameraRig`
- `MushroomRunnerEvents`

### Where it is wired

- player root is [MushroomRunnerPlayer.prefab](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Prefabs/MushroomRunnerPlayer.prefab)
- scene instance is in [MushroomRunnerGameplay.unity](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Scenes/MushroomRunnerGameplay.unity)

### When it runs

- `Awake()` pushes the tuning profile into the motor and wires dash resources
- `OnEnable()` subscribes to motor events
- `Update()` reads input and commands the motor
- `ResetRun(...)` restores the player after start or death

The key mental model is: `MushroomRunnerPlayer` decides what the player is trying to do, while `RunnerMovementMotor` decides how the body actually moves.

## Camera

### What it owns

- follow behavior
- framing offset
- FOV response from movement speed

### Scripts/components

- [RunnerCameraRig.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Core/RunnerCameraRig.cs)
- player child `CameraFollowTarget`

### Talks to

- player `CameraFollowTarget`
- player `Rigidbody` or resolved velocity source

### Where it is wired

- camera script lives on `Main Camera` in [MushroomRunnerGameplay.unity](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Scenes/MushroomRunnerGameplay.unity)
- its `target` is assigned to the player child's `CameraFollowTarget`

### When it runs

- `LateUpdate()` follows the target after movement is applied

The important idea is that the camera follows a dedicated composition target, so camera framing stays decoupled from the player's physics pivot.

## Score And Multiplier

### What it owns

- combo hit count
- multiplier state
- airtime qualification
- forward progress score
- airtime score
- current `RunScoreSnapshot`

### Scripts/components

- [RunMultiplierService.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/World/RunMultiplierService.cs)
- [RunScoreService.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/World/RunScoreService.cs)
- [RunScoreSnapshot.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Core/RunScoreSnapshot.cs)

### Talks to

- `MushroomRunnerPlayer`
- `RunnerMovementMotor`
- `RunnerCourseStreamer`
- `RunScoreHud`
- `MushroomRunnerEvents`

### Where it is wired

- `RunMultiplierService` lives on the player prefab
- `RunScoreService` lives as a scene system in [MushroomRunnerGameplay.unity](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Scenes/MushroomRunnerGameplay.unity)
- `RunScoreService` is assigned the player, the player transform, and the player's multiplier service

### When it runs

- `RunMultiplierService.Update()` watches live movement state
- `RunScoreService.Update()` converts movement progress and airtime into score
- `RunScoreService` publishes the latest snapshot whenever score state changes

The clean split is:

- `RunMultiplierService` decides how valuable the current run state is
- `RunScoreService` turns that state into points

## World

### What it owns

- start route creation
- forward course generation
- cleanup of old generated content
- score target reset on world rebuild

### Scripts/components

- [RunnerCourseStreamer.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/World/RunnerCourseStreamer.cs)
- `BounceAreaGenerationProfile`
- `BounceSpawnDefinition`

### Talks to

- player transform
- `RunScoreService`
- `RunFlowCoordinator`

### Where it is wired

- `RunnerCourseStreamer` lives in `_Systems` in [MushroomRunnerGameplay.unity](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Scenes/MushroomRunnerGameplay.unity)
- generated mushrooms go under `GeneratedMushrooms`
- generated environment goes under `GeneratedEnvironment`

### When it runs

- `BuildInitialWorld()` runs at start and reset
- `Update()` keeps spawning ahead and recycling behind the player

The world system does not own run state. It responds to run flow and rebuilds the track around the current player run.

## Reset And Failure

### What it owns

- initial run start
- reset ordering
- spawn pose selection
- failure detection
- legacy environment reset bridge

### Scripts/components

- [RunFlowCoordinator.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Core/RunFlowCoordinator.cs)
- [DeathPlaneResetVolume.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/World/DeathPlaneResetVolume.cs)
- [LegacyEnvironmentResetAdapter.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/World/LegacyEnvironmentResetAdapter.cs)

### Talks to

- `MushroomRunnerPlayer`
- `RunnerCourseStreamer`
- `MushroomRunnerEvents`
- legacy `BlockSpawner` systems

### Where it is wired

- `RunFlowCoordinator` lives in `_Systems`
- `DeathPlaneResetVolume` lives in `_Runtime`
- `LegacyEnvironmentResetAdapter` lives in `_Systems`

### When it runs

- `RunFlowCoordinator.Start()` kicks off the first run
- `ReportFailure(...)` handles death and triggers reset
- `DeathPlaneResetVolume` watches for trigger hits and low-height failure

The reset order is always:

1. reset the player
2. rebuild the course
3. reset legacy environment
4. raise the run lifecycle event

## HUD

### What it owns

- score text
- status text
- speed meter presentation

### Scripts/components

- [RunScoreHud.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Core/RunScoreHud.cs)
- [PlayerSpeedHudPresenter.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Core/PlayerSpeedHudPresenter.cs)

### Talks to

- `RunScoreService`
- `RunnerMovementMotor`

### Where it is wired

- `RunScoreHud` lives on `ScoreText`
- `PlayerSpeedHudPresenter` lives on `SpeedMeter`
- both are authored in the HUD hierarchy inside [MushroomRunnerGameplay.unity](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Scenes/MushroomRunnerGameplay.unity)

### When it runs

- `RunScoreHud` updates when the score service publishes a new snapshot
- `PlayerSpeedHudPresenter.Update()` refreshes from motor speed

The HUD is intentionally presentation-only. It does not own gameplay state.

## Legacy Boundary

### What it owns

- nothing inside the MushroomRunner loop

### Scripts/components

- [GameManager.cs](/d:/Work/FunGuy/Assets/_Game/Scripts/Gameplay/GameManager.cs)
- older `Assets/_Game/Scripts/Gameplay` systems

### Talks to

- older `MushroomSpawner_XAxis` and `CreatureBouncer_XAxis` flows

### Where it is wired

- outside the `Funguy.MushroomRunner` module

### When it runs

- only in the older gameplay path, not in MushroomRunner

If you are debugging the current runner, start inside [Assets/_Game/Funguy.MushroomRunner](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner). Do not start from `GameManager`.

## Where To Edit What

- Change player rules, state, dash logic, or reset behavior in [MushroomRunnerPlayer.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Player/MushroomRunnerPlayer.cs).
- Change actual movement feel, gravity, bounce, or dash force in [RunnerMovementMotor.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Movement/RunnerMovementMotor.cs).
- Change camera framing or FOV response in [RunnerCameraRig.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Core/RunnerCameraRig.cs).
- Change combo or multiplier behavior in [RunMultiplierService.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/World/RunMultiplierService.cs).
- Change score math or snapshot fields in [RunScoreService.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/World/RunScoreService.cs).
- Change route generation in [RunnerCourseStreamer.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/World/RunnerCourseStreamer.cs).
- Change reset and failure flow in [RunFlowCoordinator.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/Core/RunFlowCoordinator.cs) and [DeathPlaneResetVolume.cs](/d:/Work/FunGuy/Assets/_Game/Funguy.MushroomRunner/World/DeathPlaneResetVolume.cs).
- Change the exact serialized wiring reference in [MushroomRunner-System-Wiring.md](/d:/Work/FunGuy/Docs/MushroomRunner-System-Wiring.md).
