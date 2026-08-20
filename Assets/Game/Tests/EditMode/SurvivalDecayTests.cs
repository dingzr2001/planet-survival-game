using NUnit.Framework;
using PlanetSurvival.Player.Stats;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class SurvivalDecayTests
    {
        [Test]
        public void Tick_WhenVitalsAreEmpty_StacksHealthLossForEachEmptyVital()
        {
            GameObject player = CreatePlayer(out PlayerSurvival survival, out SurvivalDecay decay);
            decay.Configure(0f, 0f, 5f);
            survival.Stats.Sanity.SetCurrent(0f);
            survival.Stats.Hunger.SetCurrent(0f);

            decay.Tick(2f);

            Assert.That(survival.Stats.Health.Current, Is.EqualTo(80f));
            Object.DestroyImmediate(player);
        }

        [Test]
        public void HealthReachesZero_RaisesDeathOnceAndRejectsFurtherChanges()
        {
            GameObject player = CreatePlayer(out PlayerSurvival survival, out _);
            int deathCount = 0;
            survival.Died += () => deathCount++;

            survival.Apply(VitalType.Health, -100f);
            survival.Apply(VitalType.Health, 50f);

            Assert.That(survival.IsDead, Is.True);
            Assert.That(survival.Stats.Health.Current, Is.EqualTo(0f));
            Assert.That(deathCount, Is.EqualTo(1));
            Object.DestroyImmediate(player);
        }

        private static GameObject CreatePlayer(out PlayerSurvival survival, out SurvivalDecay decay)
        {
            var player = new GameObject("Survival Test Player");
            survival = player.AddComponent<PlayerSurvival>();
            decay = player.AddComponent<SurvivalDecay>();
            return player;
        }
    }
}
