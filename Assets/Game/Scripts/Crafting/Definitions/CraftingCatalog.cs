using System;
using System.Collections.Generic;
using PlanetSurvival.Crafting.Domain;
using UnityEngine;

namespace PlanetSurvival.Crafting.Definitions
{
    /// <summary>
    /// Recipes available from one crafting context. The catalog owns the conditions supplied by that
    /// context, which keeps handheld crafting separate from ovens and other stations.
    /// </summary>
    [CreateAssetMenu(menuName = "Planet Survival/Crafting/Catalog", fileName = "CraftingCatalog")]
    public sealed class CraftingCatalog : ScriptableObject
    {
        [SerializeField, Tooltip("Conditions supplied while this catalog is open, such as crafting.handheld.")]
        private string[] _providedConditionIds = Array.Empty<string>();
        [SerializeField, Tooltip("Recipes shown in the crafting drawer, in display order.")]
        private CraftingRecipe[] _recipes = Array.Empty<CraftingRecipe>();

        private CraftingConditionSet _conditions;

        public IReadOnlyList<CraftingRecipe> Recipes => _recipes;

        public ICraftingConditionProvider Conditions
        {
            get
            {
                _conditions ??= new CraftingConditionSet(_providedConditionIds);
                return _conditions;
            }
        }

        public bool IsValid(out string error)
        {
            if (_providedConditionIds == null || _providedConditionIds.Length == 0)
            {
                error = $"Crafting catalog '{name}' must provide at least one condition.";
                return false;
            }

            var uniqueConditions = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _providedConditionIds.Length; i++)
            {
                string conditionId = _providedConditionIds[i];
                if (string.IsNullOrWhiteSpace(conditionId) || !uniqueConditions.Add(conditionId))
                {
                    error = $"Crafting catalog '{name}' contains an empty or duplicate condition ID.";
                    return false;
                }
            }

            var uniqueRecipes = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _recipes.Length; i++)
            {
                CraftingRecipe recipe = _recipes[i];
                if (recipe == null)
                {
                    error = $"Crafting catalog '{name}' lists a missing recipe.";
                    return false;
                }

                if (!recipe.IsValid(out string recipeError))
                {
                    error = $"Crafting catalog '{name}' lists an invalid recipe: {recipeError}";
                    return false;
                }

                if (!uniqueRecipes.Add(recipe.RecipeId))
                {
                    error = $"Crafting catalog '{name}' lists recipe '{recipe.RecipeId}' twice.";
                    return false;
                }

                for (int conditionIndex = 0; conditionIndex < recipe.RequiredConditionIds.Count; conditionIndex++)
                {
                    if (!Conditions.IsConditionMet(recipe.RequiredConditionIds[conditionIndex]))
                    {
                        error = $"Crafting catalog '{name}' cannot satisfy recipe '{recipe.RecipeId}'.";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        public void Configure(string[] providedConditionIds, params CraftingRecipe[] recipes)
        {
            _providedConditionIds = providedConditionIds ?? Array.Empty<string>();
            _recipes = recipes ?? Array.Empty<CraftingRecipe>();
            _conditions = null;
        }
    }
}
