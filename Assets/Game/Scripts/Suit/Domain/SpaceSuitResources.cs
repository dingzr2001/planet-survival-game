using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.Water.Domain;

namespace PlanetSurvival.Suit.Domain
{
    /// <summary>Resources physically carried by one space suit.</summary>
    public sealed class SpaceSuitResources
    {
        public SpaceSuitResources(OxygenReservoir oxygen, LiquidContainer water)
        {
            Oxygen = oxygen ?? throw new System.ArgumentNullException(nameof(oxygen));
            Water = water ?? throw new System.ArgumentNullException(nameof(water));
        }

        public OxygenReservoir Oxygen { get; }
        public LiquidContainer Water { get; }
    }
}
