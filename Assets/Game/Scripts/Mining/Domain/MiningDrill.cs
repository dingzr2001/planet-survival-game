using System;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Mining.Definitions;
using UnityEngine;

namespace PlanetSurvival.Mining.Domain
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// Session-owned mining state. Electricity is consumed first and petroleum supplies any remaining
    /// work. Fractional progress and energy stay in the machine, so neither variable frame lengths nor
    /// a scene change can create or discard ore.
    /// </summary>
    public sealed class MiningDrill : IElectricityInput, IPetroleumInput, IItemOutput
    {
        private const float Epsilon = .0001f;

        private float _productionProgress;
        private float _outputAllowance;

        public MiningDrill(MiningDrillDefinition definition)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (!definition.IsValid(out string error))
            {
                throw new ArgumentException(error, nameof(definition));
            }
        }

        public MiningDrillDefinition Definition { get; }
        public ItemDefinition OutputItem => Definition.OutputItem;
        public int StoredOre { get; private set; }
        public float StoredElectricity { get; private set; }
        public float StoredPetroleum { get; private set; }
        public float ProductionProgress => _productionProgress;
        public int RemainingOreCapacity => Definition.OreCapacity - StoredOre;
        public float RemainingElectricityCapacity => Definition.ElectricityCapacity - StoredElectricity;
        public float RemainingPetroleumCapacity => Definition.PetroleumCapacity - StoredPetroleum;

        public MiningDrillState State => StoredOre >= Definition.OreCapacity
            ? MiningDrillState.StorageFull
            : MaximumPoweredWork() > Epsilon
                ? MiningDrillState.Producing
                : MiningDrillState.Unpowered;

        public event Action Changed;

        public void Advance(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f || StoredOre >= Definition.OreCapacity)
            {
                return;
            }

            float requestedWork = Mathf.Min(
                Definition.ProductionPerSecond * elapsedSeconds,
                Definition.OreCapacity - StoredOre - _productionProgress);
            if (requestedWork <= Epsilon)
            {
                return;
            }

            float electricWork = Mathf.Min(requestedWork, StoredElectricity / Definition.ElectricityPerOre);
            StoredElectricity = Mathf.Max(0f, StoredElectricity - electricWork * Definition.ElectricityPerOre);

            float remainingWork = requestedWork - electricWork;
            float petroleumWork = Mathf.Min(remainingWork, StoredPetroleum / Definition.PetroleumPerOre);
            StoredPetroleum = Mathf.Max(0f, StoredPetroleum - petroleumWork * Definition.PetroleumPerOre);

            float completedWork = electricWork + petroleumWork;
            if (completedWork <= Epsilon)
            {
                return;
            }

            _productionProgress += completedWork;
            int produced = Mathf.Min(Mathf.FloorToInt(_productionProgress + Epsilon), RemainingOreCapacity);
            if (produced > 0)
            {
                StoredOre += produced;
                _productionProgress = Mathf.Max(0f, _productionProgress - produced);
            }

            Changed?.Invoke();
        }

        public float ReceiveElectricity(float energyUnits)
        {
            float accepted = Mathf.Min(SanitizePositive(energyUnits), RemainingElectricityCapacity);
            if (accepted <= 0f)
            {
                return 0f;
            }

            StoredElectricity += accepted;
            Changed?.Invoke();
            return accepted;
        }

        public float ReceivePetroleum(float volume)
        {
            float accepted = Mathf.Min(SanitizePositive(volume), RemainingPetroleumCapacity);
            if (accepted <= 0f)
            {
                return 0f;
            }

            StoredPetroleum += accepted;
            Changed?.Invoke();
            return accepted;
        }

        /// <summary>Consumes whole inventory canisters and transfers their petroleum into the tank.</summary>
        public int LoadPetroleumItems(int requestedItems, InventoryModel inventory)
        {
            if (requestedItems <= 0 || inventory == null)
            {
                return 0;
            }

            ItemDefinition petroleum = Definition.PetroleumItem;
            int tankRoomItems = Mathf.FloorToInt((RemainingPetroleumCapacity + Epsilon) / Definition.PetroleumPerItem);
            int acceptedItems = Math.Min(requestedItems,
                Math.Min(tankRoomItems, inventory.GetQuantity(petroleum.ItemId)));
            if (acceptedItems <= 0)
            {
                return 0;
            }

            InventoryOperationResult consumed = inventory.ApplyTransaction(
                new[] { new InventoryItemAmount(petroleum, acceptedItems) }, null);
            if (!consumed.Succeeded)
            {
                return 0;
            }

            ReceivePetroleum(acceptedItems * Definition.PetroleumPerItem);
            return acceptedItems;
        }

        public int AcceptableInputItems(ItemDefinition item, int maximumQuantity)
        {
            if (item == null || maximumQuantity <= 0 || item.ItemId != Definition.PetroleumItem.ItemId)
            {
                return 0;
            }

            int room = Mathf.FloorToInt((RemainingPetroleumCapacity + Epsilon) / Definition.PetroleumPerItem);
            return Math.Min(maximumQuantity, room);
        }

        /// <summary>Automation input for petroleum canisters already removed from an upstream buffer.</summary>
        public int InsertInputItems(ItemDefinition item, int quantity)
        {
            int accepted = AcceptableInputItems(item, quantity);
            if (accepted > 0)
            {
                ReceivePetroleum(accepted * Definition.PetroleumPerItem);
            }

            return accepted;
        }

        /// <summary>Player collection bypasses the automation output throttle but remains inventory-safe.</summary>
        public int CollectOre(int requestedItems, InventoryModel inventory)
        {
            if (requestedItems <= 0 || inventory == null || StoredOre <= 0)
            {
                return 0;
            }

            int transferable = Math.Min(requestedItems, StoredOre);
            while (transferable > 0 && !inventory.CanAdd(OutputItem, transferable).Succeeded)
            {
                transferable--;
            }

            if (transferable <= 0 || !inventory.Add(OutputItem, transferable).Succeeded)
            {
                return 0;
            }

            StoredOre -= transferable;
            Changed?.Invoke();
            return transferable;
        }

        public int Extract(int maximumQuantity, float elapsedSeconds)
        {
            if (maximumQuantity <= 0 || elapsedSeconds <= 0f || StoredOre <= 0)
            {
                return 0;
            }

            _outputAllowance = Mathf.Min(
                Mathf.Max(1f, Definition.OutputPerSecond),
                _outputAllowance + Definition.OutputPerSecond * elapsedSeconds);
            int rateLimited = Mathf.FloorToInt(_outputAllowance + Epsilon);
            int extracted = Math.Min(StoredOre, Math.Min(maximumQuantity, rateLimited));
            if (extracted <= 0)
            {
                return 0;
            }

            StoredOre -= extracted;
            _outputAllowance -= extracted;
            Changed?.Invoke();
            return extracted;
        }

        private float MaximumPoweredWork()
        {
            return StoredElectricity / Definition.ElectricityPerOre +
                   StoredPetroleum / Definition.PetroleumPerOre;
        }

        private static float SanitizePositive(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Max(0f, value);
        }
    }
}
