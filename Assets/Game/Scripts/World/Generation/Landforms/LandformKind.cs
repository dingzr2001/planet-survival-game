namespace PlanetSurvival.World.Generation.Landforms
{
    /// <summary>The large-scale ground type at a point, decided before any resource patch is considered.</summary>
    public enum LandformKind
    {
        /// <summary>Ordinary open ground.</summary>
        Plain = 0,

        /// <summary>Low ground. Plays like plain ground but is drawn darker and sunk by hillshading.</summary>
        Basin = 1,

        /// <summary>Frozen water in the deepest basins and some crater floors. Walkable, buildable, yields ice.</summary>
        IceLake = 2,

        /// <summary>Solid rock massif or crater rim. Blocks movement and building and cannot be dug.</summary>
        Mountain = 3,

        /// <summary>
        /// Molten rock at the heart of a volcanic region. Blocks movement and building and cannot be dug,
        /// until something built over it (a bridge or a floor) makes it passable.
        /// </summary>
        LavaLake = 4
    }
}
