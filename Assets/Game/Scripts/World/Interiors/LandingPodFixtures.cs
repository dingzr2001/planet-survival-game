using PlanetSurvival.Cooking.Runtime;
using PlanetSurvival.Farming.Runtime;
using PlanetSurvival.UI.Storage;
using PlanetSurvival.UI.Water;
using PlanetSurvival.Water.Domain;
using PlanetSurvival.Water.Runtime;

namespace PlanetSurvival.World.Interiors
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// Every session-owned system the landing pod interior wires its fixtures to. It travels as one
    /// value so adding a fixture does not lengthen the builder's signature again, and so the deck
    /// builder never reaches for state on its own.
    /// </summary>
    public readonly struct LandingPodFixtures
    {
        public LandingPodFixtures(
            LiquidContainer waterSupply,
            LiquidContainer waterBottle,
            WaterRefillView refillView,
            InventoryModel refrigeratorStorage,
            InventoryModel cargoStorage,
            StorageView storageView,
            CookingStationBinding cooking,
            WaterProcessorBinding processor,
            HydroponicsBinding hydroponics)
        {
            WaterSupply = waterSupply;
            WaterBottle = waterBottle;
            RefillView = refillView;
            RefrigeratorStorage = refrigeratorStorage;
            CargoStorage = cargoStorage;
            StorageView = storageView;
            Cooking = cooking;
            Processor = processor;
            Hydroponics = hydroponics;
        }

        public LiquidContainer WaterSupply { get; }
        public LiquidContainer WaterBottle { get; }
        public WaterRefillView RefillView { get; }
        public InventoryModel RefrigeratorStorage { get; }
        public InventoryModel CargoStorage { get; }
        public StorageView StorageView { get; }
        public CookingStationBinding Cooking { get; }
        public WaterProcessorBinding Processor { get; }
        public HydroponicsBinding Hydroponics { get; }
    }
}
