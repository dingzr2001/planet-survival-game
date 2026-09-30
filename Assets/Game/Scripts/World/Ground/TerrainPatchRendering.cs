namespace PlanetSurvival.World.Ground
{
    /// <summary>How the terrain shader draws a surface's artwork when it appears as a patch.</summary>
    public enum TerrainPatchRendering
    {
        /// <summary>
        /// Cutout artwork (rocks, ore) placed once per tile with a random offset and size, free to overlap
        /// its neighbours so the tile grid never shows.
        /// </summary>
        Scattered = 0,

        /// <summary>
        /// A seamless texture tiled in world space and masked to the patch with soft, rough edges, for
        /// surfaces that cover ground rather than sit on it, such as sheet ice.
        /// </summary>
        Continuous = 1
    }
}
