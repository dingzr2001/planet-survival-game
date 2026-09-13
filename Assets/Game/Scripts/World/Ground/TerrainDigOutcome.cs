namespace PlanetSurvival.World.Ground
{
    /// <summary>What one dig did to a tile.</summary>
    public readonly struct TerrainDigOutcome
    {
        /// <summary>A dig on ground that has nothing to break: base regolith, or a tile already cleared.</summary>
        public static TerrainDigOutcome Nothing => default;

        public TerrainDigOutcome(TerrainSurfaceDefinition surface, int remainingDigs)
        {
            Surface = surface;
            RemainingDigs = remainingDigs < 0 ? 0 : remainingDigs;
        }

        /// <summary>The terrain that was dug, which still yields its resources on the clearing hit.</summary>
        public TerrainSurfaceDefinition Surface { get; }
        public int RemainingDigs { get; }
        public bool Succeeded => Surface != null;

        /// <summary>True on the hit that turned the tile back into base regolith.</summary>
        public bool ClearedTerrain => Succeeded && RemainingDigs == 0;
    }
}
