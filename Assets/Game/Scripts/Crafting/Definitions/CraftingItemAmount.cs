using System;
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
    }
}
