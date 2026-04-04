using UnityEngine;

namespace FunGuy.Runner
{
    public sealed class PlayerRunnerController : MonoBehaviour
    {
        [SerializeField] private RunnerInputHandler inputHandler;
        [SerializeField] private RunnerGridSystem gridSystem;
        [SerializeField] private GridWorld gridWorld;
        [SerializeField] private GridJumpMotor jumpMotor;
        [SerializeField] private RunnerPlayerConfig config;

        private enum PlayerPhase
        {
            Idle,
            Grounded,
            Jumping,
            Falling,
            Dead
        }

        private PlayerPhase phase = PlayerPhase.Idle;
        private Vector3Int currentCell;
        private Vector3Int jumpStartCell;
        private Vector3Int plannedTargetCell;
        private Vector3Int runStartCell;
        private float nextBounceCountdown;
        private int currentAirJumpExtensions;
        private int extraJumpsRemaining;
        private int bonusScore;
        private int score;
        private bool movementPaused;
        private RunnerSimpleTrail runnerTrail;

        public Vector3Int CurrentCell => currentCell;
        public int Score => score;

        private void Reset()
        {
            jumpMotor = GetComponent<GridJumpMotor>();
            runnerTrail = GetComponent<RunnerSimpleTrail>();
        }

        public void Configure(
            RunnerGridSystem runnerGridSystem,
            GridWorld runnerGridWorld,
            RunnerInputHandler runnerInputHandler,
            RunnerPlayerConfig playerConfig)
        {
            gridSystem = runnerGridSystem;
            gridWorld = runnerGridWorld;
            inputHandler = runnerInputHandler;
            config = playerConfig;

            if (jumpMotor == null)
            {
                jumpMotor = GetComponent<GridJumpMotor>();
            }

            if (runnerTrail == null)
            {
                runnerTrail = GetComponent<RunnerSimpleTrail>();
            }

            if (jumpMotor != null)
            {
                jumpMotor.Configure(config);
            }
        }

        public void BeginRun(Vector3Int startCell)
        {
            if (gridSystem == null || gridWorld == null || jumpMotor == null || config == null)
            {
                Debug.LogError("[PlayerRunnerController] Missing one or more dependencies.");
                return;
            }

            currentCell = startCell;
            runStartCell = startCell;
            phase = PlayerPhase.Grounded;
            bonusScore = 0;
            score = 0;
            nextBounceCountdown = config.InitialBounceDelay;
            extraJumpsRemaining = config.MaxExtraJumps;
            jumpStartCell = startCell;
            plannedTargetCell = startCell;
            currentAirJumpExtensions = 0;
            movementPaused = false;

            if (runnerTrail == null)
            {
                runnerTrail = GetComponent<RunnerSimpleTrail>();
            }

            jumpMotor.SnapTo(gridSystem.CellToWorld(currentCell));
            runnerTrail?.ClearTrail();
            inputHandler?.ClearBufferedIntent();

            RunnerGameEvents.RaiseScoreChanged(score);
            RunnerGameEvents.RaiseExtraJumpsChanged(extraJumpsRemaining, config.MaxExtraJumps);
            RunnerGameEvents.RaisePlayerLanded(currentCell);
        }

        private void Update()
        {
            if (movementPaused)
            {
                return;
            }

            if (phase == PlayerPhase.Jumping)
            {
                TryRetargetCurrentJump();
                return;
            }

            if (phase != PlayerPhase.Grounded)
            {
                return;
            }

            nextBounceCountdown -= Time.deltaTime;

            if (nextBounceCountdown <= 0f)
            {
                StartNextJump();
            }
        }

        private void StartNextJump()
        {
            MovementIntent intent = inputHandler != null ? inputHandler.ConsumeBufferedIntent() : MovementIntent.None;
            jumpStartCell = currentCell;
            currentAirJumpExtensions = 0;

            if (intent.ForceForward)
            {
                TryConsumeForwardAirJumpExtension();
            }

            plannedTargetCell = BuildTargetCell(jumpStartCell, intent);
            phase = PlayerPhase.Jumping;
            RunnerGameEvents.RaisePlayerJumpStarted(jumpStartCell, plannedTargetCell);
            jumpMotor.BeginJump(
                gridSystem.CellToWorld(jumpStartCell),
                gridSystem.CellToWorld(plannedTargetCell),
                config.GetJumpDuration(currentCell.z),
                GetCurrentFlightForwardCells(),
                GetPlannedUpwardCells(),
                ResolveJumpArrival);
        }

        private void TryRetargetCurrentJump()
        {
            if (inputHandler == null || jumpMotor == null || !jumpMotor.IsInFlight)
            {
                return;
            }

            MovementIntent intent = inputHandler.ConsumeBufferedIntent();

            if (!intent.HasInput)
            {
                return;
            }

            bool retargetLocked = jumpMotor.FlightProgressNormalized >= config.RetargetLockProgress;
            bool wantsForwardExtension = intent.ForceForward;
            bool wantsVerticalBoost = intent.LayerDelta > 0;

            if (retargetLocked && !wantsForwardExtension && !wantsVerticalBoost)
            {
                return;
            }

            bool consumedRetargetCharge = true;

            if (wantsForwardExtension)
            {
                consumedRetargetCharge = TryConsumeForwardAirJumpExtension();
            }
            else if (wantsVerticalBoost)
            {
                consumedRetargetCharge = TryConsumeAirActionCharge();
            }

            if (!consumedRetargetCharge)
            {
                if (intent.LaneDelta == 0 && intent.LayerDelta >= 0)
                {
                    return;
                }
            }

            Vector3Int candidate = BuildRetargetedCell(intent);

            if (candidate == plannedTargetCell)
            {
                jumpMotor.RetargetLanding(
                    gridSystem.CellToWorld(plannedTargetCell),
                    GetCurrentFlightForwardCells(),
                    GetPlannedUpwardCells(),
                    intent.LayerDelta > 0);
                return;
            }

            plannedTargetCell = candidate;
            jumpMotor.RetargetLanding(
                gridSystem.CellToWorld(plannedTargetCell),
                GetCurrentFlightForwardCells(),
                GetPlannedUpwardCells(),
                intent.LayerDelta > 0);
            RunnerGameEvents.RaisePlayerJumpStarted(jumpStartCell, plannedTargetCell);
        }

        private Vector3Int BuildTargetCell(Vector3Int originCell, MovementIntent intent)
        {
            int forwardSteps = config.GetForwardCellsForJumpExtensions(currentAirJumpExtensions);
            Vector3Int requestedCell = new(
                originCell.x + intent.LaneDelta,
                originCell.y + intent.LayerDelta,
                originCell.z + forwardSteps);

            Vector3Int lowerFallbackCell = new(requestedCell.x, originCell.y, requestedCell.z);
            return ResolveLandingCell(requestedCell, lowerFallbackCell, requestedCell);
        }

        private Vector3Int BuildRetargetedCell(MovementIntent intent)
        {
            int forwardSteps = config.GetForwardCellsForJumpExtensions(currentAirJumpExtensions);

            int lane = intent.LaneDelta != 0 ? jumpStartCell.x + intent.LaneDelta : plannedTargetCell.x;
            int layer = plannedTargetCell.y;
            int fallbackLayer = plannedTargetCell.y;

            if (intent.LayerDelta > 0)
            {
                layer = plannedTargetCell.y + config.ExtraJumpUpCells;
            }

            if (intent.LayerDelta < 0)
            {
                layer = plannedTargetCell.y + intent.LayerDelta;
            }

            Vector3Int requestedCell = new(lane, layer, jumpStartCell.z + forwardSteps);
            Vector3Int lowerFallbackCell = new(requestedCell.x, fallbackLayer, requestedCell.z);
            return ResolveLandingCell(requestedCell, lowerFallbackCell, plannedTargetCell);
        }

        private int GetPlannedForwardCells()
        {
            return Mathf.Max(1, plannedTargetCell.z - jumpStartCell.z);
        }

        private int GetCurrentFlightForwardCells()
        {
            int plannedForwardCells = GetPlannedForwardCells();
            int reachableForwardCells = config != null
                ? config.GetForwardCellsForJumpExtensions(currentAirJumpExtensions)
                : plannedForwardCells;
            return Mathf.Max(plannedForwardCells, reachableForwardCells);
        }

        private int GetPlannedUpwardCells()
        {
            return Mathf.Max(0, plannedTargetCell.y - jumpStartCell.y);
        }

        private void ResolveJumpArrival()
        {
            GridLandingType landingType = gridWorld.GetLandingType(plannedTargetCell, out GridSurfaceActor surface);

            if (landingType == GridLandingType.Missing)
            {
                phase = PlayerPhase.Falling;
                jumpMotor.PlayFallFromCurrent(CalculateDeathY(plannedTargetCell), HandlePlayerDeath);
                return;
            }

            currentCell = plannedTargetCell;
            jumpMotor.SnapTo(gridSystem.CellToWorld(currentCell));
            jumpMotor.PlayLandingImpact();
            RunnerGameEvents.RaisePlayerLanded(currentCell);
            surface?.PlayLandingFeedback();

            if (surface != null && surface.IsHazard)
            {
                HandlePlayerDeath();
                return;
            }

            RefreshScoreFromProgress();

            if (gridWorld.TryConsumeCollectible(currentCell, out _))
            {
                bonusScore += config.ScorePerCollectible;
                RunnerGameEvents.RaiseCollectibleCollected(currentCell);
                RefreshScoreFromProgress();
            }

            extraJumpsRemaining = config.MaxExtraJumps;
            RunnerGameEvents.RaiseExtraJumpsChanged(extraJumpsRemaining, config.MaxExtraJumps);

            phase = PlayerPhase.Grounded;
            nextBounceCountdown = config.GetTimeBetweenBounces(currentCell.z) + config.GetLandingPause(currentCell.z);
        }

        private float CalculateDeathY(Vector3Int targetCell)
        {
            float baseY = gridSystem.CellToWorld(new Vector3Int(0, gridSystem.MinLayer, targetCell.z)).y;
            float cellHeight = gridSystem.Config != null ? gridSystem.Config.cellSize.y : 1f;
            return baseY - config.DeathDepthCells * cellHeight;
        }

        private void HandlePlayerDeath()
        {
            if (phase == PlayerPhase.Dead)
            {
                return;
            }

            phase = PlayerPhase.Dead;
            RunnerGameEvents.RaisePlayerDied();
        }

        public void SetMovementPaused(bool paused)
        {
            movementPaused = paused;
        }

        public float EstimateTimeToReachCellZ(int targetZ)
        {
            if (config == null || targetZ <= currentCell.z)
            {
                return 0f;
            }

            if (phase == PlayerPhase.Dead || phase == PlayerPhase.Falling)
            {
                return float.PositiveInfinity;
            }

            float totalTime = 0f;
            int nextHopStartZ = currentCell.z;

            if (phase == PlayerPhase.Jumping)
            {
                float currentJumpDuration = config.GetJumpDuration(jumpStartCell.z);
                float remainingJumpTime = Mathf.Max(0f, (1f - jumpMotor.FlightProgressNormalized) * currentJumpDuration);
                totalTime += remainingJumpTime;

                if (targetZ <= plannedTargetCell.z)
                {
                    return totalTime;
                }

                nextHopStartZ = plannedTargetCell.z;
            }
            else
            {
                totalTime += Mathf.Max(0f, nextBounceCountdown);
            }

            totalTime += EstimateFutureTravelDuration(nextHopStartZ, targetZ);
            return totalTime;
        }

        private void RefreshScoreFromProgress()
        {
            int forwardCellsTraveled = Mathf.Max(0, currentCell.z - runStartCell.z);
            score = (forwardCellsTraveled * config.ScorePerForwardCell) + bonusScore;
            RunnerGameEvents.RaiseScoreChanged(score);
        }

        private bool TryConsumeForwardAirJumpExtension()
        {
            if (!TryConsumeAirActionCharge())
            {
                return false;
            }

            currentAirJumpExtensions++;
            return true;
        }

        private bool TryConsumeAirActionCharge()
        {
            if (extraJumpsRemaining <= 0)
            {
                return false;
            }

            extraJumpsRemaining--;
            RunnerGameEvents.RaiseExtraJumpsChanged(extraJumpsRemaining, config.MaxExtraJumps);
            return true;
        }

        private Vector3Int ResolveLandingCell(
            Vector3Int requestedCell,
            Vector3Int lowerFallbackCell,
            Vector3Int currentFallbackCell)
        {
            if (gridWorld == null || config == null)
            {
                return requestedCell;
            }

            if (CanLandOnCell(requestedCell))
            {
                return requestedCell;
            }

            if (CanLandOnCell(lowerFallbackCell))
            {
                return lowerFallbackCell;
            }

            if (TryFindBestLandingCell(requestedCell, lowerFallbackCell, currentFallbackCell, out Vector3Int bestLandingCell))
            {
                return bestLandingCell;
            }

            return currentFallbackCell;
        }

        private bool CanLandOnCell(Vector3Int cell)
        {
            return gridWorld.GetLandingType(cell, out _) != GridLandingType.Missing;
        }

        private bool TryFindBestLandingCell(
            Vector3Int requestedCell,
            Vector3Int lowerFallbackCell,
            Vector3Int currentFallbackCell,
            out Vector3Int bestLandingCell)
        {
            bestLandingCell = default;
            bool hasBestLandingCell = false;

            if (CanLandOnCell(currentFallbackCell))
            {
                bestLandingCell = currentFallbackCell;
                hasBestLandingCell = true;
            }

            int minimumForwardZ = jumpStartCell.z + (config != null ? config.BaseForwardCells : 1);

            for (int z = requestedCell.z; z >= minimumForwardZ; z--)
            {
                TryRecordLandingCandidate(new Vector3Int(requestedCell.x, requestedCell.y, z), ref bestLandingCell, ref hasBestLandingCell);
                TryRecordLandingCandidate(new Vector3Int(requestedCell.x, lowerFallbackCell.y, z), ref bestLandingCell, ref hasBestLandingCell);
                TryRecordLandingCandidate(new Vector3Int(currentFallbackCell.x, currentFallbackCell.y, z), ref bestLandingCell, ref hasBestLandingCell);

                if (hasBestLandingCell && bestLandingCell.z == z)
                {
                    break;
                }
            }

            return hasBestLandingCell;
        }

        private void TryRecordLandingCandidate(Vector3Int candidateCell, ref Vector3Int bestLandingCell, ref bool hasBestLandingCell)
        {
            if (!CanLandOnCell(candidateCell))
            {
                return;
            }

            if (!hasBestLandingCell || candidateCell.z > bestLandingCell.z)
            {
                bestLandingCell = candidateCell;
                hasBestLandingCell = true;
            }
        }

        private float EstimateFutureTravelDuration(int fromZ, int targetZ)
        {
            if (config == null || targetZ <= fromZ)
            {
                return 0f;
            }

            float totalTime = 0f;

            for (int hopStartZ = fromZ; hopStartZ < targetZ; hopStartZ++)
            {
                totalTime += config.GetJumpDuration(hopStartZ);

                if (hopStartZ + 1 < targetZ)
                {
                    int landingZ = hopStartZ + 1;
                    totalTime += config.GetTimeBetweenBounces(landingZ);
                    totalTime += config.GetLandingPause(landingZ);
                }
            }

            return totalTime;
        }
    }
}
