using System;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.Farming.Domain
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// Session-owned resource system for a planter. Manual controls and future pipe networks use the same
    /// bounded input/output methods, so neither path can overfill a buffer or create resources.
    /// </summary>
    public sealed class PlanterBox : IWaterInput, ICarbonDioxideInput, IOxygenOutput
    {
        private const float Epsilon = .0001f;
        private float _pendingWaterConsumptionMilliliters;

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
        public int RemainingWaterCapacity => Definition.WaterCapacityMilliliters - StoredWaterMilliliters;
        public float RemainingCarbonDioxideCapacity =>
            Definition.CarbonDioxideCapacityLiters - StoredCarbonDioxideLiters;
        public float RemainingOxygenCapacity => Definition.OxygenCapacityLiters - StoredOxygenLiters;

        public PlanterBoxState State => StoredOxygenLiters >= Definition.OxygenCapacityLiters - Epsilon
            ? PlanterBoxState.OxygenStorageFull
            : StoredWaterMilliliters < Definition.MinimumWaterMilliliters
                ? PlanterBoxState.NeedsWater
                : StoredCarbonDioxideLiters < Definition.MinimumCarbonDioxideLiters
                    ? PlanterBoxState.NeedsCarbonDioxide
                    : PlanterBoxState.Producing;

        public event Action Changed;

        public void Advance(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f || State != PlanterBoxState.Producing)
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
