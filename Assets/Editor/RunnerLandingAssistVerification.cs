using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Runs in a separate, temporary scene; never moves the player's live character.
public static class RunnerLandingAssistVerification
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly List<string> results = new();
    static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Fields).SetValue(obj, value);
    static T Get<T>(object obj, string field) => (T)obj.GetType().GetField(field, Fields).GetValue(obj);
    static void Check(bool pass, string label)
    {
        if (!pass) throw new Exception(label);
        results.Add("PASS " + label);
    }

    [MenuItem("Tools/Funguy/Landing Assist/Run Verification")]
    public static void Run()
    {
        results.Clear();
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        MovementTuningProfile profile = null;
        MushroomLauncher launcher = null;
        try
        {
            Check(LandingAssistMath.CanReachAxis(0.14f, 0.35f, 10f, 0.6f, 0f), "Reachable capped-speed coast is accepted (old check rejected it)");
            Check(!LandingAssistMath.CanReachAxis(0.3f, 0.35f, 10f, 0.6f, 0f), "Unreachable miss is rejected");
            Check(!LandingAssistMath.CanReachAxis(0.01f, 0.35f, 10f, 0.6f, 0.6f), "Exhausted speed budget is respected");
            Check(LandingAssistMath.CanReachAxis(-0.1f, 0.35f, 10f, 0.6f, 0.6f), "Opposite correction can use the opposite speed budget");
            Check(!LandingAssistMath.CanReach(new Vector2(-0.1f, 0f), 0.5f, Vector2.right,
                new Vector2(60, 45), new Vector2(10, 8), Vector2.zero, 1f), "Steering away cancels correction");
            Vector2 smooth = Vector2.zero;
            Vector2 step = LandingAssistMath.CalculateVelocityStep(new Vector2(-0.5f, 0), 0.25f, Vector2.zero,
                new Vector2(60, 45), new Vector2(10, 8), Vector2.zero, 1f, 20f, 0.02f, ref smooth);
            Check(step.x < -0.25f, "Strong preset supplies a noticeable velocity correction immediately");
            smooth = Vector2.zero;
            step = LandingAssistMath.CalculateVelocityStep(new Vector2(0.14f, 0), 0.35f, Vector2.zero,
                new Vector2(10, 10), new Vector2(0.6f, 0.6f), Vector2.zero, 1f, 100f, 0.02f, ref smooth);
            Check(step.x > 0.05f, "Controller accelerates early enough to execute a capped-speed coast");

            profile = ScriptableObject.CreateInstance<MovementTuningProfile>();
            Set(profile, "gravityScale", 0f);
            Set(profile, "airDrag", 0f);
            var player = new GameObject("Verification player");
            var body = player.AddComponent<SphereCollider>(); body.radius = 0.35f;
            var rb = player.AddComponent<Rigidbody>(); rb.useGravity = false;
            var motor = player.AddComponent<RunnerMovementMotor>();
            motor.rigidBody = rb;
            Set(motor, "tuningProfile", profile);
            Set(motor, "rigidBodyColliders", new Collider[] { body });
            var assist = player.GetComponent<RunnerLandingAssist>(); assist.ApplyStrongPreset();
            var mushroom = new GameObject("Verification mushroom");
            var zone = mushroom.AddComponent<BoxCollider>(); zone.isTrigger = true; zone.size = new Vector3(2, 0.3f, 2);
            launcher = mushroom.AddComponent<MushroomLauncher>();
            var arrow = new GameObject("Arrow").transform; arrow.SetParent(mushroom.transform);
            arrow.rotation = Quaternion.LookRotation(new Vector3(0, 1, 1));
            Set(launcher, "normalTrigger", zone);
            Set(launcher, "goodTrigger", zone);
            Set(launcher, "perfectTrigger", zone);
            Set(launcher, "launchDirection", arrow);
            typeof(MushroomLauncher).GetMethod("OnEnable", Fields).Invoke(launcher, null);
            var apply = typeof(RunnerLandingAssist).GetMethod("ApplyAssist", Fields);
            Vector3 Apply(Vector3 position, Vector3 velocity, MovementInputFrame input)
            {
                rb.position = position; player.transform.position = position;
                Physics.SyncTransforms();
                assist.ResetAssist();
                object[] args = { motor, velocity, input, 1f, 0.02f };
                apply.Invoke(assist, args);
                return (Vector3)args[1];
            }
            Vector3 incoming = new(0, -4, 8);
            Vector3 edgePosition = new(1.7f, 1.5f, -2);
            Vector3 rescued = Apply(edgePosition, incoming, MovementInputFrame.Empty);
            Check(rescued.x < -0.1f, "Actual collider candidate produces lateral rescue velocity");
            Vector3 natural = Apply(new Vector3(0, 1.5f, -2), incoming, MovementInputFrame.Empty);
            Check((natural - incoming).sqrMagnitude < 0.000001f, "Natural landing receives no correction");
            assist.SetLandingAssistEnabled(false);
            Vector3 disabled = Apply(edgePosition, incoming, MovementInputFrame.Empty);
            Check(disabled == incoming, "Master switch off preserves incoming velocity");
            assist.SetLandingAssistEnabled(true);
            assist.SetAssistStrength(0f);
            Check(Apply(edgePosition, incoming, MovementInputFrame.Empty) == incoming, "Zero strength preserves incoming velocity");
            assist.ApplyStrongPreset();
            Vector3 away = Apply(edgePosition, incoming, new MovementInputFrame(Vector2.right, Vector3.right, Vector3.forward, 1, false));
            Check(away.x == 0, "Actual candidate respects steering away");

            // Force future prediction to lower its local floor; the real floor must survive.
            Set(motor, "planarSpeedFloor", 12f);
            Set(motor, "planarSpeedFloorDirection", Vector3.forward);
            var predict = typeof(RunnerMovementMotor).GetMethod("PredictLandingTrajectory", Fields);
            var positions = new Vector3[49];
            int predictedSteps = (int)predict.Invoke(motor, new object[] { new Vector3(1, -4, 0), MovementInputFrame.Empty, 1f, 0.5f, positions });
            Check(Get<float>(motor, "planarSpeedFloor") == 12f, "Prediction does not change the motor's retained speed");
            Check(Get<Vector3>(motor, "planarSpeedFloorDirection") == Vector3.forward, "Prediction does not change retained direction");
            Check(Vector3.Distance(positions[1], positions[0] + new Vector3(1, -4, 0) * (0.5f / predictedSteps)) < 0.0001f,
                $"Prediction first segment uses incoming velocity (fixed={Time.fixedDeltaTime}, steps={predictedSteps})");
            Check(predictedSteps == Mathf.Clamp(Mathf.CeilToInt(0.5f / Time.fixedDeltaTime), 2, positions.Length - 1),
                "Prediction uses physics timestep rather than a separate 100 Hz simulation");

            Set(motor, "planarSpeedFloor", 0f);
            bool FlightHits(bool enabled, out float lateralSpeed)
            {
                assist.ApplyStrongPreset();
                assist.SetLandingAssistEnabled(enabled);
                Vector3 position = edgePosition;
                Vector3 velocity = incoming;
                lateralSpeed = 0f;
                for (int tick = 0; tick < 25; tick++)
                {
                    rb.position = position; player.transform.position = position;
                    Physics.SyncTransforms();
                    // Time.time is stationary in edit mode; advance the search clock explicitly.
                    Set(assist, "nextSearchTime", 0f);
                    object[] args = { motor, velocity, MovementInputFrame.Empty, 1f, 0.02f };
                    apply.Invoke(assist, args);
                    velocity = (Vector3)args[1];
                    lateralSpeed = Mathf.Max(lateralSpeed, Mathf.Abs(velocity.x));
                    position += velocity * 0.02f;
                    if (Physics.ComputePenetration(body, position, Quaternion.identity, zone,
                        zone.transform.position, Quaternion.identity, out _, out _)) return true;
                }
                return false;
            }
            Check(!FlightHits(false, out _), "A/B flight: unassisted trajectory misses the cap");
            Check(FlightHits(true, out float rescuedSpeed), $"A/B flight: assisted trajectory lands (peak lateral speed {rescuedSpeed:F2})");

            assist.ApplyStrongPreset();
            // The player passes above the entire cap rather than descending through it.
            Vector3 skimmed = Apply(new Vector3(0, 0.9f, -2), new Vector3(0, -0.2f, 12), MovementInputFrame.Empty);
            Check(skimmed.y < -0.2f, "Skim-above miss receives downward rescue");
            results.Add($"SUCCESS: {results.Count} checks");
        }
        catch (Exception e)
        {
            results.Add("FAIL " + e);
            Debug.LogException(e);
        }
        finally
        {
            SceneManager.SetActiveScene(previous);
            if (launcher != null) typeof(MushroomLauncher).GetMethod("OnDisable", Fields).Invoke(launcher, null);
            EditorSceneManager.CloseScene(scene, true);
            if (profile != null) Object.DestroyImmediate(profile);
            Directory.CreateDirectory("Temp");
            File.WriteAllLines("Temp/LandingAssistVerification.txt", results);
            Debug.Log(string.Join("\n", results));
        }
    }
}
