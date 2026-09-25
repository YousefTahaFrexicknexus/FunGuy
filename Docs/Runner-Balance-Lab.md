# Runner Balance Lab

Open **Tools > Funguy > Runner Balance Lab** outside Play Mode. Select the scene Momentum System, Distance Score Manager, Runner Movement Motor and Gameplay Manager, then a saved movement profile and one or more MushroomLauncher prefabs. All selected scene components must belong to the same scene.

1. **Reload selected targets** imports existing values into a detached draft. Equal or unordered speed caps are shown as invalid rather than silently changed.
2. Choose **Negative Sum**, **Zero Sum**, **Positive Sum**, or restore your saved **Custom** draft. Presets offer speed caps 15/30/45/60; the fitter keeps your chosen caps fixed.
3. Edit linked controls and rule lists. Probability sliders redistribute the other two probabilities proportionally. Momentum thresholds and speed boundaries can also be dragged on their graphs. Bad is orange-red, Good blue, Perfect gold, and the mixed average purple; line styles supplement the colors.
4. **Auto-Balance** generates an Add-then-Multiply proposal for each landing quality. It fits rewards, thresholds, rule values and decay strength against the requested Perfect-streak first-reach counts and the average drift targets. It does not alter source assets. Custom Add/Multiply/Set lists remain editable; Auto-Balance replaces their structure in the draft explicitly.
5. Inspect the optimizer report, reach statistics, band drift table and **Before > After** review. An approximate result is not reported as balanced. Review every target prefab: all listed prefabs receive the representative draft's quality rules.
6. **Apply reviewed draft** enables the new model on the selected Momentum System, updates selected caps/scoring/reward rules, and wires the selected scene components. It saves only selected assets and marks the scene dirty. Save the scene normally. The change is one Unity Undo group; undo asset changes can be saved normally afterward.

If an external edit changes a target, Apply asks you to Reload before overwriting anything. Apply is disabled in Play Mode. Drafts and the separate last Custom draft persist under `UserSettings/RunnerBalanceLab.asset`; they are not runtime balance assets. Closing the window or Cancel stops calculations. Refresh preview restarts them.

## Runtime model

Opt-in defaults: momentum 0?100, discrete x1/x2/x3/x4 scoring, zero passive momentum gain, hysteresis 2 points, cap time constant 0.35 seconds. Existing content keeps its legacy behavior until Apply (or explicit Inspector opt-in). Landings use existing Good/Perfect bonuses and Bad penalty; speed rules remain on each mushroom prefab. Other mistake penalties remain intact.

For the active tier, the preceding cap is its minimum sustaining forward speed. With caps 15/30/45/60, x4 requires at least 45. Below that minimum:

`loss/second = strength * (max(0, minimum - max(0, forwardSpeed)) / bandWidth)^2`

At strength 4, x4 speeds 40/30/15 lose about 0.44/4/16 momentum points per second. At or above the minimum there is no decay; x1 has no speed decay. Demotion happens below the promotion threshold minus the effective hysteresis margin, which is capped to 49% of the smaller neighboring threshold gap. Earned score is never removed.

The maximum horizontal speed uses `target + (current - target) * exp(-dt / timeConstant)`. Normal overspeed drag still controls the actual velocity. Promotions add no impulse. Startup and reset initialize x1 immediately; pause freezes momentum and cap blending. The motor refreshes momentum's speed bands when its profile changes. Missing momentum binding uses the profile's x1 cap.

`RunnerBalanceMath` is the shared runtime/editor formula source. Opted-in motor ticks advance momentum before cap blending and movement; legacy configurations retain GameplayManager's original tick path. DistanceScoreManager is the sole publisher of score-multiplier notifications.

## Reading the predictions

Defaults: 30% Bad / 50% Good / 20% Perfect, 2 seconds between landings, zero starting speed and momentum, neutral joystick, 100 landings, 2,048 mixed trials, seed 1337. The first landing follows one cadence interval. Pure traces show every simulation step and landing; the mixed graph averages post-landing samples. Markers identify sampled tier changes. Use the table for exact first-reach counts.

The simulator uses the selected profile's gravity, acceleration/steering settings, drag, bounce effectiveness and the representative arrow, plus the actual launcher rule calculation. It uses fixed 0.02-second integration and the existing horizontal soft limit. Forward/world-Z speed maintains momentum; horizontal magnitude controls the soft limit. Turning can therefore put forward speed below a band even when total horizontal speed is higher.

This is a cadence model, **not** a course collision prediction. It assumes each scheduled landing succeeds with the sampled quality. It excludes extra jumps, collisions, side-hit penalties and unplanned flight durations. Representative arrows and movement profiles matter; results do not promise equal performance for every differently angled target prefab.

Mixed reach statistics explicitly report the percentage that reached each tier, then mean/median/standard deviation **only among successful trials**. If none arrive, the label says `Not reached within N landings`. First reaching a tier does not imply it was maintained. Preview speed/momentum affect calibration; they do not change the real run's zero starting momentum/reset velocity.

Calibration starts at the midpoint of x2's speed and momentum bands, applies one sampled landing, then advances one cadence interval. Negative/Zero/Positive presets target -2/0/+2 momentum points and -2%/0/+2% speed drift. These are local expected changes, not universal results across all speeds. The band table shows the differences elsewhere. Auto-Balance accepts exact requested first-reach counts and calibration errors <=0.25 momentum points and <=0.5 percentage points of speed. Its deterministic bounded coordinate search reports unmet targets rather than silently relaxing them.

## Verification

`RunnerBalanceVerification.Run` provides batch-only regression checks for the formulas, preview/runtime agreement, all three preset fits, impossible targets, seeded statistics, notifications, reset/pause/profile changes and temporary prefab Apply/Undo/Redo. Run it in an isolated Unity project containing the runtime and editor sources; it creates and removes its own uniquely named test assets and exits Unity afterward. Results default to `../results.txt`, or the path in `RUNNER_BALANCE_RESULTS`.

Design references: [Metal: Hellsinger's performance tiers](https://www.levelinfinite.com/news/run-to-the-kills-in-metal-hellsinger/), [Warframe's gradual combo decay](https://www.warframe.com/de/patch-notes/pc/22-0-0), and [GDC: Beyond Spreadsheets](https://www.gdcvault.com/play/1026489/Beyond-Spreadsheets-Building-a-Metagame). The preset numbers are game-specific starting hypotheses, not values copied from those games.

## Window navigation and decay diagnosis

Use Tune / Targets in the sidebar. Progression shows selectable Momentum, Forward speed and Earned score graphs; click a colored legend button to toggle a curve. Hover readouts use an opaque panel. The reach table shows both total landings and additional landings from the previous tier. Mixed-run means remain conditional on reaching the tier.

Decay & sustain separates the four selectable tier curves from progression. Review & apply contains the explicit target diff. Probabilities are displayed as percentages; advanced simulation settings are collapsed by default. The window warns when the selected scene still uses legacy momentum. During Play Mode it also shows momentum, speed and the calculated tier loss rate, and warns about a mismatched motor source or invalid speed bands.

The existing model does not enforce exponential progression. Constant landing rewards and multiplicative speed gains can make later tiers feel easy. At sufficient speed, decay is exactly zero, even at x4; rewards can also outweigh decay below the minimum. For example, losing 0.44 points/second at speed 40 is much less than gaining 8 points every two seconds. A full meter of 100 must fall below 78 to demote if the x4 threshold is 80 and hysteresis is 2.

## Smooth difficulty model

New drafts and the three presets offer smooth difficulty. Existing scene settings remain unchanged until Apply; Reload imports the selected scene's actual mode. Enable **Smooth upkeep + speed loss** to opt in. Speed-only tier decay remains available for comparison.

Four editable upkeep control points sit at momentum 0, 33.33, 66.67 and 100. Defaults are 0 / 0.4 / 1.6 / 3.2 momentum points per second. Nondecreasing points are joined with smoothstep interpolation: continuous value and slope, no overshoot. Required forward speed uses the same interpolation with points 0 / x1 cap / x2 cap / x3 cap. Its deficit normalization width interpolates the four corresponding band widths.

`loss = upkeep(momentum) + speedPenaltyStrength * lerp(1, highPressure, (momentum/100)^2) * deficit^2`

`deficit = max(0, requiredSpeed(momentum) - max(0, forwardSpeed)) / interpolatedBandWidth`

The default high-pressure multiplier is 4. Adequate speed removes the deficit penalty but does not remove upkeep. Both runtime and preview call the same helper. Pause freezes both; reset remains x1; profile switches refresh speed points. No changes were made to steering, acceleration, launch rules or earned score.

**First tier landings** and **Stage cost growth** generate the progression targets. Defaults 3 and 2 produce additional costs 3 / 6 / 12, cumulative targets 3 / 9 / 21. These are optimizer targets, not promised timings. Auto-Balance still reports any missed targets or calibration conflicts. It also checks mixed-play loss at the x3 midpoint, Good loss at the x4 midpoint, and Perfect maintenance at the x4 midpoint, using x3/x4 cap speeds respectively. These are local tests; full-run results may differ.

### Difficulty & sustain diagnostics

- **Net gain / loss**: starting momentum on X, net momentum per landing plus flight on Y. Positive climbs, negative falls. Test starting forward speed is explicit and editable; actual movement losses and tier transitions run during the interval. Momentum clamping is included at 0 and 100.
- Downward zero crossings indicate possible settling points at the selected test speed. They are not guarantees about the full run, where speed evolves.
- **Smooth requirements**: separate upkeep and required-speed graphs with their own units.
- **Loss vs speed**: loss curves evaluated at each tier's momentum midpoint, not fixed tier-wide penalties.
- **No-landings lifetime** starts at momentum 100 and the test speed; it runs normal movement until x4 is lost, with a 120-second limit.
- **Bad streak** starts from the same state and checks after each Bad landing plus flight interval; it reports the first interval with a demotion, bounded by the landing horizon.
- Progression reports whole-run time fractions in x1/x2/x3/x4 for each quality and the mixed ensemble. Those include startup, so they should not be mistaken for steady-state occupancy.

Calibration and sustain checks can conflict, especially with extreme probabilities or upkeep. A failed fit remains an approximate draft, never silently marked balanced. Apply/Undo includes the smooth mode, all upkeep points, and high-momentum pressure alongside the existing settings.

### Editing the survival outcome directly

In **Difficulty & sustain > How long can x4 survive?**, edit **Target survival (seconds)** and press Enter or leave the field. The editor automatically fits the draft; **Fit target** repeats it after changing the test speed or another setting. The supported target range is 0.1 to 119 seconds, within the 120-second diagnostic horizon.

The fitter runs the actual no-landing simulation. It searches a common scale for the upkeep points and speed-penalty strength, preserving their relative shape. Caps, thresholds, rewards, launch rules and movement settings stay fixed. Requested and achieved time remain visible. Fits use a 0.05-second tolerance; unsuccessful fits report the limitation and leave the draft unchanged. The search runs in cancellable editor-update batches.

This is a one-time outcome fit, not a locked constraint for subsequent Auto-Balance runs. Longer survival generally changes progression as well; inspect the recalculated landing counts before Apply. As with all draft edits, scene components and assets are updated only through Review & apply.
