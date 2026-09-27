using System;
using PlanetSurvival.Farming.Domain;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Mining.Domain;
using PlanetSurvival.Oxygen.Definitions;
using PlanetSurvival.Power.Domain;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.Oxygen.Domain
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// Session-owned state of one water electrolyzer: it splits feed water into breathable oxygen and a
    /// hydrogen by-product, paying for the reaction with buffered electricity. Manual controls and the
    /// automation network share the same bounded input and output methods, so neither path can overfill a
    /// tank or create gas. Fractional water and energy stay inside the machine, so neither a variable
    /// frame length nor a trip into the landing pod can round a litre into existence.
    /// </summary>
    public sealed class Electrolyzer : IPowerInput, IWaterInput, IOxygenOutput, IItemOutput, IItemInput
    {
        private const float Epsilon = .0001f;

        private float _pendingWaterConsumptionMilliliters;
        private float _outputAllowance;

        public Electrolyzer(ElectrolyzerDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (!definition.IsValid(out string error))
            {
                throw new ArgumentException(error, nameof(definition));
            }
        }

        public ElectrolyzerDefinition Definition { get; }
        public ItemDefinition OutputItem => StoredOxygenItems > 0 ? Definition.OxygenItem :
            Definition.HydrogenItem != null && StoredHydrogenLiters + Epsilon >= Definition.HydrogenLitersPerItem
                ? Definition.HydrogenItem : null;
        public int StoredWaterMilliliters { get; private set; }
        public float StoredElectricity { get; private set; }

        /// <summary>Oxygen still held as gas. This is what a suit or an oxygen pipe draws from.</summary>
        public float StoredOxygenLiters { get; private set; }

        /// <summary>Oxygen bottled once the gas buffer filled. This is what a transfer post carries away.</summary>
        public int StoredOxygenItems { get; private set; }

        public float StoredHydrogenLiters { get; private set; }

        public int RemainingWaterCapacity => Definition.WaterCapacityMilliliters - StoredWaterMilliliters;
        public float RemainingElectricityCapacity => Definition.ElectricityCapacity - StoredElectricity;
        public float RemainingOxygenGasCapacity => Definition.OxygenCapacityLiters - StoredOxygenLiters;
        public int RemainingOxygenItemCapacity => Definition.OxygenItemCapacity - StoredOxygenItems;
        public float RemainingHydrogenCapacity => Definition.HydrogenCapacityLiters - StoredHydrogenLiters;

        /// <summary>True once neither the gas buffer nor the bottle bin can take any more oxygen.</summary>
        public bool IsOxygenStorageFull =>
            RemainingOxygenGasCapacity <= Epsilon && RemainingOxygenItemCapacity <= 0;

        public ElectrolyzerState State
        {
            get
            {
                if (RemainingHydrogenCapacity <= Epsilon)
                {
                    return ElectrolyzerState.HydrogenStorageFull;
                }

                if (IsOxygenStorageFull)
                {
                    return ElectrolyzerState.OxygenStorageFull;
                }

                if (StoredWaterMilliliters < Definition.WaterMillilitersPerOxygenLiter)
                {
                    return ElectrolyzerState.NeedsWater;
                }

                return StoredElectricity < Definition.ElectricityPerOxygenLiter
                    ? ElectrolyzerState.NeedsPower
                    : ElectrolyzerState.Producing;
            }
        }

        public event Action Changed;

        /// <summary>Splits as much water as the feed tank, the power buffer and the gas tanks allow.</summary>
        public void Advance(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f)
            {
                return;
            }

            // Bottling first: a buffer that filled last frame is what makes room for this one.
            bool bottled = BottleOxygen();
            float oxygen = Mathf.Min(Definition.OxygenLitersPerSecond * elapsedSeconds, RemainingOxygenGasCapacity);
            oxygen = Mathf.Min(oxygen, StoredWaterMilliliters / Definition.WaterMillilitersPerOxygenLiter);
            oxygen = Mathf.Min(oxygen, StoredElectricity / Definition.ElectricityPerOxygenLiter);
            oxygen = Mathf.Min(oxygen, RemainingHydrogenCapacity / Definition.HydrogenLitersPerOxygenLiter);
            if (oxygen <= Epsilon)
            {
                if (bottled)
                {
                    Changed?.Invoke();
                }

                return;
            }

            // Water leaves the tank in whole millilitres; the remainder is carried, never discarded.
            _pendingWaterConsumptionMilliliters += oxygen * Definition.WaterMillilitersPerOxygenLiter;
            int consumedWater = Mathf.Min(StoredWaterMilliliters,
                Mathf.FloorToInt(_pendingWaterConsumptionMilliliters + Epsilon));
            _pendingWaterConsumptionMilliliters -= consumedWater;
            StoredWaterMilliliters -= consumedWater;
            StoredElectricity = Mathf.Max(0f, StoredElectricity - oxygen * Definition.ElectricityPerOxygenLiter);
            StoredOxygenLiters = Mathf.Min(Definition.OxygenCapacityLiters, StoredOxygenLiters + oxygen);
            StoredHydrogenLiters = Mathf.Min(Definition.HydrogenCapacityLiters,
                StoredHydrogenLiters + oxygen * Definition.HydrogenLitersPerOxygenLiter);
            BottleOxygen();
            Changed?.Invoke();
        }

        public float ReceiveElectricity(float energyUnits)
        {
            float accepted = Mathf.Min(SanitizePositive(energyUnits), RemainingElectricityCapacity);
            if (accepted <= Epsilon)
            {
                return 0f;
            }

            StoredElectricity += accepted;
            Changed?.Invoke();
            return accepted;
        }

        public float RequestedElectricity(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f || IsOxygenStorageFull || RemainingHydrogenCapacity <= Epsilon ||
                StoredWaterMilliliters < Definition.WaterMillilitersPerOxygenLiter)
            {
                return 0f;
            }

            return Mathf.Min(Definition.ElectricityPerSecond * elapsedSeconds, RemainingElectricityCapacity);
        }

        public int ReceiveWater(int milliliters)
        {
            int accepted = Math.Min(Math.Max(0, milliliters), RemainingWaterCapacity);
            if (accepted == 0)
            {
                return 0;
            }

            StoredWaterMilliliters += accepted;
            Changed?.Invoke();
            return accepted;
        }

        /// <summary>Pours water out of a carried container and into the feed tank.</summary>
        public int TransferWaterFrom(LiquidContainer source, int maximumMilliliters)
        {
            if (source == null || maximumMilliliters <= 0)
            {
                return 0;
            }

            int transfer = Math.Min(maximumMilliliters, Math.Min(source.CurrentMilliliters, RemainingWaterCapacity));
            if (transfer <= 0 || !source.TryConsume(transfer))
            {
                return 0;
            }

            return ReceiveWater(transfer);
        }

        /// <summary>
        /// Taps the feed tank back into a carried container. Melted ice is the only water on the surface,
        /// so the machine that holds it is also where a bottle is refilled.
        /// </summary>
        public int DrawWaterTo(LiquidContainer target, int maximumMilliliters)
        {
            if (target == null || maximumMilliliters <= 0 || StoredWaterMilliliters <= 0)
            {
                return 0;
            }

            int transfer = Math.Min(maximumMilliliters,
                Math.Min(StoredWaterMilliliters, target.RemainingCapacityMilliliters));
            if (transfer <= 0)
            {
                return 0;
            }

            var batch = new LiquidContainer(transfer, transfer);
            int drawn = target.FillFrom(batch);
            if (drawn <= 0)
            {
                return 0;
            }

            StoredWaterMilliliters -= drawn;
            Changed?.Invoke();
            return drawn;
        }

        /// <summary>Melts whole ice chunks out of the backpack into the feed tank.</summary>
        public int LoadIceItems(int requestedItems, InventoryModel inventory)
        {
            if (requestedItems <= 0 || inventory == null)
            {
                return 0;
            }

            ItemDefinition ice = Definition.IceItem;
            int tankRoomItems = RemainingWaterCapacity / Definition.WaterMillilitersPerIceChunk;
            int acceptedItems = Math.Min(requestedItems,
                Math.Min(tankRoomItems, inventory.GetQuantity(ice.ItemId)));
            if (acceptedItems <= 0)
            {
                return 0;
            }

            InventoryOperationResult consumed = inventory.ApplyTransaction(
                new[] { new InventoryItemAmount(ice, acceptedItems) }, null);
            if (!consumed.Succeeded)
            {
                return 0;
            }

            ReceiveWater(acceptedItems * Definition.WaterMillilitersPerIceChunk);
            return acceptedItems;
        }

        public int AcceptableInputItems(ItemDefinition item, int maximumQuantity)
        {
            if (item == null || maximumQuantity <= 0 || item.ItemId != Definition.IceItem.ItemId)
            {
                return 0;
            }

            return Math.Min(maximumQuantity, RemainingWaterCapacity / Definition.WaterMillilitersPerIceChunk);
        }

        /// <summary>Automation input for ice chunks already removed from an upstream buffer.</summary>
        public int InsertInputItems(ItemDefinition item, int quantity)
        {
            int accepted = AcceptableInputItems(item, quantity);
            if (accepted > 0)
            {
                ReceiveWater(accepted * Definition.WaterMillilitersPerIceChunk);
            }

            return accepted;
        }

        public float ExtractOxygen(float maximumLiters)
        {
            float extracted = Mathf.Min(SanitizePositive(maximumLiters), StoredOxygenLiters);
            if (extracted <= Epsilon)
            {
                return 0f;
            }

            StoredOxygenLiters -= extracted;
            Changed?.Invoke();
            return extracted;
        }

        /// <summary>Tops up a suit or pod reservoir straight from the gas buffer.</summary>
        public float TransferOxygenTo(OxygenReservoir target)
        {
            if (target == null)
            {
                return 0f;
            }

            float accepted = target.Fill(Mathf.Min(StoredOxygenLiters, target.RemainingCapacityLiters));
            if (accepted <= Epsilon)
            {
                return 0f;
            }

            StoredOxygenLiters -= accepted;
            Changed?.Invoke();
            return accepted;
        }

        /// <summary>Rate-limited automation output of bottled oxygen.</summary>
        public int Extract(int maximumQuantity, float elapsedSeconds)
        {
            if (maximumQuantity <= 0 || elapsedSeconds <= 0f || OutputItem == null)
            {
                return 0;
            }

            _outputAllowance = Mathf.Min(
                Mathf.Max(1f, Definition.OutputPerSecond),
                _outputAllowance + Definition.OutputPerSecond * elapsedSeconds);
            int rateLimited = Mathf.FloorToInt(_outputAllowance + Epsilon);
            int available = StoredOxygenItems > 0 ? StoredOxygenItems :
                Mathf.FloorToInt((StoredHydrogenLiters + Epsilon) / Definition.HydrogenLitersPerItem);
            int extracted = Math.Min(available, Math.Min(maximumQuantity, rateLimited));
            if (extracted <= 0)
            {
                return 0;
            }

            if (StoredOxygenItems > 0) StoredOxygenItems -= extracted;
            else StoredHydrogenLiters -= extracted * Definition.HydrogenLitersPerItem;
            _outputAllowance -= extracted;
            Changed?.Invoke();
            return extracted;
        }

        /// <summary>Player collection bypasses the automation throttle but remains inventory-safe.</summary>
        public int CollectOxygenItems(int requestedItems, InventoryModel inventory)
        {
            if (requestedItems <= 0 || inventory == null || StoredOxygenItems <= 0)
            {
                return 0;
            }

            int transferable = Math.Min(requestedItems, StoredOxygenItems);
            while (transferable > 0 && !inventory.CanAdd(Definition.OxygenItem, transferable).Succeeded)
            {
                transferable--;
            }

            if (transferable <= 0 || !inventory.Add(Definition.OxygenItem, transferable).Succeeded)
            {
                return 0;
            }

            StoredOxygenItems -= transferable;
            Changed?.Invoke();
            return transferable;
        }

        public int CollectHydrogenItems(int requestedItems, InventoryModel inventory)
        {
            if (requestedItems <= 0 || inventory == null || Definition.HydrogenItem == null) return 0;
            int available = Mathf.FloorToInt((StoredHydrogenLiters + Epsilon) / Definition.HydrogenLitersPerItem);
            int quantity = Math.Min(requestedItems, available);
            while (quantity > 0 && !inventory.CanAdd(Definition.HydrogenItem, quantity).Succeeded) quantity--;
            if (quantity <= 0 || !inventory.Add(Definition.HydrogenItem, quantity).Succeeded) return 0;
            StoredHydrogenLiters -= quantity * Definition.HydrogenLitersPerItem;
            Changed?.Invoke();
            return quantity;
        }

        /// <summary>
        /// Blows the vent tank down to empty. Nothing on the expedition burns hydrogen yet, so releasing
        /// it is what keeps electrolysis running once the tank fills.
        /// </summary>
        public float VentHydrogen()
        {
            float vented = StoredHydrogenLiters;
            if (vented <= Epsilon)
            {
                return 0f;
            }

            StoredHydrogenLiters = 0f;
            Changed?.Invoke();
            return vented;
        }

        /// <summary>
        /// Packs the gas buffer down into bottles once it is full, which is what keeps the buffer itself
        /// available for topping up a suit. Returns true when at least one bottle was filled.
        /// </summary>
        private bool BottleOxygen()
        {
            bool bottled = false;
            while (RemainingOxygenGasCapacity <= Epsilon && RemainingOxygenItemCapacity > 0 &&
                   StoredOxygenLiters >= Definition.OxygenLitersPerItem)
            {
                StoredOxygenLiters -= Definition.OxygenLitersPerItem;
                StoredOxygenItems++;
                bottled = true;
            }

            return bottled;
        }

        private static float SanitizePositive(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
        }
    }
}
