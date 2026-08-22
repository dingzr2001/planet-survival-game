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

        [Test]
        public void Tick_SplitAndCombinedTimeSteps_ProduceSameVitals()
        {
            GameObject combinedPlayer = CreatePlayer(out PlayerSurvival combined, out SurvivalDecay combinedDecay);
            GameObject splitPlayer = CreatePlayer(out PlayerSurvival split, out SurvivalDecay splitDecay);
            combinedDecay.Configure(2f, 3f, 5f);
            splitDecay.Configure(2f, 3f, 5f);

            combinedDecay.Tick(8f);
            for (int i = 0; i < 16; i++) splitDecay.Tick(.5f);

            Assert.That(split.Stats.Hunger.Current, Is.EqualTo(combined.Stats.Hunger.Current).Within(.0001f));
            Assert.That(split.Stats.Thirst.Current, Is.EqualTo(combined.Stats.Thirst.Current).Within(.0001f));
            Assert.That(split.Stats.Health.Current, Is.EqualTo(combined.Stats.Health.Current).Within(.0001f));
            Object.DestroyImmediate(combinedPlayer);
            Object.DestroyImmediate(splitPlayer);
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
