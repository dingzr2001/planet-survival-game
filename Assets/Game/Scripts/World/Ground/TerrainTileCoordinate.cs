using System;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// Address of one square terrain tile on the endless XZ plane. Tile (0, 0) starts at the world origin
    /// and extends towards positive X and Z, so tiles are negative on the other side of the origin.
    /// </summary>
    public readonly struct TerrainTileCoordinate : IEquatable<TerrainTileCoordinate>
    {
        public TerrainTileCoordinate(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int X { get; }
        public int Z { get; }

        public static TerrainTileCoordinate FromWorld(float worldX, float worldZ, float tileSize)
        {
            if (tileSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tileSize), tileSize, "Tile size must be positive.");
            }

            return new TerrainTileCoordinate(FloorDivide(worldX, tileSize), FloorDivide(worldZ, tileSize));
        }

        public float MinX(float tileSize) => X * tileSize;
        public float MinZ(float tileSize) => Z * tileSize;
        public float CenterX(float tileSize) => (X + .5f) * tileSize;
        public float CenterZ(float tileSize) => (Z + .5f) * tileSize;

        public bool Equals(TerrainTileCoordinate other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is TerrainTileCoordinate other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Z;
        public override string ToString() => $"({X}, {Z})";

        private static int FloorDivide(float value, float size) => (int)Math.Floor(value / size);
    }
}
