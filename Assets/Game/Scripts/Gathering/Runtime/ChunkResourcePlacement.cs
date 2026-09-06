namespace PlanetSurvival.Gathering.Runtime
{
    /// <summary>One planned resource node inside a chunk, expressed in world units on the ground plane.</summary>
    public readonly struct ChunkResourcePlacement
    {
        public ChunkResourcePlacement(int entryIndex, float worldX, float worldZ)
        {
            EntryIndex = entryIndex;
            WorldX = worldX;
            WorldZ = worldZ;
        }

        /// <summary>Index into the spawn entries the plan was created from.</summary>
        public int EntryIndex { get; }

        public float WorldX { get; }
        public float WorldZ { get; }

        public override string ToString() => $"entry {EntryIndex} at ({WorldX:0.##}, {WorldZ:0.##})";
    }
}
