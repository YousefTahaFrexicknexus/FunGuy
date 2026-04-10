/**
 * Original version by Jazz Macedo
 * https://gist.github.com/jasielmacedo/c5391fd145572bebbe2b5052e3a38495
 */
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using static Rowlan.Yapp.PhysicsSettings;

namespace Rowlan.Yapp
{
    // This causes the class' static constructor to be called on load and on starting playmode
    [InitializeOnLoad]
    public static class PhysicsSimulator
    {
        // only ever register once
        static bool registered = false;

        // how long do we run physics for before we give up getting things to sleep
        static float timeToSettle = 3f;

        // how long have we been running
        static float activeTime = 0f;

        static bool active = false;

        static List<PhysicsSimulationGroup> groupRegistry = new List<PhysicsSimulationGroup>();
        static List<PhysicsSimulationGroup> undoList = new List<PhysicsSimulationGroup>();

        static int simulationIterations = 1;
        static SimulationFrameFPS simulationFrameFPS = SimulationFrameFPS.FPS_50;

        static int prevSolverIterations = 6;
        static int solverIterations = 6;

        static bool prevAutoSyncTransforms = false;
        static bool autoSyncTransforms = false;

        static PhysicsSimulator()
        {
            if (!registered)
            {
                // hook into the editor update
                EditorApplication.update += Update;

                // and the scene view OnGui
                SceneView.duringSceneGui += OnSceneGUI;

                registered = true;
            }
        }

        public static void SetSimulationFrameFPS(SimulationFrameFPS fps)
        {
            simulationFrameFPS = fps;
        }

        public static void SetSimulationIterations( int iterations)
        {
            simulationIterations = iterations;
        }

        public static void SetSimulationTime( float time)
        {
            timeToSettle = time;
        }

        public static void SetSolverIterations( int iterations)
        {
            solverIterations = iterations;
        }

        public static void SetAutoSyncTransforms(bool syncTransforms)
        {
            autoSyncTransforms = syncTransforms;
        }

        public static void RegisterGroup(PhysicsSimulationGroup group)
        {
            groupRegistry.Add(group);
        }

        public static bool IsActive()
        {
            return active;
        }

        public static void Activate()
        {
            if (!active)
            {
                //Debug.Log("Physics started");

                active = true;

                //// Normally avoid Find functions, but this is editor time and only happens once
                //workList = Object.FindObjectsOfType<Rigidbody>();

                activeTime = 0f;

                //// make sure that all rigidbodies are awake so they will actively settle against changed geometry.
                //foreach (Rigidbody body in workList)
                //{
                //    body.WakeUp();
                //}

                prevSolverIterations = Physics.defaultSolverIterations;
                prevAutoSyncTransforms = Physics.autoSyncTransforms;

                Physics.defaultSolverIterations = solverIterations;

            }
            else
            {
                //Debug.Log("Physics restarted");

                // reset
                activeTime = 0f;

            }
        }

        public static void Stop()
        {
            active = false;

            if (groupRegistry.Count > 0)
            {
                undoList.Clear();
                undoList.AddRange(groupRegistry);

                foreach (PhysicsSimulationGroup group in groupRegistry)
                {
                    group.CleanUp();
                }

                groupRegistry.Clear();

                // Debug.Log("Physics stopped");
            }

            // restore all rigidbody settings which we didn't want to be affected by the physics simulation
            PhysicsFilter.Restore();

            Physics.defaultSolverIterations = prevSolverIterations;
            Physics.autoSyncTransforms = prevAutoSyncTransforms;
        }

        private static void Update()
        {
            if (active)
            {
                if (autoSyncTransforms)
                {
                    Physics.SyncTransforms();
                }

                // save original setting
#if UNITY_6000_0_OR_NEWER
                SimulationMode prevSimulationMode = Physics.simulationMode;
#else
                bool prevAutoSimulation = Physics.autoSimulation;
#endif


                try
                {
                    foreach (PhysicsSimulationGroup group in groupRegistry)
                    {
                        group.PerformSimulateStep();
                    }

                    activeTime += Time.deltaTime;

                    // make sure we are not autosimulating
#if UNITY_6000_0_OR_NEWER
                    Physics.simulationMode = SimulationMode.Script;
#else
                    Physics.autoSimulation = false;
#endif


                    // see if all our 
                    //bool allSleeping = true;
                    //foreach (Rigidbody body in workList)
                    //{
                    //    if (body != null)
                    //    {
                    //        allSleeping &= body.IsSleeping();
                    //    }
                    //} 

                    if (/*allSleeping ||*/ activeTime >= timeToSettle)
                    {
                        Stop();
                    }
                    else
                    {
                        int fps = (int)simulationFrameFPS;
                        float fixedDeltaTime = 1f / fps;

                        for (int i = 0; i < simulationIterations; i++)
                        {
                            Physics.defaultSolverIterations = solverIterations; // always set here, will be restort to unity setting otherwise

                            // before: delta time can be inconsistent in edit mode => using fixed time
                            // Physics.Simulate(Time.deltaTime);

                            // fixed simulation steps
                            Physics.Simulate(fixedDeltaTime / simulationIterations);

                        }
                    }

                } finally {
                    // restore original setting
#if UNITY_6000_0_OR_NEWER
                    Physics.simulationMode = prevSimulationMode;
#else
                    Physics.autoSimulation = prevAutoSimulation;
#endif

                }
            }

        }

        static void OnSceneGUI(SceneView sceneView)
        {

            if (active)
            {
                Handles.BeginGUI();
                {
                    GUILayout.BeginArea(new Rect(60, 10, 100, 100));
                    GUILayout.Label("Physics Active", GUIStyles.PhysicsRunningLabelStyle, GUILayout.Width(100));
                    GUILayout.Label(string.Format(CultureInfo.InvariantCulture, "Time Left: {0:F2}", (timeToSettle - activeTime)), GUIStyles.PhysicsRunningLabelStyle, GUILayout.Width(100));
                    GUILayout.EndArea();

                }
                Handles.EndGUI();
            }
        }

        public static void UndoSimulation()
        {
            foreach (PhysicsSimulationGroup group in undoList)
            {
                group.UndoSimulation();
            }
        }
    }
}