using System;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Mining.Domain;
using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.Farming.Domain
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// Session-owned resource system for a planter. Manual controls and future pipe networks use the same
    /// bounded input/output methods, so neither path can overfill a buffer or create resources.
    /// </summary>
    public sealed class PlanterBox : IWaterInput, ICarbonDioxideInput, IOxygenOutput, IItemInput
    {
        private const float Epsilon = .0001f;
        private const double GameHoursPerDay = 24d;
        private float _pendingWaterConsumptionMilliliters;
        private double _lastAdvancedDays;
        private float _healthyGrowthGameHours;
        private float _deprivedGameHours;

        public PlanterBox(PlanterBoxDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (!definition.IsValid(out string error))
            {
                throw new ArgumentException(error, nameof(definition));
            }
        }

        public PlanterBoxDefinition Definition { get; }
        public int StoredWaterMilliliters { get; private set; }
        public float StoredCarbonDioxideLiters { get; private set; }
        public float StoredOxygenLiters { get; private set; }
        public CropDefinition Crop { get; private set; }
        public bool IsPlanted => Crop != null;
        public bool IsDead { get; private set; }
        public bool IsMature => IsPlanted && !IsDead && GrowthProgress >= 1f;
        public float GrowthProgress => !IsPlanted
            ? 0f
            : Mathf.Clamp01(_healthyGrowthGameHours / Crop.GrowthGameHours);
        public float RemainingGrowthGameHours => !IsPlanted
            ? 0f
            : Mathf.Max(0f, Crop.GrowthGameHours - _healthyGrowthGameHours);
        public float RemainingEnvironmentToleranceGameHours => !IsPlanted || IsDead
            ? 0f
            : Mathf.Max(0f, Crop.EnvironmentFailureToleranceGameHours - _deprivedGameHours);
        public int RemainingWaterCapacity => Definition.WaterCapacityMilliliters - StoredWaterMilliliters;
        public float RemainingCarbonDioxideCapacity =>
            Definition.CarbonDioxideCapacityLiters - StoredCarbonDioxideLiters;
        public float RemainingOxygenCapacity => Definition.OxygenCapacityLiters - StoredOxygenLiters;

        public PlanterBoxState State => !IsPlanted
            ? PlanterBoxState.Empty
            : IsDead
                ? PlanterBoxState.Dead
                : IsMature
                    ? PlanterBoxState.Mature
                    : StoredWaterMilliliters < Definition.MinimumWaterMilliliters
                ? PlanterBoxState.NeedsWater
                : StoredCarbonDioxideLiters < Definition.MinimumCarbonDioxideLiters
                    ? PlanterBoxState.NeedsCarbonDioxide
                    : StoredOxygenLiters >= Definition.OxygenCapacityLiters - Epsilon
                        ? PlanterBoxState.OxygenStorageFull
                        : PlanterBoxState.Growing;

        public event Action Changed;

        public void Advance(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f ||
                StoredWaterMilliliters < Definition.MinimumWaterMilliliters ||
                StoredCarbonDioxideLiters < Definition.MinimumCarbonDioxideLiters ||
                StoredOxygenLiters >= Definition.OxygenCapacityLiters - Epsilon)
            {
                return;
            }

            float oxygen = Mathf.Min(Definition.OxygenLitersPerSecond * elapsedSeconds, RemainingOxygenCapacity);
            oxygen = Mathf.Min(oxygen, StoredWaterMilliliters / Definition.WaterMillilitersPerOxygenLiter);
            oxygen = Mathf.Min(oxygen, StoredCarbonDioxideLiters / Definition.CarbonDioxideLitersPerOxygenLiter);
            if (oxygen <= Epsilon)
            {
                return;
            }

            _pendingWaterConsumptionMilliliters += oxygen * Definition.WaterMillilitersPerOxygenLiter;
            int consumedWater = Mathf.Min(StoredWaterMilliliters,
                Mathf.FloorToInt(_pendingWaterConsumptionMilliliters + Epsilon));
            _pendingWaterConsumptionMilliliters -= consumedWater;
            StoredWaterMilliliters -= consumedWater;
            StoredCarbonDioxideLiters = Mathf.Max(0f,
                StoredCarbonDioxideLiters - oxygen * Definition.CarbonDioxideLitersPerOxygenLiter);
            StoredOxygenLiters = Mathf.Min(Definition.OxygenCapacityLiters, StoredOxygenLiters + oxygen);
            Changed?.Invoke();
        }

        /// <summary>Advances crop health on expedition time and resource conversion on real time.</summary>
        public void Advance(float elapsedSeconds, double nowDays)
        {
            if (!IsPlanted || IsDead || double.IsNaN(nowDays) || double.IsInfinity(nowDays)) return;

            double elapsedGameHours = Math.Max(0d, (nowDays - _lastAdvancedDays) * GameHoursPerDay);
            _lastAdvancedDays = nowDays;
            if (elapsedGameHours <= 0d || IsMature) return;

            bool hasWater = StoredWaterMilliliters >= Definition.MinimumWaterMilliliters;
            bool hasCarbonDioxide = StoredCarbonDioxideLiters >= Definition.MinimumCarbonDioxideLiters;
            if (hasWater && hasCarbonDioxide)
            {
                _deprivedGameHours = 0f;
                _healthyGrowthGameHours = Mathf.Min(Crop.GrowthGameHours,
                    _healthyGrowthGameHours + (float)elapsedGameHours);
                Advance(elapsedSeconds);
            }
            else
            {
                _deprivedGameHours += (float)elapsedGameHours;
                if (_deprivedGameHours >= Crop.EnvironmentFailureToleranceGameHours)
                {
                    IsDead = true;
                }
            }

            Changed?.Invoke();
        }

        public FarmingResult Plant(CropDefinition crop, InventoryModel inventory, double nowDays)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (crop == null)
                return FarmingResult.Fail(FarmingFailure.InvalidCrop, "No crop is selected.");
            if (!crop.IsValid(out string error))
                return FarmingResult.Fail(FarmingFailure.InvalidCrop, error);
            if (!Definition.Supports(crop))
                return FarmingResult.Fail(FarmingFailure.UnsupportedCrop, $"{crop.DisplayName} cannot grow in this planter.");
            if (IsPlanted)
                return FarmingResult.Fail(FarmingFailure.SlotOccupied, "The planter is already occupied.");
            if (inventory.GetQuantity(crop.SeedItem.ItemId) < crop.SeedQuantity)
                return FarmingResult.Fail(FarmingFailure.MissingSeed,
                    $"Planting needs {crop.SeedQuantity} × {crop.SeedItem.DisplayName}.");

            InventoryOperationResult consumed = inventory.ApplyTransaction(
                new[] { new InventoryItemAmount(crop.SeedItem, crop.SeedQuantity) }, null);
            if (!consumed.Succeeded) return FarmingResult.Fail(FarmingFailure.MissingSeed, consumed.Message);

            Crop = crop;
            IsDead = false;
            _healthyGrowthGameHours = 0f;
            _deprivedGameHours = 0f;
            _lastAdvancedDays = nowDays;
            Changed?.Invoke();
            return FarmingResult.Success();
        }

        public FarmingResult Harvest(InventoryModel inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (!IsPlanted) return FarmingResult.Fail(FarmingFailure.SlotEmpty, "The planter is empty.");
            if (IsDead) return FarmingResult.Fail(FarmingFailure.CropDead, "The dead crop must be cleared.");
            if (!IsMature) return FarmingResult.Fail(FarmingFailure.NotRipe,
                $"{Crop.DisplayName} needs {RemainingGrowthGameHours:0.0} more game hours.");

            InventoryOperationResult stored = inventory.ApplyTransaction(null,
                new[] { new InventoryItemAmount(Crop.HarvestItem, Crop.HarvestQuantity) });
            if (!stored.Succeeded) return FarmingResult.Fail(FarmingFailure.InventoryFull, stored.Message);
            ClearCrop();
            return FarmingResult.Success();
        }

        public bool ClearDeadCrop()
        {
            if (!IsDead) return false;
            ClearCrop();
            return true;
        }

        private void ClearCrop()
        {
            Crop = null;
            IsDead = false;
            _healthyGrowthGameHours = 0f;
            _deprivedGameHours = 0f;
            _lastAdvancedDays = 0d;
            Changed?.Invoke();
        }

        public int ReceiveWater(int milliliters)
        {
            int accepted = Math.Min(Math.Max(0, milliliters), RemainingWaterCapacity);
            if (accepted == 0) return 0;
            StoredWaterMilliliters += accepted;
            Changed?.Invoke();
            return accepted;
        }

        public int TransferWaterFrom(LiquidContainer source, int maximumMilliliters)
        {
            if (source == null || maximumMilliliters <= 0) return 0;
            int transfer = Math.Min(maximumMilliliters, Math.Min(source.CurrentMilliliters, RemainingWaterCapacity));
            if (transfer <= 0 || !source.TryConsume(transfer)) return 0;
            return ReceiveWater(transfer);
        }

        public float ReceiveCarbonDioxide(float liters)
        {
            float accepted = Mathf.Min(SanitizePositive(liters), RemainingCarbonDioxideCapacity);
            if (accepted <= Epsilon) return 0f;
            StoredCarbonDioxideLiters += accepted;
            Changed?.Invoke();
            return accepted;
        }

        public int LoadCarbonDioxideItems(int requestedItems, InventoryModel inventory)
        {
            if (requestedItems <= 0 || inventory == null) return 0;
            int room = Mathf.FloorToInt((RemainingCarbonDioxideCapacity + Epsilon) /
                                        Definition.CarbonDioxideLitersPerCanister);
            int accepted = Math.Min(requestedItems,
                Math.Min(room, inventory.GetQuantity(Definition.CarbonDioxideCanister.ItemId)));
            if (accepted <= 0) return 0;
            InventoryOperationResult consumed = inventory.ApplyTransaction(
                new[] { new InventoryItemAmount(Definition.CarbonDioxideCanister, accepted) }, null);
            if (!consumed.Succeeded) return 0;
            ReceiveCarbonDioxide(accepted * Definition.CarbonDioxideLitersPerCanister);
            return accepted;
        }

        public int AcceptableInputItems(ItemDefinition item, int maximumQuantity)
        {
            if (item == null || maximumQuantity <= 0 || item.ItemId != Definition.CarbonDioxideCanister.ItemId)
            {
                return 0;
            }

            int room = Mathf.FloorToInt((RemainingCarbonDioxideCapacity + Epsilon) /
                                        Definition.CarbonDioxideLitersPerCanister);
            return Math.Min(maximumQuantity, room);
        }

        /// <summary>Automation input for CO₂ canisters already removed from an upstream buffer.</summary>
        public int InsertInputItems(ItemDefinition item, int quantity)
        {
            int accepted = AcceptableInputItems(item, quantity);
            if (accepted > 0)
            {
                ReceiveCarbonDioxide(accepted * Definition.CarbonDioxideLitersPerCanister);
            }

            return accepted;
        }

        public float ExtractOxygen(float maximumLiters)
        {
            float extracted = Mathf.Min(SanitizePositive(maximumLiters), StoredOxygenLiters);
            if (extracted <= Epsilon) return 0f;
            StoredOxygenLiters -= extracted;
            Changed?.Invoke();
            return extracted;
        }

        public float TransferOxygenTo(OxygenReservoir target)
        {
            if (target == null) return 0f;
            float accepted = target.Fill(Mathf.Min(StoredOxygenLiters, target.RemainingCapacityLiters));
            if (accepted <= Epsilon) return 0f;
            StoredOxygenLiters -= accepted;
            Changed?.Invoke();
            return accepted;
        }

        private static float SanitizePositive(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
    }
}
