using NUnit.Framework;
using UnityEngine;

namespace Funguy.MushroomRunner.Tests.EditMode
{
    public sealed class RunScoreServiceTests
    {
        private GameObject root;
        private RunMultiplierService multiplierService;
        private RunScoreService scoreService;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("RunScoreServiceTests");
            multiplierService = root.AddComponent<RunMultiplierService>();
            scoreService = root.AddComponent<RunScoreService>();
            scoreService.SetTarget(root.transform);
            scoreService.SetMultiplierService(multiplierService);
            multiplierService.ResetState(true, 0f);
            scoreService.ResetProgress(0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ForwardProgress_UsesCurrentMultiplier()
        {
            MovementInputFrame input = CreateForwardInput();
            multiplierService.RegisterBounce(input, 0f);
            multiplierService.RegisterBounce(input, 0.1f);
            multiplierService.RegisterBounce(input, 0.2f);
            multiplierService.Step(input, grounded: false, currentTime: 0.2f);

            scoreService.ResetProgress(0f);
            scoreService.Step(10f, 0.2f);

            Assert.That(scoreService.CurrentScore, Is.EqualTo(15));
            Assert.That(scoreService.CurrentSnapshot.Score, Is.EqualTo(15));
            Assert.That(scoreService.CurrentSnapshot.ComboHits, Is.EqualTo(3));
            Assert.That(scoreService.CurrentSnapshot.Multiplier, Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        public void AirtimeReward_AccumulatesBaseAndMultiplierWindows()
        {
            MovementInputFrame input = CreateForwardInput();
            multiplierService.RegisterBounce(input, 0f);
            multiplierService.RegisterBounce(input, 0f);

            scoreService.ResetProgress(0f);

            multiplierService.Step(input, grounded: false, currentTime: 0.5f);
            scoreService.Step(0f, 0.5f);
            Assert.That(scoreService.CurrentScore, Is.EqualTo(1));

            multiplierService.Step(input, grounded: false, currentTime: 1.3f);
            scoreService.Step(0f, 1.3f);

            Assert.That(scoreService.CurrentScore, Is.EqualTo(9));
            Assert.That(scoreService.CurrentSnapshot.IsAirborne, Is.True);
            Assert.That(scoreService.CurrentSnapshot.HasQualifiedAirtime, Is.True);
            Assert.That(scoreService.CurrentSnapshot.HasQualifiedAirtimeMultiplier, Is.True);
            Assert.That(scoreService.CurrentSnapshot.RewardedAirtimeSeconds, Is.EqualTo(0.95f).Within(0.001f));
        }

        [Test]
        public void ResetProgress_ClearsScoreAndReanchorsFurthestDistance()
        {
            scoreService.Step(12f, 0.1f);
            Assert.That(scoreService.CurrentScore, Is.EqualTo(12));

            scoreService.ResetProgress(25f);

            Assert.That(scoreService.CurrentScore, Is.Zero);
            Assert.That(scoreService.FurthestForwardZ, Is.EqualTo(25f).Within(0.001f));
            Assert.That(scoreService.CurrentSnapshot.Score, Is.Zero);
        }

        private static MovementInputFrame CreateForwardInput()
        {
            return new MovementInputFrame(new Vector2(0f, 1f), Vector3.forward, Vector3.forward, 1f, false);
        }
    }
}
