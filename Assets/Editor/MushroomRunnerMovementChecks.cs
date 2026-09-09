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
        TraceSet actual = RecordTraces(profile);
        Check(expected.traces.Count == actual.traces.Count, "Trace count");
        for (int i = 0; i < expected.traces.Count; i++)
        {
            Trace a = actual.traces[i], e = expected.traces[i];
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
                BounceMovementMath.ApplyAirAcceleration(ref velocity, profile, input, Vector3.up, step < 4, step >= 30 && step < 39, 0.02f);
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
            Set(motor, "planarSpeedFloor", 50f);
            Set(motor, "planarSpeedFloorDirection", Vector3.forward);
            ceiling = 15;
            Invoke(motor, "FixedUpdate");
            Near(motor.CurrentMaxSpeed, 15, 0, "Downshift");
            Near((float)Get(motor, "planarSpeedFloor"), 15, 0, "Downshift lowers retained floor");
            ceiling = 60;
            motor.rigidBody.linearVelocity = new Vector3(0, 0, 10);
            Invoke(motor, "FixedUpdate");
            Check(motor.Velocity.z <= 15.001f, "Upshift cannot restore old floor");
            motor.SetMotorEnabled(false);
            motor.RequestDash();
            Vector3 frozen = motor.Velocity;
            Invoke(motor, "FixedUpdate");
            Near(motor.Velocity, frozen, 0, "Disabled motor");
            motor.ResetMotion(Vector3.zero, Quaternion.identity);
            motor.SetMotorEnabled(true);
            int bounces = 0, dashes = 0, charges = 1;
            motor.Bounced += _ => { bounces++; charges = 1; };
            motor.Dashed += () => dashes++;
            motor.SetDashResourceHandler(() => charges > 0 && charges-- > 0);
            foreach (float vertical in new[] { -20f, 5f })
            {
                charges = 1;
                motor.rigidBody.linearVelocity = new Vector3(0, vertical, 10);
                motor.RequestDash();
                Invoke(motor, "FixedUpdate");
                Check(motor.Velocity.y > 0, "Air jump launches while rising/falling");
                motor.RequestDash();
                Invoke(motor, "FixedUpdate");
            }
            Check(dashes == 2 && charges == 0, "Air jump consumes once per charge");
            Check(motor.TryBounce(null, bounce), "Directed bounce accepted");
            Check(bounces == 1 && charges == 1, "Bounce publishes once and replenishes");

            // Exercise the contact path with a known candidate, independent of collision callback timing.
            object contacts = Get(motor, "bounceContacts");
            Check(!(bool)Get(contacts, "hasBounceCandidate"), "No stale candidate after directed bounce");
            SimpleValidationSurface responseSurface = new();
            Collider collider = surface.GetComponent<Collider>();
            Type candidateType = contacts.GetType().GetNestedType("BounceCandidate", BindingFlags.Public);
            Set(contacts, "lastBounceCandidate", Activator.CreateInstance(candidateType, responseSurface, collider, Vector3.zero, Vector3.up, Time.time, 1f));
            Set(contacts, "hasBounceCandidate", true);
            Invoke(motor, "FixedUpdate");
            Check(bounces == 2, "Contact bounce publishes once");
            Check(Physics.GetIgnoreCollision(go.GetComponent<Collider>(), collider), "Post-bounce collision ignore");
            Invoke(motor, "OnDisable");
            Check(!Physics.GetIgnoreCollision(go.GetComponent<Collider>(), collider), "Disable restores collisions");
            motor.ResetMotion(Vector3.one, Quaternion.identity);
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
    public BounceSurfaceResponse GetBounceResponse(in BounceContext context) => new(1, 0, 0, context.BaseJumpForce, 0, Vector3.forward, 1);
}
