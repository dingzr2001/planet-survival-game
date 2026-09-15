using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.World.Ground;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class TerrainTileMapTests
    {
        private const int WorldSeed = 4242;
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
        public void UnconfiguredMap_ReportsBaseGroundEverywhere()
        {
            var map = new TerrainTileMap();

            Assert.That(map.GetSurface(new TerrainTileCoordinate(3, -7)), Is.Null);
            Assert.That(map.IsDiggable(new TerrainTileCoordinate(3, -7)), Is.False);
            Assert.That(map.Dig(new TerrainTileCoordinate(3, -7)).Succeeded, Is.False);
        }

        [Test]
        public void CoveredTile_StartsWithTheSurfaceDigCount()
        {
            TerrainTileMap map = CreateCoveringMap(digCount: 3);

            Assert.That(map.GetRemainingDigs(new TerrainTileCoordinate(0, 0)), Is.EqualTo(3));
            Assert.That(map.GetSurface(new TerrainTileCoordinate(0, 0)), Is.Not.Null);
        }

        [Test]
        public void Digging_ClearsTheTileOnlyAfterTheConfiguredNumberOfDigs()
        {
            TerrainTileMap map = CreateCoveringMap(digCount: 3);
            var tile = new TerrainTileCoordinate(2, 5);

            TerrainDigOutcome first = map.Dig(tile);
            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.RemainingDigs, Is.EqualTo(2));
            Assert.That(first.ClearedTerrain, Is.False);

            Assert.That(map.Dig(tile).RemainingDigs, Is.EqualTo(1));

            TerrainDigOutcome last = map.Dig(tile);
            Assert.That(last.ClearedTerrain, Is.True);
            Assert.That(map.GetSurface(tile), Is.Null);
            Assert.That(map.IsDiggable(tile), Is.False);
        }

        [Test]
        public void SingleDigTerrain_ClearsOnTheFirstDig()
        {
            TerrainTileMap map = CreateCoveringMap(digCount: 1);
            var tile = new TerrainTileCoordinate(-4, 9);

            Assert.That(map.Dig(tile).ClearedTerrain, Is.True);
            Assert.That(map.GetSurface(tile), Is.Null);
        }

        [Test]
        public void ClearedTile_StaysClearedAndYieldsNothingFurther()
        {
            TerrainTileMap map = CreateCoveringMap(digCount: 1);
            var tile = new TerrainTileCoordinate(1, 1);
            map.Dig(tile);

            Assert.That(map.Dig(tile).Succeeded, Is.False);
            Assert.That(map.GetLayerIndex(tile), Is.EqualTo(ClusteredTerrainLayout.BaseLayerIndex));
        }

        [Test]
        public void Digging_LeavesNeighbouringTilesUntouched()
        {
            TerrainTileMap map = CreateCoveringMap(digCount: 5);
            map.Dig(new TerrainTileCoordinate(0, 0));

            Assert.That(map.GetRemainingDigs(new TerrainTileCoordinate(1, 0)), Is.EqualTo(5));
            Assert.That(map.GetRemainingDigs(new TerrainTileCoordinate(0, 1)), Is.EqualTo(5));
        }

        [Test]
        public void Digging_RaisesTileChangedForTheDugTile()
        {
            TerrainTileMap map = CreateCoveringMap(digCount: 2);
            var tile = new TerrainTileCoordinate(6, -3);
            var changed = new List<TerrainTileCoordinate>();
            map.TileChanged += changed.Add;

            map.Dig(tile);

            Assert.That(changed.Count, Is.EqualTo(1));
            Assert.That(changed[0], Is.EqualTo(tile));
        }

        [Test]
        public void Clear_RestoresDugTilesAndAnnouncesThem()
        {
            TerrainTileMap map = CreateCoveringMap(digCount: 1);
            var tile = new TerrainTileCoordinate(8, 8);
            map.Dig(tile);
            var changed = new List<TerrainTileCoordinate>();
            map.TileChanged += changed.Add;

            map.Clear();

            Assert.That(map.GetSurface(tile), Is.Not.Null);
            Assert.That(map.DugTileCount, Is.Zero);
            Assert.That(changed, Contains.Item(tile));
        }

        [Test]
        public void Reconfiguring_WithTheSameSeedAndGrid_KeepsProgress()
        {
            TerrainPatchSettings settings = CreateCoveringSettings(digCount: 3);
            var map = new TerrainTileMap();
            map.Configure(WorldSeed, settings);
            var tile = new TerrainTileCoordinate(2, 2);
            map.Dig(tile);

            // A second visit to the surface reconfigures the same map; a half-dug rock face must stay so.
            map.Configure(WorldSeed, settings);

            Assert.That(map.GetRemainingDigs(tile), Is.EqualTo(2));
        }

        [Test]
        public void Reconfiguring_WithADifferentSeed_DropsProgressThatNoLongerMeansAnything()
        {
            TerrainPatchSettings settings = CreateCoveringSettings(digCount: 3);
            var map = new TerrainTileMap();
            map.Configure(WorldSeed, settings);
            map.Dig(new TerrainTileCoordinate(2, 2));

            map.Configure(WorldSeed + 1, settings);

            Assert.That(map.DugTileCount, Is.Zero);
        }

        [Test]
        public void VisualLayer_FollowsTheFieldRatherThanTheDigGrid()
        {
            // A layer whose patches are small next to the tile makes the field cross its threshold inside
            // a tile, which is the case an outline locked to whole tiles could never draw.
            var surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
            surface.Configure("patchy", "Patchy", 1, 1f, string.Empty);
            _created.Add(surface);
            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            settings.Configure(0, TileSize, 8, 1, new TerrainPatchLayer(surface, 10f, .5f, 5));
            _created.Add(settings);
            var map = new TerrainTileMap();
            map.Configure(31337, settings);

            bool splitTileFound = false;
            for (int x = 0; x < 40 && !splitTileFound; x++)
            {
                for (int z = 0; z < 40 && !splitTileFound; z++)
                {
                    var tile = new TerrainTileCoordinate(x, z);
                    int corner = map.GetVisualLayerIndex(
                        tile.MinX(TileSize) + .1f, tile.MinZ(TileSize) + .1f);
                    int opposite = map.GetVisualLayerIndex(
                        tile.MinX(TileSize) + TileSize - .1f, tile.MinZ(TileSize) + TileSize - .1f);
                    splitTileFound = corner != opposite;
                }
            }

            Assert.That(splitTileFound, Is.True);
        }

        [Test]
        public void VisualLayer_OverAClearedTile_ShowsBaseGroundEverywhereInsideIt()
        {
            TerrainTileMap map = CreateCoveringMap(digCount: 1);
            var tile = new TerrainTileCoordinate(3, 4);
            map.Dig(tile);

            // The outline is drawn more finely than the dig grid, but a dug tile has to clear completely
            // or the hole would keep a fringe of rock inside it.
            for (float x = .05f; x < TileSize; x += TileSize / 5f)
            {
                for (float z = .05f; z < TileSize; z += TileSize / 5f)
                {
                    Assert.That(
                        map.GetVisualLayerIndex(tile.MinX(TileSize) + x, tile.MinZ(TileSize) + z),
                        Is.EqualTo(ClusteredTerrainLayout.BaseLayerIndex));
                }
            }
        }

        [Test]
        public void CoverFade_IsSolidDeepInsideAPatchAndZeroOverADugTile()
        {
            TerrainTileMap map = CreateCoveringMap(digCount: 1);
            var tile = new TerrainTileCoordinate(5, 5);

            Assert.That(map.GetCoverFade(tile.CenterX(TileSize), tile.CenterZ(TileSize), .8f),
                Is.EqualTo(1f).Within(.0001f));

            map.Dig(tile);

            Assert.That(map.GetCoverFade(tile.CenterX(TileSize), tile.CenterZ(TileSize), .8f), Is.Zero,
                "A dug tile clears outright; its hole is meant to stay crisp.");
        }

        [Test]
        public void CoverFade_FallsOffTowardsTheRimOfAPatch()
        {
            var surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
            surface.Configure("patchy", "Patchy", 1, 1f, string.Empty);
            _created.Add(surface);
            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            settings.Configure(0, TileSize, 8, 1, new TerrainPatchLayer(surface, 12f, .5f, 5));
            _created.Add(settings);
            var map = new TerrainTileMap();
            map.Configure(31337, settings);

            bool foundPartialFade = false;
            for (float x = 0f; x < 120f && !foundPartialFade; x += .5f)
            {
                for (float z = 0f; z < 120f && !foundPartialFade; z += .5f)
                {
                    float fade = map.GetCoverFade(x, z, .8f);
                    foundPartialFade = fade > .01f && fade < .99f;
                }
            }

            Assert.That(foundPartialFade, Is.True,
                "Nothing ever fades, so patch rims would end on a hard cut through the artwork.");
        }

        [Test]
        public void CoverFade_WithoutAFeather_IsAlwaysSolid()
        {
            TerrainTileMap map = CreateCoveringMap(digCount: 1);

            Assert.That(map.GetCoverFade(1f, 1f, 0f), Is.EqualTo(1f));
        }

        [Test]
        public void IsClearedByDigging_IsTrueOnlyAfterTheLastDig()
        {
            TerrainTileMap map = CreateCoveringMap(digCount: 2);
            var tile = new TerrainTileCoordinate(0, 0);

            Assert.That(map.IsClearedByDigging(tile), Is.False);
            map.Dig(tile);
            Assert.That(map.IsClearedByDigging(tile), Is.False);
            map.Dig(tile);
            Assert.That(map.IsClearedByDigging(tile), Is.True);
        }

        [Test]
        public void TileAt_AddressesTilesFromWorldPositionsAcrossTheOrigin()
        {
            TerrainTileMap map = CreateCoveringMap(digCount: 1);

            Assert.That(map.TileAt(new Vector3(1f, 0f, 1f)), Is.EqualTo(new TerrainTileCoordinate(0, 0)));
            Assert.That(map.TileAt(new Vector3(-1f, 0f, -1f)), Is.EqualTo(new TerrainTileCoordinate(-1, -1)));
            Assert.That(map.TileAt(new Vector3(7f, 0f, -4f)), Is.EqualTo(new TerrainTileCoordinate(2, -2)));
        }

        /// <summary>A map whose single layer covers every tile, so the tests can address any coordinate.</summary>
        private TerrainTileMap CreateCoveringMap(int digCount)
        {
            var map = new TerrainTileMap();
            map.Configure(WorldSeed, CreateCoveringSettings(digCount));
            return map;
        }

        private TerrainPatchSettings CreateCoveringSettings(int digCount)
        {
            var surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
            surface.name = "Test Terrain";
            surface.Configure("test_terrain", "Test Terrain", digCount, 1f, string.Empty);
            _created.Add(surface);

            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            settings.name = "Test Terrain Patches";
            // Full coverage: the layer reaches everywhere, which keeps these tests about the tile map
            // rather than about where the noise field happens to put a patch.
            settings.Configure(0, TileSize, 8, 1, new TerrainPatchLayer(surface, 20f, 1f, 0));
            _created.Add(settings);
            return settings;
        }
    }
}
