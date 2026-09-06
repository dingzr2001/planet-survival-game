using System;
using System.Collections.Generic;
using PlanetSurvival.World.Chunks;

namespace PlanetSurvival.Gathering.Runtime
{
    /// <summary>
    /// Plans the resource nodes of a single chunk. The plan only depends on the world seed and the chunk
    /// coordinate, so a chunk produces the same layout no matter when or how often the player visits it.
    /// </summary>
    public static class ChunkResourcePlanner
    {
        // Rejection sampling budget per node. A node that never finds a spot far enough from its neighbours is
        // dropped, which keeps sparse chunks cheap instead of looping until the spacing constraint is satisfied.
        private const int PlacementAttempts = 12;

        /// <param name="densities">
        /// Expected node count per chunk for each spawn entry. Fractional values are resolved per chunk, so a
        /// density below one makes the entry appear in only a fraction of the chunks.
        /// </param>
        public static IReadOnlyList<ChunkResourcePlacement> Plan(ChunkCoordinate chunk, int worldSeed,
            float chunkSize, float minimumSpacing, IReadOnlyList<float> densities)
        {
            if (chunkSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Chunk size must be positive.");
            }

            if (densities == null || densities.Count == 0)
            {
                return Array.Empty<ChunkResourcePlacement>();
            }

            var random = new Random(CreateChunkSeed(worldSeed, chunk));
            float originX = chunk.OriginX(chunkSize);
            float originZ = chunk.OriginZ(chunkSize);
            float squaredSpacing = Math.Max(0f, minimumSpacing) * Math.Max(0f, minimumSpacing);
            var placements = new List<ChunkResourcePlacement>();

            for (int entryIndex = 0; entryIndex < densities.Count; entryIndex++)
            {
                int count = ResolveCount(densities[entryIndex], random);
                for (int node = 0; node < count; node++)
                {
                    for (int attempt = 0; attempt < PlacementAttempts; attempt++)
                    {
                        float worldX = originX + (float)random.NextDouble() * chunkSize;
                        float worldZ = originZ + (float)random.NextDouble() * chunkSize;
                        if (!IsFarEnough(worldX, worldZ, placements, squaredSpacing))
                        {
                            continue;
                        }

                        placements.Add(new ChunkResourcePlacement(entryIndex, worldX, worldZ));
                        break;
                    }
                }
            }

            return placements;
        }

        private static int ResolveCount(float density, Random random)
        {
            if (density <= 0f)
            {
                return 0;
            }

            int guaranteed = (int)Math.Floor(density);
            float chance = density - guaranteed;
            return chance > 0f && random.NextDouble() < chance ? guaranteed + 1 : guaranteed;
        }

        private static bool IsFarEnough(float worldX, float worldZ, List<ChunkResourcePlacement> placements,
            float squaredSpacing)
        {
            for (int i = 0; i < placements.Count; i++)
            {
                float deltaX = worldX - placements[i].WorldX;
                float deltaZ = worldZ - placements[i].WorldZ;
                if (deltaX * deltaX + deltaZ * deltaZ < squaredSpacing)
                {
                    return false;
                }
            }

            return true;
        }

        // FNV-1a keeps neighbouring chunks from sharing similar seeds, which would make their layouts correlate.
        private static int CreateChunkSeed(int worldSeed, ChunkCoordinate chunk)
        {
            unchecked
            {
                const int prime = 16777619;
                int hash = (int)2166136261;
                hash = (hash ^ worldSeed) * prime;
                hash = (hash ^ chunk.X) * prime;
                hash = (hash ^ chunk.Z) * prime;
                // System.Random rejects negative seeds on some runtimes, so keep the hash non-negative.
                return hash & int.MaxValue;
            }
        }
    }
}
