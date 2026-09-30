using NUnit.Framework;
using PlanetSurvival.World.Generation.Landforms;
using PlanetSurvival.World.Ground;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class CraterMarkingsTests
    {
        private const int Seed = 4242;

        private LandformSettings _settings;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<LandformSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void Marks_DustTheFloor_AndThrowEjectaJustPastTheRimInEveryDirection()
        {
            _settings.ConfigureCraters(1f, 0f);
            var craters = new CraterField(Seed, _settings, new Vector2(-1e6f, -1e6f), 0f);
            var appearance = new LandformAppearance();
            Assert.That(craters.TryGetCrater(2, 3, out Crater crater), Is.True);

            Assert.That(CraterMarkings.Sample(craters, crater.CenterX, crater.CenterZ, appearance),
                Is.LessThan(-.9f), "The floor should be dusted.");
            for (int step = 0; step < 16; step++)
            {
                float angle = step * Mathf.PI / 8f;
                float x = crater.CenterX + Mathf.Cos(angle) * crater.Radius * 1.1f;
                float z = crater.CenterZ + Mathf.Sin(angle) * crater.Radius * 1.1f;
                Assert.That(CraterMarkings.Sample(craters, x, z, appearance), Is.GreaterThan(.5f),
                    "Ejecta should blanket the ground right outside the rim.");
            }
        }

        [Test]
        public void Ejecta_BreaksIntoRaysFurtherOut()
        {
            _settings.ConfigureCraters(1f, 0f);
            var craters = new CraterField(Seed, _settings, new Vector2(-1e6f, -1e6f), 0f);
            var appearance = new LandformAppearance();
            Assert.That(craters.TryGetCrater(2, 3, out Crater crater), Is.True);

            float lowest = float.MaxValue;
            float highest = float.MinValue;
            for (int step = 0; step < 72; step++)
            {
                float angle = step * Mathf.PI / 36f;
                float x = crater.CenterX + Mathf.Cos(angle) * crater.Radius * 1.6f;
                float z = crater.CenterZ + Mathf.Sin(angle) * crater.Radius * 1.6f;
                float mark = CraterMarkings.Sample(craters, x, z, appearance);
                lowest = Mathf.Min(lowest, mark);
                highest = Mathf.Max(highest, mark);
            }

            Assert.That(highest - lowest, Is.GreaterThan(.15f), "Ejecta should vary around the crater, as rays.");
            Assert.That(lowest, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void Marks_AreZeroWithoutCraters()
        {
            _settings.ConfigureCraters(0f, 0f);
            var craters = new CraterField(Seed, _settings, Vector2.zero, 0f);

            Assert.That(CraterMarkings.Sample(craters, 120f, -40f, new LandformAppearance()), Is.Zero);
        }
    }
}
