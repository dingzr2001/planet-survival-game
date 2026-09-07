using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.Crafting.Definitions
{
    [CreateAssetMenu(menuName = "Planet Survival/Crafting/Recipe", fileName = "CraftingRecipe")]
    public sealed class CraftingRecipe : ScriptableObject
    {
        [SerializeField] private string _recipeId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField, Tooltip("All listed resources are consumed by one craft.")]
        private CraftingItemAmount[] _inputs = Array.Empty<CraftingItemAmount>();
        [SerializeField, Tooltip("All listed resources are produced by one craft.")]
        private CraftingItemAmount[] _outputs = Array.Empty<CraftingItemAmount>();
        [SerializeField, Tooltip("Stable condition IDs supplied by the current crafting context, such as station.workbench.")]
        private string[] _requiredConditionIds = Array.Empty<string>();

        public string RecipeId => _recipeId;
        public string DisplayName => _displayName;
        public IReadOnlyList<CraftingItemAmount> Inputs => _inputs;
        public IReadOnlyList<CraftingItemAmount> Outputs => _outputs;
        public IReadOnlyList<string> RequiredConditionIds => _requiredConditionIds;

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(_recipeId) || string.IsNullOrWhiteSpace(_displayName))
            {
                error = "A crafting recipe requires a non-empty ID and display name.";
                return false;
            }

            if (!ValidateItems(_inputs, "input", out error) || !ValidateItems(_outputs, "output", out error))
            {
                return false;
            }

            if (_requiredConditionIds == null || _requiredConditionIds.Length == 0)
            {
                error = $"Recipe '{_recipeId}' requires at least one crafting condition.";
                return false;
            }

            var uniqueConditions = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _requiredConditionIds.Length; i++)
            {
                string conditionId = _requiredConditionIds[i];
                if (string.IsNullOrWhiteSpace(conditionId) || !uniqueConditions.Add(conditionId))
                {
                    error = $"Recipe '{_recipeId}' contains an empty or duplicate condition ID.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public void Configure(
            string recipeId,
            string displayName,
            CraftingItemAmount[] inputs,
            CraftingItemAmount[] outputs,
            params string[] requiredConditionIds)
        {
            _recipeId = recipeId;
            _displayName = displayName;
            _inputs = inputs ?? Array.Empty<CraftingItemAmount>();
            _outputs = outputs ?? Array.Empty<CraftingItemAmount>();
            _requiredConditionIds = requiredConditionIds ?? Array.Empty<string>();
        }

        private bool ValidateItems(CraftingItemAmount[] amounts, string role, out string error)
        {
            if (amounts == null || amounts.Length == 0)
            {
                error = $"Recipe '{_recipeId}' requires at least one {role}.";
                return false;
            }

            var uniqueItems = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < amounts.Length; i++)
            {
                CraftingItemAmount amount = amounts[i];
                if (amount.Item == null)
                {
                    error = $"Recipe '{_recipeId}' has a missing {role} item.";
                    return false;
                }

                if (!amount.Item.IsValid(out string itemError))
                {
                    error = $"Recipe '{_recipeId}' has an invalid {role}: {itemError}";
                    return false;
                }

                if (amount.Quantity <= 0 || !uniqueItems.Add(amount.Item.ItemId))
                {
                    error = $"Recipe '{_recipeId}' contains a non-positive or duplicate {role} amount for '{amount.Item.ItemId}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }
    }
}
