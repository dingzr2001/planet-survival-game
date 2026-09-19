using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class CraftingCatalogTests
    {
        private readonly List<Object> _createdAssets = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                Object.DestroyImmediate(_createdAssets[i]);
            }
        }

        [Test]
        public void IsValid_WhenCatalogProvidesRecipeCondition_ReturnsTrue()
        {
            CraftingRecipe recipe = CreateRecipe("crafting.handheld");
            CraftingCatalog catalog = CreateCatalog("crafting.handheld", recipe);

            bool valid = catalog.IsValid(out string error);

            Assert.That(valid, Is.True, error);
            Assert.That(catalog.Conditions.IsConditionMet("crafting.handheld"), Is.True);
        }

        [Test]
        public void IsValid_WhenRecipeNeedsAnotherStation_ReturnsFalse()
        {
            CraftingRecipe recipe = CreateRecipe("station.oven");
            CraftingCatalog catalog = CreateCatalog("crafting.handheld", recipe);

            bool valid = catalog.IsValid(out string error);

            Assert.That(valid, Is.False);
            Assert.That(error, Does.Contain("cannot satisfy"));
        }

        private CraftingRecipe CreateRecipe(string conditionId)
        {
            ItemDefinition input = ScriptableObject.CreateInstance<ItemDefinition>();
            input.Configure("input", "Input", 1, 10, false, true);
            _createdAssets.Add(input);
            ItemDefinition output = ScriptableObject.CreateInstance<ItemDefinition>();
            output.Configure("output", "Output", 1, 10, false, true);
            _createdAssets.Add(output);

            CraftingRecipe recipe = ScriptableObject.CreateInstance<CraftingRecipe>();
            recipe.Configure(
                "test_recipe",
                "Test Recipe",
                new[] { new CraftingItemAmount(input, 1) },
                new[] { new CraftingItemAmount(output, 1) },
                conditionId);
            _createdAssets.Add(recipe);
            return recipe;
        }

        private CraftingCatalog CreateCatalog(string conditionId, CraftingRecipe recipe)
        {
            CraftingCatalog catalog = ScriptableObject.CreateInstance<CraftingCatalog>();
            catalog.Configure(new[] { conditionId }, recipe);
            _createdAssets.Add(catalog);
            return catalog;
        }
    }
}
