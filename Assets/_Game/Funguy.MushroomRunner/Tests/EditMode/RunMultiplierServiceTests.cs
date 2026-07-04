using NUnit.Framework;
using UnityEngine;

namespace Funguy.MushroomRunner.Tests.EditMode
{
    public sealed class RunMultiplierServiceTests
    {
        private GameObject root;
        private RunMultiplierService service;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("RunMultiplierServiceTests");
            service = root.AddComponent<RunMultiplierService>();
            service.ResetState(true, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void RegisterBounce_GrowsComboAndCapsMultiplier()
        {
            MovementInputFrame input = CreateForwardInput();

            service.RegisterBounce(input, 0f);
            service.RegisterBounce(input, 0.1f);
            service.RegisterBounce(input, 0.2f);
            service.RegisterBounce(input, 0.3f);
            service.RegisterBounce(input, 0.4f);

            Assert.That(service.CurrentComboHits, Is.EqualTo(5));
            Assert.That(service.CurrentComboMultiplier, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void ReleasingForwardInput_UsesGraceWindowBeforeBreakingCombo()
        {
            service.RegisterBounce(CreateForwardInput(), 0f);

            service.Step(MovementInputFrame.Empty, grounded: false, currentTime: 0.2f);
            Assert.That(service.CurrentComboHits, Is.EqualTo(1));
            Assert.That(service.IsComboBreakPending, Is.True);

            service.Step(MovementInputFrame.Empty, grounded: false, currentTime: 0.49f);
            Assert.That(service.CurrentComboHits, Is.EqualTo(1));

            service.Step(MovementInputFrame.Empty, grounded: false, currentTime: 0.51f);
            Assert.That(service.CurrentComboHits, Is.Zero);
            Assert.That(service.IsComboBreakPending, Is.False);
        }

        [Test]
        public void BrakeIntent_BreaksComboImmediately()
        {
            service.RegisterBounce(CreateForwardInput(), 0f);

            service.Step(CreateBrakeInput(), grounded: false, currentTime: 0.05f);

            Assert.That(service.CurrentComboHits, Is.Zero);
            Assert.That(service.HasActiveCombo, Is.False);
        }

        [Test]
        public void AirtimeThresholds_QualifyInOrder()
        {
            service.RegisterBounce(CreateForwardInput(), 0f);

            service.Step(CreateForwardInput(), grounded: false, currentTime: 0.84f);
            Assert.That(service.HasQualifiedAirtime, Is.False);
            Assert.That(service.HasQualifiedAirtimeMultiplier, Is.False);

            service.Step(CreateForwardInput(), grounded: false, currentTime: 0.85f);
            Assert.That(service.HasQualifiedAirtime, Is.True);
            Assert.That(service.HasQualifiedAirtimeMultiplier, Is.False);

            service.Step(CreateForwardInput(), grounded: false, currentTime: 1.15f);
            Assert.That(service.HasQualifiedAirtimeMultiplier, Is.True);
            Assert.That(service.RewardedAirtimeSeconds, Is.EqualTo(0.8f).Within(0.001f));
        }

        private static MovementInputFrame CreateForwardInput()
        {
            return new MovementInputFrame(new Vector2(0f, 1f), Vector3.forward, Vector3.forward, 1f, false);
        }

        private static MovementInputFrame CreateBrakeInput()
        {
            return new MovementInputFrame(new Vector2(0f, -1f), Vector3.back, Vector3.forward, 1f, false);
        }
    }
}
