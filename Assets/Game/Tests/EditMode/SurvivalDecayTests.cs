using NUnit.Framework;
using PlanetSurvival.Core.Flow;
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

        [Test]
        public void Bind_WhenPlayerIsRecreated_PreservesSessionVitals()
        {
            var session = new GameSessionState();
            GameObject indoorPlayer = CreatePlayer(out PlayerSurvival indoorSurvival, out _);
            indoorSurvival.Bind(session.SurvivalStats);
            indoorSurvival.Apply(VitalType.Health, -15f);
            indoorSurvival.Apply(VitalType.Sanity, -20f);
            indoorSurvival.Apply(VitalType.Hunger, -25f);
            indoorSurvival.Apply(VitalType.Thirst, -30f);
            Object.DestroyImmediate(indoorPlayer);

            GameObject outdoorPlayer = CreatePlayer(out PlayerSurvival outdoorSurvival, out _);
            outdoorSurvival.Bind(session.SurvivalStats);

            Assert.That(outdoorSurvival.Stats.Health.Current, Is.EqualTo(85f));
            Assert.That(outdoorSurvival.Stats.Sanity.Current, Is.EqualTo(80f));
            Assert.That(outdoorSurvival.Stats.Hunger.Current, Is.EqualTo(75f));
            Assert.That(outdoorSurvival.Stats.Thirst.Current, Is.EqualTo(70f));
            Object.DestroyImmediate(outdoorPlayer);
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
