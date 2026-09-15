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
        public void Layer_WithZeroCoverage_CoversNothing()
        {
            var layer = new TerrainPatchLayer(CreateSurface("dust", 1), 20f, 0f, 0);
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
        public void SelectLayer_PrefersTheFirstLayerThatCoversThePoint()
        {
            TerrainSurfaceDefinition hard = CreateSurface("hard", 5);
            TerrainSurfaceDefinition soft = CreateSurface("soft", 1);
            // A layer with full coverage reaches everywhere, so ordering alone decides the tile.
            var layers = new[]
            {
                new TerrainPatchLayer(hard, 20f, 1f, 11),
                new TerrainPatchLayer(soft, 20f, 1f, 22)
            };

            Assert.That(ClusteredTerrainLayout.SelectLayer(layers, WorldSeed, 9f, -21f), Is.Zero);
        }

        [Test]
        public void SelectLayer_FallsBackToBaseGroundWhereNoLayerReaches()
        {
            var layers = new[] { new TerrainPatchLayer(CreateSurface("hard", 5), 20f, 0f, 11) };

            Assert.That(ClusteredTerrainLayout.SelectLayer(layers, WorldSeed, 9f, -21f),
                Is.EqualTo(ClusteredTerrainLayout.BaseLayerIndex));
        }

        /// <summary>
        /// The authoring value is an approximate fraction, not a noise implementation detail. This checks
        /// several useful rarity levels against the field itself so a change to the distribution cannot
        /// silently invalidate every terrain configuration.
        /// </summary>
        [TestCase(.02f)]
        [TestCase(.08f)]
        [TestCase(.2f)]
        public void Layer_TargetCoverage_ApproximatelyMatchesMeasuredGround(float targetCoverage)
        {
            TerrainSurfaceDefinition surface = CreateSurface("dust", 1);
            var layer = new TerrainPatchLayer(surface, 24f, targetCoverage, 99);

            float measuredCoverage = MeasureCoverage(layer);

            Assert.That(layer.TargetCoverage, Is.EqualTo(targetCoverage).Within(.0001f));
            Assert.That(measuredCoverage, Is.InRange(targetCoverage * .7f, targetCoverage * 1.3f),
                $"Requested {targetCoverage:P0}, but the reusable field covered {measuredCoverage:P1}.");
        }

        /// <summary>
        /// The property the whole approach exists for: whatever the coverage, the covered tiles arrive
        /// stuck together rather than sprinkled one at a time.
        /// </summary>
        [Test]
        public void Layer_CoversGroundInConnectedRunsRatherThanSpeckle()
        {
            var layer = new TerrainPatchLayer(CreateSurface("dust", 1), 24f, .15f, 99);
            const int side = 160;
            var covered = new bool[side, side];

            for (int x = 0; x < side; x++)
            {
                for (int z = 0; z < side; z++)
                {
                    var tile = new TerrainTileCoordinate(x - side / 2, z - side / 2);
                    covered[x, z] = layer.Covers(WorldSeed, tile.CenterX(TileSize), tile.CenterZ(TileSize));
                }
            }

            Assert.That(IsolatedTileFraction(covered, side), Is.LessThan(.1f),
                "Most covered tiles should touch another one.");
        }

        private float MeasureCoverage(TerrainPatchLayer layer)
        {
            const int side = 240;
            int covered = 0;
            for (int x = 0; x < side; x++)
            {
                for (int z = 0; z < side; z++)
                {
                    var tile = new TerrainTileCoordinate(x - side / 2, z - side / 2);
                    if (layer.Covers(WorldSeed, tile.CenterX(TileSize), tile.CenterZ(TileSize)))
                    {
                        covered++;
                    }
                }
            }

            return covered / (float)(side * side);
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
