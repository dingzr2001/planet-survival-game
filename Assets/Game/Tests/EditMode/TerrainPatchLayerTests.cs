using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.World.Ground;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    /// <summary>
    /// Covers the reusable half of terrain generation: the clustered field and the layer selection built
    /// on it. These say nothing about rock in particular, because neither does the code under test.
    /// </summary>
    public sealed class TerrainPatchLayerTests
    {
        private const int WorldSeed = 8128;
        private const float TileSize = 3f;

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void NoiseField_RepeatsForTheSamePoint()
        {
            float first = ClusteredNoiseField.Sample(WorldSeed, 20f, 13.5f, -41.25f);
            float second = ClusteredNoiseField.Sample(WorldSeed, 20f, 13.5f, -41.25f);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void NoiseField_StaysInsideTheUnitRange()
        {
            for (int i = 0; i < 500; i++)
            {
                float value = ClusteredNoiseField.Sample(WorldSeed, 20f, i * 7.3f, i * -4.1f);
                Assert.That(value, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void NoiseField_MovesLessBetweenNeighboursThanBetweenDistantPoints()
        {
            // The whole reason a threshold on this field produces connected patches instead of speckle.
            float neighbourDelta = 0f;
            float distantDelta = 0f;
            const int samples = 400;

            for (int i = 0; i < samples; i++)
            {
                float x = i * 11.7f;
                float z = i * -5.3f;
                float value = ClusteredNoiseField.Sample(WorldSeed, 24f, x, z);
                neighbourDelta += Mathf.Abs(ClusteredNoiseField.Sample(WorldSeed, 24f, x + TileSize, z) - value);
                distantDelta += Mathf.Abs(ClusteredNoiseField.Sample(WorldSeed, 24f, x + 240f, z) - value);
            }

            Assert.That(neighbourDelta / samples, Is.LessThan(distantDelta / samples * .5f));
        }

        [Test]
        public void NoiseField_SeparatesLayersThatUseDifferentSeedOffsets()
        {
            float first = ClusteredNoiseField.Sample(WorldSeed ^ 1613, 20f, 60f, 60f);
            float second = ClusteredNoiseField.Sample(WorldSeed ^ 7817, 20f, 60f, 60f);

            Assert.That(second, Is.Not.EqualTo(first).Within(.0001f));
        }

        [Test]
        public void Layer_WithoutSurface_CoversNothing()
        {
            var layer = new TerrainPatchLayer(null, 20f, 0f, 0);

            Assert.That(layer.Covers(WorldSeed, 0f, 0f), Is.False);
        }

        [Test]
        public void Layer_WithFullThreshold_CoversNothing()
        {
            var layer = new TerrainPatchLayer(CreateSurface("dust", 1), 20f, 1f, 0);
            int covered = 0;

            for (int x = 0; x < 60; x++)
            {
                for (int z = 0; z < 60; z++)
                {
                    if (layer.Covers(WorldSeed, x * TileSize, z * TileSize))
                    {
                        covered++;
                    }
                }
            }

            Assert.That(covered, Is.Zero);
        }

        [Test]
        public void SelectLayer_PrefersTheFirstLayerThatReachesItsThreshold()
        {
            TerrainSurfaceDefinition hard = CreateSurface("hard", 5);
            TerrainSurfaceDefinition soft = CreateSurface("soft", 1);
            // A layer with threshold zero covers everywhere, so ordering alone decides the tile.
            var layers = new[]
            {
                new TerrainPatchLayer(hard, 20f, 0f, 11),
                new TerrainPatchLayer(soft, 20f, 0f, 22)
            };

            Assert.That(ClusteredTerrainLayout.SelectLayer(layers, WorldSeed, 9f, -21f), Is.Zero);
        }

        [Test]
        public void SelectLayer_FallsBackToBaseGroundWhereNoLayerReaches()
        {
            var layers = new[] { new TerrainPatchLayer(CreateSurface("hard", 5), 20f, 1f, 11) };

            Assert.That(ClusteredTerrainLayout.SelectLayer(layers, WorldSeed, 9f, -21f),
                Is.EqualTo(ClusteredTerrainLayout.BaseLayerIndex));
        }

        /// <summary>
        /// Records what the thresholds shipped in <c>DefaultTerrainPatches</c> actually cover. Thresholds
        /// are not percentages — the field clusters around its middle — so this is where the numbers used
        /// by the authoring code are justified, and it fails if a change to the noise moves them.
        /// </summary>
        [Test]
        public void ShippedRockLayers_CoverRoughlyAQuarterOfTheSurfaceInConnectedPatches()
        {
            var layers = new[]
            {
                new TerrainPatchLayer(CreateSurface("boulder_field", 5), 15f, .78f, 1613),
                new TerrainPatchLayer(CreateSurface("broken_rock", 3), 20f, .73f, 7817),
                new TerrainPatchLayer(CreateSurface("loose_scree", 1), 28f, .68f, 3271)
            };
            const int side = 160;
            var covered = new bool[side, side];
            int rockTiles = 0;

            for (int x = 0; x < side; x++)
            {
                for (int z = 0; z < side; z++)
                {
                    var tile = new TerrainTileCoordinate(x - side / 2, z - side / 2);
                    int layerIndex = ClusteredTerrainLayout.SelectLayer(
                        layers, WorldSeed, tile.CenterX(TileSize), tile.CenterZ(TileSize));
                    covered[x, z] = layerIndex != ClusteredTerrainLayout.BaseLayerIndex;
                    if (covered[x, z])
                    {
                        rockTiles++;
                    }
                }
            }

            float coverage = rockTiles / (float)(side * side);
            Assert.That(coverage, Is.InRange(.18f, .34f),
                "Rock should break up the regolith, not replace it.");
            Assert.That(IsolatedTileFraction(covered, side), Is.LessThan(.2f),
                "Rock is meant to generate in runs; most tiles should touch another rock tile.");
        }

        private static float IsolatedTileFraction(bool[,] covered, int side)
        {
            int rockTiles = 0;
            int isolated = 0;

            for (int x = 1; x < side - 1; x++)
            {
                for (int z = 1; z < side - 1; z++)
                {
                    if (!covered[x, z])
                    {
                        continue;
                    }

                    rockTiles++;
                    if (!covered[x - 1, z] && !covered[x + 1, z] && !covered[x, z - 1] && !covered[x, z + 1])
                    {
                        isolated++;
                    }
                }
            }

            return rockTiles == 0 ? 1f : isolated / (float)rockTiles;
        }

        private TerrainSurfaceDefinition CreateSurface(string terrainId, int digCount)
        {
            var surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
            surface.name = terrainId;
            surface.Configure(terrainId, terrainId, digCount, 1f, string.Empty);
            _created.Add(surface);
            return surface;
        }
    }
}
