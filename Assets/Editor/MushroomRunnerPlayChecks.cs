using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch-only physics smoke test in an empty, unsaved scene. No authored scenes or scoring state are changed.
[InitializeOnLoad]
public static class MushroomRunnerPlayChecks
{
    const string SessionKey = "MushroomRunner.PhysicsChecks";
    static RunnerMovementMotor triggerMotor, contactMotor;
    static Collider contactSurface;
    static int triggerBounces, contactBounces, airJumps, phase;
    static float phaseStarted;
    static double deadline;
    static int controlledLandings;
    static RunnerMovementMotor[] aimingMotors;

    static MushroomRunnerPlayChecks()
    {
        EditorApplication.playModeStateChanged += OnPlayState;
        EditorApplication.update += Tick;
    }

    public static void Start()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run this check in Unity batch mode.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(SessionKey, true);
        EditorApplication.isPlaying = true;
    }

    static void OnPlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(SessionKey, false) || state != PlayModeStateChange.EnteredPlayMode) return;
        try
        {
            deadline = EditorApplication.timeSinceStartup + 30;
            var profile = AssetDatabase.LoadAssetAtPath<MovementTuningProfile>("Assets/_Game/Funguy.MushroomRunner/ScriptableObjects/Config/Presets/BalancedMomentum.asset");
            triggerMotor = CreateMotor(-100, profile);
            contactMotor = CreateMotor(100, profile);
            triggerMotor.Bounced += _ => triggerBounces++;
            contactMotor.Bounced += _ => contactBounces++;
            triggerMotor.Dashed += () => airJumps++;

            GameObject trigger = new("Trigger test mushroom");
            trigger.transform.position = new Vector3(-100, 0, 0);
            trigger.AddComponent<BoxCollider>().size = new Vector3(80, 1, 160);
            SimpleBounceMushroom mushroom = trigger.AddComponent<SimpleBounceMushroom>();
            mushroom.SetBounceProfile(ScriptableObject.CreateInstance<MushroomBounceProfile>());

            GameObject solid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            solid.transform.position = new Vector3(100, 0, 0);
            solid.transform.localScale = new Vector3(80, 1, 160);
            solid.AddComponent<Mushroom>();
            contactSurface = solid.GetComponent<MeshCollider>();
            Physics.SyncTransforms();
            phase = 0;
            phaseStarted = Time.time;
        }
        catch (Exception exception) { Finish(exception); }
    }

    static RunnerMovementMotor CreateMotor(float x, MovementTuningProfile profile)
    {
        GameObject go = new("Physics test player");
        go.transform.position = new Vector3(x, 2, -10);
        go.AddComponent<SphereCollider>().radius = 0.45f;
        RunnerMovementMotor motor = go.AddComponent<RunnerMovementMotor>();
        motor.SetTuningProfile(profile);
        motor.SetSpeedLimitProvider(() => 60);
        motor.rigidBody.linearVelocity = new Vector3(0, -8, 60);
        return motor;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(SessionKey, false) || !EditorApplication.isPlaying || triggerMotor == null) return;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Physics smoke test timed out.");
            float elapsed = Time.time - phaseStarted;
            if (phase == 0 && elapsed >= 0.5f)
            {
                Require(triggerBounces == 1, "High-speed trigger bounce must fire once");
                Require(contactBounces == 1, "High-speed collision bounce must fire once");
                Require(!Physics.GetIgnoreCollision(contactMotor.GetComponent<Collider>(), contactSurface), "Expired ignore must be restored");
                triggerMotor.rigidBody.position = new Vector3(-100, 50, -100);
                triggerMotor.rigidBody.linearVelocity = new Vector3(0, 0, 50);
                triggerMotor.SetSpeedLimitProvider(() => 15);
                phase = 1;
                phaseStarted = Time.time;
            }
            else if (phase == 1 && elapsed >= 0.7f)
            {
                Require(triggerMotor.Velocity.z >= 14.7f && triggerMotor.Velocity.z < 16, "Downshift must settle smoothly near 15");
                Require(triggerMotor.Velocity.y < 0, "Downshift must preserve the falling arc");
                triggerMotor.RequestDash();
                phase = 2;
                phaseStarted = Time.time;
            }
            else if (phase == 2 && elapsed >= 0.1f)
            {
                Require(airJumps == 1 && triggerMotor.Velocity.y > 0, "Falling air jump must launch once");
                StartControlledLandings();
                phase = 3;
                phaseStarted = Time.time;
            }
            else if (phase == 3 && elapsed >= 1.5f)
            {
                Require(controlledLandings == 4, "All four controlled landings must hit their small mushroom once");
                Require(Vector3.Distance(aimingMotors[0].Velocity, aimingMotors[1].Velocity) < .01f,
                    "Keyboard and full-strength touch frames must produce the same 40-speed flight");
                Require(Vector3.Distance(aimingMotors[2].Velocity, aimingMotors[3].Velocity) < .01f,
                    "Keyboard and full-strength touch frames must produce the same 60-speed flight");
                for (int i = 0; i < aimingMotors.Length; i++)
                {
                    float speed = i < 2 ? 40 : 60;
                    aimingMotors[i].ResetMotion(new Vector3(400 + i * 100, 100, 0), Quaternion.identity);
                    aimingMotors[i].rigidBody.linearVelocity = new Vector3(12, -1, speed);
                    aimingMotors[i].SetInput(MovementInputFrame.Empty);
                }
                phase = 4;
                phaseStarted = Time.time;
            }
            else if (phase == 4 && elapsed >= .3f)
            {
                for (int i = 0; i < aimingMotors.Length; i++)
                {
                    float speed = i < 2 ? 40 : 60;
                    Require(Mathf.Abs(aimingMotors[i].Velocity.x) < .7f, "Release must remove at least 94% of sideways drift in .3 seconds");
                    Require(aimingMotors[i].Velocity.z > speed - 2f, "Release must preserve useful forward carry");
                    Require(aimingMotors[i].rigidBody.position.x - (400 + i * 100) < 1.3f, "Release drift must stay within the landing window");
                }
                Finish(null);
            }
        }
        catch (Exception exception) { Finish(exception); }
    }

    static void StartControlledLandings()
    {
        var profile = triggerMotor.TuningProfile;
        var bounce = AssetDatabase.LoadAssetAtPath<MushroomBounceProfile>("Assets/_Game/Funguy.MushroomRunner/ScriptableObjects/Config/StandardMushroomBounceProfile.asset");
        aimingMotors = new RunnerMovementMotor[4];
        for (int i = 0; i < aimingMotors.Length; i++)
        {
            float speed = i < 2 ? 40 : 60;
            // The input pipeline normalizes keyboard diagonals and full-stick diagonals to the same frame.
            Vector2 move = i % 2 == 0 ? new Vector2(1, -1).normalized : Vector2.ClampMagnitude(new Vector2(1, -1), 1);
            MovementInputFrame input = new(move, new Vector3(move.x, 0, move.y), Vector3.forward, move.magnitude, false);
            float offset = 400 + i * 100;
            Vector3 start = new(offset, 4, 0);
            Vector3 position = start;
            Vector3 velocity = new(0, 10, speed);
            float dt = Time.fixedDeltaTime;
            for (int step = 0; step < 300; step++)
            {
                BounceMovementMath.ApplyShapedGravity(ref velocity, profile, Vector3.up, dt);
                BounceMovementMath.ApplyDive(ref velocity, profile, input.BrakeAmount, Vector3.up, dt);
                BounceMovementMath.ApplyAirMovement(ref velocity, profile, input, Vector3.up, false, false, dt);
                BounceMovementMath.ApplyPlanarDrag(ref velocity, Vector3.up, profile.AirDrag, dt);
                BounceMovementMath.ApplySoftSpeedLimit(ref velocity, profile, Vector3.up, speed, dt);
                position += velocity * dt;
                if (position.y <= .65f && velocity.y < 0) break;
            }
            GameObject platform = new("Controlled landing mushroom " + i);
            platform.transform.position = new Vector3(position.x, 0, position.z);
            platform.AddComponent<BoxCollider>().size = new Vector3(2, .4f, 2);
            platform.AddComponent<SimpleBounceMushroom>().SetBounceProfile(bounce);
            RunnerMovementMotor motor = CreateMotor(offset, profile);
            aimingMotors[i] = motor;
            motor.ResetMotion(start, Quaternion.identity);
            motor.SetSpeedLimitProvider(() => speed);
            motor.rigidBody.linearVelocity = new Vector3(0, 10, speed);
            motor.SetInput(input);
            bool landed = false;
            motor.Bounced += data =>
            {
                if (landed)
                {
                    Finish(new Exception("Controlled landing publishes once"));
                    return;
                }
                landed = true;
                controlledLandings++;
                // Stop further landing input after the first hit to avoid intentionally bouncing twice.
                motor.SetInput(MovementInputFrame.Empty);
            };
        }
        Physics.SyncTransforms();
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Finish(Exception exception)
    {
        SessionState.SetBool(SessionKey, false);
        if (exception != null) Debug.LogException(exception);
        else Debug.Log("MOVEMENT PHYSICS CHECKS PASSED: trigger/collision landings at 60, collision restoration, downshift, falling air jump, four controlled small-target landings at 40/60 with equivalent keyboard/touch frames, and release grip with forward carry.");
        EditorApplication.Exit(exception == null ? 0 : 1);
    }
}
