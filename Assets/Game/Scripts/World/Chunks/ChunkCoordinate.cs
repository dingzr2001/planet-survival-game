using System;

namespace PlanetSurvival.World.Chunks
{
    /// <summary>
    /// Address of a square chunk on the endless XZ plane. Chunk (0, 0) starts at the world origin and
    /// extends towards positive X and Z, so chunk coordinates are negative on the other side of the origin.
    /// </summary>
    public readonly struct ChunkCoordinate : IEquatable<ChunkCoordinate>
    {
        public ChunkCoordinate(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int X { get; }
        public int Z { get; }

        public static ChunkCoordinate FromWorld(float worldX, float worldZ, float chunkSize)
        {
            if (chunkSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Chunk size must be positive.");
            }

            return new ChunkCoordinate(FloorDivide(worldX, chunkSize), FloorDivide(worldZ, chunkSize));
        }

        public float OriginX(float chunkSize) => X * chunkSize;

        public float OriginZ(float chunkSize) => Z * chunkSize;

        /// <summary>Number of chunk steps to <paramref name="other"/> on a square ring around this chunk.</summary>
        public int RingDistanceTo(ChunkCoordinate other)
        {
            return Math.Max(Math.Abs(X - other.X), Math.Abs(Z - other.Z));
        }

        public bool Equals(ChunkCoordinate other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is ChunkCoordinate other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Z;
        public override string ToString() => $"({X}, {Z})";

        private static int FloorDivide(float value, float size) => (int)Math.Floor(value / size);
    }
}
