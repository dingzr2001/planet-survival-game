using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Player.Stats;
using PlanetSurvival.Suit.Domain;
using PlanetSurvival.Water.Domain;

namespace PlanetSurvival.Core.Flow
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>Runtime state that must survive scene changes during one expedition.</summary>
    public sealed class GameSessionState
    {
        public const int WaterBottleCapacityMilliliters = 500;
        public const int InitialLandingPodWaterMilliliters = 20000;
        public const float SpaceSuitOxygenCapacityLiters = 600f;
        public const float InitialSpaceSuitOxygenLiters = SpaceSuitOxygenCapacityLiters;
        public const float LandingPodOxygenCapacityLiters = 36000f;
        public const float InitialLandingPodOxygenLiters = LandingPodOxygenCapacityLiters;
        public const int PlayerInventorySlots = 20;
        public const int PlayerInventoryCapacity = 30;
        public const int RefrigeratorSlots = 12;
        public const int RefrigeratorCapacity = 60;
        public const int CargoStorageSlots = 30;
        public const int CargoStorageCapacity = 300;
        public const int InitialEnergyBarCount = 12;

        public GameSessionState()
        {
            SpaceSuit = new SpaceSuitResources(
                new OxygenReservoir(SpaceSuitOxygenCapacityLiters, InitialSpaceSuitOxygenLiters),
                new LiquidContainer(WaterBottleCapacityMilliliters));
            LandingPodWaterSupply = new LiquidContainer(
                InitialLandingPodWaterMilliliters,
                InitialLandingPodWaterMilliliters);
            LandingPodOxygenSupply = new OxygenReservoir(
                LandingPodOxygenCapacityLiters,
                InitialLandingPodOxygenLiters);
            SurvivalStats = new SurvivalStats();
            PlayerInventory = new InventoryModel(PlayerInventoryCapacity, PlayerInventorySlots);
            RefrigeratorStorage = new InventoryModel(RefrigeratorCapacity, RefrigeratorSlots);
            CargoStorage = new InventoryModel(CargoStorageCapacity, CargoStorageSlots);
        }

        public SpaceSuitResources SpaceSuit { get; }
        // Compatibility alias: the existing drinking-water interactions now operate on the suit reservoir.
        public LiquidContainer WaterBottle => SpaceSuit.Water;
        public LiquidContainer LandingPodWaterSupply { get; }
        public OxygenReservoir LandingPodOxygenSupply { get; }
        public SurvivalStats SurvivalStats { get; }
        public InventoryModel PlayerInventory { get; }
        public InventoryModel RefrigeratorStorage { get; }
        public InventoryModel CargoStorage { get; }

        public void Reset()
        {
            WaterBottle.Reset(0);
            SpaceSuit.Oxygen.Reset(InitialSpaceSuitOxygenLiters);
            LandingPodWaterSupply.Reset(InitialLandingPodWaterMilliliters);
            LandingPodOxygenSupply.Reset(InitialLandingPodOxygenLiters);
            SurvivalStats.Reset();
            PlayerInventory.Clear();
            RefrigeratorStorage.Clear();
            CargoStorage.Clear();
        }

        public InventoryOperationResult Reset(ItemDefinition startingCargoItem, int quantity)
        {
            Reset();
            return CargoStorage.Add(startingCargoItem, quantity);
        }
    }
}
