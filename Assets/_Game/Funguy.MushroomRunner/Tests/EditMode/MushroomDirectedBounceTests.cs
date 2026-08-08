using NUnit.Framework;
using UnityEngine;

namespace Funguy.MushroomRunner.Tests.EditMode
{
    public sealed class MushroomDirectedBounceTests
    {
        private MushroomBounceProfile profile;
        private GameObject directionObject;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<MushroomBounceProfile>();
            directionObject = new GameObject("LaunchDirection");
        }

        [TearDown]
        public void TearDown()
        {
            if (profile != null)
            {
                Object.DestroyImmediate(profile);
            }

            if (directionObject != null)
            {
                Object.DestroyImmediate(directionObject);
            }
        }

        [Test]
        public void DirectedResponse_UsesTransformForward()
        {
            directionObject.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            BounceContext context = CreateContext(Vector3.zero);

            BounceSurfaceResponse response = profile.CreateDirectedResponse(directionObject.transform, context);

            AssertVectorsEqual(response.LaunchDirection.normalized, directionObject.transform.forward, 0.0001f);
        }

        [Test]
        public void DirectedResponse_UsesWorldForward_WhenTransformIsMissing()
        {
            BounceContext context = CreateContext(Vector3.zero);

            BounceSurfaceResponse response = profile.CreateDirectedResponse(null, context);

            AssertVectorsEqual(response.LaunchDirection.normalized, Vector3.forward, 0.0001f);
        }

        [Test]
        public void NegativePlanarBoost_SlowsWithoutReversingAlongLaunchDirection()
        {
            BounceSurfaceResponse response = new(
                velocityScale: 1f,
                directionalInfluence: 1f,
                planarBoost: -10f,
                upwardImpulse: 0f,
                impactRecoveryFactor: 0f,
                launchDirection: Vector3.forward,
                upBlend: 1f);

            Vector3 result = BounceMovementMath.ApplyBounceResponse(
                new Vector3(0f, 0f, 3f),
                response,
                null,
                Vector3.up);

            Assert.That(result.z, Is.EqualTo(0f).Within(0.0001f));
        }

        private static BounceContext CreateContext(Vector3 incomingVelocity)
        {
            return new BounceContext(
                incomingVelocity,
                Vector3.zero,
                Vector3.up,
                Vector3.up,
                9f,
                MovementInputFrame.Empty);
        }

        private static void AssertVectorsEqual(Vector3 actual, Vector3 expected, float tolerance)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance));
        }
    }
}
