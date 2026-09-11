using System;
using System.Collections.Generic;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.World.Chunks;
using UnityEngine;

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

            int chunkSeed = CreateChunkSeed(worldSeed, chunk);
            var random = new System.Random(chunkSeed);
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

                        // Derived from the chunk seed and the node's index rather than drawn from the
                        // random sequence, so adding art variants never shifts where anything spawns.
                        placements.Add(new ChunkResourcePlacement(
                            entryIndex, worldX, worldZ, CreateVariantSeed(chunkSeed, placements.Count)));
                        break;
                    }
                }
            }

            return placements;
        }

        /// <summary>
        /// Plans continuous, variably sized resources. Chunk coordinates remain a streaming implementation
        /// detail: node centres and extents use world units, and candidates inspect neighbouring chunks so
        /// large resources cannot overlap across a chunk boundary.
        /// </summary>
        public static IReadOnlyList<ChunkResourcePlacement> PlanResources(ChunkCoordinate chunk, int worldSeed,
            float chunkSize, float minimumSpacing, IReadOnlyList<ResourceSpawnEntry> entries)
        {
            if (chunkSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, "Chunk size must be positive.");
            }

            if (entries == null || entries.Count == 0)
            {
                return Array.Empty<ChunkResourcePlacement>();
            }

            Vector2 maximumFootprint = FindMaximumFootprint(entries);
            float clearance = Math.Max(0f, minimumSpacing);
            float inspectionDistance = Math.Max(maximumFootprint.x, maximumFootprint.y) + clearance;
            int neighbourRadius = Math.Max(1, (int)Math.Ceiling(inspectionDistance / chunkSize) + 1);
            List<Candidate> targetCandidates = CreateCandidates(chunk, worldSeed, chunkSize, entries);
            if (targetCandidates.Count == 0)
            {
                return Array.Empty<ChunkResourcePlacement>();
            }

            var nearbyCandidates = new List<Candidate>();
            for (int x = chunk.X - neighbourRadius; x <= chunk.X + neighbourRadius; x++)
            {
                for (int z = chunk.Z - neighbourRadius; z <= chunk.Z + neighbourRadius; z++)
                {
                    nearbyCandidates.AddRange(CreateCandidates(
                        new ChunkCoordinate(x, z), worldSeed, chunkSize, entries));
                }
            }

            var accepted = new List<ChunkResourcePlacement>(targetCandidates.Count);
            for (int i = 0; i < targetCandidates.Count; i++)
            {
                Candidate candidate = targetCandidates[i];
                if (!HasHigherPriorityConflict(candidate, nearbyCandidates, clearance))
                {
                    accepted.Add(candidate.Placement);
                }
            }

            return accepted;
        }

        private static int ResolveCount(float density, System.Random random)
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

        private static List<Candidate> CreateCandidates(ChunkCoordinate chunk, int worldSeed, float chunkSize,
            IReadOnlyList<ResourceSpawnEntry> entries)
        {
            int chunkSeed = CreateChunkSeed(worldSeed, chunk);
            var random = new System.Random(chunkSeed);
            float originX = chunk.OriginX(chunkSize);
            float originZ = chunk.OriginZ(chunkSize);
            var candidates = new List<Candidate>();
            int placementId = 0;

            for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
            {
                ResourceSpawnEntry entry = entries[entryIndex];
                ResourceNodeDefinition definition = entry.Definition;
                int count = ResolveCount(definition != null ? entry.NodesPerChunk : 0f, random);
                for (int node = 0; node < count; node++)
                {
                    float worldX = originX + (float)random.NextDouble() * chunkSize;
                    float worldZ = originZ + (float)random.NextDouble() * chunkSize;
                    int variantSeed = CreateVariantSeed(chunkSeed, placementId);
                    var placement = new ChunkResourcePlacement(
                        entryIndex, worldX, worldZ, variantSeed, placementId);
                    candidates.Add(new Candidate(
                        chunk, placement, definition.SelectWorldFootprint(variantSeed),
                        CreateCandidatePriority(chunkSeed, placementId)));
                    placementId++;
                }
            }

            return candidates;
        }

        private static Vector2 FindMaximumFootprint(IReadOnlyList<ResourceSpawnEntry> entries)
        {
            Vector2 maximum = Vector2.one * .1f;
            for (int i = 0; i < entries.Count; i++)
            {
                ResourceNodeDefinition definition = entries[i].Definition;
                if (definition == null)
                {
                    continue;
                }

                Vector2 footprint = definition.MaximumWorldFootprint;
                maximum = new Vector2(Mathf.Max(maximum.x, footprint.x), Mathf.Max(maximum.y, footprint.y));
            }

            return maximum;
        }

        private static bool HasHigherPriorityConflict(Candidate candidate, List<Candidate> nearby,
            float minimumSpacing)
        {
            for (int i = 0; i < nearby.Count; i++)
            {
                Candidate other = nearby[i];
                if (candidate.IsSameNode(other) || !other.HasPriorityOver(candidate) ||
                    !FootprintsConflict(candidate, other, minimumSpacing))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        private static bool FootprintsConflict(Candidate first, Candidate second, float minimumSpacing)
        {
            float gapX = Math.Abs(first.Placement.WorldX - second.Placement.WorldX)
                         - (first.Footprint.x + second.Footprint.x) * .5f;
            float gapZ = Math.Abs(first.Placement.WorldZ - second.Placement.WorldZ)
                         - (first.Footprint.y + second.Footprint.y) * .5f;
            float separatedX = Math.Max(0f, gapX);
            float separatedZ = Math.Max(0f, gapZ);

            if (gapX < 0f && gapZ < 0f)
            {
                return true;
            }

            return separatedX * separatedX + separatedZ * separatedZ < minimumSpacing * minimumSpacing;
        }

        private static uint CreateCandidatePriority(int chunkSeed, int placementId)
        {
            unchecked
            {
                uint hash = (uint)chunkSeed ^ 2166136261u;
                hash = (hash ^ (uint)placementId) * 16777619u;
                hash ^= hash >> 13;
                hash *= 0x85ebca6bu;
                return hash ^ (hash >> 16);
            }
        }

        /// <summary>
        /// A stable per-node number used to pick its art variant. Mixing the index into the chunk seed
        /// keeps neighbouring nodes of one chunk from all landing on the same variant.
        /// </summary>
        private static int CreateVariantSeed(int chunkSeed, int placementIndex)
        {
            unchecked
            {
                const int prime = 16777619;
                int hash = (chunkSeed ^ 0x5f356495) * prime;
                hash = (hash ^ placementIndex) * prime;
                return hash;
            }
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

        private readonly struct Candidate
        {
            public Candidate(ChunkCoordinate chunk, ChunkResourcePlacement placement, Vector2 footprint,
                uint priority)
            {
                Chunk = chunk;
                Placement = placement;
                Footprint = footprint;
                Priority = priority;
            }

            public ChunkCoordinate Chunk { get; }
            public ChunkResourcePlacement Placement { get; }
            public Vector2 Footprint { get; }
            private uint Priority { get; }

            public bool IsSameNode(Candidate other)
            {
                return Chunk.Equals(other.Chunk) && Placement.PlacementId == other.Placement.PlacementId;
            }

            public bool HasPriorityOver(Candidate other)
            {
                if (Priority != other.Priority)
                {
                    return Priority < other.Priority;
                }

                if (Chunk.X != other.Chunk.X)
                {
                    return Chunk.X < other.Chunk.X;
                }

                if (Chunk.Z != other.Chunk.Z)
                {
                    return Chunk.Z < other.Chunk.Z;
                }

                return Placement.PlacementId < other.Placement.PlacementId;
            }
        }
    }
}
