using PlanetSurvival.Items.Definitions;

namespace PlanetSurvival.Mining.Domain
{
    /// <summary>Rate-limited item output that a belt or pipe adapter can poll.</summary>
    public interface IItemOutput
    {
        ItemDefinition OutputItem { get; }
        int Extract(int maximumQuantity, float elapsedSeconds);
    }
}
