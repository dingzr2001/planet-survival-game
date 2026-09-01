using System.Text.RegularExpressions;
using NUnit.Framework;
using PlanetSurvival.Player.Stats;
using UnityEngine;
using UnityEngine.TestTools;

namespace PlanetSurvival.Tests
{
    public sealed class PlayerOxygenTests
    {
        private GameObject _player;
        private PlayerSurvival _survival;
        private PlayerOxygen _oxygen;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Oxygen Test Player");
            _survival = _player.AddComponent<PlayerSurvival>();
            _oxygen = _player.AddComponent<PlayerOxygen>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
        }

        [Test]
        public void Consume_CannotPushOxygenBelowZero()
        {
            _oxygen.Consume(250f);

            Assert.That(_oxygen.Current, Is.EqualTo(0f));
            Assert.That(_oxygen.Normalized, Is.EqualTo(0f));
        }

        [Test]
        public void Restore_CannotPushOxygenAboveMaximum()
        {
            _oxygen.Consume(40f);
            _oxygen.Restore(500f);

            Assert.That(_oxygen.Current, Is.EqualTo(_oxygen.Maximum));
            Assert.That(_oxygen.Normalized, Is.EqualTo(1f));
        }

        [Test]
        public void SetCurrent_ClampsIntoTheLegalRange()
        {
            _oxygen.SetCurrent(-20f);
            Assert.That(_oxygen.Current, Is.EqualTo(0f));

            _oxygen.SetCurrent(180f);
            Assert.That(_oxygen.Current, Is.EqualTo(100f));
        }

        [Test]
        public void Changed_IsRaisedWithCurrentAndMaximum()
        {
            float reportedCurrent = -1f;
            float reportedMaximum = -1f;
            _oxygen.Changed += (current, maximum) => { reportedCurrent = current; reportedMaximum = maximum; };

            _oxygen.Consume(25f);

            Assert.That(reportedCurrent, Is.EqualTo(75f));
            Assert.That(reportedMaximum, Is.EqualTo(100f));
        }

        [Test]
        public void Consume_WithNegativeAmount_IsRejectedWithDiagnostics()
        {
            LogAssert.Expect(LogType.Error, new Regex("rejected a negative amount"));

            _oxygen.Consume(-30f);

            Assert.That(_oxygen.Current, Is.EqualTo(100f));
        }

        [Test]
        public void SetCurrent_WithNonFiniteValue_IsRejectedWithDiagnostics()
        {
            LogAssert.Expect(LogType.Error, new Regex("non-finite"));

            _oxygen.SetCurrent(float.NaN);

            Assert.That(_oxygen.Current, Is.EqualTo(100f));
        }

        [Test]
        public void Normalized_WithZeroMaximum_IsZero()
        {
            _survival.Stats.Oxygen.SetBaseMaximum(0f);

            Assert.That(_oxygen.Maximum, Is.EqualTo(0f));
            Assert.That(_oxygen.Normalized, Is.EqualTo(0f));
        }

        [Test]
        public void Oxygen_IsReachableThroughTheSharedVitalApi()
        {
            _survival.Apply(VitalType.Oxygen, -60f);

            Assert.That(_oxygen.Current, Is.EqualTo(40f));
            Assert.That(_survival.Stats.Get(VitalType.Oxygen), Is.SameAs(_survival.Stats.Oxygen));
        }
    }
}
