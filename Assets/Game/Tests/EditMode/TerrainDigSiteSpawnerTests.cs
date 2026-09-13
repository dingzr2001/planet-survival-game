using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.World.Ground;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class TerrainDigSiteSpawnerTests
    {
        private const float TileSize = 3f;

        // What PlayerInteractor reaches, and what the neighbourhood size has to be sized against.
        private const float InteractionRadius = 2f;

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
        public void Spawner_CoversTheTileUnderThePlayerAndItsNeighbours()
        {
            TerrainDigSiteSpawner spawner = CreateSpawner(CreateCoveringMap(), Vector3.one * 1.5f);

            Assert.That(spawner.ActiveSiteCount, Is.EqualTo(9));
        }

        [Test]
        public void Spawner_LeavesBaseGroundWithNothingToInteractWith()
        {
            TerrainDigSiteSpawner spawner = CreateSpawner(CreateBareMap(), Vector3.one * 1.5f);

            Assert.That(spawner.ActiveSiteCount, Is.Zero);
        }

        [Test]
        public void Spawner_WithoutATarget_KeepsNoSites()
        {
            TerrainDigSiteSpawner spawner = CreateSpawner(CreateCoveringMap(), Vector3.zero);
            spawner.SetTarget(null);

            Assert.That(spawner.ActiveSiteCount, Is.Zero);
        }

        /// <summary>
        /// The one geometric claim the neighbourhood size rests on: from the worst spot on a tile — its
        /// corner — every one of the tiles the player could reasonably swing at is still inside the
        /// interactor's radius, and nothing two tiles away sneaks into range.
        /// </summary>
        [Test]
        public void SitesAroundThePlayer_AreAllReachableFromTheWorstCornerOfTheirTile()
        {
            var tileCorner = new Vector3(TileSize, 0f, TileSize);
            TerrainDigSiteSpawner spawner = CreateSpawner(CreateCoveringMap(), tileCorner);
            var sites = new List<TerrainDigSite>(spawner.GetComponentsInChildren<TerrainDigSite>());

            Assert.That(sites.Count, Is.EqualTo(9));
            int reachable = 0;
            for (int i = 0; i < sites.Count; i++)
            {
                float distance = Vector3.Distance(
                    tileCorner, sites[i].GetComponent<Collider>().ClosestPoint(tileCorner));
                if (distance <= InteractionRadius)
                {
                    reachable++;
                }
            }

            // The four tiles meeting at that corner; the five behind the player are correctly out of reach.
            Assert.That(reachable, Is.EqualTo(4));
        }

        private TerrainDigSiteSpawner CreateSpawner(TerrainTileMap map, Vector3 targetPosition)
        {
            var target = new GameObject("Player");
            target.transform.position = targetPosition;
            _created.Add(target);

            var root = new GameObject("Terrain Patches");
            _created.Add(root);
            TerrainDigSiteSpawner spawner = root.AddComponent<TerrainDigSiteSpawner>();
            spawner.Configure(map);
            spawner.SetTarget(target.transform);
            return spawner;
        }

        private TerrainTileMap CreateCoveringMap() => CreateMap(threshold: 0f);

        private TerrainTileMap CreateBareMap() => CreateMap(threshold: 1f);

        private TerrainTileMap CreateMap(float threshold)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.Configure("test_stone", "Test Stone", 1, 20, false, true);
            _created.Add(item);

            var surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
            surface.name = "Test Terrain";
            surface.Configure("test_terrain", "Test Terrain", 3, 1f, string.Empty,
                new ResourceYield(item, 1));
            _created.Add(surface);

            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            settings.Configure(0, TileSize, 8, 1, new TerrainPatchLayer(surface, 20f, threshold, 0));
            _created.Add(settings);

            var map = new TerrainTileMap();
            map.Configure(7, settings);
            return map;
        }
    }
}
