using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Deterministic regression checks; test objects are isolated spatially and always cleaned up.</summary>
public static class RunnerModifierVerification
{
    static int checks;
    static readonly List<Object> temporary = new();
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < 0.002f, message + ": expected " + expected + ", got " + actual);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
    static void Call(object target, string method) => target.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
    static T Keep<T>(T value) where T : Object { temporary.Add(value); return value; }

    [MenuItem("Tools/Mushroom Runner/Verify Timed Modifiers")]
    public static void Run()
    {
        checks = 0;
        try
        {
            var profile = Keep(ScriptableObject.CreateInstance<MovementTuningProfile>());
            Set(profile, "maxSpeedX1", 100f); Set(profile, "airDrag", 0f); Set(profile, "gravityScale", 0f);
            Set(profile, "useBounceFlightShaper", false);
            var chill = Keep(ScriptableObject.CreateInstance<MushroomModifierDefinition>());
            var perfect = Keep(ScriptableObject.CreateInstance<MushroomModifierDefinition>());
            Set(perfect, "effectType", MushroomModifierType.PerfectLandings);
            var body = Keep(new GameObject("Modifier Verification Player"));
            body.transform.position = new Vector3(10000, 10000, 10000);
            body.AddComponent<SphereCollider>().radius = 0.5f;
            var motor = body.AddComponent<RunnerMovementMotor>();
            motor.rigidBody = body.GetComponent<Rigidbody>();
            motor.rigidBody.useGravity = false;
            motor.SetTuningProfile(profile);
            var effects = motor.Modifiers;
            motor.rigidBody.linearVelocity = new Vector3(4, 8, 20);
            Check(effects.Apply(chill), "Chill accepted");
            Near(motor.Velocity.z, 14, "Immediate chill"); Near(motor.Velocity.x, 2.8f, "Lateral chill"); Near(motor.Velocity.y, 8, "Vertical preserved");
            Near(motor.CurrentMaxSpeed, 70, "Effective cap"); Near(motor.UnmodifiedMaxSpeed, 100, "Nominal cap");
            effects.AdvanceTimers(2);
            effects.Apply(chill);
            Near(effects.GetRemaining(MushroomModifierType.Chilling), 10, "Refresh duration"); Near(motor.Velocity.z, 14, "Refresh does not compound");
            Launch(motor, new MushroomSpeedRule(MushroomSpeedMode.Add, 10));
            Near(motor.Velocity.z, 21, "20 plus 10 under chill"); Near(motor.Velocity.y, 30, "Launch height uses nominal speed");
            Launch(motor, new MushroomSpeedRule(MushroomSpeedMode.Set, 10)); Near(motor.Velocity.z, 7, "Set speed");
            Launch(motor, new MushroomSpeedRule(MushroomSpeedMode.Multiply, 2)); Near(motor.Velocity.z, 14, "Multiply speed");
            Check(MushroomLauncher.TryCalculateLaunch(motor.UnmodifiedVelocity, 1, new[] {
                new MushroomSpeedRule(MushroomSpeedMode.Set, 10), new MushroomSpeedRule(MushroomSpeedMode.Add, 5),
                new MushroomSpeedRule(MushroomSpeedMode.Multiply, 2) }, new Vector3(0, 1, 1), out var sequence), "Sequence valid");
            motor.ApplyMushroomLaunch(sequence, null, Vector3.zero, Vector3.up); Near(motor.Velocity.z, 21, "Ordered rules scale once");
            for (int i = 0; i < 20; i++) Call(motor, "FixedUpdate");
            Near(motor.Velocity.z, 21, "Repeated physics ticks do not compound");
            // An external collision changes the current velocity. Expiry must preserve that change.
            motor.rigidBody.linearVelocity = new Vector3(0, -3, 7);
            effects.AdvanceTimers(10);
            Near(motor.Velocity.z, 10, "Expiry preserves collision loss"); Near(motor.Velocity.y, -3, "Expiry preserves vertical");
            effects.Apply(chill); effects.AdvanceTimers(2); effects.Apply(perfect);
            Check(effects.ForcesPerfectLandings && effects.IsActive(MushroomModifierType.Chilling), "Effects coexist");
            effects.SetPaused(true); effects.AdvanceTimers(20);
            Near(effects.GetRemaining(MushroomModifierType.Chilling), 8, "Pause freezes timer");
            effects.SetPaused(false); effects.AdvanceTimers(8);
            Check(!effects.IsActive(MushroomModifierType.Chilling) && effects.ForcesPerfectLandings, "Independent expiry");
            effects.AdvanceTimers(2); Check(!effects.ForcesPerfectLandings, "Perfect expiry");
            effects.Apply(chill); motor.ResetMotion(body.transform.position, Quaternion.identity);
            Check(!effects.IsActive(MushroomModifierType.Chilling), "Reset clears effects"); Near(motor.Velocity.z, 0, "Reset clears speed");
            effects.Apply(chill); motor.SetMotorEnabled(false);
            Check(!effects.IsActive(MushroomModifierType.Chilling), "Motor disable clears effects"); motor.SetMotorEnabled(true);

            // Exercise actual launcher overlap, quality selection, duplicate suppression and atomic pickup.
            var mushroom = Keep(new GameObject("Modifier Verification Mushroom"));
            mushroom.transform.position = body.transform.position;
            var normal = mushroom.AddComponent<BoxCollider>(); normal.isTrigger = true; normal.size = Vector3.one * 4;
            var launcher = mushroom.AddComponent<MushroomLauncher>();
            var direction = new GameObject("Direction").transform; direction.SetParent(mushroom.transform, false); direction.forward = new Vector3(0, 1, 1);
            var good = Zone(mushroom.transform, "Good", 20); var perfectZone = Zone(mushroom.transform, "Perfect", 40);
            Set(launcher, "normalTrigger", normal); Set(launcher, "goodTrigger", good); Set(launcher, "perfectTrigger", perfectZone); Set(launcher, "launchDirection", direction);
            Set(launcher, "modifier", perfect);
            Set(launcher, "normalRules", new List<MushroomSpeedRule> { new(MushroomSpeedMode.Add, 0) });
            Set(launcher, "goodRules", new List<MushroomSpeedRule> { new(MushroomSpeedMode.Add, 5) });
            Set(launcher, "perfectRules", new List<MushroomSpeedRule> { new(MushroomSpeedMode.Add, 10) });
            int landingCount = 0; LandingQuality lastQuality = LandingQuality.Bad;
            Action<LandingQuality> listener = quality => { landingCount++; lastQuality = quality; };
            GameplayEvents.OnMushroomLanding += listener;
            try
            {
                motor.rigidBody.linearVelocity = new Vector3(0, -2, 20); Physics.SyncTransforms();
                Check(launcher.TryLaunch(motor), "Granting bounce succeeds");
                Check(lastQuality == LandingQuality.Perfect, "Bad zone becomes Perfect immediately");
                Near(motor.Velocity.z, 30, "Granting bounce Perfect speed rule");
                effects.AdvanceTimers(2); Check(!launcher.TryLaunch(motor), "Duplicate contact rejected");
                Check(landingCount == 1, "Exactly one landing reward"); Near(effects.GetRemaining(MushroomModifierType.PerfectLandings), 8, "Rejected bounce does not refresh");
                Call(launcher, "OnDisable"); effects.Clear(); Set(launcher, "modifier", chill);
                direction.forward = Vector3.back;
                Check(!launcher.TryLaunch(motor), "Invalid launch rejected"); Check(!effects.IsActive(MushroomModifierType.Chilling), "Rejected launch gives no effect");
                direction.forward = new Vector3(0, 1, 1); motor.SetMotorEnabled(false);
                Check(!launcher.TryLaunch(motor), "Disabled motor rejected"); Check(!effects.IsActive(MushroomModifierType.Chilling), "Disabled motor gives no effect");
                motor.SetMotorEnabled(true); effects.Apply(perfect); motor.rigidBody.linearVelocity = new Vector3(0, -2, 20);
                Check(launcher.TryLaunch(motor), "Chill pickup with Perfect succeeds"); Near(motor.Velocity.z, 21, "Both modifiers affect same granting bounce");
                Check(lastQuality == LandingQuality.Perfect, "Perfect reward while chilled");
            }
            finally { GameplayEvents.OnMushroomLanding -= listener; }

            // Soft limits, floor, braking, tier caps and air jump use nominal motion.
            effects.Clear(); Set(profile, "maxSpeedX1", 15f); Set(profile, "maxSpeedX2", 30f); motor.SetTuningProfile(profile);
            motor.rigidBody.linearVelocity = new Vector3(0, 0, 40); effects.Apply(chill);
            float expected = 40f; Vector3 expectedVelocity = new Vector3(0, 0, expected);
            BounceMovementMath.ApplySoftSpeedLimit(ref expectedVelocity, profile, Vector3.up, Time.fixedDeltaTime, 15f);
            Call(motor, "FixedUpdate"); Near(motor.Velocity.z, expectedVelocity.z * 0.7f, "Soft cap applied in nominal units");
            var momentum = body.AddComponent<MomentumSystem>();
            momentum.CurrentTier = MomentumTier.Medium;
            motor.SetMomentumSystem(momentum);
            Near(motor.CurrentMaxSpeed, 21, "Tier cap scaled once");
            Set(momentum, "useTierSpeedDecay", true); momentum.IsRunning = true; momentum.CurrentMomentum = 30;
            Set(motor, "blendedMaxSpeed", 15f);
            float expectedBlend = RunnerBalanceMath.BlendCap(15, 30, momentum.TierSpeedTransitionTime, Time.fixedDeltaTime);
            Call(motor, "FixedUpdate"); Near(motor.UnmodifiedMaxSpeed, expectedBlend, "Tier blending remains nominal");
            Near(motor.CurrentMaxSpeed, expectedBlend * 0.7f, "Blended effective cap");
            motor.SetMomentumSystem(null);
            effects.Clear(); motor.SetTuningProfile(profile);
            Set(profile, "maxSpeedX1", 100f); Set(profile, "maxSpeedX2", 100f);
            motor.rigidBody.linearVelocity = new Vector3(0, 0, 20); effects.Apply(chill);
            motor.SetInput(new MovementInputFrame(new Vector2(0, -1), Vector3.back, Vector3.forward, 1, false));
            Call(motor, "FixedUpdate"); Check(motor.Velocity.z < 14f, "Braking decreases chilled speed");
            float afterBrake = motor.Velocity.z; effects.Clear(); Near(motor.Velocity.z, afterBrake / 0.7f, "Expiry retains braking");
            motor.SetInput(MovementInputFrame.Empty); effects.Apply(chill);
            motor.rigidBody.linearVelocity = new Vector3(0, -4, 7); motor.RequestDash(); Call(motor, "FixedUpdate");
            Near(motor.Velocity.y, profile.DashForce, "Air jump strength unchanged"); Near(motor.Velocity.z, 7, "Air jump does not reapply slow");
            effects.Clear(); motor.rigidBody.linearVelocity = new Vector3(0, 0, 20);
            Launch(motor, new MushroomSpeedRule(MushroomSpeedMode.Add, 10)); Near(motor.Velocity.z, 30, "No modifier unchanged");
            Set(chill, "duration", float.NaN); Check(!effects.Apply(chill), "Invalid definition rejected");
            File.WriteAllText("Temp/ModifierVerification.txt", "PASS: " + checks + " checks; Play Mode=" + Application.isPlaying);
            Debug.Log("Timed modifiers PASS: " + checks + " checks.");
        }
        catch (Exception e) { File.WriteAllText("Temp/ModifierVerification.txt", "FAIL after " + checks + " checks\n" + e); throw; }
        finally
        {
            for (int i = temporary.Count - 1; i >= 0; i--) if (temporary[i] != null) Object.DestroyImmediate(temporary[i]);
            temporary.Clear();
        }
    }

    static BoxCollider Zone(Transform parent, string name, float x)
    {
        var zone = new GameObject(name); zone.transform.SetParent(parent, false); zone.transform.localPosition = Vector3.right * x;
        var collider = zone.AddComponent<BoxCollider>(); collider.isTrigger = true; return collider;
    }

    static void Launch(RunnerMovementMotor motor, MushroomSpeedRule rule)
    {
        Check(MushroomLauncher.TryCalculateLaunch(motor.UnmodifiedVelocity, 1f, rule, new Vector3(0, 1, 1), out var velocity), "Launch math valid");
        Check(motor.ApplyMushroomLaunch(velocity, null, Vector3.zero, Vector3.up), "Motor accepts launch");
    }
}
