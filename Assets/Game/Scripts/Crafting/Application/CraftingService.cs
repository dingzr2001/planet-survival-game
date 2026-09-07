using System;
using System.Collections.Generic;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Crafting.Domain;
using PlanetSurvival.Inventory.Domain;

namespace PlanetSurvival.Crafting.Application
{
    public sealed class CraftingService
    {
        private readonly PlanetSurvival.Inventory.Domain.Inventory _inventory;
        private readonly QuickBarConfiguration _quickBar;

        public CraftingService(PlanetSurvival.Inventory.Domain.Inventory inventory, QuickBarConfiguration quickBar)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _quickBar = quickBar ?? throw new ArgumentNullException(nameof(quickBar));
        }

        public CraftingResult CanCraft(CraftingRecipe recipe, ICraftingConditionProvider conditions)
        {
            if (recipe == null)
            {
                return CraftingResult.Fail(CraftingFailure.InvalidRecipe, "Crafting recipe is missing.");
            }

            if (!recipe.IsValid(out string recipeError))
            {
                return CraftingResult.Fail(CraftingFailure.InvalidRecipe, recipeError);
            }

            if (conditions == null)
            {
                return CraftingResult.Fail(CraftingFailure.MissingCondition, "Crafting conditions are unavailable.");
            }

            for (int i = 0; i < recipe.RequiredConditionIds.Count; i++)
            {
                string conditionId = recipe.RequiredConditionIds[i];
                if (!conditions.IsConditionMet(conditionId))
                {
                    return CraftingResult.Fail(
                        CraftingFailure.MissingCondition,
                        $"Crafting condition '{conditionId}' is not met.");
                }
            }

            List<InventoryItemAmount> inputs = ConvertAmounts(recipe.Inputs);
            for (int i = 0; i < inputs.Count; i++)
            {
                if (_inventory.GetQuantity(inputs[i].Definition.ItemId) < inputs[i].Quantity)
                {
                    return CraftingResult.Fail(
                        CraftingFailure.MissingIngredients,
                        $"Not enough '{inputs[i].Definition.ItemId}' to craft '{recipe.RecipeId}'.");
                }
            }

            InventoryOperationResult inventoryResult = _inventory.CanApplyTransaction(
                inputs, ConvertAmounts(recipe.Outputs));
            if (!inventoryResult.Succeeded)
            {
                CraftingFailure failure = inventoryResult.Failure == InventoryFailure.InsufficientQuantity
                    ? CraftingFailure.MissingIngredients
                    : CraftingFailure.InventoryFull;
                return CraftingResult.Fail(failure, inventoryResult.Message);
            }

            return CraftingResult.Success();
        }

        public CraftingResult Craft(CraftingRecipe recipe, ICraftingConditionProvider conditions)
        {
            CraftingResult validation = CanCraft(recipe, conditions);
            if (!validation.Succeeded)
            {
                return validation;
            }

            InventoryOperationResult inventoryResult = _inventory.ApplyTransaction(
                ConvertAmounts(recipe.Inputs), ConvertAmounts(recipe.Outputs));
            if (!inventoryResult.Succeeded)
            {
                return CraftingResult.Fail(CraftingFailure.InventoryFull, inventoryResult.Message);
            }

            AssignOutputsToQuickBar(recipe.Outputs);
            return CraftingResult.Success();
        }

        private void AssignOutputsToQuickBar(IReadOnlyList<CraftingItemAmount> outputs)
        {
            for (int outputIndex = 0; outputIndex < outputs.Count; outputIndex++)
            {
                string itemId = outputs[outputIndex].Item.ItemId;
                if (IsItemAssigned(itemId))
                {
                    continue;
                }

                for (int stackIndex = 0; stackIndex < _inventory.Stacks.Count; stackIndex++)
                {
                    ItemStack stack = _inventory.Stacks[stackIndex];
                    if (stack.Definition.ItemId == itemId)
                    {
                        _quickBar.AssignFirstAvailable(stack.StackId);
                        break;
                    }
                }
            }
        }

        private bool IsItemAssigned(string itemId)
        {
            for (int i = 0; i < _quickBar.StackIds.Count; i++)
            {
                ItemStack stack = _inventory.FindStack(_quickBar.StackIds[i]);
                if (stack != null && stack.Definition.ItemId == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<InventoryItemAmount> ConvertAmounts(IReadOnlyList<CraftingItemAmount> amounts)
        {
            var converted = new List<InventoryItemAmount>(amounts.Count);
            for (int i = 0; i < amounts.Count; i++)
            {
                converted.Add(amounts[i].ToInventoryAmount());
            }

            return converted;
        }
    }
}
