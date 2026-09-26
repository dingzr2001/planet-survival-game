using PlanetSurvival.Items.Definitions;

namespace PlanetSurvival.Mining.Domain
{
    /// <summary>
    /// Item intake a belt or pipe adapter can push into. Implementations decide which item they take and
    /// how much room is left, so the transport layer never needs to know what a building consumes.
    /// </summary>
    public interface IItemInput
    {
        /// <summary>How many of <paramref name="item"/> would fit right now, up to the requested amount.</summary>
        int AcceptableInputItems(ItemDefinition item, int maximumQuantity);

        /// <summary>Takes items already removed from an upstream buffer. Returns how many were kept.</summary>
        int InsertInputItems(ItemDefinition item, int quantity);
    }
}
