using System;
using System.Collections.Generic;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Crafting.Definitions
{
    [Serializable]
    public struct CraftingItemAmount
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField, Min(1)] private int _quantity;

        public CraftingItemAmount(ItemDefinition item, int quantity)
        {
            _item = item;
            _quantity = quantity;
        }

        public ItemDefinition Item => _item;
        public int Quantity => _quantity;

        public InventoryItemAmount ToInventoryAmount()
        {
            return new InventoryItemAmount(_item, _quantity);
        }

        /// <summary>
        /// Shapes a recipe side into the amounts an inventory transaction expects. The multiplier is the
        /// number of times the recipe runs, so one transaction covers a whole batch.
        /// </summary>
        public static List<InventoryItemAmount> ToInventoryAmounts(
            IReadOnlyList<CraftingItemAmount> amounts, int multiplier = 1)
        {
            if (amounts == null)
            {
                return new List<InventoryItemAmount>();
            }

            var converted = new List<InventoryItemAmount>(amounts.Count);
            for (int i = 0; i < amounts.Count; i++)
            {
                CraftingItemAmount amount = amounts[i];
                converted.Add(new InventoryItemAmount(amount.Item, amount.Quantity * multiplier));
            }

            return converted;
        }
    }
}
