using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Mining.Definitions
{
    /// <summary>Static balance and connection contract for one kind of mining drill.</summary>
    [CreateAssetMenu(menuName = "Planet Survival/Mining/Mining Drill", fileName = "MiningDrillDefinition")]
    public sealed class MiningDrillDefinition : ScriptableObject
    {
        [Header("Deposit")]
        [SerializeField] private string _requiredTerrainId = "iron";
        [SerializeField] private string _requiredTerrainDisplayName = "iron";
        [SerializeField] private ItemDefinition _outputItem;
        [SerializeField, Min(.01f), Tooltip("Ore items produced per real second while powered.")]
        private float _productionPerSecond = .2f;
        [SerializeField, Min(1), Tooltip("Ore items retained before production pauses.")]
        private int _oreCapacity = 20;
        [SerializeField, Min(.01f), Tooltip("Maximum ore items per real second an automation connection may remove.")]
        private float _outputPerSecond = 2f;

        [Header("Electricity")]
        [SerializeField, Min(.01f), Tooltip("Electric energy consumed per ore item.")]
        private float _electricityPerOre = 5f;
        [SerializeField, Min(.01f), Tooltip("Small input buffer used by a future power connector.")]
        private float _electricityCapacity = 25f;

        [Header("Petroleum")]
        [SerializeField] private ItemDefinition _petroleumItem;
        [SerializeField, Min(.01f), Tooltip("Liquid petroleum represented by one inventory canister.")]
        private float _petroleumPerItem = 5f;
        [SerializeField, Min(.01f), Tooltip("Petroleum consumed per ore item.")]
        private float _petroleumPerOre = 1f;
        [SerializeField, Min(.01f), Tooltip("Internal liquid fuel tank size.")]
        private float _petroleumCapacity = 20f;

        public string RequiredTerrainId => _requiredTerrainId;
        public string DisplayName => string.IsNullOrWhiteSpace(name) ? "Mining Drill" : name;
        public string RequiredTerrainDisplayName => string.IsNullOrWhiteSpace(_requiredTerrainDisplayName)
            ? _requiredTerrainId
            : _requiredTerrainDisplayName;
        public ItemDefinition OutputItem => _outputItem;
        public float ProductionPerSecond => Mathf.Max(.01f, _productionPerSecond);
        public int OreCapacity => Mathf.Max(1, _oreCapacity);
        public float OutputPerSecond => Mathf.Max(.01f, _outputPerSecond);
        public float ElectricityPerOre => Mathf.Max(.01f, _electricityPerOre);
        public float ElectricityCapacity => Mathf.Max(.01f, _electricityCapacity);
        public ItemDefinition PetroleumItem => _petroleumItem;
        public float PetroleumPerItem => Mathf.Max(.01f, _petroleumPerItem);
        public float PetroleumPerOre => Mathf.Max(.01f, _petroleumPerOre);
        public float PetroleumCapacity => Mathf.Max(.01f, _petroleumCapacity);

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(_requiredTerrainId))
            {
                error = "A mining drill requires a terrain ID.";
                return false;
            }

            if (_outputItem == null)
            {
                error = "The ore output is missing.";
                return false;
            }

            if (!_outputItem.IsValid(out string outputError))
            {
                error = $"The ore output is invalid: {outputError}";
                return false;
            }

            if (_petroleumItem == null)
            {
                error = "The petroleum item is missing.";
                return false;
            }

            if (!_petroleumItem.IsValid(out string petroleumError))
            {
                error = $"The petroleum item is invalid: {petroleumError}";
                return false;
            }

            if (_productionPerSecond <= 0f || _oreCapacity <= 0 || _outputPerSecond <= 0f ||
                _electricityPerOre <= 0f || _electricityCapacity <= 0f ||
                _petroleumPerItem <= 0f || _petroleumPerOre <= 0f || _petroleumCapacity <= 0f)
            {
                error = "Mining rates, capacities, and energy costs must all be positive.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Configure(string requiredTerrainId, string requiredTerrainDisplayName,
            ItemDefinition outputItem, float productionPerSecond, int oreCapacity, float outputPerSecond,
            float electricityPerOre, float electricityCapacity, ItemDefinition petroleumItem,
            float petroleumPerItem, float petroleumPerOre, float petroleumCapacity)
        {
            _requiredTerrainId = requiredTerrainId ?? string.Empty;
            _requiredTerrainDisplayName = requiredTerrainDisplayName ?? string.Empty;
            _outputItem = outputItem;
            _productionPerSecond = productionPerSecond;
            _oreCapacity = oreCapacity;
            _outputPerSecond = outputPerSecond;
            _electricityPerOre = electricityPerOre;
            _electricityCapacity = electricityCapacity;
            _petroleumItem = petroleumItem;
            _petroleumPerItem = petroleumPerItem;
            _petroleumPerOre = petroleumPerOre;
            _petroleumCapacity = petroleumCapacity;
        }
    }
}
