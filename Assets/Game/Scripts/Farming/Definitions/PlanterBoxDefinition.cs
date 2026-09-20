using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Farming.Definitions
{
    [CreateAssetMenu(menuName = "Planet Survival/Farming/Planter Box", fileName = "PlanterBox")]
    public sealed class PlanterBoxDefinition : ScriptableObject
    {
        [SerializeField, Min(1)] private int _waterCapacityMilliliters = 10000;
        [SerializeField, Min(1)] private int _minimumWaterMilliliters = 1000;
        [SerializeField, Min(.1f)] private float _carbonDioxideCapacityLiters = 500f;
        [SerializeField, Min(.1f)] private float _minimumCarbonDioxideLiters = 25f;
        [SerializeField, Min(.1f)] private float _oxygenCapacityLiters = 500f;
        [SerializeField, Min(.001f)] private float _oxygenLitersPerSecond = 1f;
        [SerializeField, Min(.001f)] private float _waterMillilitersPerOxygenLiter = 2f;
        [SerializeField, Min(.001f)] private float _carbonDioxideLitersPerOxygenLiter = 1f;
        [SerializeField] private ItemDefinition _carbonDioxideCanister;
        [SerializeField, Min(.1f)] private float _carbonDioxideLitersPerCanister = 50f;

        public int WaterCapacityMilliliters => _waterCapacityMilliliters;
        public int MinimumWaterMilliliters => _minimumWaterMilliliters;
        public float CarbonDioxideCapacityLiters => _carbonDioxideCapacityLiters;
        public float MinimumCarbonDioxideLiters => _minimumCarbonDioxideLiters;
        public float OxygenCapacityLiters => _oxygenCapacityLiters;
        public float OxygenLitersPerSecond => _oxygenLitersPerSecond;
        public float WaterMillilitersPerOxygenLiter => _waterMillilitersPerOxygenLiter;
        public float CarbonDioxideLitersPerOxygenLiter => _carbonDioxideLitersPerOxygenLiter;
        public ItemDefinition CarbonDioxideCanister => _carbonDioxideCanister;
        public float CarbonDioxideLitersPerCanister => _carbonDioxideLitersPerCanister;

        public bool IsValid(out string error)
        {
            if (_waterCapacityMilliliters <= 0 || _minimumWaterMilliliters <= 0 ||
                _minimumWaterMilliliters > _waterCapacityMilliliters)
            {
                error = "Planter water capacity and minimum must be positive, with the minimum within capacity.";
                return false;
            }

            if (_carbonDioxideCapacityLiters <= 0f || _minimumCarbonDioxideLiters <= 0f ||
                _minimumCarbonDioxideLiters > _carbonDioxideCapacityLiters || _oxygenCapacityLiters <= 0f ||
                _oxygenLitersPerSecond <= 0f || _waterMillilitersPerOxygenLiter <= 0f ||
                _carbonDioxideLitersPerOxygenLiter <= 0f || _carbonDioxideLitersPerCanister <= 0f)
            {
                error = "Planter gas capacities, thresholds and conversion rates must be positive.";
                return false;
            }

            if (_carbonDioxideCanister == null)
            {
                error = "Planter needs a carbon dioxide canister item.";
                return false;
            }

            if (!_carbonDioxideCanister.IsValid(out string itemError))
            {
                error = $"Planter needs a valid carbon dioxide canister: {itemError}";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Configure(int waterCapacityMilliliters, int minimumWaterMilliliters,
            float carbonDioxideCapacityLiters, float minimumCarbonDioxideLiters, float oxygenCapacityLiters,
            float oxygenLitersPerSecond, float waterMillilitersPerOxygenLiter,
            float carbonDioxideLitersPerOxygenLiter, ItemDefinition carbonDioxideCanister,
            float carbonDioxideLitersPerCanister)
        {
            _waterCapacityMilliliters = waterCapacityMilliliters;
            _minimumWaterMilliliters = minimumWaterMilliliters;
            _carbonDioxideCapacityLiters = carbonDioxideCapacityLiters;
            _minimumCarbonDioxideLiters = minimumCarbonDioxideLiters;
            _oxygenCapacityLiters = oxygenCapacityLiters;
            _oxygenLitersPerSecond = oxygenLitersPerSecond;
            _waterMillilitersPerOxygenLiter = waterMillilitersPerOxygenLiter;
            _carbonDioxideLitersPerOxygenLiter = carbonDioxideLitersPerOxygenLiter;
            _carbonDioxideCanister = carbonDioxideCanister;
            _carbonDioxideLitersPerCanister = carbonDioxideLitersPerCanister;
        }
    }
}
