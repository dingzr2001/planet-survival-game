namespace PlanetSurvival.Gathering.Runtime
{
    /// <summary>One planned resource node inside a chunk, expressed in world units on the ground plane.</summary>
    public readonly struct ChunkResourcePlacement
    {
        public ChunkResourcePlacement(int entryIndex, float worldX, float worldZ, int variantSeed = 0)
        {
            EntryIndex = entryIndex;
            WorldX = worldX;
            WorldZ = worldZ;
            VariantSeed = variantSeed;
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

        public override string ToString() => $"entry {EntryIndex} at ({WorldX:0.##}, {WorldZ:0.##})";
    }
}
