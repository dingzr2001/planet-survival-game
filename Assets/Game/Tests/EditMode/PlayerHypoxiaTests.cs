using System.Text.RegularExpressions;
using NUnit.Framework;
using PlanetSurvival.Player.Stats;
using UnityEngine;
using UnityEngine.TestTools;

namespace PlanetSurvival.Tests
{
    public sealed class PlayerHypoxiaTests
    {
        private const float Tolerance = .001f;

        private GameObject _player;
        private PlayerSurvival _survival;
        private PlayerOxygen _oxygen;
        private PlayerHypoxia _hypoxia;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Hypoxia Test Player");
            _survival = _player.AddComponent<PlayerSurvival>();
            _oxygen = _player.AddComponent<PlayerOxygen>();
            _hypoxia = _player.AddComponent<PlayerHypoxia>();

            // Edit Mode never runs Awake; reading the rule assembles the component's references.
            Assert.That(_hypoxia.Rule, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
        }

        [Test]
        public void Tick_WithFullOxygen_LeavesVitalsUntouched()
        {
            _hypoxia.Tick(4f);

            Assert.That(_survival.Stats.Sanity.Current, Is.EqualTo(100f));
            Assert.That(_survival.Stats.Health.Current, Is.EqualTo(100f));
            Assert.That(_hypoxia.Level, Is.EqualTo(HypoxiaLevel.Safe));
        }

        [Test]
        public void Tick_BelowSanityThreshold_DrainsSanityOnly()
        {
            _oxygen.SetNormalized(.3f);

            _hypoxia.Tick(1f);

            Assert.That(_survival.Stats.Sanity.Current, Is.EqualTo(100f - 7.589f).Within(Tolerance));
            Assert.That(_survival.Stats.Health.Current, Is.EqualTo(100f));
            Assert.That(_hypoxia.Level, Is.EqualTo(HypoxiaLevel.LowOxygen));
        }

        [Test]
        public void Tick_BelowHealthThreshold_DrainsSanityAndHealth()
        {
            _oxygen.SetNormalized(.1f);

            _hypoxia.Tick(1f);

            Assert.That(_survival.Stats.Sanity.Current, Is.EqualTo(100f - 21.466f).Within(Tolerance));
            Assert.That(_survival.Stats.Health.Current, Is.EqualTo(100f - 21.213f).Within(Tolerance));
            Assert.That(_hypoxia.Level, Is.EqualTo(HypoxiaLevel.Critical));
        }

        [Test]
        public void Tick_AfterOxygenIsRestored_StopsDrainingWithoutRefunding()
        {
            _oxygen.SetNormalized(0f);
            _hypoxia.Tick(1f);
            float sanityAfterHypoxia = _survival.Stats.Sanity.Current;
            float healthAfterHypoxia = _survival.Stats.Health.Current;

            _oxygen.Restore(_oxygen.Maximum);
            _hypoxia.Tick(1f);

            Assert.That(sanityAfterHypoxia, Is.LessThan(100f));
            Assert.That(_survival.Stats.Sanity.Current, Is.EqualTo(sanityAfterHypoxia));
            Assert.That(_survival.Stats.Health.Current, Is.EqualTo(healthAfterHypoxia));
            Assert.That(_hypoxia.Level, Is.EqualTo(HypoxiaLevel.Safe));
        }

        [Test]
        public void Tick_SplitAndCombinedTimeSteps_ProduceSameVitals()
        {
            _oxygen.SetNormalized(.1f);
            _hypoxia.Tick(1f);
            float combinedSanity = _survival.Stats.Sanity.Current;
            float combinedHealth = _survival.Stats.Health.Current;

            _survival.Stats.Sanity.SetCurrent(100f);
            _survival.Stats.Health.SetCurrent(100f);
            for (int i = 0; i < 10; i++)
            {
                _hypoxia.Tick(.1f);
            }

            Assert.That(_survival.Stats.Sanity.Current, Is.EqualTo(combinedSanity).Within(Tolerance));
            Assert.That(_survival.Stats.Health.Current, Is.EqualTo(combinedHealth).Within(Tolerance));
        }

        [Test]
        public void Tick_WithNonPositiveTimeStep_ChangesNothing()
        {
            _oxygen.SetNormalized(0f);

            _hypoxia.Tick(0f);
            _hypoxia.Tick(-3f);

            Assert.That(_survival.Stats.Sanity.Current, Is.EqualTo(100f));
            Assert.That(_survival.Stats.Health.Current, Is.EqualTo(100f));
        }

        [Test]
        public void Tick_WhenDisabled_ChangesNothing()
        {
            _oxygen.SetNormalized(0f);
            _hypoxia.enabled = false;

            _hypoxia.Tick(1f);

            Assert.That(_survival.Stats.Sanity.Current, Is.EqualTo(100f));
            Assert.That(_survival.Stats.Health.Current, Is.EqualTo(100f));
        }

        [Test]
        public void Tick_AfterDeath_ChangesNothing()
        {
            _survival.Apply(VitalType.Health, -100f);
            _oxygen.SetNormalized(0f);

            _hypoxia.Tick(1f);

            Assert.That(_survival.IsDead, Is.True);
            Assert.That(_survival.Stats.Sanity.Current, Is.EqualTo(100f));
            Assert.That(_survival.Stats.Health.Current, Is.EqualTo(0f));
        }

        [Test]
        public void Tick_WithNonFiniteTimeStep_IsRejectedWithDiagnostics()
        {
            _oxygen.SetNormalized(0f);
            LogAssert.Expect(LogType.Error, new Regex("non-finite time step"));

            _hypoxia.Tick(float.NaN);

            Assert.That(_survival.Stats.Sanity.Current, Is.EqualTo(100f));
            Assert.That(_survival.Stats.Health.Current, Is.EqualTo(100f));
        }

        [Test]
        public void OxygenChange_UpdatesLevelWithoutTicking()
        {
            int levelChanges = 0;
            HypoxiaLevel lastLevel = HypoxiaLevel.Safe;
            _hypoxia.LevelChanged += level => { levelChanges++; lastLevel = level; };

            _oxygen.SetNormalized(.3f);
            _oxygen.SetNormalized(.05f);

            Assert.That(levelChanges, Is.EqualTo(2));
            Assert.That(lastLevel, Is.EqualTo(HypoxiaLevel.Critical));
            Assert.That(_hypoxia.Level, Is.EqualTo(HypoxiaLevel.Critical));
            Assert.That(_hypoxia.LastResult.SanitySeverity, Is.EqualTo(.9f).Within(Tolerance));
            Assert.That(_survival.Stats.Sanity.Current, Is.EqualTo(100f));
        }

        [Test]
        public void Configure_WithIllegalThresholds_IsNormalizedWithDiagnostics()
        {
            LogAssert.Expect(LogType.Warning, new Regex("normalized its thresholds"));

            _hypoxia.Configure(.3f, .6f, 10f, 20f, 1f, 1f);

            Assert.That(_hypoxia.Rule.SanityThreshold, Is.EqualTo(.3f).Within(Tolerance));
            Assert.That(_hypoxia.Rule.HealthThreshold, Is.EqualTo(.3f).Within(Tolerance));
        }

        [Test]
        public void Configure_WithNonFiniteValue_FallsBackWithDiagnostics()
        {
            LogAssert.Expect(LogType.Error, new Regex("non-finite"));

            _hypoxia.Configure(float.NaN, .2f, 30f, 60f, 1.5f, 1.5f);

            Assert.That(_hypoxia.Rule.SanityThreshold, Is.EqualTo(HypoxiaRule.DefaultSanityThreshold));
        }

        [Test]
        public void Configure_WithCustomCurve_IsUsedByTick()
        {
            _hypoxia.Configure(1f, .5f, 10f, 20f, 1f, 1f);
            _oxygen.SetNormalized(.5f);

            _hypoxia.Tick(1f);

            // Ss = (1 - 0.5) / 1 = 0.5 -> 5 sanity; oxygen sits exactly on the health threshold.
            Assert.That(_survival.Stats.Sanity.Current, Is.EqualTo(95f).Within(Tolerance));
            Assert.That(_survival.Stats.Health.Current, Is.EqualTo(100f));
        }
    }
}
