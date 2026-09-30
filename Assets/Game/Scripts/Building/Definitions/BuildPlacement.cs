namespace PlanetSurvival.Building.Definitions
{
    /// <summary>How a buildable is positioned when the player places it.</summary>
    public enum BuildPlacement
    {
        /// <summary>Snaps to the construction grid and claims whole cells, so neighbours line up edge to edge.</summary>
        Grid = 0,

        /// <summary>
        /// Stands at the exact cursor position and claims only its own rectangle, like Factorio's
        /// placeable-off-grid entities. Reserved for small items that never connect to a neighbour.
        /// </summary>
        OffGrid = 1
    }
}
