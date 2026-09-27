using System;
using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Power.Definitions
{
    [Serializable]
    public struct CombustionFuelRecipe
    {
        [SerializeField] private ItemDefinition _fuel;
        [SerializeField, Min(1)] private int _fuelItems;
        [SerializeField, Min(1)] private int _oxygenItems;
        [SerializeField, Min(.1f)] private float _burnSeconds;
        [SerializeField, Min(.1f), Tooltip("Chemical energy released by one reaction before conversion losses.")]
        private float _chemicalEnergyPerBurn;
        [SerializeField, Range(0f, 1f)] private float _conversionEfficiency;
        [SerializeField] private ItemDefinition _primaryByproduct;
        [SerializeField, Min(0)] private int _primaryQuantity;
        [SerializeField] private ItemDefinition _secondaryByproduct;
        [SerializeField, Min(0)] private int _secondaryQuantity;

        public ItemDefinition Fuel => _fuel;
        public int FuelItems => _fuelItems;
        public int OxygenItems => _oxygenItems;
        public float BurnSeconds => _burnSeconds;
        public float ElectricityPerBurn => _chemicalEnergyPerBurn * _conversionEfficiency;
        public float ConversionEfficiency => _conversionEfficiency;
        public ItemDefinition PrimaryByproduct => _primaryByproduct;
        public int PrimaryQuantity => _primaryQuantity;
        public ItemDefinition SecondaryByproduct => _secondaryByproduct;
        public int SecondaryQuantity => _secondaryQuantity;

        public CombustionFuelRecipe(ItemDefinition fuel, int fuelItems, int oxygenItems, float burnSeconds,
            float chemicalEnergyPerBurn, float conversionEfficiency, ItemDefinition primaryByproduct,
            int primaryQuantity, ItemDefinition secondaryByproduct = null, int secondaryQuantity = 0)
        {
            _fuel = fuel;
            _fuelItems = fuelItems;
            _oxygenItems = oxygenItems;
            _burnSeconds = burnSeconds;
            _chemicalEnergyPerBurn = chemicalEnergyPerBurn;
            _conversionEfficiency = conversionEfficiency;
            _primaryByproduct = primaryByproduct;
            _primaryQuantity = primaryQuantity;
            _secondaryByproduct = secondaryByproduct;
            _secondaryQuantity = secondaryQuantity;
        }

        public bool IsValid => _fuel != null && _fuelItems > 0 && _oxygenItems > 0 &&
                               _burnSeconds > 0f && _chemicalEnergyPerBurn > 0f &&
                               _conversionEfficiency > 0f && _conversionEfficiency <= 1f &&
                               _primaryByproduct != null && _primaryQuantity > 0 && _secondaryQuantity >= 0 &&
                               (_secondaryQuantity == 0 || _secondaryByproduct != null);
    }
}
