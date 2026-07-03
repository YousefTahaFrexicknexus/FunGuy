using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Funguy.MushroomRunner.Tests.EditMode
{
    public sealed class BounceAreaGenerationProfileTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        private BounceAreaGenerationProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = ScriptableObject.CreateInstance<BounceAreaGenerationProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            if (profile != null)
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void GetSeed_ReturnsConfiguredSeed_WhenRandomizeSeedIsDisabled()
        {
            SetPrivateField("seed", 4242);
            SetPrivateField("randomizeSeed", false);

            Assert.That(profile.GetSeed(), Is.EqualTo(4242));
        }

        [Test]
        public void GetSeed_ProducesDifferentValuesAcrossRapidCalls_WhenRandomizeSeedIsEnabled()
        {
            SetPrivateField("seed", 1337);
            SetPrivateField("randomizeSeed", true);

            HashSet<int> samples = new();
            for (int index = 0; index < 8; index++)
            {
                samples.Add(profile.GetSeed());
            }

            Assert.That(samples.Count, Is.GreaterThan(1));
        }

        private void SetPrivateField(string fieldName, object value)
        {
            FieldInfo field = typeof(BounceAreaGenerationProfile).GetField(fieldName, InstanceNonPublic);
            Assert.That(field, Is.Not.Null, $"Missing private field '{fieldName}'.");
            field.SetValue(profile, value);
        }
    }
}
