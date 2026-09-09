using UnityEngine;
using PlanetSurvival.Items.Definitions;

namespace PlanetSurvival.Farming.Definitions
{
    /// <summary>
    /// One crop a hydroponics slot can grow: what it is sown from, how long it needs, how much water the
    /// slot draws when it is planted, and what a harvest returns. Growth is measured in game hours so a
    /// crop ripens on expedition time rather than on how long the habitat scene happened to stay loaded.
    /// </summary>
    [CreateAssetMenu(menuName = "Planet Survival/Farming/Crop", fileName = "Crop")]
    public sealed class CropDefinition : ScriptableObject
    {
        [SerializeField] private string _cropId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField, Tooltip("Item consumed to sow one slot. Most crops are sown from their own produce.")]
        private ItemDefinition _seedItem;
        [SerializeField, Min(1), Tooltip("How many seed items one planting consumes.")]
        private int _seedQuantity = 1;
        [SerializeField, Tooltip("Item a ripe slot yields.")]
        private ItemDefinition _harvestItem;
        [SerializeField, Min(1), Tooltip("How many items one harvest returns. It must exceed the seed cost for farming to be worth doing.")]
        private int _harvestQuantity = 1;
        [SerializeField, Min(.1f), Tooltip("Game hours between planting and a ripe slot.")]
        private float _growthGameHours = 20f;
        [SerializeField, Min(0), Tooltip("Water drawn from the pod reserve the moment the slot is planted.")]
        private int _waterMilliliters = 1500;

        public string CropId => _cropId;
        public string DisplayName => _displayName;
        public ItemDefinition SeedItem => _seedItem;
        public int SeedQuantity => Mathf.Max(1, _seedQuantity);
        public ItemDefinition HarvestItem => _harvestItem;
        public int HarvestQuantity => Mathf.Max(1, _harvestQuantity);
        public float GrowthGameHours => Mathf.Max(.1f, _growthGameHours);
        public int WaterMilliliters => Mathf.Max(0, _waterMilliliters);

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(_cropId) || string.IsNullOrWhiteSpace(_displayName))
            {
                error = "A crop requires a non-empty ID and display name.";
                return false;
            }

            if (_seedItem == null)
            {
                error = $"Crop '{_cropId}' needs a seed item.";
                return false;
            }

            if (!_seedItem.IsValid(out string seedError))
            {
                error = $"Crop '{_cropId}' has an invalid seed item: {seedError}";
                return false;
            }

            if (_harvestItem == null)
            {
                error = $"Crop '{_cropId}' needs a harvest item.";
                return false;
            }

            if (!_harvestItem.IsValid(out string harvestError))
            {
                error = $"Crop '{_cropId}' has an invalid harvest item: {harvestError}";
                return false;
            }

            if (_seedQuantity <= 0 || _harvestQuantity <= 0)
            {
                error = $"Crop '{_cropId}' requires positive seed and harvest amounts.";
                return false;
            }

            if (_growthGameHours <= 0f)
            {
                error = $"Crop '{_cropId}' must take a positive number of game hours to grow.";
                return false;
            }

            if (_waterMilliliters < 0)
            {
                error = $"Crop '{_cropId}' cannot consume a negative volume of water.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Configure(string cropId, string displayName, ItemDefinition seedItem, int seedQuantity,
            ItemDefinition harvestItem, int harvestQuantity, float growthGameHours, int waterMilliliters)
        {
            _cropId = cropId;
            _displayName = displayName;
            _seedItem = seedItem;
            _seedQuantity = Mathf.Max(1, seedQuantity);
            _harvestItem = harvestItem;
            _harvestQuantity = Mathf.Max(1, harvestQuantity);
            _growthGameHours = Mathf.Max(.1f, growthGameHours);
            _waterMilliliters = Mathf.Max(0, waterMilliliters);
        }
    }
}
