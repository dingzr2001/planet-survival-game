using PlanetSurvival.Core.Time;
using PlanetSurvival.Farming.Definitions;
using PlanetSurvival.Farming.Domain;
using PlanetSurvival.UI.Farming;
using PlanetSurvival.Water.Domain;

namespace PlanetSurvival.Farming.Runtime
{
    /// <summary>
    /// Everything a placed hydroponics rack needs: the session-owned trays, the crop they grow, the water
    /// reserve a planting draws from, the panel it opens, and the expedition clock its crops ripen on.
    /// </summary>
    public readonly struct HydroponicsBinding
    {
        public HydroponicsBinding(HydroponicsRack rack, CropDefinition crop, LiquidContainer waterSupply,
            HydroponicsView view, GameClock clock)
        {
            Rack = rack;
            Crop = crop;
            WaterSupply = waterSupply;
            View = view;
            Clock = clock;
        }

        public HydroponicsRack Rack { get; }
        public CropDefinition Crop { get; }
        public LiquidContainer WaterSupply { get; }
        public HydroponicsView View { get; }
        public GameClock Clock { get; }

        public bool IsComplete => Rack != null && Crop != null && WaterSupply != null
                                  && View != null && Clock != null;
    }
}
