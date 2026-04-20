using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

namespace Rowlan.Yapp
{

    public class PhysicsExtension 
    {
        #region Properties
        SerializedProperty forceApplyType;
        SerializedProperty maxIterations;
        SerializedProperty forceMinMax;
        SerializedProperty forceAngleInDegrees;
        SerializedProperty randomizeForceAngle;
        SerializedProperty simulationTime;
        SerializedProperty simulationSteps;
        SerializedProperty simulationFrameFPS;
        SerializedProperty collisionDetectionMode;
        SerializedProperty solverIterations;
        SerializedProperty autoSyncTransforms;
        #endregion Properties

#pragma warning disable 0414
        PrefabPainterEditor editor;
        #pragma warning restore 0414

        PrefabPainter editorTarget;

        public PhysicsExtension(PrefabPainterEditor editor)
        {
            this.editor = editor;
            this.editorTarget = editor.GetPainter();

            forceApplyType = editor.FindProperty(x => x.physicsSettings.forceApplyType);
            maxIterations = editor.FindProperty(x => x.physicsSettings.maxIterations);
            forceMinMax = editor.FindProperty(x => x.physicsSettings.forceMinMax);
            forceAngleInDegrees = editor.FindProperty(x => x.physicsSettings.forceAngleInDegrees);
            randomizeForceAngle = editor.FindProperty(x => x.physicsSettings.randomizeForceAngle);
            simulationTime = editor.FindProperty(x => x.physicsSettings.simulationTime);
            simulationSteps = editor.FindProperty(x => x.physicsSettings.simulationSteps);
            simulationFrameFPS = editor.FindProperty(x => x.physicsSettings.simulationFrameFPS);
            collisionDetectionMode = editor.FindProperty(x => x.physicsSettings.collisionDetectionMode);
            solverIterations = editor.FindProperty(x => x.physicsSettings.solverIterations);
            autoSyncTransforms = editor.FindProperty(x => x.physicsSettings.autoSyncTransforms);
        }

        public void OnInspectorGUI()
        {
            // separator
            GUILayout.BeginVertical("box");
            //addGUISeparator();

            EditorGUILayout.LabelField("Physics Settings", GUIStyles.BoxTitleStyle);

            #region Settings

            EditorGUILayout.PropertyField(forceApplyType, new GUIContent("Force Apply Type"));

            EditorGUILayout.PropertyField(forceMinMax, new GUIContent("Force Min/Max"));
            EditorGUILayout.PropertyField(forceAngleInDegrees, new GUIContent("Force Angle (Degrees)"));
            EditorGUILayout.PropertyField(randomizeForceAngle, new GUIContent("Randomize Force Angle"));

            #endregion Settings

            EditorGUILayout.Space();

            #region Simulate Continuously

            EditorGUILayout.LabelField("Simulation", GUIStyles.GroupTitleStyle);

            EditorGUILayout.PropertyField(simulationTime, new GUIContent("Time", "The time in seconds for which the physics simulation will be running"));
            EditorGUILayout.PropertyField(simulationSteps, new GUIContent("Iterations", "The number of Physics.Simulate() invocations per frame"));
            EditorGUILayout.PropertyField(simulationFrameFPS, new GUIContent("FPS per Frame", "The step time at which Physics.Simulate is invoked. 50 means: Update all physics objects as if 0.02 seconds have passed. 0.02 = 1 / 50. Higher value means slower simulation"));
            EditorGUILayout.PropertyField(collisionDetectionMode, new GUIContent("Collision Detection", "Detection mode of the Unity rigidbody. Continuous is more accurate than discrete and can prevent objects from falling through, but it is slower"));
            EditorGUILayout.PropertyField(solverIterations, new GUIContent("Solver Iterations", "Solver accuracy. Higher value costs more performance. Default 6 [1,255]"));
            EditorGUILayout.PropertyField(autoSyncTransforms, new GUIContent("Auto Sync Transforms", "Whether or not to automatically sync transform changes with the physics system whenever a Transform component changes."));

            EditorGUILayout.HelpBox( "Please backup your scene before using Editor Physics! Physics runs on the entire scene, not just specific objects!", MessageType.Warning);

            GUILayout.BeginHorizontal();

            // colorize the button differently in case the physics is running, so that the user gets an indicator that the physics have to be stopped
            // GUI.color = PhysicsSimulator.IsActive() ? GUIStyles.PhysicsRunningButtonBackgroundColor : GUIStyles.DefaultBackgroundColor;
            if (GUILayout.Button("Start"))
            {
                StartSimulation();
            }
            // GUI.color = GUIStyles.DefaultBackgroundColor;

            if (GUILayout.Button("Stop"))
            {
                StopSimulation();
            }

            GUILayout.EndHorizontal();

            #endregion Simulate Continuously

            EditorGUILayout.Space();

            #region Undo
            EditorGUILayout.LabelField("Undo", GUIStyles.GroupTitleStyle);

            if (GUILayout.Button("Undo Last Simulation"))
            {
                ResetAllBodies();
            }
            #endregion Undo

            GUILayout.EndVertical();
        }

        #region Physics Simulation

        private void ResetAllBodies()
        {
            PhysicsSimulator.UndoSimulation();
        }

        #endregion Physics Simulation

        private void StartSimulation()
        {
            Transform[] containerChildren = PrefabUtils.GetContainerChildren(editorTarget.container);
            AutoPhysicsSimulation.ApplyPhysics(editorTarget.physicsSettings, containerChildren, SpawnSettings.AutoSimulationType.Enabled);
        }

        private void StopSimulation()
        {
            PhysicsSimulator.Stop();
        } 

    }
}
