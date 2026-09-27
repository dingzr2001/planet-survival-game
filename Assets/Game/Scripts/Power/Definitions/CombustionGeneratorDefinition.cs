using System;
using System.Collections.Generic;
using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Power.Definitions
{
    [CreateAssetMenu(menuName = "Planet Survival/Power/Combustion Generator", fileName = "CombustionGenerator")]
    public sealed class CombustionGeneratorDefinition : ScriptableObject
    {
        [SerializeField] private ItemDefinition _oxygenItem;
        [SerializeField, Min(1)] private int _fuelCapacity = 12;
        [SerializeField, Min(1)] private int _oxygenCapacity = 20;
        [SerializeField, Min(1)] private int _byproductCapacity = 20;
        [SerializeField] private CombustionFuelRecipe[] _fuels = Array.Empty<CombustionFuelRecipe>();

        public ItemDefinition OxygenItem => _oxygenItem;
        public int FuelCapacity => _fuelCapacity;
        public int OxygenCapacity => _oxygenCapacity;
        public int ByproductCapacity => _byproductCapacity;
        public IReadOnlyList<CombustionFuelRecipe> Fuels => _fuels;

        public bool TryGetRecipe(ItemDefinition item, out CombustionFuelRecipe recipe)
        {
            foreach (CombustionFuelRecipe candidate in _fuels)
            {
                if (item != null && candidate.Fuel != null && candidate.Fuel.ItemId == item.ItemId)
                {
                    recipe = candidate;
                    return true;
                }
            }
            recipe = default;
            return false;
        }

        public bool IsValid(out string error)
        {
            if (_oxygenItem == null || _fuelCapacity <= 0 || _oxygenCapacity <= 0 ||
                _byproductCapacity <= 0 || _fuels == null || _fuels.Length == 0)
            {
                error = "Combustion generator needs oxygen, positive capacities, and fuel recipes.";
                return false;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (CombustionFuelRecipe recipe in _fuels)
            {
                if (!recipe.IsValid || !ids.Add(recipe.Fuel.ItemId) || recipe.Fuel.ItemId == _oxygenItem.ItemId ||
                    recipe.FuelItems > _fuelCapacity || recipe.OxygenItems > _oxygenCapacity ||
                    recipe.PrimaryQuantity + recipe.SecondaryQuantity > _byproductCapacity)
                {
                    error = "Combustion generator has a duplicate or invalid fuel recipe, or a reaction exceeds its buffers.";
                    return false;
                }
            }
            error = string.Empty;
            return true;
        }

        public void Configure(ItemDefinition oxygenItem, int fuelCapacity, int oxygenCapacity,
            int byproductCapacity, params CombustionFuelRecipe[] fuels)
        {
            _oxygenItem = oxygenItem;
            _fuelCapacity = fuelCapacity;
            _oxygenCapacity = oxygenCapacity;
            _byproductCapacity = byproductCapacity;
            _fuels = fuels ?? Array.Empty<CombustionFuelRecipe>();
        }
    }
}
