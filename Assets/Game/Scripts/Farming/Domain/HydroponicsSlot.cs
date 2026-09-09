using System;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Water.Domain;

namespace PlanetSurvival.Farming.Domain
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// One growing position of a hydroponics rack. Planting pays the whole cost up front — the seed
    /// leaves the backpack and the water leaves the pod reserve — and stamps the expedition time the crop
    /// ripens at. Nothing is ticked afterwards, so a crop keeps growing while the player is out on the
    /// surface, which is the point: you plant, you leave, you come back to food.
    /// </summary>
    public sealed class HydroponicsSlot
    {
        private const double GameHoursPerDay = 24d;

        private double _plantedAtDays;
        private double _ripeAtDays;

        public HydroponicsSlot(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "A slot index cannot be negative.");
            }

            Index = index;
        }

        public int Index { get; }

        /// <summary>What is growing here, or null while the slot is empty.</summary>
        public CropDefinition Crop { get; private set; }

        public bool IsPlanted => Crop != null;

        /// <summary>Raised when the slot is planted, harvested, or cleared.</summary>
        public event Action Changed;

        public bool IsRipe(double nowDays)
        {
            return IsPlanted && IsFinite(nowDays) && nowDays >= _ripeAtDays;
        }

        /// <summary>How far the crop has grown, from 0 to 1. An empty slot reports zero.</summary>
        public float Progress(double nowDays)
        {
            if (!IsPlanted || !IsFinite(nowDays))
            {
                return 0f;
            }

            double total = _ripeAtDays - _plantedAtDays;
            if (total <= 0d)
            {
                return 1f;
            }

            return (float)Math.Clamp((nowDays - _plantedAtDays) / total, 0d, 1d);
        }

        /// <summary>Game hours left before the crop can be harvested. Zero once it is ripe.</summary>
        public float RemainingGameHours(double nowDays)
        {
            if (!IsPlanted || !IsFinite(nowDays))
            {
                return 0f;
            }

            return (float)Math.Max(0d, (_ripeAtDays - nowDays) * GameHoursPerDay);
        }

        /// <summary>
        /// Sows the slot, consuming the seed from <paramref name="inventory"/> and the crop's water from
        /// <paramref name="waterSupply"/>. Neither is touched unless both can be paid.
        /// </summary>
        public FarmingResult Plant(CropDefinition crop, InventoryModel inventory, LiquidContainer waterSupply,
            double nowDays)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (waterSupply == null)
            {
                throw new ArgumentNullException(nameof(waterSupply));
            }

            if (crop == null)
            {
                return FarmingResult.Fail(FarmingFailure.InvalidCrop, "No crop is selected.");
            }

            if (!crop.IsValid(out string cropError))
            {
                return FarmingResult.Fail(FarmingFailure.InvalidCrop, cropError);
            }

            if (!IsFinite(nowDays))
            {
                return FarmingResult.Fail(FarmingFailure.InvalidCrop, "The expedition clock is unavailable.");
            }

            if (IsPlanted)
            {
                return FarmingResult.Fail(
                    FarmingFailure.SlotOccupied,
                    $"{Crop.DisplayName} is already growing in tray {Index + 1}.");
            }

            if (inventory.GetQuantity(crop.SeedItem.ItemId) < crop.SeedQuantity)
            {
                return FarmingResult.Fail(
                    FarmingFailure.MissingSeed,
                    $"Planting needs {crop.SeedQuantity} × {crop.SeedItem.DisplayName}.");
            }

            if (waterSupply.CurrentMilliliters < crop.WaterMilliliters)
            {
                return FarmingResult.Fail(
                    FarmingFailure.NotEnoughWater,
                    $"Planting needs {crop.WaterMilliliters} mL of water; the reserve holds {waterSupply.CurrentMilliliters} mL.");
            }

            InventoryOperationResult seedTaken = inventory.ApplyTransaction(
                new[] { new InventoryItemAmount(crop.SeedItem, crop.SeedQuantity) }, null);
            if (!seedTaken.Succeeded)
            {
                return FarmingResult.Fail(FarmingFailure.MissingSeed, seedTaken.Message);
            }

            if (crop.WaterMilliliters > 0 && !waterSupply.TryConsume(crop.WaterMilliliters))
            {
                // Unreachable while the volume check above holds; refunding keeps a seed from vanishing.
                inventory.ApplyTransaction(null, new[] { new InventoryItemAmount(crop.SeedItem, crop.SeedQuantity) });
                return FarmingResult.Fail(FarmingFailure.NotEnoughWater, "The water reserve emptied mid-planting.");
            }

            Crop = crop;
            _plantedAtDays = nowDays;
            _ripeAtDays = nowDays + crop.GrowthGameHours / GameHoursPerDay;
            Changed?.Invoke();
            return FarmingResult.Success();
        }

        /// <summary>
        /// Moves a ripe crop into the backpack. A backpack without room leaves the produce in the tray,
        /// so a harvest is never lost by arriving with a full load of ice.
        /// </summary>
        public FarmingResult Harvest(InventoryModel inventory, double nowDays)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (!IsPlanted)
            {
                return FarmingResult.Fail(FarmingFailure.SlotEmpty, $"Tray {Index + 1} is empty.");
            }

            if (!IsRipe(nowDays))
            {
                return FarmingResult.Fail(
                    FarmingFailure.NotRipe,
                    $"{Crop.DisplayName} needs {RemainingGameHours(nowDays):0.0} more game hours.");
            }

            InventoryOperationResult stored = inventory.ApplyTransaction(
                null, new[] { new InventoryItemAmount(Crop.HarvestItem, Crop.HarvestQuantity) });
            if (!stored.Succeeded)
            {
                return FarmingResult.Fail(FarmingFailure.InventoryFull, stored.Message);
            }

            Clear();
            return FarmingResult.Success();
        }

        /// <summary>Empties the tray without returning anything. Used when an expedition restarts.</summary>
        public void Clear()
        {
            if (!IsPlanted)
            {
                return;
            }

            Crop = null;
            _plantedAtDays = 0d;
            _ripeAtDays = 0d;
            Changed?.Invoke();
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
