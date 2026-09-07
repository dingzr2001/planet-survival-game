using NUnit.Framework;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Player.Stats;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class InventoryTests
    {
        private ItemDefinition _water;

        [SetUp]
        public void SetUp()
        {
            _water = ScriptableObject.CreateInstance<ItemDefinition>();
            _water.Configure("water_bag", "Water Bag", 2, 3, true, true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_water);
        }

        [Test]
        public void Add_FillsExistingStackBeforeCreatingAnother()
        {
            var inventory = new PlanetSurvival.Inventory.Domain.Inventory(20);

            Assert.That(inventory.Add(_water, 2).Succeeded, Is.True);
            Assert.That(inventory.Add(_water, 3).Succeeded, Is.True);

            Assert.That(inventory.Stacks, Has.Count.EqualTo(2));
            Assert.That(inventory.Stacks[0].Quantity, Is.EqualTo(3));
            Assert.That(inventory.Stacks[1].Quantity, Is.EqualTo(2));
            Assert.That(inventory.UsedCapacity, Is.EqualTo(10));
        }

        [Test]
        public void Add_WhenCapacityIsInsufficient_IsAtomic()
        {
            var inventory = new PlanetSurvival.Inventory.Domain.Inventory(5);
            inventory.Add(_water, 2);

            InventoryOperationResult result = inventory.Add(_water, 1);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure, Is.EqualTo(InventoryFailure.InsufficientCapacity));
            Assert.That(inventory.Stacks, Has.Count.EqualTo(1));
            Assert.That(inventory.Stacks[0].Quantity, Is.EqualTo(2));
            Assert.That(inventory.UsedCapacity, Is.EqualTo(4));
        }

        [Test]
        public void Remove_LastItem_ClearsQuickBarReference()
        {
            var inventory = new PlanetSurvival.Inventory.Domain.Inventory(10);
            inventory.Add(_water, 1);
            string stackId = inventory.Stacks[0].StackId;
            using var quickBar = new QuickBarConfiguration(inventory);
            quickBar.Assign(0, stackId);

            InventoryOperationResult result = inventory.Remove(stackId, 1);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(quickBar.StackIds[0], Is.Null);
            Assert.That(inventory.UsedCapacity, Is.Zero);
        }

        [Test]
        public void InvalidQuantity_DoesNotChangeInventory()
        {
            var inventory = new PlanetSurvival.Inventory.Domain.Inventory(10);

            InventoryOperationResult result = inventory.Add(_water, 0);

            Assert.That(result.Failure, Is.EqualTo(InventoryFailure.InvalidQuantity));
            Assert.That(inventory.Stacks, Is.Empty);
        }

        [Test]
        public void Add_WhenNoSlotRemains_IsAtomicEvenWithFreeCapacity()
        {
            var food = ScriptableObject.CreateInstance<ItemDefinition>();
            food.Configure("ration", "Ration", 1, 1, true, true);
            var inventory = new PlanetSurvival.Inventory.Domain.Inventory(100, 1);
            inventory.Add(_water, 1);

            InventoryOperationResult result = inventory.Add(food, 1);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure, Is.EqualTo(InventoryFailure.InsufficientSlots));
            Assert.That(inventory.Stacks, Has.Count.EqualTo(1));
            Assert.That(inventory.UsedCapacity, Is.EqualTo(2));
            Object.DestroyImmediate(food);
        }

        [Test]
        public void Add_UsesFreeSpaceInExistingStackBeforeRequiringSlot()
        {
            var inventory = new PlanetSurvival.Inventory.Domain.Inventory(20, 1);
            inventory.Add(_water, 1);

            InventoryOperationResult result = inventory.Add(_water, 2);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(inventory.Stacks, Has.Count.EqualTo(1));
            Assert.That(inventory.Stacks[0].Quantity, Is.EqualTo(3));
        }

        [Test]
        public void Transfer_WhenDestinationCannotFitStack_DoesNotChangeEitherInventory()
        {
            var source = new PlanetSurvival.Inventory.Domain.Inventory(20, 2);
            var destination = new PlanetSurvival.Inventory.Domain.Inventory(20, 1);
            var food = ScriptableObject.CreateInstance<ItemDefinition>();
            food.Configure("ration", "Ration", 1, 1, true, true);
            source.Add(_water, 1);
            destination.Add(food, 1);

            InventoryOperationResult result = InventoryTransfer.Transfer(
                source, destination, source.Stacks[0].StackId, 1);

            Assert.That(result.Failure, Is.EqualTo(InventoryFailure.InsufficientSlots));
            Assert.That(source.Stacks, Has.Count.EqualTo(1));
            Assert.That(destination.Stacks, Has.Count.EqualTo(1));
            Object.DestroyImmediate(food);
        }

        [Test]
        public void Transfer_MovesRequestedQuantityAndPreservesCapacityAccounting()
        {
            var source = new PlanetSurvival.Inventory.Domain.Inventory(20, 2);
            var destination = new PlanetSurvival.Inventory.Domain.Inventory(20, 2);
            source.Add(_water, 3);
            string stackId = source.Stacks[0].StackId;

            InventoryOperationResult result = InventoryTransfer.Transfer(source, destination, stackId, 2);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(source.Stacks[0].Quantity, Is.EqualTo(1));
            Assert.That(source.UsedCapacity, Is.EqualTo(2));
            Assert.That(destination.Stacks[0].Quantity, Is.EqualTo(2));
            Assert.That(destination.UsedCapacity, Is.EqualTo(4));
        }

        [Test]
        public void GameSessionReset_ClearsBackpackAndBothStorageSpaces()
        {
            var session = new GameSessionState();
            session.PlayerInventory.Add(_water, 1);
            session.RefrigeratorStorage.Add(_water, 1);
            session.CargoStorage.Add(_water, 1);

            session.Reset();

            Assert.That(session.PlayerInventory.Stacks, Is.Empty);
            Assert.That(session.RefrigeratorStorage.Stacks, Is.Empty);
            Assert.That(session.CargoStorage.Stacks, Is.Empty);
        }

        [Test]
        public void GameSessionReset_WithStartingSupply_ProvisionsCargoOnly()
        {
            var session = new GameSessionState();

            InventoryOperationResult result = session.Reset(_water, GameSessionState.InitialEnergyBarCount);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.PlayerInventory.Stacks, Is.Empty);
            Assert.That(session.RefrigeratorStorage.Stacks, Is.Empty);
            Assert.That(session.CargoStorage.Stacks, Has.Count.EqualTo(4));
            Assert.That(session.CargoStorage.Stacks[0].Definition, Is.SameAs(_water));
            Assert.That(session.CargoStorage.Stacks[0].Quantity, Is.EqualTo(3));
            Assert.That(session.CargoStorage.Stacks[3].Quantity, Is.EqualTo(3));
        }

        [Test]
        public void PlayerInventory_FirstPickup_WhenQuickBarIsEmpty_AssignsFirstSlot()
        {
            var player = new GameObject("Inventory Test Player");
            PlayerInventory playerInventory = player.AddComponent<PlayerInventory>();

            InventoryOperationResult result = playerInventory.Add(_water, 1);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(playerInventory.QuickBar.StackIds[0],
                Is.EqualTo(playerInventory.Inventory.Stacks[0].StackId));
            Object.DestroyImmediate(player);
        }

        [Test]
        public void PlayerInventory_DifferentPickups_AssignConsecutiveQuickBarSlots()
        {
            var food = ScriptableObject.CreateInstance<ItemDefinition>();
            food.Configure("ration", "Ration", 1, 5, true, true);
            var player = new GameObject("Inventory Test Player");
            PlayerInventory playerInventory = player.AddComponent<PlayerInventory>();

            playerInventory.Add(_water, 1);
            playerInventory.Add(food, 1);

            Assert.That(playerInventory.QuickBar.StackIds[0],
                Is.EqualTo(playerInventory.Inventory.Stacks[0].StackId));
            Assert.That(playerInventory.QuickBar.StackIds[1],
                Is.EqualTo(playerInventory.Inventory.Stacks[1].StackId));
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(food);
        }

        [Test]
        public void Use_EnergyBar_ConvertsCaloriesToHungerAndRestoresSanity()
        {
            var energyBar = ScriptableObject.CreateInstance<ItemDefinition>();
            energyBar.Configure("energy_bar", "Energy Bar", 1, 12, true, true,
                new VitalEffect(VitalType.Sanity, 6f));
            energyBar.ConfigureNutrition(500);
            var player = new GameObject("Inventory Test Player");
            PlayerSurvival survival = player.AddComponent<PlayerSurvival>();
            PlayerInventory playerInventory = player.AddComponent<PlayerInventory>();
            survival.Stats.Hunger.SetCurrent(30f);
            survival.Stats.Sanity.SetCurrent(40f);
            playerInventory.Add(energyBar, 1);

            InventoryOperationResult result = playerInventory.Use(playerInventory.Inventory.Stacks[0].StackId);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(survival.Stats.Hunger.Current, Is.EqualTo(50f));
            Assert.That(survival.Stats.Sanity.Current, Is.EqualTo(46f));
            Assert.That(playerInventory.Inventory.Stacks, Is.Empty);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(energyBar);
        }

        [Test]
        public void NutritionBalance_UsesDailyCaloriesForFullHungerMeter()
        {
            Assert.That(NutritionBalance.ToHungerPoints(500), Is.EqualTo(20f));
            Assert.That(NutritionBalance.ToHungerPoints(2500), Is.EqualTo(100f));
            Assert.That(NutritionBalance.ToHungerPoints(-100), Is.Zero);
        }
    }
}
