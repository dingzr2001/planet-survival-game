namespace PlanetSurvival.World.Generation.Landforms
{
    /// <summary>
    /// The landform field at one point. <see cref="Elevation"/> is normalised against the seed's own
    /// thresholds: 0 is the ice-lake shoreline and 1 is the foot of the mountains, so presentation can
    /// shade relief and draw shorelines without knowing the raw noise range.
    /// </summary>
    public readonly struct LandformSample
    {
        public LandformSample(float elevation, float basinLevel, LandformKind kind)
        {
            Elevation = elevation;
            BasinLevel = basinLevel;
            Kind = kind;
        }

        public float Elevation { get; }

        /// <summary>The normalised elevation below which ground counts as basin, for this seed.</summary>
        public float BasinLevel { get; }

        public LandformKind Kind { get; }

        public bool BlocksMovement => Blocks(Kind);

        /// <summary>
        /// Whether the ground itself stops walking and building: mountains and lava lakes. The one rule every
        /// system asks, so something built over lava later needs to change it in one place.
        /// </summary>
        public static bool Blocks(LandformKind kind) => kind == LandformKind.Mountain || kind == LandformKind.LavaLake;
    }
}
