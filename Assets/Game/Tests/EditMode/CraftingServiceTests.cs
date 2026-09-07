using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Crafting.Application;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Crafting.Domain;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class CraftingServiceTests
    {
        private readonly List<Object> _createdAssets = new();
        private ItemDefinition _stone;
        private ItemDefinition _fiber;
        private ItemDefinition _axe;
        private ItemDefinition _scrap;

        [SetUp]
        public void SetUp()
        {
            _stone = CreateItem("stone", 1, 20);
            _fiber = CreateItem("fiber", 1, 20);
            _axe = CreateItem("axe", 2, 1);
            _scrap = CreateItem("scrap", 1, 10);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                Object.DestroyImmediate(_createdAssets[i]);
            }
        }

        [Test]
        public void Craft_WithMultipleInputsAndOutputs_ConsumesAndProducesAtomically()
        {
            var inventory = new PlanetSurvival.Inventory.Domain.Inventory(30, 10);
            inventory.Add(_stone, 3);
            inventory.Add(_fiber, 2);
            using var quickBar = new QuickBarConfiguration(inventory);
            CraftingRecipe recipe = CreateRecipe(
                new[] { new CraftingItemAmount(_stone, 2), new CraftingItemAmount(_fiber, 1) },
                new[] { new CraftingItemAmount(_axe, 1), new CraftingItemAmount(_scrap, 2) });
            var service = new CraftingService(inventory, quickBar);

            CraftingResult result = service.Craft(recipe, Conditions("station.workbench"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(inventory.GetQuantity("stone"), Is.EqualTo(1));
            Assert.That(inventory.GetQuantity("fiber"), Is.EqualTo(1));
            Assert.That(inventory.GetQuantity("axe"), Is.EqualTo(1));
            Assert.That(inventory.GetQuantity("scrap"), Is.EqualTo(2));
            Assert.That(AssignedItemId(inventory, quickBar, 0), Is.EqualTo("axe"));
            Assert.That(AssignedItemId(inventory, quickBar, 1), Is.EqualTo("scrap"));
        }

        [Test]
        public void Craft_WhenConditionIsMissing_DoesNotConsumeInputs()
        {
            var inventory = new PlanetSurvival.Inventory.Domain.Inventory(10);
            inventory.Add(_stone, 2);
            using var quickBar = new QuickBarConfiguration(inventory);
            CraftingRecipe recipe = CreateRecipe(
                new[] { new CraftingItemAmount(_stone, 2) },
                new[] { new CraftingItemAmount(_axe, 1) });
            var service = new CraftingService(inventory, quickBar);

            CraftingResult result = service.Craft(recipe, Conditions("player.outdoors"));

            Assert.That(result.Failure, Is.EqualTo(CraftingFailure.MissingCondition));
            Assert.That(inventory.GetQuantity("stone"), Is.EqualTo(2));
            Assert.That(inventory.GetQuantity("axe"), Is.Zero);
        }

        [Test]
        public void Craft_WhenOutputsDoNotFit_DoesNotConsumeInputs()
        {
            var largeOutput = CreateItem("large_output", 6, 1);
            var inventory = new PlanetSurvival.Inventory.Domain.Inventory(5, 5);
            inventory.Add(_stone, 2);
            using var quickBar = new QuickBarConfiguration(inventory);
            CraftingRecipe recipe = CreateRecipe(
                new[] { new CraftingItemAmount(_stone, 2) },
                new[] { new CraftingItemAmount(largeOutput, 1) });
            var service = new CraftingService(inventory, quickBar);

            CraftingResult result = service.Craft(recipe, Conditions("station.workbench"));

            Assert.That(result.Failure, Is.EqualTo(CraftingFailure.InventoryFull));
            Assert.That(inventory.GetQuantity("stone"), Is.EqualTo(2));
            Assert.That(inventory.GetQuantity("large_output"), Is.Zero);
        }

        [Test]
        public void Craft_WhenConsumedInputsFreeEnoughSpace_SucceedsFromFullInventory()
        {
            var inventory = new PlanetSurvival.Inventory.Domain.Inventory(2, 2);
            inventory.Add(_stone, 2);
            using var quickBar = new QuickBarConfiguration(inventory);
            CraftingRecipe recipe = CreateRecipe(
                new[] { new CraftingItemAmount(_stone, 2) },
                new[] { new CraftingItemAmount(_axe, 1) });
            var service = new CraftingService(inventory, quickBar);

            CraftingResult result = service.Craft(recipe, Conditions("station.workbench"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(inventory.GetQuantity("stone"), Is.Zero);
            Assert.That(inventory.GetQuantity("axe"), Is.EqualTo(1));
            Assert.That(inventory.UsedCapacity, Is.EqualTo(2));
        }

        [Test]
        public void Craft_WhenQuickBarIsFull_KeepsOutputInBackpackWithoutReplacingAssignments()
        {
            var inventory = new PlanetSurvival.Inventory.Domain.Inventory(100, 30);
            using var quickBar = new QuickBarConfiguration(inventory);
            for (int i = 0; i < QuickBarConfiguration.SlotCount; i++)
            {
                ItemDefinition filler = CreateItem($"filler_{i}", 1, 1);
                inventory.Add(filler, 1);
                Assert.That(quickBar.Assign(i, inventory.Stacks[i].StackId), Is.True);
            }

            inventory.Add(_stone, 1);
            string[] assignmentsBefore = new string[QuickBarConfiguration.SlotCount];
            for (int i = 0; i < assignmentsBefore.Length; i++)
            {
                assignmentsBefore[i] = quickBar.StackIds[i];
            }

            CraftingRecipe recipe = CreateRecipe(
                new[] { new CraftingItemAmount(_stone, 1) },
                new[] { new CraftingItemAmount(_scrap, 1) });
            var service = new CraftingService(inventory, quickBar);

            CraftingResult result = service.Craft(recipe, Conditions("station.workbench"));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(inventory.GetQuantity("scrap"), Is.EqualTo(1));
            Assert.That(quickBar.StackIds, Is.EqualTo(assignmentsBefore));
        }

        private ItemDefinition CreateItem(string itemId, int capacity, int maximumStackSize)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.Configure(itemId, itemId, capacity, maximumStackSize, false, true);
            _createdAssets.Add(item);
            return item;
        }

        private CraftingRecipe CreateRecipe(CraftingItemAmount[] inputs, CraftingItemAmount[] outputs)
        {
            CraftingRecipe recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            recipe.Configure("test_recipe", "Test Recipe", inputs, outputs, "station.workbench");
            _createdAssets.Add(recipe);
            return recipe;
        }

        private static CraftingConditionSet Conditions(params string[] conditionIds)
        {
            return new CraftingConditionSet(conditionIds);
        }

        private static string AssignedItemId(
            PlanetSurvival.Inventory.Domain.Inventory inventory,
            QuickBarConfiguration quickBar,
            int slotIndex)
        {
            return inventory.FindStack(quickBar.StackIds[slotIndex])?.Definition.ItemId;
        }
    }
}
