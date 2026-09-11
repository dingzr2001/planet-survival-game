using System;
using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Gathering.Runtime;
using PlanetSurvival.World.Chunks;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class ChunkResourcePlannerTests
    {
        private const int WorldSeed = 8128;
        private const float ChunkSize = 32f;

        [Test]
        public void Plan_SameChunkAndSeed_ProducesSamePlacements()
        {
            var chunk = new ChunkCoordinate(-3, 5);
            IReadOnlyList<ChunkResourcePlacement> first = Plan(chunk, 4f, 2.5f, 1.5f);
            IReadOnlyList<ChunkResourcePlacement> second = Plan(chunk, 4f, 2.5f, 1.5f);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].EntryIndex, Is.EqualTo(first[i].EntryIndex));
                Assert.That(second[i].WorldX, Is.EqualTo(first[i].WorldX));
                Assert.That(second[i].WorldZ, Is.EqualTo(first[i].WorldZ));
            }
        }

        [Test]
        public void Plan_PlacesNodesInsideTheirOwnChunk()
        {
            for (int x = -4; x <= 4; x++)
            for (int z = -4; z <= 4; z++)
            {
                var chunk = new ChunkCoordinate(x, z);
                IReadOnlyList<ChunkResourcePlacement> placements = Plan(chunk, 4f, 2.5f, 1.5f);
                for (int i = 0; i < placements.Count; i++)
                {
                    Assert.That(placements[i].WorldX, Is.InRange(chunk.OriginX(ChunkSize), chunk.OriginX(ChunkSize) + ChunkSize));
                    Assert.That(placements[i].WorldZ, Is.InRange(chunk.OriginZ(ChunkSize), chunk.OriginZ(ChunkSize) + ChunkSize));
                }
            }
        }

        [Test]
        public void Plan_RespectsMinimumSpacingWithinAChunk()
        {
            const float spacing = 4f;
            IReadOnlyList<ChunkResourcePlacement> placements = Plan(new ChunkCoordinate(2, -7), spacing, 3f, 3f);

            for (int i = 0; i < placements.Count; i++)
            for (int j = i + 1; j < placements.Count; j++)
            {
                float deltaX = placements[i].WorldX - placements[j].WorldX;
                float deltaZ = placements[i].WorldZ - placements[j].WorldZ;
                Assert.That(deltaX * deltaX + deltaZ * deltaZ, Is.GreaterThanOrEqualTo(spacing * spacing));
            }
        }

        [Test]
        public void Plan_EveryChunkCanCarryResources()
        {
            int populatedChunks = 0;
            const int chunkCount = 40;
            for (int x = 0; x < chunkCount; x++)
            {
                if (Plan(new ChunkCoordinate(x, x * 3 - 17), 4f, 2.5f, 1.5f).Count > 0)
                {
                    populatedChunks++;
                }
            }

            Assert.That(populatedChunks, Is.EqualTo(chunkCount));
        }

        [Test]
        public void Plan_FractionalDensity_SpreadsNodesOverChunks()
        {
            const float density = .5f;
            const int chunkCount = 400;
            int nodeCount = 0;
            int emptyChunks = 0;
            for (int x = 0; x < 20; x++)
            for (int z = 0; z < 20; z++)
            {
                int count = Plan(new ChunkCoordinate(x, z), 4f, density).Count;
                nodeCount += count;
                if (count == 0)
                {
                    emptyChunks++;
                }
            }

            Assert.That(emptyChunks, Is.GreaterThan(0), "A density below one must leave some chunks empty.");
            Assert.That(nodeCount / (float)chunkCount, Is.EqualTo(density).Within(.1f));
        }

        [Test]
        public void Plan_WithoutDensities_ReturnsNoPlacements()
        {
            Assert.That(ChunkResourcePlanner.Plan(new ChunkCoordinate(0, 0), WorldSeed, ChunkSize, 4f, null),
                Is.Empty);
            Assert.That(Plan(new ChunkCoordinate(0, 0), 4f, 0f, 0f), Is.Empty);
        }

        [Test]
        public void Plan_NonPositiveChunkSize_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ChunkResourcePlanner.Plan(new ChunkCoordinate(0, 0), WorldSeed, 0f, 4f, new[] { 1f }));
        }

        [Test]
        public void PlanResources_KeepsArbitrarySizedFootprintsApartAcrossChunkBoundaries()
        {
            var definition = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            definition.Configure("large", "Large Resource", 1f, 2f, string.Empty,
                new Vector3(11.25f, 1f, 8.4f));
            var entries = new[] { new ResourceSpawnEntry(definition, 3f) };
            var placements = new List<ChunkResourcePlacement>();

            for (int x = -2; x <= 2; x++)
            for (int z = -2; z <= 2; z++)
            {
                placements.AddRange(ChunkResourcePlanner.PlanResources(
                    new ChunkCoordinate(x, z), WorldSeed, ChunkSize, 1.5f, entries));
            }

            Assert.That(placements.Count, Is.GreaterThan(0));
            Vector2 size = definition.SelectWorldFootprint(0);
            for (int i = 0; i < placements.Count; i++)
            for (int j = i + 1; j < placements.Count; j++)
            {
                float gapX = Mathf.Max(0f,
                    Mathf.Abs(placements[i].WorldX - placements[j].WorldX) - size.x);
                float gapZ = Mathf.Max(0f,
                    Mathf.Abs(placements[i].WorldZ - placements[j].WorldZ) - size.y);
                Assert.That(gapX * gapX + gapZ * gapZ, Is.GreaterThanOrEqualTo(1.5f * 1.5f));
            }

            UnityEngine.Object.DestroyImmediate(definition);
        }

        private static IReadOnlyList<ChunkResourcePlacement> Plan(ChunkCoordinate chunk, float minimumSpacing,
            params float[] densities)
        {
            return ChunkResourcePlanner.Plan(chunk, WorldSeed, ChunkSize, minimumSpacing, densities);
        }
    }
}
