using PlanetSurvival.Mining.Domain;
using PlanetSurvival.UI.Mining;

namespace PlanetSurvival.Mining.Runtime
{
    /// <summary>Runtime dependencies required to operate and inspect a placed drill.</summary>
    public readonly struct MiningDrillBinding
    {
        public MiningDrillBinding(MiningDrill drill, MiningDrillView view)
        {
            Drill = drill;
            View = view;
        }

        public MiningDrill Drill { get; }
        public MiningDrillView View { get; }
        public bool IsComplete => Drill != null && View != null;
    }
}
