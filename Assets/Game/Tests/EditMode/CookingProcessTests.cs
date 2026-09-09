using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Cooking.Definitions;
using PlanetSurvival.Cooking.Domain;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Crafting.Domain;
using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    public sealed class CookingProcessTests
    {
        private const string OvenConditionId = "station.oven";
        private const float RoastSeconds = 30f;

        private readonly List<Object> _createdAssets = new();
        private ItemDefinition _potato;
        private ItemDefinition _roastPotato;
        private CraftingRecipe _roastRecipe;
        private CookingStationDefinition _oven;

        [SetUp]
        public void SetUp()
        {
            _potato = CreateItem("potato", "Potato", 1, 20);
            _roastPotato = CreateItem("roast_potato", "Roast Potato", 1, 20);
            _roastRecipe = CreateRecipe("roast_potato", "Roast Potato",
                new CraftingItemAmount(_potato, 1), new CraftingItemAmount(_roastPotato, 1));
            _oven = CreateStation("oven", "Oven", OvenConditionId, _roastRecipe);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                Object.DestroyImmediate(_createdAssets[i]);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void Station_OffersTheRecipesItsConditionsCover()
        {
            CraftingRecipe campfireOnly = CreateRecipe("grilled_root", "Grilled Root",
                new CraftingItemAmount(_potato, 1), new CraftingItemAmount(_roastPotato, 1),
                "station.campfire");

            Assert.That(_oven.Supports(_roastRecipe), Is.True);
            Assert.That(_oven.Supports(campfireOnly), Is.False);
            Assert.That(_oven.IsValid(out string error), Is.True, error);
        }

        [Test]
        public void Start_ConsumesIngredientsImmediatelyAndRunsTheTimer()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 3);
            var process = new CookingProcess("oven");

            CraftingResult result = process.Start(_roastRecipe, inventory, _oven.Conditions);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(process.State, Is.EqualTo(CookingState.Cooking));
            Assert.That(inventory.GetQuantity("potato"), Is.EqualTo(2));
            Assert.That(inventory.GetQuantity("roast_potato"), Is.Zero);
            Assert.That(process.RemainingSeconds, Is.EqualTo(RoastSeconds));
        }

        [Test]
        public void Advance_ThenCollect_MovesTheFinishedDishIntoTheBackpack()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 1);
            var process = new CookingProcess("oven");
            process.Start(_roastRecipe, inventory, _oven.Conditions);

            process.Advance(RoastSeconds * .5f);
            Assert.That(process.State, Is.EqualTo(CookingState.Cooking));
            Assert.That(process.Progress, Is.EqualTo(.5f).Within(1e-4f));

            process.Advance(RoastSeconds);
            Assert.That(process.State, Is.EqualTo(CookingState.Ready));
            Assert.That(process.RemainingSeconds, Is.Zero);
            Assert.That(inventory.GetQuantity("roast_potato"), Is.Zero, "The dish waits inside the station.");

            CraftingResult collected = process.Collect(inventory);

            Assert.That(collected.Succeeded, Is.True, collected.Message);
            Assert.That(inventory.GetQuantity("roast_potato"), Is.EqualTo(1));
            Assert.That(process.State, Is.EqualTo(CookingState.Idle));
            Assert.That(process.ActiveRecipe, Is.Null);
        }

        [Test]
        public void Start_WithABatch_ScalesIngredientsTimeAndYield()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 5);
            var process = new CookingProcess("oven");

            CraftingResult result = process.Start(_roastRecipe, inventory, _oven.Conditions, 3);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(process.BatchCount, Is.EqualTo(3));
            Assert.That(inventory.GetQuantity("potato"), Is.EqualTo(2));
            Assert.That(process.TotalSeconds, Is.EqualTo(RoastSeconds * 3f));

            process.Advance(RoastSeconds * 3f);
            Assert.That(process.Collect(inventory).Succeeded, Is.True);
            Assert.That(inventory.GetQuantity("roast_potato"), Is.EqualTo(3));
        }

        [Test]
        public void MaxBatches_CountsHowOftenTheBackpackCanRunTheRecipe()
        {
            var inventory = new InventoryModel(30, 20);

            Assert.That(CookingProcess.MaxBatches(_roastRecipe, inventory), Is.Zero);

            inventory.Add(_potato, 7);

            Assert.That(CookingProcess.MaxBatches(_roastRecipe, inventory), Is.EqualTo(7));
        }

        [Test]
        public void Start_WithABatchLargerThanTheBackpack_ChangesNothing()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 2);
            var process = new CookingProcess("oven");

            CraftingResult result = process.Start(_roastRecipe, inventory, _oven.Conditions, 3);

            Assert.That(result.Failure, Is.EqualTo(CraftingFailure.MissingIngredients));
            Assert.That(process.State, Is.EqualTo(CookingState.Idle));
            Assert.That(inventory.GetQuantity("potato"), Is.EqualTo(2));
        }

        [Test]
        public void Cancel_RefundsTheWholeBatch()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 4);
            var process = new CookingProcess("oven");
            process.Start(_roastRecipe, inventory, _oven.Conditions, 4);

            Assert.That(process.Cancel(inventory).Succeeded, Is.True);
            Assert.That(inventory.GetQuantity("potato"), Is.EqualTo(4));
        }

        [Test]
        public void Start_WithANonPositiveBatch_IsRejected()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 1);
            var process = new CookingProcess("oven");

            CraftingResult result = process.Start(_roastRecipe, inventory, _oven.Conditions, 0);

            Assert.That(result.Failure, Is.EqualTo(CraftingFailure.InvalidRecipe));
            Assert.That(inventory.GetQuantity("potato"), Is.EqualTo(1));
        }

        [Test]
        public void Start_WithoutIngredients_ChangesNothing()
        {
            var inventory = new InventoryModel(30, 20);
            var process = new CookingProcess("oven");

            CraftingResult result = process.Start(_roastRecipe, inventory, _oven.Conditions);

            Assert.That(result.Failure, Is.EqualTo(CraftingFailure.MissingIngredients));
            Assert.That(process.State, Is.EqualTo(CookingState.Idle));
        }

        [Test]
        public void Start_AtAStationThatCannotCookTheRecipe_IsRejected()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 1);
            CookingStationDefinition campfire = CreateStation("campfire", "Campfire", "station.campfire");
            var process = new CookingProcess("campfire");

            CraftingResult result = process.Start(_roastRecipe, inventory, campfire.Conditions);

            Assert.That(result.Failure, Is.EqualTo(CraftingFailure.MissingCondition));
            Assert.That(inventory.GetQuantity("potato"), Is.EqualTo(1));
        }

        [Test]
        public void Start_WhileTheStationIsBusy_IsRejected()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 2);
            var process = new CookingProcess("oven");
            process.Start(_roastRecipe, inventory, _oven.Conditions);

            CraftingResult second = process.Start(_roastRecipe, inventory, _oven.Conditions);

            Assert.That(second.Failure, Is.EqualTo(CraftingFailure.StationBusy));
            Assert.That(inventory.GetQuantity("potato"), Is.EqualTo(1), "The second craft must not consume anything.");
        }

        [Test]
        public void Cancel_WhileCookingRefundsTheIngredients()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_potato, 1);
            var process = new CookingProcess("oven");
            process.Start(_roastRecipe, inventory, _oven.Conditions);
            process.Advance(RoastSeconds * .25f);

            CraftingResult result = process.Cancel(inventory);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(inventory.GetQuantity("potato"), Is.EqualTo(1));
            Assert.That(process.State, Is.EqualTo(CookingState.Idle));
        }

        [Test]
        public void Collect_WithAFullBackpack_KeepsTheDishInTheStation()
        {
            // Exactly one slot, so the free potato slot cannot also hold the roast potato.
            var inventory = new InventoryModel(4, 1);
            inventory.Add(_potato, 1);
            var process = new CookingProcess("oven");
            process.Start(_roastRecipe, inventory, _oven.Conditions);
            process.Advance(RoastSeconds);
            var filler = CreateItem("filler", "Filler", 1, 1);
            inventory.Add(filler, 1);

            CraftingResult result = process.Collect(inventory);

            Assert.That(result.Failure, Is.EqualTo(CraftingFailure.InventoryFull));
            Assert.That(process.State, Is.EqualTo(CookingState.Ready));

            inventory.Remove(inventory.Stacks[0].StackId, 1);

            Assert.That(process.Collect(inventory).Succeeded, Is.True);
            Assert.That(inventory.GetQuantity("roast_potato"), Is.EqualTo(1));
        }

        [Test]
        public void Collect_WhenNothingIsCooking_ReportsNothingReady()
        {
            var process = new CookingProcess("oven");

            CraftingResult result = process.Collect(new InventoryModel(30, 20));

            Assert.That(result.Failure, Is.EqualTo(CraftingFailure.NothingReady));
        }

        private ItemDefinition CreateItem(string itemId, string displayName, int capacity, int stackSize)
        {
            ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.Configure(itemId, displayName, capacity, stackSize, false, true);
            _createdAssets.Add(item);
            return item;
        }

        private CraftingRecipe CreateRecipe(string recipeId, string displayName,
            CraftingItemAmount input, CraftingItemAmount output, string conditionId = OvenConditionId)
        {
            CraftingRecipe recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            recipe.Configure(recipeId, displayName, new[] { input }, new[] { output }, conditionId);
            recipe.ConfigureDuration(RoastSeconds);
            _createdAssets.Add(recipe);
            return recipe;
        }

        private CookingStationDefinition CreateStation(string stationId, string displayName,
            string conditionId, params CraftingRecipe[] recipes)
        {
            CookingStationDefinition station = ScriptableObject.CreateInstance<CookingStationDefinition>();
            station.Configure(stationId, displayName, new[] { conditionId }, recipes);
            _createdAssets.Add(station);
            return station;
        }
    }
}
