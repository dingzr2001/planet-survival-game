namespace PlanetSurvival.World.Generation.Landforms
{
    /// <summary>One generated impact crater, in world metres.</summary>
    public readonly struct Crater
    {
        public Crater(float centerX, float centerZ, float radius, bool hasIce, bool blocksMovement,
            int gapCount, float firstGapAngle)
        {
            CenterX = centerX;
            CenterZ = centerZ;
            Radius = radius;
            HasIce = hasIce;
            BlocksMovement = blocksMovement;
            GapCount = gapCount;
            FirstGapAngle = firstGapAngle;
        }

        public float CenterX { get; }
        public float CenterZ { get; }

        /// <summary>Distance from the centre to the rim crest.</summary>
        public float Radius { get; }

        /// <summary>Whether the floor is sunk deep enough to freeze into an ice lake.</summary>
        public bool HasIce { get; }

        /// <summary>Whether the rim rises into impassable mountain, broken only by its gaps.</summary>
        public bool BlocksMovement { get; }

        public int GapCount { get; }

        /// <summary>Angle, in radians, of the first walkable gap in a blocking rim.</summary>
        public float FirstGapAngle { get; }
    }
}
