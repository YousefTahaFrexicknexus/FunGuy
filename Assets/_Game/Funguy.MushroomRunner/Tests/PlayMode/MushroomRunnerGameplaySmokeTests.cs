using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Funguy.MushroomRunner.Tests.PlayMode
{
    public sealed class MushroomRunnerGameplaySmokeTests
    {
        private const string ScenePath = "Assets/_Game/Funguy.MushroomRunner/Scenes/MushroomRunnerGameplay.unity";
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator GameplayScene_WiresPlayerCameraRunFlowAndHudWithoutDuplicates()
        {
            int playerRegisteredCount = 0;

            void HandlePlayerRegistered(PlayerRegisteredEvent eventData)
            {
                if (eventData.Player != null)
                {
                    playerRegisteredCount++;
                }
            }

            MushroomRunnerEvents.PlayerRegistered += HandlePlayerRegistered;

            try
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                    ScenePath,
                    new LoadSceneParameters(LoadSceneMode.Single));

                yield return null;

                MushroomRunnerPlayer player = Object.FindFirstObjectByType<MushroomRunnerPlayer>();
                RunnerCameraRig cameraRig = Object.FindFirstObjectByType<RunnerCameraRig>();
                RunFlowCoordinator coordinator = Object.FindFirstObjectByType<RunFlowCoordinator>();
                RunScoreHud[] scoreHuds = Object.FindObjectsByType<RunScoreHud>(FindObjectsSortMode.None);
                PlayerSpeedHudPresenter[] speedHuds = Object.FindObjectsByType<PlayerSpeedHudPresenter>(FindObjectsSortMode.None);
                RunMultiplierService multiplierService = player != null ? player.GetComponent<RunMultiplierService>() : null;

                Assert.That(playerRegisteredCount, Is.EqualTo(1));
                Assert.That(player, Is.Not.Null);
                Assert.That(multiplierService, Is.Not.Null);
                Assert.That(player.InputSource, Is.Not.Null);
                Assert.That(player.CameraFollowTarget, Is.Not.Null);
                Assert.That(cameraRig, Is.Not.Null);
                Assert.That(coordinator, Is.Not.Null);
                Assert.That(scoreHuds.Length, Is.EqualTo(1));
                Assert.That(speedHuds.Length, Is.EqualTo(1));
                Assert.That(player.InputSource.MovementCamera, Is.SameAs(Camera.main));
                Assert.That(ReadPrivateField<Transform>(cameraRig, "target"), Is.SameAs(player.CameraFollowTarget));

                FieldInfo availableDashChargesField = typeof(MushroomRunnerPlayer).GetField("availableDashCharges", InstanceNonPublic);
                MethodInfo handleBounceMethod = typeof(MushroomRunnerPlayer).GetMethod("HandleBounce", InstanceNonPublic);

                Assert.That(availableDashChargesField, Is.Not.Null);
                Assert.That(handleBounceMethod, Is.Not.Null);

                availableDashChargesField.SetValue(player, 0);
                handleBounceMethod.Invoke(
                    player,
                    new object[]
                    {
                        new BounceEventData(null, Vector3.zero, Vector3.up, Vector3.zero, Vector3.zero, default)
                    });

                Assert.That((int)availableDashChargesField.GetValue(player), Is.GreaterThan(0));

                coordinator.ResetRun();
                yield return null;

                coordinator.ReportFailure(RunFailureReason.FellBelowDeathPlane);
                yield return null;

                Assert.That(Object.FindObjectsByType<RunScoreHud>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
                Assert.That(Object.FindObjectsByType<PlayerSpeedHudPresenter>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            }
            finally
            {
                MushroomRunnerEvents.PlayerRegistered -= HandlePlayerRegistered;
            }
        }

        private static T ReadPrivateField<T>(object target, string fieldName)
            where T : class
        {
            FieldInfo field = target.GetType().GetField(fieldName, InstanceNonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}' on {target.GetType().Name}.");
            return field.GetValue(target) as T;
        }
    }
}
