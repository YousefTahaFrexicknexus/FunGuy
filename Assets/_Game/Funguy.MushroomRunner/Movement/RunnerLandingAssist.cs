using Unity.Profiling;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class RunnerLandingAssist : MonoBehaviour
{
    const int MaximumPredictionSteps = 48;
    static readonly ProfilerMarker SearchMarker = new("Funguy.LandingAssist.Search");
    static readonly ProfilerMarker CorrectionMarker = new("Funguy.LandingAssist.Correction");

    [SerializeField, Tooltip("Master switch for landing assistance. Disabled preserves ordinary movement.")]
    bool landingAssistEnabled = true;
    [SerializeField, Range(0f, 1f), Tooltip("Overall strength multiplier for both assist modes. Zero disables all assist work; acceleration settings below define full strength.")]
    float assistStrength = 1f;
    [SerializeField, Tooltip("Rescue small sideways/forward edge misses using horizontal corrections.")]
    bool edgeAssistEnabled = true;
    [Header("Detection")]
    [SerializeField, Min(0.1f), Tooltip("Base search radius. Movement speed extends it along the predicted trajectory.")]
    float searchRadius = 4f;
    [SerializeField, Range(0.05f, 0.5f)] float predictionTime = 0.5f;
    [SerializeField, Min(0f)] float maximumEdgeMiss = 1.5f;
    [SerializeField, Range(1f, 30f)] float searchesPerSecond = 30f;
    [SerializeField, Tooltip("Solid obstacles that block assistance. Trigger colliders and the player's own body are ignored.")]
    LayerMask obstructionLayers = Physics.DefaultRaycastLayers;
    [Header("Edge Assist Strength")]
    [SerializeField, Min(0f)] float sidewaysAcceleration = 60f;
    [SerializeField, Min(0f)] float forwardAcceleration = 45f;
    [SerializeField, Min(0f), Tooltip("Maximum net sideways speed supplied by assistance during one descent.")]
    float maximumSidewaysVelocityCorrection = 10f;
    [SerializeField, Min(0f), Tooltip("Maximum net forward/back speed supplied by assistance during one descent.")]
    float maximumForwardVelocityCorrection = 8f;
    [Header("Input and Smoothing")]
    [SerializeField, Range(0f, 1f), Tooltip("Both modes: fraction of assistance available with neutral input. Steering toward a landing permits more; steering away cancels it.")]
    float neutralInputStrength = 1f;
    [SerializeField, Min(0.1f), Tooltip("Both modes: how quickly the extra acceleration builds toward its target.")]
    float correctionResponse = 10f;
    [Header("Skim Above Assist")]
    [SerializeField, Tooltip("While descending toward a mushroom, add a controlled downward pull if the predicted path narrowly passes above it.")]
    bool skimAssistEnabled = true;
    [SerializeField, Min(0f), Tooltip("Maximum predicted vertical gap above the actual landing collider that skim assist may rescue, in world units.")]
    float maximumSkimHeight = 0.75f;
    [SerializeField, Min(0f), Tooltip("Maximum extra downward acceleration at full strength, in world units per second squared. Steering intent and Neutral Input Strength also scale it.")]
    float downwardAcceleration = 40f;
    [SerializeField, Min(0f), Tooltip("Maximum additional falling speed supplied by skim assist during one descent, in world units per second.")]
    float maximumDownwardVelocityCorrection = 8f;
    [Header("Debug")]
    [SerializeField, Tooltip("Draw the player marker, search, trajectory, target and status in the Scene view, even when unselected. Enable the Scene view Gizmos toggle.")]
    bool drawDebugGizmos = true;
    [SerializeField, Tooltip("Show the moving prediction as a thick magenta line in the Game view during Editor Play mode. This debug overlay is excluded from builds and does not need the Game view Gizmos toggle.")]
    bool showPredictionInGameView = true;
#if UNITY_EDITOR
    readonly Vector3[] debugTrajectory = new Vector3[MaximumPredictionSteps + 1];
    int debugPredictionSteps;
    RunnerMovementMotor debugMotor;
    LineRenderer debugLine;
    LineRenderer debugOutline;
    Material debugLineMaterial;
#endif

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
    float debugPredictionAt = float.NegativeInfinity;
    float debugCorrectionAt = float.NegativeInfinity;
    public string DebugStatus { get; private set; } = "Waiting for movement";
    public Vector3 LastAppliedVelocityStep { get; private set; }

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

    [ContextMenu("Apply Strong Landing Assist Preset")]
    public void ApplyStrongPreset()
    {
        landingAssistEnabled = edgeAssistEnabled = skimAssistEnabled = drawDebugGizmos = true;
        assistStrength = neutralInputStrength = 1f;
        searchRadius = 8f;
        predictionTime = 0.5f;
        maximumEdgeMiss = 1.5f;
        searchesPerSecond = 30f;
        sidewaysAcceleration = 60f;
        forwardAcceleration = 45f;
        maximumSidewaysVelocityCorrection = 10f;
        maximumForwardVelocityCorrection = 8f;
        correctionResponse = 20f;
        maximumSkimHeight = 0.75f;
        downwardAcceleration = 40f;
        maximumDownwardVelocityCorrection = 8f;
        ResetAssist();
    }

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

    void OnDisable()
    {
        ResetAssist();
#if UNITY_EDITOR
        ReleasePredictionLine();
#endif
    }

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
        LastAppliedVelocityStep = Vector3.zero;
    }

    internal void ApplyAssist(RunnerMovementMotor motor, ref Vector3 velocity, in MovementInputFrame input,
        float horizontalMultiplier, float deltaTime)
    {
        LastAppliedVelocityStep = Vector3.zero;
        if (!landingAssistEnabled || assistStrength <= 0f || (!edgeAssistEnabled && !skimAssistEnabled)
            || !isActiveAndEnabled || !motor.CanAcceptMushroomLaunch
            || motor.IsGrounded || deltaTime <= 0f || Vector3.Dot(velocity, motor.UpDirection) >= -0.1f)
        {
            ResetAssist();
            DebugStatus = !landingAssistEnabled || assistStrength <= 0f ? "Disabled"
                : !edgeAssistEnabled && !skimAssistEnabled ? "Both assist modes disabled"
                : !motor.CanAcceptMushroomLaunch ? "Motor unavailable"
                : motor.IsGrounded ? "Grounded" : "Waiting to descend";
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
        debugPredictionAt = Time.realtimeSinceStartup;
        DebugStatus = "Searching for a reachable landing";

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
            || IsObstructed(motor, selected.LandingPosition, selected.Launcher))
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
            LastAppliedVelocityStep = applied;
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
                LastAppliedVelocityStep -= up * downwardStep;
            }
            else smoothedDownwardAcceleration = 0f;
            debugLandingPoint = selected.LandingPosition;
            debugCorrection = selected.Error;
            debugCorrectionAt = Time.realtimeSinceStartup;
            DebugStatus = $"Assisting {target.name} | {selected.Time:F2}s | extra speed {injectedVelocity.magnitude:F2}";
        }
    }

    Candidate FindCandidate(RunnerMovementMotor motor, Vector3 velocity, Vector3 up, Vector3 right,
        Vector3 forward, in MovementInputFrame input, Vector2 accelerationLimits, Vector2 velocityLimits)
    {
        Candidate best = default;
        if (target != null && EvaluateCandidate(target, motor, velocity, up, right, forward, input,
            accelerationLimits, velocityLimits, out Candidate current, out bool currentSafe))
        {
            if (currentSafe) { DebugStatus = "Already on course to land"; return default; }
            if (!IsObstructed(motor, current.LandingPosition, current.Launcher)) best = current;
        }
        var candidates = MushroomLauncher.ActiveLandingTargets;
        for (int index = 0; index < candidates.Count; index++)
        {
            MushroomLauncher candidate = candidates[index];
            if (candidate == target) continue;
            if (!EvaluateCandidate(candidate, motor, velocity, up, right, forward, input,
                accelerationLimits, velocityLimits, out Candidate evaluated, out bool safe)) continue;
            // A naturally successful trajectory always takes priority over rescuing another cap.
            if (safe) { DebugStatus = "Already on course to land"; return default; }
            float threshold = best.Launcher == target && target != null ? best.Score * 0.8f : best.Score;
            if (best.Launcher != null && evaluated.Score >= threshold) continue;
            if (!IsObstructed(motor, evaluated.LandingPosition, evaluated.Launcher)) best = evaluated;
        }
        if (best.Launcher == null) DebugStatus = $"No reachable landing ({candidates.Count} active targets)";
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
                // Also sample where the bottom of the body crosses the cap's top.
                // Endpoints alone can skip the entire edge-rescue band in one physics tick.
                float verticalTravel = Vector3.Dot(segment, up);
                float top = Vector3.Dot(bounds.center, up) + ProjectedExtent(bounds, up);
                float bottom = Vector3.Dot(bodyCenter, up) - ProjectedExtent(bodyBounds, up);
                float crossing = verticalTravel < -0.0001f ? Mathf.Clamp01((top - bottom) / verticalTravel) : 1f;
                for (int sample = 0; sample < 3; sample++)
                {
                    float along = sample == 0 ? fraction : sample == 1 ? crossing : 1f;
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

    static float ProjectedExtent(Bounds bounds, Vector3 axis) =>
        Mathf.Abs(axis.x) * bounds.extents.x + Mathf.Abs(axis.y) * bounds.extents.y + Mathf.Abs(axis.z) * bounds.extents.z;

    bool IsObstructed(RunnerMovementMotor motor, Vector3 landingPosition, MushroomLauncher landingTarget)
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
            if (collider == null || collider.attachedRigidbody == motor.rigidBody) continue;
            // A cap's own solid presentation collider is the destination, not a wall.
            if (landingTarget != null && collider.GetComponentInParent<MushroomLauncher>() == landingTarget) continue;
            DebugStatus = $"Blocked by {collider.name}";
            return true;
        }
        return false;
    }

    void ClearTarget()
    {
        target = null;
        targetGeneration = 0;
        smoothedAcceleration = Vector2.zero;
        smoothedDownwardAcceleration = 0f;
    }

    static Vector2 ToAxes(Vector3 value, Vector3 right, Vector3 forward) => new(
        Vector3.Dot(value, right), Vector3.Dot(value, forward));

#if UNITY_EDITOR
    // Preview every rendered frame, including ascent, dash and frames without a target.
    // Keep its buffer separate from the trajectory used by the assist's physics tick.
    void LateUpdate()
    {
        if (debugMotor == null) debugMotor = GetComponent<RunnerMovementMotor>();
        if (!drawDebugGizmos || !Application.isPlaying || debugMotor == null || !debugMotor.CanAcceptMushroomLaunch)
        {
            debugPredictionSteps = 0;
            if (debugLine != null) debugLine.enabled = false;
            if (debugOutline != null) debugOutline.enabled = false;
            return;
        }
        debugPredictionSteps = debugMotor.PredictDebugLandingTrajectory(predictionTime, debugTrajectory);
        if (!showPredictionInGameView)
        {
            if (debugLine != null) debugLine.enabled = false;
            if (debugOutline != null) debugOutline.enabled = false;
            return;
        }
        if (debugLine == null)
        {
            Shader shader = Shader.Find("Hidden/Funguy/LandingPrediction");
            if (shader == null) return;
            debugLineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            debugOutline = CreatePredictionLine("Prediction outline (Editor only)", Color.black, 0.18f, 100);
            debugLine = CreatePredictionLine("Prediction path (Editor only)", new Color(1f, 0.05f, 0.8f), 0.09f, 101);
        }
        UpdatePredictionLine(debugOutline);
        UpdatePredictionLine(debugLine);
    }

    LineRenderer CreatePredictionLine(string objectName, Color color, float width, int order)
    {
        var lineObject = new GameObject(objectName) { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        lineObject.transform.SetParent(transform, false);
        var line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = debugLineMaterial;
        line.useWorldSpace = true;
        line.startColor = line.endColor = color;
        line.startWidth = line.endWidth = width;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.sortingOrder = order;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        return line;
    }

    void UpdatePredictionLine(LineRenderer line)
    {
        line.enabled = true;
        line.positionCount = debugPredictionSteps + 1;
        for (int index = 0; index <= debugPredictionSteps; index++) line.SetPosition(index, debugTrajectory[index]);
    }

    void ReleasePredictionLine()
    {
        if (debugLine != null) DestroyDebugObject(debugLine.gameObject);
        if (debugOutline != null) DestroyDebugObject(debugOutline.gameObject);
        if (debugLineMaterial != null) DestroyDebugObject(debugLineMaterial);
        debugLine = debugOutline = null;
        debugLineMaterial = null;
        debugPredictionSteps = 0;
    }

    static void DestroyDebugObject(Object value)
    {
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }

    // Called by the Editor DrawGizmo hook, including when the Inspector is collapsed.
    public void DrawAssistGizmos()
    {
        if (!drawDebugGizmos) return;
        var previousDepth = UnityEditor.Handles.zTest;
        Color previousColor = UnityEditor.Handles.color;
        UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;
        try
        {
            Vector3 player = transform.position;
            var motor = GetComponent<RunnerMovementMotor>();
            Vector3 up = motor != null ? motor.UpDirection : Vector3.up;
            UnityEditor.Handles.color = Color.cyan;
            UnityEditor.Handles.DrawWireDisc(player, up, 0.25f);
            UnityEditor.Handles.DrawWireDisc(player, up, maximumEdgeMiss);
            UnityEditor.Handles.Label(player + up * 0.6f, "LANDING ASSIST\n" +
                (!Application.isPlaying ? (landingAssistEnabled ? "Enabled - preview (play to predict)" : "Disabled") : DebugStatus));
            bool recentPrediction = Application.isPlaying && Time.realtimeSinceStartup - debugPredictionAt < 0.75f;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
            Gizmos.DrawWireSphere(recentPrediction ? searchCenter : player, recentPrediction ? currentSearchRadius : searchRadius);
            if (Application.isPlaying && debugPredictionSteps > 0)
            {
                // Black outline keeps the prediction visible against the sky and grid gizmos.
                for (int index = 1; index <= debugPredictionSteps; index++)
                {
                    UnityEditor.Handles.color = Color.black;
                    UnityEditor.Handles.DrawAAPolyLine(10f, debugTrajectory[index - 1], debugTrajectory[index]);
                    UnityEditor.Handles.color = new Color(1f, 0.05f, 0.8f);
                    UnityEditor.Handles.DrawAAPolyLine(5f, debugTrajectory[index - 1], debugTrajectory[index]);
                }
                Vector3 end = debugTrajectory[debugPredictionSteps];
                UnityEditor.Handles.DrawWireDisc(end, up, 0.25f);
                UnityEditor.Handles.Label(end + up * 0.3f, $"Prediction +{predictionTime:F2}s");
            }
            if (Application.isPlaying && Time.realtimeSinceStartup - debugCorrectionAt < 0.75f)
            {
                UnityEditor.Handles.color = Color.green;
                UnityEditor.Handles.DrawAAPolyLine(5f, player, debugLandingPoint);
                UnityEditor.Handles.DrawWireDisc(debugLandingPoint, up, 0.2f);
                UnityEditor.Handles.color = Color.yellow;
                UnityEditor.Handles.DrawAAPolyLine(5f, debugLandingPoint - debugCorrection, debugLandingPoint);
                UnityEditor.Handles.Label(debugLandingPoint + up * 0.3f, $"Rescue {debugCorrection.magnitude:F2}m");
            }
        }
        finally
        {
            UnityEditor.Handles.zTest = previousDepth;
            UnityEditor.Handles.color = previousColor;
        }
    }
#endif
}
