namespace PlanetSurvival.Gathering.Runtime
{
    /// <summary>One planned resource node inside a chunk, expressed in world units on the ground plane.</summary>
    public readonly struct ChunkResourcePlacement
    {
        public ChunkResourcePlacement(int entryIndex, float worldX, float worldZ, int variantSeed = 0,
            int placementId = 0)
        {
            EntryIndex = entryIndex;
            WorldX = worldX;
            WorldZ = worldZ;
            VariantSeed = variantSeed;
            PlacementId = placementId;
        }

        /// <summary>Index into the spawn entries the plan was created from.</summary>
        public int EntryIndex { get; }

        public float WorldX { get; }
        public float WorldZ { get; }

        /// <summary>
        /// Chooses which cutout of the resource this node wears. It is part of the plan rather than
        /// drawn when the node is built, so streaming a chunk back in restores the same look.
        /// </summary>
        public int VariantSeed { get; }

        /// <summary>
        /// Stable raw candidate index inside the owning chunk. Filtering a nearby overlapping candidate does
        /// not renumber this node, so gathered-state identity survives streaming.
        /// </summary>
        public int PlacementId { get; }

        public override string ToString() => $"entry {EntryIndex} at ({WorldX:0.##}, {WorldZ:0.##})";
    }
}
