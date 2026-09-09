using System;
using System.Collections.Generic;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Crafting.Domain;
using UnityEngine;

namespace PlanetSurvival.Cooking.Definitions
{
    /// <summary>
    /// One appliance or cooking spot: which recipes its menu offers and which crafting conditions it
    /// satisfies. A recipe only appears here when the station meets every condition the recipe requires,
    /// so an oven recipe can never be prepared on a campfire even if both assets list it.
    /// </summary>
    [CreateAssetMenu(menuName = "Planet Survival/Cooking/Station", fileName = "CookingStation")]
    public sealed class CookingStationDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Stable ID used to keep this station's pot cooking across scene changes.")]
        private string _stationId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField, Tooltip("Crafting condition IDs this station provides, such as station.oven.")]
        private string[] _providedConditionIds = Array.Empty<string>();
        [SerializeField, Tooltip("Every dish this station can prepare.")]
        private CraftingRecipe[] _recipes = Array.Empty<CraftingRecipe>();

        private CraftingConditionSet _conditions;

        public string StationId => _stationId;
        public string DisplayName => _displayName;
        public IReadOnlyList<string> ProvidedConditionIds => _providedConditionIds;
        public IReadOnlyList<CraftingRecipe> Recipes => _recipes;

        /// <summary>The conditions this station contributes when a recipe is validated.</summary>
        public ICraftingConditionProvider Conditions
        {
            get
            {
                // Built on first use rather than in OnEnable, so the set also exists for definitions
                // created in memory by tests and editor tooling.
                _conditions ??= new CraftingConditionSet(_providedConditionIds);
                return _conditions;
            }
        }

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(_stationId) || string.IsNullOrWhiteSpace(_displayName))
            {
                error = "A cooking station requires a non-empty ID and display name.";
                return false;
            }

            if (_providedConditionIds == null || _providedConditionIds.Length == 0)
            {
                error = $"Cooking station '{_stationId}' must provide at least one crafting condition.";
                return false;
            }

            var uniqueRecipes = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _recipes.Length; i++)
            {
                CraftingRecipe recipe = _recipes[i];
                if (recipe == null)
                {
                    error = $"Cooking station '{_stationId}' lists a missing recipe.";
                    return false;
                }

                if (!recipe.IsValid(out string recipeError))
                {
                    error = $"Cooking station '{_stationId}' lists an invalid recipe: {recipeError}";
                    return false;
                }

                if (!uniqueRecipes.Add(recipe.RecipeId))
                {
                    error = $"Cooking station '{_stationId}' lists recipe '{recipe.RecipeId}' twice.";
                    return false;
                }

                if (!Supports(recipe))
                {
                    error = $"Cooking station '{_stationId}' cannot satisfy the conditions of '{recipe.RecipeId}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>True when this station meets every condition the recipe asks for.</summary>
        public bool Supports(CraftingRecipe recipe)
        {
            if (recipe == null)
            {
                return false;
            }

            for (int i = 0; i < recipe.RequiredConditionIds.Count; i++)
            {
                if (!Conditions.IsConditionMet(recipe.RequiredConditionIds[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public void Configure(string stationId, string displayName, string[] providedConditionIds,
            params CraftingRecipe[] recipes)
        {
            _stationId = stationId;
            _displayName = displayName;
            _providedConditionIds = providedConditionIds ?? Array.Empty<string>();
            _recipes = recipes ?? Array.Empty<CraftingRecipe>();
            _conditions = null;
        }
    }
}
