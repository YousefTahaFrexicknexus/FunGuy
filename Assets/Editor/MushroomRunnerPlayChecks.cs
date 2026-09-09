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
                Require(triggerMotor.Velocity.z >= 14.9f && triggerMotor.Velocity.z < 16, "Downshift must settle smoothly near 15");
                Require(triggerMotor.Velocity.y < 0, "Downshift must preserve the falling arc");
                triggerMotor.RequestDash();
                phase = 2;
                phaseStarted = Time.time;
            }
            else if (phase == 2 && elapsed >= 0.1f)
            {
                Require(airJumps == 1 && triggerMotor.Velocity.y > 0, "Falling air jump must launch once");
                Finish(null);
            }
        }
        catch (Exception exception) { Finish(exception); }
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    static void Finish(Exception exception)
    {
        SessionState.SetBool(SessionKey, false);
        if (exception != null) Debug.LogException(exception);
        else Debug.Log("MOVEMENT PHYSICS CHECKS PASSED: trigger/collision landings at 60, collision restoration, downshift, falling air jump.");
        EditorApplication.Exit(exception == null ? 0 : 1);
    }
}
