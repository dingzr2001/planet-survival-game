using NUnit.Framework;
using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.Player.Stats;
using PlanetSurvival.Suit.Domain;
using PlanetSurvival.Suit.Runtime;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class PlayerOxygenConsumptionTests
    {
        private GameObject _player;
        private PlayerOxygen _oxygen;
        private PlayerSurvival _survival;
        private PlayerSpaceSuit _suit;
        private PlayerOxygenConsumption _consumption;
        private OxygenReservoir _suitOxygen;
        private OxygenReservoir _podOxygen;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Oxygen Consumer");
            _survival = _player.AddComponent<PlayerSurvival>();
            _oxygen = _player.AddComponent<PlayerOxygen>();
            _suit = _player.AddComponent<PlayerSpaceSuit>();
            _suitOxygen = new OxygenReservoir(600f, 600f);
            _podOxygen = new OxygenReservoir(1000f, 1000f);
            _suit.Bind(new SpaceSuitResources(_suitOxygen, new LiquidContainer(500)), true);
            _consumption = _player.AddComponent<PlayerOxygenConsumption>();
            _consumption.Bind(_oxygen, _suit, _podOxygen);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_player);

        [Test]
        public void DefaultRate_IsFiveLitersPerGameHour()
        {
            Assert.That(_consumption.ConsumptionLitersPerGameHour, Is.EqualTo(5f));
        }

        [Test]
        public void Tick_WhenSuitIsEquipped_ConsumesSuitAtFixedGameTimeRate()
        {
            _consumption.ConfigureRate(30f);

            _consumption.Tick(2.5f);

            Assert.That(_suitOxygen.CurrentLiters, Is.EqualTo(525f));
            Assert.That(_podOxygen.CurrentLiters, Is.EqualTo(1000f));
            Assert.That(_oxygen.Normalized, Is.EqualTo(.875f).Within(.0001f));
        }

        [Test]
        public void Tick_WhenSuitIsNotEquipped_ConsumesLandingPodSupply()
        {
            _suit.SetEquipped(false);
            _consumption.ConfigureRate(40f);

            _consumption.Tick(3f);

            Assert.That(_suitOxygen.CurrentLiters, Is.EqualTo(600f));
            Assert.That(_podOxygen.CurrentLiters, Is.EqualTo(880f));
            Assert.That(_oxygen.Normalized, Is.EqualTo(.88f).Within(.0001f));
        }

        [Test]
        public void ChangingSuitState_ImmediatelyChangesTheActiveSupply()
        {
            _podOxygen.Consume(500f);
            _suit.SetEquipped(false);
            Assert.That(_consumption.ActiveSupply, Is.SameAs(_podOxygen));
            Assert.That(_oxygen.Normalized, Is.EqualTo(.5f).Within(.0001f));

            _suit.SetEquipped(true);
            Assert.That(_consumption.ActiveSupply, Is.SameAs(_suitOxygen));
            Assert.That(_oxygen.Normalized, Is.EqualTo(1f));
        }

        [Test]
        public void Tick_AfterPlayerDeath_DoesNotConsumeOxygen()
        {
            _survival.Apply(VitalType.Health, -_survival.Stats.Health.Maximum);

            _consumption.Tick(2f);

            Assert.That(_suitOxygen.CurrentLiters, Is.EqualTo(600f));
        }
    }
}
