using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Run from Tools > Mushroom Runner, or Unity -batchmode -executeMethod MushroomRunnerMovementChecks.Run.
public static class MushroomRunnerMovementChecks
{
    const string ProfilePath = "Assets/_Game/Funguy.MushroomRunner/ScriptableObjects/Config/Presets/BalancedMomentum.asset";
    const string FixturePath = "Assets/Editor/MovementTests/BalancedMomentumBaseline.json";
    static int assertions;

    [Serializable] public class TraceSet { public List<Trace> traces = new(); }
    [Serializable] public class Trace
    {
        public string name;
        public List<Vector3> samples = new();
        public float apex;
        public float duration;
        public Vector3 position;
    }

    [MenuItem("Tools/Mushroom Runner/Validate Movement and Gears")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        assertions = 0;
        MovementTuningProfile profile = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<MovementTuningProfile>(ProfilePath));
        try
        {
            CheckBaseline(profile);
            CheckResponsiveControls(profile);
            CheckGears(profile);
            CheckMotor(profile);
            CheckScoreBinding(profile);
            CheckScoring();
            CheckReach(profile);
            CheckScenes();
            Debug.Log($"MOVEMENT CHECKS PASSED: {assertions} assertions.");
        }
        finally { UnityEngine.Object.DestroyImmediate(profile); }
    }

    static void CheckBaseline(MovementTuningProfile profile)
    {
        TraceSet expected = JsonUtility.FromJson<TraceSet>(File.ReadAllText(FixturePath));
        // Isolate unchanged gravity/launch formulas using the original resistance. Live drag is tested below.
        float currentDrag = profile.AirDrag;
        Set(profile, "airDrag", .18f);
        TraceSet actual;
        try { actual = RecordTraces(profile); }
        finally { Set(profile, "airDrag", currentDrag); }
        Check(expected.traces.Count == actual.traces.Count, "Trace count");
        for (int i = 0; i < expected.traces.Count; i++)
        {
            Trace a = actual.traces[i], e = expected.traces[i];
            // These heading/braking cases deliberately changed; behavioral checks below replace their old traces.
            if (e.name == "diagonal" || e.name == "reverse" || e.name == "brake") continue;
            Check(a.name == e.name && a.samples.Count == e.samples.Count, e.name + " shape");
            for (int j = 0; j < e.samples.Count; j++) Near(a.samples[j], e.samples[j], 0.002f, e.name + " sample " + j);
            Near(a.position, e.position, 0.005f, e.name + " distance");
            Near(a.apex, e.apex, 0.002f, e.name + " apex");
            Near(a.duration, e.duration, 0.0001f, e.name + " duration");
        }
    }

    // Fixed inputs and 0.02-second steps isolate the movement formulas from editor frame timing.
    public static TraceSet RecordTraces(MovementTuningProfile profile)
    {
        TraceSet result = new();
        Vector3[] directions = { Vector3.forward, new Vector3(1, 0, 1).normalized, Vector3.back, Vector3.back, Vector3.zero };
        for (int scenario = 0; scenario < directions.Length; scenario++)
        {
            Trace trace = new() { name = new[] { "forward", "diagonal", "reverse", "brake", "coast" }[scenario] };
            Vector3 velocity = new(0, 0, 20);
            for (int step = 0; step < 180; step++)
            {
                MovementInputFrame input = new(new Vector2(0, scenario == 3 ? -1 : 1), directions[scenario], Vector3.forward, scenario == 4 ? 0 : 1, false);
                BounceMovementMath.ApplyAirMovement(ref velocity, profile, input, Vector3.up, step < 4, step >= 30 && step < 39, 0.02f);
                BounceMovementMath.ApplyPlanarDrag(ref velocity, Vector3.up, profile.AirDrag, 0.02f);
                BounceMovementMath.ApplySoftSpeedLimit(ref velocity, profile, Vector3.up, 48f, 0.02f);
                trace.position += velocity * 0.02f;
                if (step % 10 == 0) trace.samples.Add(velocity);
            }
            result.traces.Add(trace);
        }
        foreach (float speed in new[] { 10f, 20f, 40f, 65f })
        for (int surface = 0; surface < 3; surface++)
        {
            Trace trace = new() { name = $"flight-{speed}-{surface}" };
            BounceSurfaceResponse response = new(surface == 2 ? 0.7f : 1f, 0.45f, surface == 1 ? 8f : 0f, profile.BaseJumpForce, 0.25f, Vector3.forward, 0.85f, surface == 2, 3f);
            Vector3 velocity = BounceMovementMath.ApplyBounceResponse(new Vector3(0, -12, speed), response, profile, Vector3.up);
            BounceFlightShapeState shape = BounceMovementMath.CreateBounceFlightShapeState(velocity, profile, Vector3.up);
            trace.samples.Add(velocity);
            for (int step = 0; step < 400; step++)
            {
                if (!BounceMovementMath.ApplyBounceFlightShaper(ref velocity, profile, Vector3.up, ref shape, 0.02f))
                    BounceMovementMath.ApplyShapedGravity(ref velocity, profile, Vector3.up, 0.02f);
                BounceMovementMath.ApplyPlanarDrag(ref velocity, Vector3.up, surface == 2 ? 3f : profile.AirDrag, 0.02f);
                BounceMovementMath.ApplySoftSpeedLimit(ref velocity, profile, Vector3.up, 48f, 0.02f);
                trace.position += velocity * 0.02f;
                trace.apex = Mathf.Max(trace.apex, trace.position.y);
                trace.duration += 0.02f;
                if (step % 10 == 0) trace.samples.Add(velocity);
                if (trace.position.y < 0) break;
            }
            trace.samples.Add(velocity);
            result.traces.Add(trace);
        }
        return result;
    }

    static MovementInputFrame Input(float x, float y)
    {
        Vector2 move = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        return new MovementInputFrame(move, new Vector3(move.x, 0, move.y).normalized, Vector3.forward, move.magnitude, false);
    }

    static void CheckResponsiveControls(MovementTuningProfile profile)
    {
        foreach (float speed in new[] { 15f, 25f, 40f, 60f })
        {
            Vector3 velocity = new(0, 7, speed);
            for (int i = 0; i < 30; i++)
                BounceMovementMath.ApplyAirMovement(ref velocity, profile, Input(1, 0), Vector3.up, true, false, .01f);
            Near(velocity.x, profile.StrafeSpeed * (1 - Mathf.Exp(-.3f / profile.SteeringResponse)), .001f, "Bounded lateral aiming at every gear");
            Near(velocity.z, speed, 0, "Sideways aiming preserves forward carry");
            Near(velocity.y, 7, 0, "Sideways aiming preserves vertical speed");
            Vector3 half = new(0, 0, speed);
            BounceMovementMath.ApplyLateralControl(ref half, profile, Input(.5f, 0), Vector3.up, .3f);
            Near(half.x, velocity.x * .5f, .001f, "Analog input scales sideways speed");
            Vector3 countersteer = velocity;
            BounceMovementMath.ApplyLateralControl(ref countersteer, profile, Input(-1, 0), Vector3.up, .07f);
            Check(countersteer.x < 0, "Countersteer reverses lateral motion within .07 seconds");
            float drift = 0;
            for (int i = 0; i < 100; i++)
            {
                BounceMovementMath.ApplyAirMovement(ref velocity, profile, MovementInputFrame.Empty, Vector3.up, false, false, .01f);
                drift += velocity.x * .01f;
            }
            Check(drift < 1.2f && velocity.x < .001f, "Release catches sideways drift within a mushroom-sized window");
            Near(velocity.z, speed, 0, "Release does not convert lost sideways speed into forward speed");
            velocity = new Vector3(0, 7, speed);
            for (int i = 0; i < 1000; i++)
                BounceMovementMath.ApplyAirMovement(ref velocity, profile, Input(i % 20 < 10 ? 1 : -1, 0), Vector3.up, false, true, .01f);
            Near(velocity.z, speed, .0001f, "Repeated corrections cannot manufacture forward speed");
            Check(Mathf.Abs(velocity.x) <= profile.StrafeSpeed, "Repeated corrections remain bounded");
            Vector3 rotated = Vector3.right * speed + Vector3.up * 7;
            MovementInputFrame rotatedInput = new(new Vector2(1, 0), Vector3.back, Vector3.right, 1, false);
            BounceMovementMath.ApplyLateralControl(ref rotated, profile, rotatedInput, Vector3.up, .1f);
            Check(rotated.z < 0 && rotated.x == speed && rotated.y == 7, "Aiming uses camera-relative axes");

            foreach (float amount in new[] { 1f, .5f, .001f })
            foreach (int steps in new[] { 1, 25 })
            {
                velocity = new Vector3(0, 7, speed);
                for (int i = 0; i < steps; i++)
                    BounceMovementMath.ApplyAirMovement(ref velocity, profile, Input(0, -amount), Vector3.up, true, true, .25f / steps);
                Near(velocity.z, speed * Mathf.Exp(-amount), .0002f, "Exponential brake across gears, analog strengths and step sizes");
                Near(velocity.x, 0, 0, "Back input never turns backwards");
                Near(velocity.y, 7, 0, "Horizontal brake leaves vertical speed to dive");
            }
            velocity = Vector3.forward * speed;
            BounceMovementMath.ApplyAirMovement(ref velocity, profile, Input(1, -1), Vector3.up, true, false, .1f);
            Check(velocity.x > 0 && velocity.z > 0 && velocity.z < speed, "Backward diagonal aims sideways while braking forward carry");
        }
        foreach (float vertical in new[] { -12f, 12f })
        {
            Vector3 velocity = new(0, vertical, 40);
            float history = BounceMovementMath.ApplyDive(ref velocity, profile, 1, Vector3.up, .25f);
            Near(history, 10, 0, "Dive adds 40 units per second squared");
            Near(velocity, new Vector3(0, vertical - 10, 40), 0, "Dive works rising and falling");
            Vector3 released = velocity;
            history += BounceMovementMath.ApplyDive(ref velocity, profile, 0, Vector3.up, .25f);
            Near(velocity, released, 0, "Release immediately stops extra pull");
            BounceSurfaceResponse response = new(1, 0, 0, 10, .3f, Vector3.forward, 1);
            Vector3 original = BounceMovementMath.ApplyBounceResponse(new Vector3(0, vertical, 40), response, profile, Vector3.up);
            Near(BounceMovementMath.ApplyBounceResponse(velocity, response, profile, Vector3.up, history), original, .0001f, "Dive does not manufacture impact recovery after release");
        }
        var standard = AssetDatabase.LoadAssetAtPath<MushroomBounceProfile>("Assets/_Game/Funguy.MushroomRunner/ScriptableObjects/Config/StandardMushroomBounceProfile.asset");
        Check(standard != null, "Standard mushroom asset");
        Vector3 incoming = new(0, -10, 40);
        var context = new BounceContext(incoming, Vector3.zero, Vector3.up, Vector3.up, profile.BaseJumpForce, MovementInputFrame.Empty);
        var outgoing = BounceMovementMath.ApplyBounceResponse(incoming, standard.CreateResponse(null, context), profile, Vector3.up);
        Near(Vector3.ProjectOnPlane(outgoing, Vector3.up).magnitude, 44.35f, .001f, "Standard bounce adds 1.35 plus 3 without doubling speed");
        // Ordinary bounces must still build a fast run once air resistance is real.
        Vector3 carry = new(0, -12, 40);
        for (int hop = 0; hop < 15; hop++)
        {
            var hopContext = new BounceContext(carry, Vector3.zero, Vector3.up, Vector3.up, profile.BaseJumpForce, MovementInputFrame.Empty);
            carry = BounceMovementMath.ApplyBounceResponse(carry, standard.CreateResponse(null, hopContext), profile, Vector3.up);
            var shape = BounceMovementMath.CreateBounceFlightShapeState(carry, profile, Vector3.up);
            float height = 0;
            bool landed = false;
            for (int step = 0; step < 400; step++)
            {
                BounceMovementMath.ApplyBounceFlightShaper(ref carry, profile, Vector3.up, ref shape, .02f);
                BounceMovementMath.ApplyAirMovement(ref carry, profile, MovementInputFrame.Empty, Vector3.up, false, false, .02f);
                BounceMovementMath.ApplyPlanarDrag(ref carry, Vector3.up, profile.AirDrag, .02f);
                BounceMovementMath.ApplySoftSpeedLimit(ref carry, profile, Vector3.up, 60, .02f);
                height += carry.y * .02f;
                if (height < 0) { landed = true; break; }
            }
            Check(landed, "Ordinary bounce returns to landing height");
        }
        Check(carry.z > 55 && carry.z <= 60, "Consecutive ordinary bounces still build high-gear speed with resistance");
        Debug.Log($"AIR GRIP BALANCE: 15 ordinary hops at x4 build 40 to {carry.z:F2} units/s, with effective drag.");
    }

    static void CheckGears(MovementTuningProfile profile)
    {
        float[] multipliers = { -5, 0, 1, 1.9f, 2, 3, 4, 5, 100, float.NaN, float.PositiveInfinity, float.NegativeInfinity };
        float[] expected = { 15, 15, 15, 15, 25, 40, 60, 60, 60, 15, 60, 15 };
        for (int i = 0; i < expected.Length; i++) Near(profile.GetMaxSpeed(multipliers[i]), expected[i], 0, "Gear lookup");
        foreach (float cap in new[] { 15f, 25f, 40f, 60f })
        {
            Vector3 velocity = new(39, -11, 52);
            Vector3 direction = new Vector3(velocity.x, 0, velocity.z).normalized;
            BounceMovementMath.ApplySoftSpeedLimit(ref velocity, profile, Vector3.up, cap, 0.02f);
            Near(velocity.y, -11, 0, "Cap preserves vertical speed");
            Near(new Vector3(velocity.x, 0, velocity.z).normalized, direction, 0.00001f, "Cap preserves direction");
            float speed = Vector3.ProjectOnPlane(velocity, Vector3.up).magnitude;
            Check(speed > cap && speed < 65, "Soft overspeed");
            velocity = new Vector3(0, 4, 10);
            BounceMovementMath.ApplySoftSpeedLimit(ref velocity, profile, Vector3.up, cap, 0.02f);
            Near(velocity, new Vector3(0, 4, 10), 0, "Upshift does not accelerate");
        }
        Set(profile, "speedGears", Array.Empty<float>());
        Near(profile.GetMaxSpeed(4), 60, 0, "Empty gears default");
        Set(profile, "speedGears", new[] { 20f, 20f, 12f, float.NaN, float.PositiveInfinity });
        Invoke(profile, "OnValidate");
        Near(profile.GetMaxSpeed(5), 20, 0, "Gear validation");
        Set(profile, "speedGears", new[] { 15f, 25f, 40f, 60f });
    }

    static void CheckMotor(MovementTuningProfile profile)
    {
        GameObject go = new("Movement validation player");
        go.AddComponent<SphereCollider>();
        RunnerMovementMotor motor = go.AddComponent<RunnerMovementMotor>();
        MushroomBounceProfile bounce = ScriptableObject.CreateInstance<MushroomBounceProfile>();
        GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            Invoke(motor, "Awake");
            motor.SetTuningProfile(profile);
            float ceiling = 60;
            motor.SetSpeedLimitProvider(() => ceiling);
            motor.rigidBody.linearVelocity = new Vector3(0, 3, 50);
            ceiling = 15;
            Invoke(motor, "FixedUpdate");
            Near(motor.CurrentMaxSpeed, 15, 0, "Downshift");
            ceiling = 60;
            motor.rigidBody.linearVelocity = new Vector3(0, 0, 10);
            Invoke(motor, "FixedUpdate");
            Check(motor.Velocity.z < 10, "Upshift cannot restore discarded speed or cancel drag");
            motor.SetMotorEnabled(false);
            motor.RequestDash();
            Vector3 frozen = motor.Velocity;
            Invoke(motor, "FixedUpdate");
            Near(motor.Velocity, frozen, 0, "Disabled motor");
            motor.ResetMotion(Vector3.zero, Quaternion.identity);
            motor.SetMotorEnabled(true);
            int bounces = 0, dashes = 0, charges = 1;
            BounceEventData lastBounce = default;
            motor.Bounced += data => { bounces++; charges = 1; lastBounce = data; };
            motor.Dashed += () => dashes++;
            motor.SetDashResourceHandler(() => charges > 0 && charges-- > 0);
            foreach (float vertical in new[] { -20f, 5f })
            {
                charges = 1;
                motor.rigidBody.linearVelocity = new Vector3(0, vertical, 10);
                Set(motor, "diveAddedDownSpeed", 8f);
                motor.RequestDash();
                Invoke(motor, "FixedUpdate");
                Near((float)Get(motor, "diveAddedDownSpeed"), 0, 0, "Air jump clears dive history");
                Check(motor.Velocity.y > 0, "Air jump launches while rising/falling");
                motor.RequestDash();
                Invoke(motor, "FixedUpdate");
            }
            Check(dashes == 2 && charges == 0, "Air jump consumes once per charge");
            motor.SetInput(Input(0, -1));
            motor.rigidBody.linearVelocity = new Vector3(0, -12, 40);
            Invoke(motor, "FixedUpdate");
            float brakedSpeed = motor.Velocity.z;
            Check((float)Get(motor, "diveAddedDownSpeed") > 0, "Motor records dive history");
            motor.SetInput(MovementInputFrame.Empty);
            Invoke(motor, "FixedUpdate");
            Check(motor.Velocity.z <= brakedSpeed + .0001f, "Brake release cannot restore discarded speed");
            Check((float)Get(motor, "diveAddedDownSpeed") > 0, "Release preserves dive history");
            float triggerDiveHistory = (float)Get(motor, "diveAddedDownSpeed");
            Check(motor.TryBounce(null, bounce), "Directed bounce accepted");
            Near(lastBounce.OutgoingVelocity.y, lastBounce.Response.UpwardImpulse + Mathf.Max(0, -lastBounce.IncomingVelocity.y - triggerDiveHistory) * lastBounce.Response.ImpactRecoveryFactor,
                .0001f, "Trigger bounce excludes accumulated dive recovery");
            Near((float)Get(motor, "diveAddedDownSpeed"), 0, 0, "Trigger bounce clears dive history");
            Check(bounces == 1 && charges == 1, "Bounce publishes once and replenishes");
            motor.rigidBody.linearVelocity = new Vector3(0, 10, 40);
            motor.SetInput(MovementInputFrame.Empty);
            Invoke(motor, "FixedUpdate");
            Near(motor.Velocity.z, 40 - profile.AirDrag * Time.fixedDeltaTime, .0001f, "Drag stays effective after a bounce");
            for (int i = 0; i < 49; i++) Invoke(motor, "FixedUpdate");
            Near(motor.Velocity.z, 40 - profile.AirDrag * Time.fixedDeltaTime * 50, .001f, "No hidden speed restoration during coasting");

            // Exercise the contact path with a known candidate, independent of collision callback timing.
            object contacts = Get(motor, "bounceContacts");
            Check(!(bool)Get(contacts, "hasBounceCandidate"), "No stale candidate after directed bounce");
            SimpleValidationSurface responseSurface = new();
            Collider collider = surface.GetComponent<Collider>();
            Type candidateType = contacts.GetType().GetNestedType("BounceCandidate", BindingFlags.Public);
            Set(contacts, "lastBounceCandidate", Activator.CreateInstance(candidateType, responseSurface, collider, Vector3.zero, Vector3.up, Time.time, 1f));
            Set(contacts, "hasBounceCandidate", true);
            motor.rigidBody.linearVelocity = new Vector3(0, -20, 40);
            Set(motor, "diveAddedDownSpeed", 8f);
            motor.SetInput(Input(-1, 0));
            Invoke(motor, "FixedUpdate");
            Near(lastBounce.OutgoingVelocity.y, lastBounce.Response.UpwardImpulse + Mathf.Max(0, -lastBounce.IncomingVelocity.y - 8) * lastBounce.Response.ImpactRecoveryFactor,
                .0001f, "Collision bounce excludes accumulated dive recovery");
            Near((float)Get(motor, "diveAddedDownSpeed"), 0, 0, "Collision bounce clears dive history");
            Check(motor.Velocity.x < lastBounce.OutgoingVelocity.x, "Turning is available on the collision-bounce step");
            Check(bounces == 2, "Contact bounce publishes once");
            Check(Physics.GetIgnoreCollision(go.GetComponent<Collider>(), collider), "Post-bounce collision ignore");
            Set(motor, "diveAddedDownSpeed", 8f);
            Invoke(motor, "OnDisable");
            Near((float)Get(motor, "diveAddedDownSpeed"), 0, 0, "Disable clears dive history");
            Check(!Physics.GetIgnoreCollision(go.GetComponent<Collider>(), collider), "Disable restores collisions");
            Set(motor, "diveAddedDownSpeed", 8f);
            motor.ResetMotion(Vector3.one, Quaternion.identity);
            Near((float)Get(motor, "diveAddedDownSpeed"), 0, 0, "Reset clears dive history");
            Near(motor.Velocity, Vector3.zero, 0, "Reset clears velocity");
            Check(!(bool)Get(contacts, "hasBounceCandidate"), "Reset clears contact");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(surface);
            UnityEngine.Object.DestroyImmediate(bounce);
        }
    }

    static void CheckScoreBinding(MovementTuningProfile profile)
    {
        GameObject go = new("Score binding validation");
        try
        {
            RunnerMovementMotor motor = go.AddComponent<RunnerMovementMotor>();
            MushroomRunnerPlayer player = go.AddComponent<MushroomRunnerPlayer>();
            MomentumSystem momentum = go.AddComponent<MomentumSystem>();
            DistanceScoreManager score = go.AddComponent<DistanceScoreManager>();
            Invoke(motor, "Awake");
            Invoke(player, "Awake");
            Set(score, "momentumSystem", momentum);
            player.SetTuningProfile(profile);
            player.BindScoreManager(score);
            foreach (MomentumTier tier in Enum.GetValues(typeof(MomentumTier)))
            {
                momentum.CurrentTier = tier;
                Invoke(motor, "FixedUpdate");
                Near(motor.CurrentMaxSpeed, profile.GetMaxSpeed(score.CurrentMultiplier), 0, "Score source gear");
            }
            player.BindScoreManager(null);
            Near(motor.CurrentMaxSpeed, 15, 0, "Unbound score uses x1");
            player.SetState(MushroomRunnerPlayer.PlayerState.Disabled);
            player.ResetRun(Vector3.zero, Quaternion.identity);
            Check(player.State == MushroomRunnerPlayer.PlayerState.Active, "Player restart");
            MovementTuningProfile alternative = ScriptableObject.CreateInstance<MovementTuningProfile>();
            try
            {
                Set(alternative, "speedGears", new[] { 22f });
                player.SetTuningProfile(alternative);
                Near(motor.CurrentMaxSpeed, 22, 0, "Runtime profile change");
            }
            finally { UnityEngine.Object.DestroyImmediate(alternative); }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static void CheckReach(MovementTuningProfile profile)
    {
        MushroomBounceProfile bounce = ScriptableObject.CreateInstance<MushroomBounceProfile>();
        try
        {
            foreach (float cap in new[] { 15f, 25f, 40f, 60f })
            {
                // Reproduce the evaluator's no-input coast to locate its expected landing window.
                Vector3 velocity = BounceMovementMath.ApplyBounceResponse(new Vector3(0, -12, 65), bounce.CreateResponse(null,
                    new BounceContext(new Vector3(0, -12, 65), Vector3.zero, Vector3.up, Vector3.up, profile.BaseJumpForce, MovementInputFrame.Empty)), profile, Vector3.up);
                BounceFlightShapeState shape = BounceMovementMath.CreateBounceFlightShapeState(velocity, profile, Vector3.up);
                Vector3 position = Vector3.zero;
                for (int i = 0; i < 400; i++)
                {
                    BounceMovementMath.ApplyBounceFlightShaper(ref velocity, profile, Vector3.up, ref shape, 0.02f);
                    if (i > 0)
                    {
                        BounceMovementMath.ApplyPlanarDrag(ref velocity, Vector3.up, profile.AirDrag, 0.02f);
                        BounceMovementMath.ApplySoftSpeedLimit(ref velocity, profile, Vector3.up, cap, 0.02f);
                    }
                    position += velocity * 0.02f;
                    if (position.y < 0) break;
                }
                BounceReachRequest request = new(Vector3.zero, new Vector3(0, -12, 65), new Vector3(0, 0, position.z), bounce, profile,
                    BounceIntentDirective.Maintain, Vector3.up, 0, 0, 3, 1, 0.02f, 8, cap);
                Check(BounceReachEvaluator.TryEvaluate(request, out _), "Reachable landing at gear " + cap);
                BounceReachRequest lateral = new(Vector3.zero, new Vector3(0, -12, 65), new Vector3(5, 0, position.z), bounce, profile,
                    BounceIntentDirective.Maintain, Vector3.up, 0, 0, 3, 1, .02f, 8, cap);
                Check(BounceReachEvaluator.TryEvaluate(lateral, out _), "Finite lateral correction reaches a nearby mushroom at gear " + cap);
                BounceReachRequest impossible = new(Vector3.zero, new Vector3(0, -12, 65), new Vector3(100, 0, position.z), bounce, profile,
                    BounceIntentDirective.Maintain, Vector3.up, 0, 0, 3, 1, .02f, 3, cap);
                Check(!BounceReachEvaluator.TryEvaluate(impossible, out _), "Prediction cannot bypass the sideways speed limit");
                BounceReachRequest coast = new(Vector3.zero, new Vector3(0, -12, cap), Vector3.forward * 100, bounce, profile,
                    BounceIntentDirective.Maintain, Vector3.up, 0, 0, 1000, .2f, .01f, 8, cap);
                BounceReachRequest dive = new(Vector3.zero, new Vector3(0, -12, cap), Vector3.forward * 100, bounce, profile,
                    BounceIntentDirective.Brake, Vector3.up, 0, 0, 1000, .2f, .01f, 8, cap);
                Check(BounceReachEvaluator.TryEvaluate(coast, out var coastResult), "Coasting prediction lands");
                Check(BounceReachEvaluator.TryEvaluate(dive, out var diveResult), "Diving prediction lands");
                Check(diveResult.FlightTime < coastResult.FlightTime && diveResult.LandingPosition.z < coastResult.LandingPosition.z,
                    "Brake prediction shortens flight and forward travel at every gear");
                Check(diveResult.DiveAddedDownSpeed > 0 && coastResult.DiveAddedDownSpeed == 0, "Prediction records only dive-added descent");
                BounceReachRequest next = new(Vector3.zero, diveResult.LandingVelocity, Vector3.forward * 100, bounce, profile,
                    BounceIntentDirective.Maintain, Vector3.up, 0, 0, 1000, .2f, .01f, 8, cap, diveResult.DiveAddedDownSpeed);
                Check(BounceReachEvaluator.TryEvaluate(next, out var nextResult), "Next hop carries dive history");
                var context = new BounceContext(diveResult.LandingVelocity, Vector3.zero, Vector3.up, Vector3.up, profile.BaseJumpForce, MovementInputFrame.Empty);
                Vector3 expectedLaunch = BounceMovementMath.ApplyBounceResponse(diveResult.LandingVelocity, bounce.CreateResponse(null, context), profile, Vector3.up, diveResult.DiveAddedDownSpeed);
                Near(nextResult.LaunchVelocity, expectedLaunch, .0001f, "Next predicted rebound excludes dive recovery");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(bounce); }
    }

    static void CheckScoring()
    {
        GameObject go = new("Scoring regression validation");
        Action<int> scoreListener = _ => { };
        Action<float> multiplierListener = _ => { };
        GameplayEvents.OnScoreChanged += scoreListener;
        GameplayEvents.OnMultiplierChanged += multiplierListener;
        try
        {
            MomentumSystem momentum = go.AddComponent<MomentumSystem>();
            DistanceScoreManager score = go.AddComponent<DistanceScoreManager>();
            Invoke(momentum, "Awake");
            Set(score, "momentumSystem", momentum);
            Set(score, "playerTransform", go.transform);
            momentum.BeginRun();
            score.BeginRun();
            momentum.OnMushroomLanded(LandingQuality.Perfect);
            momentum.OnMushroomLanded(LandingQuality.Good);
            momentum.OnMushroomLanded(LandingQuality.Bad);
            Near(momentum.CurrentMomentum, 17.5f, 0, "Existing landing rewards");
            momentum.OnMushroomLanded(LandingQuality.Perfect);
            momentum.OnMushroomLanded(LandingQuality.Perfect);
            momentum.Tick(20f, 1f);
            momentum.Tick(20f, 1f);
            Near(momentum.CurrentMomentum, 51.5f, 0, "Existing speed reward");
            Near(score.CurrentMultiplier, 3f, 0, "Existing tier multiplier");
            go.transform.position = new Vector3(0, 0, 10);
            Invoke(score, "Update");
            Check(score.CurrentScore == 300, "Existing distance score");
            momentum.SetPaused(true);
            score.SetPaused(true);
            momentum.Tick(20f, 1f);
            go.transform.position += Vector3.forward * 10;
            Invoke(score, "Update");
            Check(score.CurrentScore == 300, "Paused scoring");
            Near(momentum.CurrentMomentum, 51.5f, 0, "Paused momentum");
            momentum.SetPaused(false);
            score.SetPaused(false);
            go.transform.position += Vector3.forward;
            Invoke(score, "Update");
            Check(score.CurrentScore == 330, "Resume synchronizes distance");
            momentum.ResetMomentum();
            score.ResetScore();
            Near(score.CurrentMultiplier, 1, 0, "Reset multiplier");
            Check(score.CurrentScore == 0, "Reset score");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            GameplayEvents.OnScoreChanged -= scoreListener;
            GameplayEvents.OnMultiplierChanged -= multiplierListener;
        }
    }

    static void CheckScenes()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string path in new[] {
                "Assets/_Game/Funguy.MushroomRunner/Scenes/MushroomRunnerGameplay.unity",
                "Assets/_Game/Funguy.MushroomRunner/Scenes/MushroomRunnerGameplay 1.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "Missing script: " + t.name);
            }
        }
        finally
        {
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    static object Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    static void Near(float actual, float expected, float tolerance, string message) => Check(Mathf.Abs(actual - expected) <= tolerance, $"{message}: {actual} versus {expected}");
    static void Near(Vector3 actual, Vector3 expected, float tolerance, string message) => Check(Vector3.Distance(actual, expected) <= tolerance, $"{message}: {actual} versus {expected}");
    static void Check(bool success, string message)
    {
        assertions++;
        if (!success) throw new Exception(message);
    }
}

public sealed class SimpleValidationSurface : IBounceSurface, IBounceSurfaceBehavior
{
    public bool AllowsBounceWhileMovingUpward => true;
    public float PostBounceCollisionIgnoreDuration => 0.08f;
    public BounceSurfaceResponse GetBounceResponse(in BounceContext context) => new(1, 0, 0, context.BaseJumpForce, .3f, Vector3.forward, 1);
}
