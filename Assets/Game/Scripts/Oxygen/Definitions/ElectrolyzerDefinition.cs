using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Oxygen.Definitions
{
    /// <summary>
    /// Static balance and connection contract for one kind of water electrolyzer. Rates are authored per
    /// litre of oxygen because oxygen is what the machine exists to make: water, electricity and the
    /// hydrogen by-product are all expressed against it, so one edit rebalances the whole reaction.
    /// </summary>
    [CreateAssetMenu(menuName = "Planet Survival/Oxygen/Electrolyzer", fileName = "Electrolyzer")]
    public sealed class ElectrolyzerDefinition : ScriptableObject
    {
        [Header("Water")]
        [SerializeField, Min(1), Tooltip("Feed-water tank size, in millilitres.")]
        private int _waterCapacityMilliliters = 20000;
        [SerializeField, Tooltip("Gathered ice the machine melts into its feed tank.")]
        private ItemDefinition _iceItem;
        [SerializeField, Min(1), Tooltip("Water one ice chunk melts down to, in millilitres.")]
        private int _waterMillilitersPerIceChunk = 1000;
        [SerializeField, Min(.001f), Tooltip("Feed water split per litre of oxygen, in millilitres.")]
        private float _waterMillilitersPerOxygenLiter = 5f;

        [Header("Electricity")]
        [SerializeField, Min(.01f), Tooltip("Input buffer filled by a power-pole connection.")]
        private float _electricityCapacity = 40f;
        [SerializeField, Min(.001f), Tooltip("Electric energy consumed per litre of oxygen.")]
        private float _electricityPerOxygenLiter = .2f;

        [Header("Gas output")]
        [SerializeField, Min(.001f), Tooltip("Litres of oxygen split per real second while fully powered.")]
        private float _oxygenLitersPerSecond = 10f;
        [SerializeField, Min(.1f), Tooltip("Oxygen held as gas before it is bottled, in litres.")]
        private float _oxygenCapacityLiters = 600f;
        [SerializeField, Min(.001f), Tooltip("Hydrogen released per litre of oxygen, in litres.")]
        private float _hydrogenLitersPerOxygenLiter = 2f;
        [SerializeField, Min(.1f), Tooltip("Hydrogen the vent tank holds before electrolysis stalls, in litres.")]
        private float _hydrogenCapacityLiters = 1200f;
        [SerializeField, Tooltip("Bottle used when hydrogen is collected or exported.")]
        private ItemDefinition _hydrogenItem;
        [SerializeField, Min(.1f)] private float _hydrogenLitersPerItem = 100f;

        [Header("Bottling")]
        [SerializeField, Tooltip("Item the surplus oxygen is bottled into once the gas buffer is full.")]
        private ItemDefinition _oxygenItem;
        [SerializeField, Min(.1f), Tooltip("Oxygen packed into one bottled item, in litres.")]
        private float _oxygenLitersPerItem = 100f;
        [SerializeField, Min(1), Tooltip("Bottled items retained before electrolysis stalls.")]
        private int _oxygenItemCapacity = 10;
        [SerializeField, Min(.01f), Tooltip("Maximum bottled items per real second an automation connection may remove.")]
        private float _outputPerSecond = 1f;

        public int WaterCapacityMilliliters => Mathf.Max(1, _waterCapacityMilliliters);
        public ItemDefinition IceItem => _iceItem;
        public int WaterMillilitersPerIceChunk => Mathf.Max(1, _waterMillilitersPerIceChunk);
        public float WaterMillilitersPerOxygenLiter => Mathf.Max(.001f, _waterMillilitersPerOxygenLiter);
        public float ElectricityCapacity => Mathf.Max(.01f, _electricityCapacity);
        public float ElectricityPerOxygenLiter => Mathf.Max(.001f, _electricityPerOxygenLiter);
        public float OxygenLitersPerSecond => Mathf.Max(.001f, _oxygenLitersPerSecond);
        public float OxygenCapacityLiters => Mathf.Max(.1f, _oxygenCapacityLiters);
        public float HydrogenLitersPerOxygenLiter => Mathf.Max(.001f, _hydrogenLitersPerOxygenLiter);
        public float HydrogenCapacityLiters => Mathf.Max(.1f, _hydrogenCapacityLiters);
        public ItemDefinition HydrogenItem => _hydrogenItem;
        public float HydrogenLitersPerItem => Mathf.Max(.1f, _hydrogenLitersPerItem);
        public ItemDefinition OxygenItem => _oxygenItem;
        public float OxygenLitersPerItem => Mathf.Max(.1f, _oxygenLitersPerItem);
        public int OxygenItemCapacity => Mathf.Max(1, _oxygenItemCapacity);
        public float OutputPerSecond => Mathf.Max(.01f, _outputPerSecond);

        /// <summary>Electric energy one full second of unrestricted electrolysis asks for.</summary>
        public float ElectricityPerSecond => OxygenLitersPerSecond * ElectricityPerOxygenLiter;

        public bool IsValid(out string error)
        {
            if (_iceItem == null)
            {
                error = "The electrolyzer needs an ice item for its feed tank.";
                return false;
            }

            if (!_iceItem.IsValid(out string iceError))
            {
                error = $"The electrolyzer ice item is invalid: {iceError}";
                return false;
            }

            if (_oxygenItem == null)
            {
                error = "The electrolyzer needs a bottled oxygen item.";
                return false;
            }

            if (!_oxygenItem.IsValid(out string oxygenError))
            {
                error = $"The electrolyzer oxygen item is invalid: {oxygenError}";
                return false;
            }

            if (_hydrogenItem != null && !_hydrogenItem.IsValid(out _))
            {
                error = "The electrolyzer needs a valid bottled hydrogen item.";
                return false;
            }

            if (_waterCapacityMilliliters <= 0 || _waterMillilitersPerIceChunk <= 0 ||
                _waterMillilitersPerOxygenLiter <= 0f || _electricityCapacity <= 0f ||
                _electricityPerOxygenLiter <= 0f || _oxygenLitersPerSecond <= 0f ||
                _oxygenCapacityLiters <= 0f || _hydrogenLitersPerOxygenLiter <= 0f ||
                _hydrogenCapacityLiters <= 0f || _hydrogenLitersPerItem <= 0f || _oxygenLitersPerItem <= 0f ||
                _oxygenItemCapacity <= 0 || _outputPerSecond <= 0f)
            {
                error = "Electrolyzer capacities, rates and conversion costs must all be positive.";
                return false;
            }

            if (_waterMillilitersPerIceChunk > _waterCapacityMilliliters)
            {
                error = "One ice chunk must melt down to no more water than the feed tank holds.";
                return false;
            }

            if (_oxygenLitersPerItem > _oxygenCapacityLiters)
            {
                error = "The gas buffer must hold at least one bottle worth of oxygen.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Configure(ItemDefinition iceItem, ItemDefinition oxygenItem,
            int waterCapacityMilliliters, int waterMillilitersPerIceChunk, float waterMillilitersPerOxygenLiter,
            float electricityCapacity, float electricityPerOxygenLiter, float oxygenLitersPerSecond,
            float oxygenCapacityLiters, float hydrogenLitersPerOxygenLiter, float hydrogenCapacityLiters,
            float oxygenLitersPerItem, int oxygenItemCapacity, float outputPerSecond,
            ItemDefinition hydrogenItem = null, float hydrogenLitersPerItem = 100f)
        {
            _iceItem = iceItem;
            _oxygenItem = oxygenItem;
            _waterCapacityMilliliters = waterCapacityMilliliters;
            _waterMillilitersPerIceChunk = waterMillilitersPerIceChunk;
            _waterMillilitersPerOxygenLiter = waterMillilitersPerOxygenLiter;
            _electricityCapacity = electricityCapacity;
            _electricityPerOxygenLiter = electricityPerOxygenLiter;
            _oxygenLitersPerSecond = oxygenLitersPerSecond;
            _oxygenCapacityLiters = oxygenCapacityLiters;
            _hydrogenLitersPerOxygenLiter = hydrogenLitersPerOxygenLiter;
            _hydrogenCapacityLiters = hydrogenCapacityLiters;
            _hydrogenItem = hydrogenItem;
            _hydrogenLitersPerItem = hydrogenLitersPerItem;
            _oxygenLitersPerItem = oxygenLitersPerItem;
            _oxygenItemCapacity = oxygenItemCapacity;
            _outputPerSecond = outputPerSecond;
        }
    }
}
