using PlanetSurvival.Core.Time;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.UI.Water;
using PlanetSurvival.Water.Domain;

namespace PlanetSurvival.Water.Runtime
{
    /// <summary>
    /// Everything a placed water processor needs: the session-owned machine, the ice it accepts, the tank
    /// it pours into, the panel it opens, and the expedition clock its batch is stamped against. Bundled
    /// so scene builders pass one argument instead of five.
    /// </summary>
    public readonly struct WaterProcessorBinding
    {
        public WaterProcessorBinding(WaterProcessor processor, ItemDefinition iceItem,
            LiquidContainer waterSupply, WaterProcessorView view, GameClock clock)
        {
            Processor = processor;
            IceItem = iceItem;
            WaterSupply = waterSupply;
            View = view;
            Clock = clock;
        }

        public WaterProcessor Processor { get; }
        public ItemDefinition IceItem { get; }
        public LiquidContainer WaterSupply { get; }
        public WaterProcessorView View { get; }
        public GameClock Clock { get; }

        public bool IsComplete => Processor != null && IceItem != null && WaterSupply != null
                                  && View != null && Clock != null;
    }
}
