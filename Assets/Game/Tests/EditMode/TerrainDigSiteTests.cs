using NUnit.Framework;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Stats;
using PlanetSurvival.World.Ground;
using UnityEngine;
using UnityEngine.TestTools;

namespace PlanetSurvival.Tests
{
    public sealed class TerrainDigSiteTests
    {
        private const float TileSize = 3f;
        private static readonly TerrainTileCoordinate Tile = new(0, 0);

        private GameObject _actor;
        private GameObject _siteObject;
        private ItemDefinition _stone;
        private TerrainSurfaceDefinition _surface;
        private TerrainPatchSettings _settings;
        private TerrainTileMap _map;

        [SetUp]
        public void SetUp()
        {
            _stone = ScriptableObject.CreateInstance<ItemDefinition>();
            _stone.Configure("test_stone", "Test Stone", 1, 20, false, true);
            _surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
            _surface.Configure("test_rock", "Test Rock", 3, 1f, string.Empty,
                new ResourceYield(_stone, 1));
            _settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            // Full coverage so the layer reaches every tile the test addresses.
            _settings.Configure(0, TileSize, 8, 1, new TerrainPatchLayer(_surface, 20f, 1f, 0));
            _map = new TerrainTileMap();
            _map.Configure(1234, _settings);

            _actor = new GameObject("Digger");
            _actor.AddComponent<PlayerSurvival>();
            _actor.AddComponent<PlayerInventory>();
            _actor.transform.position = new Vector3(Tile.CenterX(TileSize), 0f, Tile.CenterZ(TileSize));

            _siteObject = new GameObject("Dig Site");
            _siteObject.transform.position = _actor.transform.position;
            _siteObject.AddComponent<BoxCollider>().size = new Vector3(TileSize, .1f, TileSize);
            _siteObject.AddComponent<TerrainDigSite>().Configure(_map, Tile);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_siteObject);
            Object.DestroyImmediate(_actor);
            Object.DestroyImmediate(_settings);
            Object.DestroyImmediate(_surface);
            Object.DestroyImmediate(_stone);
        }

        [Test]
        public void Advance_CompletesOneDig_YieldsStoneAndWearsTheTileDown()
        {
            TerrainDigSite site = _siteObject.GetComponent<TerrainDigSite>();
            PlayerInventory inventory = _actor.GetComponent<PlayerInventory>();

            site.Interact(CreateContext());
            site.Advance(1f);

            Assert.That(inventory.Inventory.Stacks[0].Quantity, Is.EqualTo(1));
            Assert.That(_map.GetRemainingDigs(Tile), Is.EqualTo(2));
            // Two digs short of clearing, so the site is still worth keeping around.
            Assert.That(site.Surface, Is.Not.Null);
        }

        [Test]
        public void Advance_AcrossEveryDig_ClearsTheTileAndPaysForEachSwing()
        {
            TerrainDigSite site = _siteObject.GetComponent<TerrainDigSite>();
            PlayerInventory inventory = _actor.GetComponent<PlayerInventory>();

            for (int i = 0; i < 3; i++)
            {
                site.Interact(CreateContext());
                site.Advance(1f);
            }

            Assert.That(inventory.Inventory.Stacks[0].Quantity, Is.EqualTo(3));
            Assert.That(_map.GetSurface(Tile), Is.Null);
            Assert.That(site.Surface, Is.Null);
        }

        [Test]
        public void ClearedTile_RefusesFurtherInteraction()
        {
            TerrainDigSite site = _siteObject.GetComponent<TerrainDigSite>();
            for (int i = 0; i < 3; i++)
            {
                site.Interact(CreateContext());
                site.Advance(1f);
            }

            Assert.That(site.CanInteract(CreateContext()), Is.False);
            Assert.That(site.Prompt, Is.Empty);
        }

        [Test]
        public void Prompt_CountsDownTheRemainingDigs()
        {
            TerrainDigSite site = _siteObject.GetComponent<TerrainDigSite>();

            Assert.That(site.Prompt, Does.Contain("3 left"));
            site.Interact(CreateContext());
            site.Advance(1f);
            Assert.That(site.Prompt, Does.Contain("2 left"));
        }

        [Test]
        public void Advance_WhenDiggerWalksOff_CancelsWithoutYieldOrProgress()
        {
            TerrainDigSite site = _siteObject.GetComponent<TerrainDigSite>();
            PlayerInventory inventory = _actor.GetComponent<PlayerInventory>();

            site.Interact(CreateContext());
            _actor.transform.position += Vector3.right * 12f;
            site.Advance(.5f);

            Assert.That(site.IsDigging, Is.False);
            Assert.That(inventory.Inventory.Stacks, Is.Empty);
            Assert.That(_map.GetRemainingDigs(Tile), Is.EqualTo(3));
        }

        [Test]
        public void Interact_WithoutTheRequiredTool_RefusesToStart()
        {
            _surface.Configure("test_rock", "Test Rock", 3, 1f, "pickaxe",
                new ResourceYield(_stone, 1));
            TerrainDigSite site = _siteObject.GetComponent<TerrainDigSite>();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("requires item"));
            site.Interact(CreateContext());

            Assert.That(site.IsDigging, Is.False);
            Assert.That(_map.GetRemainingDigs(Tile), Is.EqualTo(3));
        }

        private InteractionContext CreateContext()
        {
            return new InteractionContext(_actor, _actor.GetComponent<PlayerSurvival>(),
                _actor.GetComponent<PlayerInventory>());
        }
    }
}
