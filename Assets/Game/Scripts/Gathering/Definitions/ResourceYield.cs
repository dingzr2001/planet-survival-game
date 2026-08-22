using System;
using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Gathering.Definitions
{
    [Serializable]
    public struct ResourceYield
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField, Min(1)] private int _quantity;

        public ResourceYield(ItemDefinition item, int quantity)
        {
            _item = item;
            _quantity = Mathf.Max(1, quantity);
        }

        public ItemDefinition Item => _item;
        public int Quantity => _quantity;
    }
}
