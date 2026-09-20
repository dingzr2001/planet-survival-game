using System;
using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Building.Definitions
{
    /// <summary>A construction cost that may be paid by any item in a material group.</summary>
    [Serializable]
    public struct TaggedBuildingMaterialAmount
    {
        [SerializeField] private ItemMaterialTag _materialTag;
        [SerializeField, Min(1)] private int _quantity;

        public TaggedBuildingMaterialAmount(ItemMaterialTag materialTag, int quantity)
        {
            _materialTag = materialTag;
            _quantity = quantity;
        }

        public ItemMaterialTag MaterialTag => _materialTag;
        public int Quantity => _quantity;
    }
}
