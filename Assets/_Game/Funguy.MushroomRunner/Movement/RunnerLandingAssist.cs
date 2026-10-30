using Unity.Profiling;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunnerLandingAssist : MonoBehaviour
{
    const int MaximumPredictionSteps = 48;
    static readonly ProfilerMarker SearchMarker = new("Funguy.LandingAssist.Search");
    static readonly ProfilerMarker CorrectionMarker = new("Funguy.LandingAssist.Correction");

    [SerializeField, Tooltip("Master switch for landing assistance. Disabled preserves ordinary movement.")]
    bool landingAssistEnabled;
    [SerializeField, Range(0f, 1f), Tooltip("Overall strength multiplier for both assist modes. Zero disables all assist work; acceleration settings below define full strength.")]
    float assistStrength = 1f;
    [SerializeField, Tooltip("Rescue small sideways/forward edge misses using horizontal corrections.")]
    bool edgeAssistEnabled = true;
    [Header("Detection")]
    [SerializeField, Min(0.1f), Tooltip("Base search radius. Movement speed extends it along the predicted trajectory.")]
    float searchRadius = 4f;
    [SerializeField, Range(0.05f, 0.5f)] float predictionTime = 0.35f;
    [SerializeField, Min(0f)] float maximumEdgeMiss = 0.35f;
    [SerializeField, Range(1f, 30f)] float searchesPerSecond = 10f;
    [SerializeField, Tooltip("Solid obstacles that block assistance. Trigger colliders and the player's own body are ignored.")]
    LayerMask obstructionLayers = Physics.DefaultRaycastLayers;
    [Header("Edge Assist Strength")]
    [SerializeField, Min(0f)] float sidewaysAcceleration = 3f;
    [SerializeField, Min(0f)] float forwardAcceleration = 1.5f;
    [SerializeField, Min(0f), Tooltip("Maximum net sideways speed supplied by assistance during one descent.")]
    float maximumSidewaysVelocityCorrection = 0.6f;
    [SerializeField, Min(0f), Tooltip("Maximum net forward/back speed supplied by assistance during one descent.")]
    float maximumForwardVelocityCorrection = 0.3f;
    [Header("Input and Smoothing")]
    [SerializeField, Range(0f, 1f), Tooltip("Both modes: fraction of assistance available with neutral input. Steering toward a landing permits more; steering away cancels it.")]
    float neutralInputStrength = 0.35f;
    [SerializeField, Min(0.1f), Tooltip("Both modes: how quickly the extra acceleration builds toward its target.")]
    float correctionResponse = 10f;
    [Header("Skim Above Assist")]
    [SerializeField, Tooltip("While descending toward a mushroom, add a controlled downward pull if the predicted path narrowly passes above it.")]
    bool skimAssistEnabled;
    [SerializeField, Min(0f), Tooltip("Maximum predicted vertical gap above the actual landing collider that skim assist may rescue, in world units.")]
    float maximumSkimHeight = 0.5f;
    [SerializeField, Min(0f), Tooltip("Maximum extra downward acceleration at full strength, in world units per second squared. Steering intent and Neutral Input Strength also scale it.")]
    float downwardAcceleration = 12f;
    [SerializeField, Min(0f), Tooltip("Maximum additional falling speed supplied by skim assist during one descent, in world units per second.")]
    float maximumDownwardVelocityCorrection = 3f;
    [Header("Debug")]
    [SerializeField, Tooltip("Draw the search area, prediction and correction only in the Scene view when selected.")]
    bool drawDebugGizmos;

    readonly Vector3[] trajectory = new Vector3[MaximumPredictionSteps + 1];
    readonly RaycastHit[] obstructionHits = new RaycastHit[16];
    MushroomLauncher target;
    uint targetGeneration;
    float nextSearchTime;
    Vector2 smoothedAcceleration;
    float smoothedDownwardAcceleration;
    Vector3 injectedVelocity;
    int predictionSteps;
    float predictionStep;
    float currentSearchRadius;
    Vector3 searchCenter;
    Vector3 debugLandingPoint;
    Vector3 debugCorrection;

    struct Candidate
    {
        public MushroomLauncher Launcher;
        public Vector3 Error;
        public Vector3 LandingPosition;
        public float Time;
        public float Score;
    }

    public bool LandingAssistEnabled => landingAssistEnabled;
    public bool SkimAssistEnabled => skimAssistEnabled;
    public float AssistStrength => assistStrength;

    public void SetLandingAssistEnabled(bool enabled)
    {
        landingAssistEnabled = enabled;
        ResetAssist();
    }

    public void SetSkimAssistEnabled(bool enabled)
    {
        skimAssistEnabled = enabled;
        ResetAssist();
    }

    public void SetAssistStrength(float strength)
    {
        assistStrength = Mathf.Clamp01(strength);
        ResetAssist();
    }

    void OnDisable() => ResetAssist();

    void OnValidate()
    {
        assistStrength = Mathf.Clamp01(assistStrength);
        searchRadius = Mathf.Max(0.1f, searchRadius);
        predictionTime = Mathf.Clamp(predictionTime, 0.05f, 0.5f);
        maximumEdgeMiss = Mathf.Max(0f, maximumEdgeMiss);
        searchesPerSecond = Mathf.Clamp(searchesPerSecond, 1f, 30f);
        sidewaysAcceleration = Mathf.Max(0f, sidewaysAcceleration);
        forwardAcceleration = Mathf.Max(0f, forwardAcceleration);
        maximumSidewaysVelocityCorrection = Mathf.Max(0f, maximumSidewaysVelocityCorrection);
        maximumForwardVelocityCorrection = Mathf.Max(0f, maximumForwardVelocityCorrection);
        neutralInputStrength = Mathf.Clamp01(neutralInputStrength);
        correctionResponse = Mathf.Max(0.1f, correctionResponse);
        maximumSkimHeight = Mathf.Max(0f, maximumSkimHeight);
        downwardAcceleration = Mathf.Max(0f, downwardAcceleration);
        maximumDownwardVelocityCorrection = Mathf.Max(0f, maximumDownwardVelocityCorrection);
        if (!landingAssistEnabled || assistStrength <= 0f || (!edgeAssistEnabled && !skimAssistEnabled)) ResetAssist();
    }

    public void ResetAssist()
    {
        target = null;
        targetGeneration = 0;
        nextSearchTime = 0f;
        smoothedAcceleration = Vector2.zero;
        smoothedDownwardAcceleration = 0f;
        injectedVelocity = Vector3.zero;
        predictionSteps = 0;
        debugCorrection = Vector3.zero;
    }

    internal void ApplyAssist(RunnerMovementMotor motor, ref Vector3 velocity, in MovementInputFrame input,
        float horizontalMultiplier, float deltaTime)
    {
        if (!landingAssistEnabled || assistStrength <= 0f || (!edgeAssistEnabled && !skimAssistEnabled)
            || !isActiveAndEnabled || !motor.CanAcceptMushroomLaunch
            || motor.IsGrounded || deltaTime <= 0f || Vector3.Dot(velocity, motor.UpDirection) >= -0.1f)
        {
            ResetAssist();
            return;
        }

        Vector3 up = motor.UpDirection;
        Vector3 forward = Vector3.ProjectOnPlane(input.ReferenceForward, up).normalized;
        if (forward.sqrMagnitude < 0.001f) forward = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;
        Vector3 right = Vector3.Cross(up, forward).normalized;
        Vector2 accelerationLimits = new Vector2(sidewaysAcceleration, forwardAcceleration) * assistStrength;
        Vector2 velocityLimits = new(maximumSidewaysVelocityCorrection, maximumForwardVelocityCorrection);
        Vector3 actualVelocity = motor.ScaleHorizontal(velocity, horizontalMultiplier);

        if (target != null && (!target.CanAssistLandingFor(motor) || target.LandingTargetGeneration != targetGeneration))
        {
            ClearTarget();
            nextSearchTime = 0f;
        }
        bool search = Time.time >= nextSearchTime;
        if (!search && target == null) return;

        predictionSteps = motor.PredictLandingTrajectory(velocity, input, horizontalMultiplier, predictionTime, trajectory);
        predictionStep = predictionTime / predictionSteps;
        searchCenter = motor.rigidBody.position;
        currentSearchRadius = searchRadius + actualVelocity.magnitude * predictionTime;

        Candidate selected = default;
        if (search)
        {
            using (SearchMarker.Auto())
            {
                nextSearchTime = Time.time + 1f / searchesPerSecond;
                selected = FindCandidate(motor, actualVelocity, up, right, forward, input, accelerationLimits, velocityLimits);
            }
            if (selected.Launcher != target)
            {
                smoothedAcceleration = Vector2.zero;
                smoothedDownwardAcceleration = 0f;
            }
            target = selected.Launcher;
            targetGeneration = target != null ? target.LandingTargetGeneration : 0;
        }
        else if (!EvaluateCandidate(target, motor, actualVelocity, up, right, forward, input,
            accelerationLimits, velocityLimits, out selected, out bool safe) || safe
            || IsObstructed(motor, selected.LandingPosition))
        {
            ClearTarget();
            return;
        }

        if (target == null) return;
        using (CorrectionMarker.Auto())
        {
            Vector2 error = ToAxes(selected.Error, right, forward);
            Vector2 step = LandingAssistMath.CalculateVelocityStep(error, selected.Time, input.Move,
                accelerationLimits, velocityLimits, ToAxes(injectedVelocity, right, forward), neutralInputStrength,
                correctionResponse, deltaTime, ref smoothedAcceleration);
            Vector3 before = Vector3.ProjectOnPlane(actualVelocity, up);
            Vector3 after = before + right * step.x + forward * step.y;
            // Never fight braking, reverse forward travel, or increase an existing overspeed.
            if (input.BrakeAmount > 0f && Vector3.Dot(after - before, forward) > 0f)
                after -= forward * Vector3.Dot(after - before, forward);
            if (Vector3.Dot(before, forward) >= 0f && Vector3.Dot(after, forward) < 0f)
                after -= forward * Vector3.Dot(after, forward);
            after = Vector3.ClampMagnitude(after, Mathf.Max(before.magnitude, motor.CurrentMaxSpeed));
            Vector3 applied = after - before;
            injectedVelocity += applied;
            velocity += applied / Mathf.Max(0.01f, horizontalMultiplier);
            float drop = Mathf.Max(0f, -Vector3.Dot(selected.Error, up));
            if (skimAssistEnabled && drop > 0f)
            {
                float downwardStep = LandingAssistMath.CalculateDownwardVelocityStep(drop, selected.Time,
                    downwardAcceleration * assistStrength * SkimIntentWeight(target, motor, input, up, forward),
                    maximumDownwardVelocityCorrection, Mathf.Max(0f, -Vector3.Dot(injectedVelocity, up)),
                    correctionResponse, deltaTime, ref smoothedDownwardAcceleration);
                velocity -= up * downwardStep;
                injectedVelocity -= up * downwardStep;
            }
            else smoothedDownwardAcceleration = 0f;
            debugLandingPoint = selected.LandingPosition;
            debugCorrection = selected.Error;
        }
    }

    Candidate FindCandidate(RunnerMovementMotor motor, Vector3 velocity, Vector3 up, Vector3 right,
        Vector3 forward, in MovementInputFrame input, Vector2 accelerationLimits, Vector2 velocityLimits)
    {
        Candidate best = default;
        if (target != null && EvaluateCandidate(target, motor, velocity, up, right, forward, input,
            accelerationLimits, velocityLimits, out Candidate current, out bool currentSafe))
        {
            if (currentSafe) return default;
            if (!IsObstructed(motor, current.LandingPosition)) best = current;
        }
        var candidates = MushroomLauncher.ActiveLandingTargets;
        for (int index = 0; index < candidates.Count; index++)
        {
            MushroomLauncher candidate = candidates[index];
            if (candidate == target) continue;
            if (!EvaluateCandidate(candidate, motor, velocity, up, right, forward, input,
                accelerationLimits, velocityLimits, out Candidate evaluated, out bool safe)) continue;
            // A naturally successful trajectory always takes priority over rescuing another cap.
            if (safe) return default;
            float threshold = best.Launcher == target && target != null ? best.Score * 0.8f : best.Score;
            if (best.Launcher != null && evaluated.Score >= threshold) continue;
            if (!IsObstructed(motor, evaluated.LandingPosition)) best = evaluated;
        }
        return best;
    }

    bool EvaluateCandidate(MushroomLauncher launcher, RunnerMovementMotor motor, Vector3 velocity,
        Vector3 up, Vector3 right, Vector3 forward, in MovementInputFrame input,
        Vector2 accelerationLimits, Vector2 velocityLimits, out Candidate best, out bool safe)
    {
        best = default;
        safe = false;
        if (launcher == null || !launcher.CanAssistLandingFor(motor) || launcher.gameObject.scene != motor.gameObject.scene) return false;
        Collider zone = launcher.NormalLandingCollider;
        Bounds bounds = zone.bounds;
        if (bounds.SqrDistance(searchCenter) > currentSearchRadius * currentSearchRadius
            || Vector3.Dot(bounds.center - searchCenter, up) > 0f) return false;
        Vector3 travel = Vector3.ProjectOnPlane(velocity, up).normalized;
        if (travel.sqrMagnitude < 0.001f) travel = forward;
        if (Vector3.Dot(bounds.center - searchCenter, travel) < -maximumEdgeMiss) return false;
        Bounds expanded = bounds;
        expanded.Expand(Mathf.Max(maximumEdgeMiss, skimAssistEnabled ? maximumSkimHeight : 0f) * 2f);
        Collider[] bodies = motor.LandingBodyColliders;
        for (int bodyIndex = 0; bodyIndex < bodies.Length; bodyIndex++)
        {
            Collider body = bodies[bodyIndex];
            if (body == null || body.isTrigger || !body.enabled || !body.gameObject.activeInHierarchy
                || body.attachedRigidbody != motor.rigidBody) continue;
            Bounds bodyBounds = body.bounds;
            for (int index = 1; index <= predictionSteps; index++)
            {
                Vector3 start = trajectory[index - 1];
                Vector3 segment = trajectory[index] - start;
                Vector3 bodyCenter = bodyBounds.center + start - searchCenter;
                float fraction = segment.sqrMagnitude > 0.000001f
                    ? Mathf.Clamp01(Vector3.Dot(bounds.center - bodyCenter, segment) / segment.sqrMagnitude) : 1f;
                // Check the segment's closest approach as well as its endpoint at high speeds.
                for (int sample = 0; sample < 2; sample++)
                {
                    float along = sample == 0 ? fraction : 1f;
                    float time = (index - 1 + along) * predictionStep;
                    if (time < 0.02f) continue;
                    Vector3 position = start + segment * along;
                    Vector3 delta = position - searchCenter;
                    Bounds translated = bodyBounds;
                    translated.center += delta;
                    if (!expanded.Intersects(translated) || Vector3.Dot(translated.center - bounds.center, up) < 0f) continue;
                    Vector3 bodyPosition = body.transform.position + delta;
                    if (Overlaps(body, bodyPosition, zone))
                    {
                        safe = true;
                        return true;
                    }
                    Vector3 onZone = zone.ClosestPoint(translated.center);
                    Vector3 onBody = body.ClosestPoint(onZone - delta) + delta;
                    onZone = zone.ClosestPoint(onBody);
                    Vector3 gap = onZone - onBody;
                    if (gap.sqrMagnitude < 0.000001f)
                    {
                        safe = true;
                        return true;
                    }
                    float verticalGap = Vector3.Dot(gap, up);
                    Vector3 planarGap = Vector3.ProjectOnPlane(gap, up);
                    float miss = planarGap.magnitude;
                    if (miss > maximumEdgeMiss) continue;
                    Vector3 planarCorrection = miss > 0.0001f ? planarGap + planarGap / miss * 0.015f : Vector3.zero;
                    if (planarCorrection.magnitude > maximumEdgeMiss) continue;
                    if (edgeAssistEnabled && miss > 0.0001f && Mathf.Abs(verticalGap) <= 0.05f)
                    {
                        ConsiderCorrection(launcher, motor, body, bodyPosition, zone, position, planarCorrection,
                            time, up, right, forward, input, accelerationLimits, velocityLimits, ref best);
                    }
                    float drop = -verticalGap;
                    if (skimAssistEnabled && drop > 0.0001f && drop <= maximumSkimHeight
                        && (edgeAssistEnabled || miss <= 0.0001f))
                    {
                        Vector3 correction = planarCorrection - up * (drop + 0.015f);
                        ConsiderCorrection(launcher, motor, body, bodyPosition, zone, position, correction,
                            time, up, right, forward, input, accelerationLimits, velocityLimits, ref best);
                    }
                }
            }
        }
        return best.Launcher != null;
    }

    void ConsiderCorrection(MushroomLauncher launcher, RunnerMovementMotor motor, Collider body,
        Vector3 bodyPosition, Collider zone, Vector3 predictedPosition, Vector3 correction, float time,
        Vector3 up, Vector3 right, Vector3 forward, in MovementInputFrame input,
        Vector2 accelerationLimits, Vector2 velocityLimits, ref Candidate best)
    {
        if (!LandingAssistMath.CanReach(ToAxes(correction, right, forward), time, input.Move,
            accelerationLimits, velocityLimits, ToAxes(injectedVelocity, right, forward), neutralInputStrength)) return;
        float drop = Mathf.Max(0f, -Vector3.Dot(correction, up));
        if (drop > 0f && !LandingAssistMath.CanReachDownward(drop, time,
            downwardAcceleration * assistStrength * SkimIntentWeight(launcher, motor, input, up, forward),
            maximumDownwardVelocityCorrection, Mathf.Max(0f, -Vector3.Dot(injectedVelocity, up)))) return;
        float score = correction.magnitude + time * 0.05f;
        if (best.Launcher != null && score >= best.Score) return;
        // A proposed downward/horizontal move must genuinely overlap the landing collider.
        if (!Overlaps(body, bodyPosition + correction, zone)) return;
        best = new Candidate { Launcher = launcher, Error = correction,
            LandingPosition = predictedPosition + correction, Time = time, Score = score };
    }

    float SkimIntentWeight(MushroomLauncher launcher, RunnerMovementMotor motor,
        in MovementInputFrame input, Vector3 up, Vector3 forward)
    {
        if (!input.HasMoveInput) return neutralInputStrength;
        Vector3 toward = Vector3.ProjectOnPlane(launcher.NormalLandingCollider.bounds.center - motor.rigidBody.position, up).normalized;
        if (toward.sqrMagnitude < 0.001f) toward = forward;
        Vector3 wish = Vector3.ProjectOnPlane(input.WishDirection, up).normalized;
        return LandingAssistMath.InputWeight(1f, Vector3.Dot(wish, toward) * input.Magnitude, neutralInputStrength);
    }

    static bool Overlaps(Collider body, Vector3 position, Collider zone) => Physics.ComputePenetration(
        body, position, body.transform.rotation, zone, zone.transform.position, zone.transform.rotation, out _, out _);

    bool IsObstructed(RunnerMovementMotor motor, Vector3 landingPosition)
    {
        Vector3 offset = landingPosition - motor.rigidBody.position;
        float distance = offset.magnitude;
        if (distance < 0.001f) return false;
        int count = Physics.RaycastNonAlloc(motor.rigidBody.position, offset / distance, obstructionHits,
            Mathf.Max(0f, distance - 0.02f), obstructionLayers, QueryTriggerInteraction.Ignore);
        if (count == obstructionHits.Length) return true;
        for (int index = 0; index < count; index++)
        {
            Collider collider = obstructionHits[index].collider;
            if (collider != null && collider.attachedRigidbody != motor.rigidBody) return true;
        }
        return false;
    }

    void ClearTarget()
    {
        target = null;
        targetGeneration = 0;
        smoothedAcceleration = Vector2.zero;
        smoothedDownwardAcceleration = 0f;
        debugCorrection = Vector3.zero;
    }

    static Vector2 ToAxes(Vector3 value, Vector3 right, Vector3 forward) => new(
        Vector3.Dot(value, right), Vector3.Dot(value, forward));

    void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos) return;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(predictionSteps > 0 ? searchCenter : transform.position,
            predictionSteps > 0 ? currentSearchRadius : searchRadius);
        for (int index = 1; index <= predictionSteps; index++) Gizmos.DrawLine(trajectory[index - 1], trajectory[index]);
        if (target == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(debugLandingPoint, 0.1f);
        Gizmos.DrawLine(debugLandingPoint - debugCorrection, debugLandingPoint);
    }
}
