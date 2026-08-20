using NUnit.Framework;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
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
    }
}
