using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FunGuy.Runner.Editor
{
    public static class RunnerCinemachineSceneTools
    {
        private const string BrainTypeName = "Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine";
        private const string CameraTypeName = "Unity.Cinemachine.CinemachineCamera, Unity.Cinemachine";
        private const string FollowTypeName = "Unity.Cinemachine.CinemachineFollow, Unity.Cinemachine";
        private const string RotationComposerTypeName = "Unity.Cinemachine.CinemachineRotationComposer, Unity.Cinemachine";
        private const string BindingModeTypeName = "Unity.Cinemachine.TargetTracking.BindingMode, Unity.Cinemachine";

        private static readonly Vector3 CameraFollowOffset = new(0f, 4.25f, -10.8f);
        private static readonly Vector3 CameraPositionDamping = new(0.35f, 0.5f, 0.25f);
        private static readonly Vector3 CameraRotationDamping = new(0.2f, 0.25f, 0.15f);

        [MenuItem("FunGuy/Runner/Setup Active Scene Cinemachine Camera")]
        public static void SetupActiveSceneCinemachineCamera()
        {
            Scene scene = SceneManager.GetActiveScene();
            PlayerRunnerController player = UnityEngine.Object.FindFirstObjectByType<PlayerRunnerController>();

            if (player == null)
            {
                Debug.LogError("[RunnerCinemachineSceneTools] No PlayerRunnerController found in the active scene.");
                return;
            }

            Transform presentationRoot = ResolvePresentationRoot(scene);
            Camera unityCamera = Camera.main;

            if (unityCamera == null)
            {
                unityCamera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            }

            if (unityCamera == null)
            {
                GameObject cameraObject = new("Main Camera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                cameraObject.transform.SetParent(presentationRoot, false);
                unityCamera = cameraObject.AddComponent<Camera>();
            }

            if (!TryConfigureSceneCamera(scene, presentationRoot, unityCamera.gameObject, player.transform))
            {
                Debug.LogWarning("[RunnerCinemachineSceneTools] Cinemachine is not available yet. Install the package in Package Manager, wait for Unity to compile, then run this menu again.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            GameObject cinemachineCameraObject = presentationRoot.Find("RunnerCinemachineCamera")?.gameObject;
            Selection.activeGameObject = cinemachineCameraObject != null ? cinemachineCameraObject : unityCamera.gameObject;
            Debug.Log("[RunnerCinemachineSceneTools] Cinemachine setup complete. Look for 'RunnerCinemachineCamera' under '_Presentation'.");
        }

        internal static bool TryConfigureSceneCamera(Scene scene, Transform presentationRoot, GameObject cameraObject, Transform playerRoot)
        {
            Type brainType = ResolveType(BrainTypeName);
            Type cameraType = ResolveType(CameraTypeName);
            Type followType = ResolveType(FollowTypeName);
            Type rotationComposerType = ResolveType(RotationComposerTypeName);
            Type bindingModeType = ResolveType(BindingModeTypeName);

            if (brainType == null || cameraType == null || followType == null || rotationComposerType == null || bindingModeType == null)
            {
                return false;
            }

            Transform cameraAnchor = EnsureCameraAnchor(scene, playerRoot);
            SetupUnityCamera(cameraObject, presentationRoot);
            RemoveManualFollow(cameraObject);
            EnsureComponent(cameraObject, brainType);

            GameObject cinemachineObject = FindOrCreateChild(scene, presentationRoot, "RunnerCinemachineCamera");

            Component cinemachineCamera = EnsureComponent(cinemachineObject, cameraType);
            Component follow = EnsureComponent(cinemachineObject, followType);
            EnsureComponent(cinemachineObject, rotationComposerType);

            SetPropertyIfPresent(cinemachineCamera, "Follow", cameraAnchor);
            SetPropertyIfPresent(cinemachineCamera, "LookAt", cameraAnchor);
            SetFieldIfPresent(follow, "FollowOffset", CameraFollowOffset);
            ConfigureTrackerSettings(follow, bindingModeType);

            cameraObject.transform.position = cameraAnchor.position + CameraFollowOffset;
            cameraObject.transform.rotation = Quaternion.LookRotation(cameraAnchor.position - cameraObject.transform.position, Vector3.up);
            return true;
        }

        private static Transform ResolvePresentationRoot(Scene scene)
        {
            GameObject presentationRootObject = GameObject.Find("_Presentation");

            if (presentationRootObject != null)
            {
                return presentationRootObject.transform;
            }

            presentationRootObject = new GameObject("_Presentation");
            SceneManager.MoveGameObjectToScene(presentationRootObject, scene);
            return presentationRootObject.transform;
        }

        private static void SetupUnityCamera(GameObject cameraObject, Transform presentationRoot)
        {
            cameraObject.name = "Main Camera";
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(presentationRoot, true);

            Camera unityCamera = cameraObject.GetComponent<Camera>();

            if (unityCamera == null)
            {
                unityCamera = cameraObject.AddComponent<Camera>();
            }

            unityCamera.clearFlags = CameraClearFlags.Skybox;
            unityCamera.nearClipPlane = 0.1f;
            unityCamera.farClipPlane = 300f;
            unityCamera.fieldOfView = 50f;

            if (cameraObject.GetComponent<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }
        }

        private static Transform EnsureCameraAnchor(Scene scene, Transform playerRoot)
        {
            RunnerCameraAnchor anchor = playerRoot.GetComponentInChildren<RunnerCameraAnchor>(true);

            if (anchor == null)
            {
                GameObject anchorObject = new("CameraAnchor");
                SceneManager.MoveGameObjectToScene(anchorObject, scene);
                anchorObject.transform.SetParent(playerRoot, false);
                anchor = Undo.AddComponent<RunnerCameraAnchor>(anchorObject);
            }

            anchor.ApplyAnchorOffset();
            return anchor.AnchorTransform;
        }

        private static void RemoveManualFollow(GameObject cameraObject)
        {
            RunnerCameraFollow fallbackFollow = cameraObject.GetComponent<RunnerCameraFollow>();

            if (fallbackFollow != null)
            {
                Undo.DestroyObjectImmediate(fallbackFollow);
            }
        }

        private static GameObject FindOrCreateChild(Scene scene, Transform parent, string objectName)
        {
            Transform existing = parent.Find(objectName);

            if (existing != null)
            {
                return existing.gameObject;
            }

            GameObject child = new(objectName);
            SceneManager.MoveGameObjectToScene(child, scene);
            child.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(child, $"Create {objectName}");
            return child;
        }

        private static Component EnsureComponent(GameObject gameObject, Type componentType)
        {
            Component component = gameObject.GetComponent(componentType);

            if (component != null)
            {
                return component;
            }

            return Undo.AddComponent(gameObject, componentType);
        }

        private static void ConfigureTrackerSettings(Component followComponent, Type bindingModeType)
        {
            FieldInfo trackerSettingsField = followComponent.GetType().GetField("TrackerSettings", BindingFlags.Public | BindingFlags.Instance);

            if (trackerSettingsField == null)
            {
                return;
            }

            object trackerSettings = trackerSettingsField.GetValue(followComponent);
            try
            {
                SetFieldOnStruct(trackerSettings, "BindingMode", Enum.Parse(bindingModeType, "WorldSpace"));
            }
            catch (ArgumentException)
            {
            }

            SetFieldOnStruct(trackerSettings, "PositionDamping", CameraPositionDamping);
            SetFieldOnStruct(trackerSettings, "RotationDamping", CameraRotationDamping);
            trackerSettingsField.SetValue(followComponent, trackerSettings);
        }

        private static void SetPropertyIfPresent(object target, string propertyName, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);

            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value);
            }
        }

        private static void SetFieldIfPresent(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private static void SetFieldOnStruct(object boxedStruct, string fieldName, object value)
        {
            if (boxedStruct == null)
            {
                return;
            }

            FieldInfo field = boxedStruct.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(boxedStruct, value);
            }
        }

        private static Type ResolveType(string typeName)
        {
            return Type.GetType(typeName, false);
        }
    }
}
