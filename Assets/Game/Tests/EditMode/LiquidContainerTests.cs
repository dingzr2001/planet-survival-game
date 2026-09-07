using System;
using NUnit.Framework;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Player.Stats;
using PlanetSurvival.Water.Domain;
using PlanetSurvival.Water.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlanetSurvival.Tests
{
    public sealed class LiquidContainerTests
    {
        [Test]
        public void FillFrom_TransfersOnlyBottleRemainingCapacity()
        {
            var bottle = new LiquidContainer(500, 125);
            var supply = new LiquidContainer(20000, 20000);

            int transferred = bottle.FillFrom(supply);

            Assert.That(transferred, Is.EqualTo(375));
            Assert.That(bottle.CurrentMilliliters, Is.EqualTo(500));
            Assert.That(supply.CurrentMilliliters, Is.EqualTo(19625));
        }

        [Test]
        public void FillFrom_WhenSupplyIsLow_DoesNotCreateWater()
        {
            var bottle = new LiquidContainer(500);
            var supply = new LiquidContainer(20000, 180);

            int transferred = bottle.FillFrom(supply);

            Assert.That(transferred, Is.EqualTo(180));
            Assert.That(bottle.CurrentMilliliters, Is.EqualTo(180));
            Assert.That(supply.CurrentMilliliters, Is.Zero);
        }

        [Test]
        public void FillFrom_CommitsBothContainersBeforeRaisingChangeEvents()
        {
            var bottle = new LiquidContainer(500);
            var supply = new LiquidContainer(20000, 1000);
            bool observedCommittedState = false;
            supply.Changed += (_, _) =>
                observedCommittedState = supply.CurrentMilliliters == 500 && bottle.CurrentMilliliters == 500;

            bottle.FillFrom(supply);

            Assert.That(observedCommittedState, Is.True);
        }

        [Test]
        public void TryConsume_RejectsInvalidOrUnavailableVolumesAtomically()
        {
            var bottle = new LiquidContainer(500, 200);

            Assert.That(bottle.TryConsume(0), Is.False);
            Assert.That(bottle.TryConsume(201), Is.False);
            Assert.That(bottle.CurrentMilliliters, Is.EqualTo(200));
            Assert.That(bottle.TryConsume(125), Is.True);
            Assert.That(bottle.CurrentMilliliters, Is.EqualTo(75));
        }

        [Test]
        public void Constructor_RejectsInvalidCapacityAndVolume()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LiquidContainer(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LiquidContainer(500, 501));
        }

        [Test]
        public void GameSessionReset_RestoresBottleAndLandingPodReserve()
        {
            var session = new GameSessionState();
            session.WaterBottle.FillFrom(session.LandingPodWaterSupply);
            session.LandingPodWaterSupply.TryConsume(1000);
            session.SpaceSuit.Oxygen.Consume(100f);
            session.LandingPodOxygenSupply.Consume(1000f);
            session.SurvivalStats.Health.SetCurrent(10f);
            session.SurvivalStats.Sanity.SetCurrent(20f);
            session.SurvivalStats.Hunger.SetCurrent(30f);
            session.SurvivalStats.Thirst.SetCurrent(40f);

            session.Reset();

            Assert.That(session.WaterBottle.CapacityMilliliters,
                Is.EqualTo(GameSessionState.WaterBottleCapacityMilliliters));
            Assert.That(session.WaterBottle.CurrentMilliliters, Is.Zero);
            Assert.That(session.LandingPodWaterSupply.CurrentMilliliters,
                Is.EqualTo(GameSessionState.InitialLandingPodWaterMilliliters));
            Assert.That(session.SpaceSuit.Oxygen.CurrentLiters,
                Is.EqualTo(GameSessionState.InitialSpaceSuitOxygenLiters));
            Assert.That(session.LandingPodOxygenSupply.CurrentLiters,
                Is.EqualTo(GameSessionState.InitialLandingPodOxygenLiters));
            Assert.That(session.SurvivalStats.Health.Current, Is.EqualTo(session.SurvivalStats.Health.Maximum));
            Assert.That(session.SurvivalStats.Sanity.Current, Is.EqualTo(session.SurvivalStats.Sanity.Maximum));
            Assert.That(session.SurvivalStats.Hunger.Current, Is.EqualTo(session.SurvivalStats.Hunger.Maximum));
            Assert.That(session.SurvivalStats.Thirst.Current, Is.EqualTo(session.SurvivalStats.Thirst.Maximum));
            Assert.That(session.PlayerInventory.TotalSlots, Is.EqualTo(GameSessionState.PlayerInventorySlots));
            Assert.That(session.RefrigeratorStorage.TotalSlots, Is.EqualTo(GameSessionState.RefrigeratorSlots));
            Assert.That(session.RefrigeratorStorage.TotalCapacity, Is.EqualTo(GameSessionState.RefrigeratorCapacity));
            Assert.That(session.CargoStorage.TotalSlots, Is.EqualTo(GameSessionState.CargoStorageSlots));
            Assert.That(session.CargoStorage.TotalCapacity, Is.EqualTo(GameSessionState.CargoStorageCapacity));
            Assert.That(session.PlayerInventory.Stacks, Is.Empty);
            Assert.That(session.RefrigeratorStorage.Stacks, Is.Empty);
            Assert.That(session.CargoStorage.Stacks, Is.Empty);
        }

        [Test]
        public void Drink_ConsumesOneHundredMillilitersAndRestoresTwentyPercentThirst()
        {
            var player = new GameObject("Water Bottle Test Player");
            PlayerSurvival survival = player.AddComponent<PlayerSurvival>();
            survival.Stats.Thirst.SetCurrent(45f);
            var container = new LiquidContainer(500, 300);
            PlayerWaterBottle bottle = player.AddComponent<PlayerWaterBottle>();
            bottle.Bind(container, survival);

            WaterDrinkResult result = bottle.TryDrink();

            Assert.That(result, Is.EqualTo(WaterDrinkResult.Succeeded));
            Assert.That(container.CurrentMilliliters, Is.EqualTo(200));
            Assert.That(survival.Stats.Thirst.Current, Is.EqualTo(65f));
            Object.DestroyImmediate(player);
        }

        [Test]
        public void Drink_WhenThirstIsFull_DoesNotWasteWater()
        {
            var player = new GameObject("Full Thirst Test Player");
            PlayerSurvival survival = player.AddComponent<PlayerSurvival>();
            var container = new LiquidContainer(500, 500);
            PlayerWaterBottle bottle = player.AddComponent<PlayerWaterBottle>();
            bottle.Bind(container, survival);

            WaterDrinkResult result = bottle.TryDrink();

            Assert.That(result, Is.EqualTo(WaterDrinkResult.NotThirsty));
            Assert.That(container.CurrentMilliliters, Is.EqualTo(500));
            Object.DestroyImmediate(player);
        }

        [Test]
        public void Drink_WithLessThanOneServing_DoesNotChangeThirstOrWater()
        {
            var player = new GameObject("Low Water Test Player");
            PlayerSurvival survival = player.AddComponent<PlayerSurvival>();
            survival.Stats.Thirst.SetCurrent(50f);
            var container = new LiquidContainer(500, 99);
            PlayerWaterBottle bottle = player.AddComponent<PlayerWaterBottle>();
            bottle.Bind(container, survival);

            WaterDrinkResult result = bottle.TryDrink();

            Assert.That(result, Is.EqualTo(WaterDrinkResult.NotEnoughWater));
            Assert.That(container.CurrentMilliliters, Is.EqualTo(99));
            Assert.That(survival.Stats.Thirst.Current, Is.EqualTo(50f));
            Object.DestroyImmediate(player);
        }
    }
}
