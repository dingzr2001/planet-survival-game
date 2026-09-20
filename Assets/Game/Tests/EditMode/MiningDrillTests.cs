using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Building.Application;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Inventory.Domain;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Mining.Definitions;
using PlanetSurvival.Mining.Domain;
using PlanetSurvival.World.Ground;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    public sealed class MiningDrillTests
    {
        private readonly List<Object> _created = new();
        private ItemDefinition _ore;
        private ItemDefinition _petroleum;
        private ItemDefinition _alloy;

        [SetUp]
        public void SetUp()
        {
            _ore = CreateItem("iron_ore", "Iron Ore", 2, 20);
            _petroleum = CreateItem("petroleum_canister", "Petroleum Canister", 5, 10);
            _alloy = CreateItem("aluminum_alloy", "Aluminum Alloy", 1, 20);
        }

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
        public void Advance_WithPetroleum_ProducesAtConfiguredRateAndConsumesFuel()
        {
            MiningDrill drill = CreateDrill(productionPerSecond: .5f);
            drill.ReceivePetroleum(10f);

            drill.Advance(6f);

            Assert.That(drill.StoredOre, Is.EqualTo(3));
            Assert.That(drill.StoredPetroleum, Is.EqualTo(7f).Within(.001f));
            Assert.That(drill.StoredElectricity, Is.Zero);
        }

        [Test]
        public void Advance_UsesElectricityBeforePetroleum()
        {
            MiningDrill drill = CreateDrill(productionPerSecond: 1f);
            drill.ReceiveElectricity(10f);
            drill.ReceivePetroleum(5f);

            drill.Advance(3f);

            Assert.That(drill.StoredOre, Is.EqualTo(3));
            Assert.That(drill.StoredElectricity, Is.Zero);
            Assert.That(drill.StoredPetroleum, Is.EqualTo(4f).Within(.001f));
        }

        [Test]
        public void Advance_StopsAtStorageCapacityWithoutBurningExtraFuel()
        {
            MiningDrill drill = CreateDrill(productionPerSecond: 5f, oreCapacity: 3);
            drill.ReceivePetroleum(10f);

            drill.Advance(10f);
            float fuelAfterFilling = drill.StoredPetroleum;
            drill.Advance(10f);

            Assert.That(drill.StoredOre, Is.EqualTo(3));
            Assert.That(fuelAfterFilling, Is.EqualTo(7f).Within(.001f));
            Assert.That(drill.StoredPetroleum, Is.EqualTo(fuelAfterFilling));
            Assert.That(drill.State, Is.EqualTo(MiningDrillState.StorageFull));
        }

        [Test]
        public void PlayerTransfers_LoadWholeCanistersAndCollectOnlyWhatFits()
        {
            MiningDrill drill = CreateDrill(productionPerSecond: 1f);
            var inventory = new InventoryModel(totalCapacity: 7, totalSlots: 3);
            inventory.Add(_petroleum, 1);

            int loaded = drill.LoadPetroleumItems(1, inventory);
            drill.Advance(4f);
            int collected = drill.CollectOre(4, inventory);

            Assert.That(loaded, Is.EqualTo(1));
            Assert.That(inventory.GetQuantity(_petroleum.ItemId), Is.Zero);
            Assert.That(collected, Is.EqualTo(3), "Three two-capacity ore fit in a seven-capacity backpack.");
            Assert.That(drill.StoredOre, Is.EqualTo(1));
        }

        [Test]
        public void AutomationOutput_IsLimitedByConfiguredRate()
        {
            MiningDrill drill = CreateDrill(productionPerSecond: 10f, outputPerSecond: 2f);
            drill.ReceivePetroleum(10f);
            drill.Advance(1f);

            Assert.That(drill.Extract(10, .25f), Is.Zero);
            Assert.That(drill.Extract(10, .25f), Is.EqualTo(1));
            Assert.That(drill.Extract(10, .5f), Is.EqualTo(1));
        }

        [Test]
        public void AutomationOutput_SlowerThanOneItemPerSecondCarriesFractionalAllowance()
        {
            MiningDrill drill = CreateDrill(productionPerSecond: 10f, outputPerSecond: .5f);
            drill.ReceivePetroleum(10f);
            drill.Advance(1f);

            Assert.That(drill.Extract(10, 1f), Is.Zero);
            Assert.That(drill.Extract(10, 1f), Is.EqualTo(1));
        }

        [Test]
        public void Placement_RequiresIronTerrainAndRejectsDugOutGround()
        {
            MiningDrillDefinition drillDefinition = CreateDefinition();
            BuildableDefinition buildable = CreateBuildable(drillDefinition);
            TerrainTileMap terrain = CreateCoveringTerrain("iron");
            var inventory = new InventoryModel(30, 10);
            inventory.Add(_alloy, 8);
            var service = new BuildingService(inventory, new BuildGrid(), terrain);
            var footprint = new BuildFootprint(Vector2Int.zero, Vector2Int.one);

            Assert.That(service.CanPlace(buildable, footprint).Succeeded, Is.True);

            terrain.Dig(terrain.TileAt(service.Grid.CellCenter(Vector2Int.zero)));
            BuildResult dugOut = service.CanPlace(buildable, footprint);

            Assert.That(dugOut.Succeeded, Is.False);
            Assert.That(dugOut.Failure, Is.EqualTo(BuildFailure.WrongTerrain));
        }

        [Test]
        public void Placement_RejectsAnyOtherTerrain()
        {
            MiningDrillDefinition drillDefinition = CreateDefinition();
            BuildableDefinition buildable = CreateBuildable(drillDefinition);
            var inventory = new InventoryModel(30, 10);
            inventory.Add(_alloy, 8);
            var service = new BuildingService(inventory, new BuildGrid(), CreateCoveringTerrain("ice"));

            BuildResult result = service.CanPlace(
                buildable, new BuildFootprint(Vector2Int.zero, Vector2Int.one));

            Assert.That(result.Failure, Is.EqualTo(BuildFailure.WrongTerrain));
        }

        [Test]
        public void Placement_AllowsExtractorOnConfiguredBaseSurface()
        {
            MiningDrillDefinition drillDefinition = CreateDefinition();
            drillDefinition.Configure(
                "regolith", "ordinary regolith", _ore, 1f, 20, 2f,
                electricityPerOre: 5f, electricityCapacity: 25f,
                petroleumItem: _petroleum, petroleumPerItem: 5f, petroleumPerOre: 1f,
                petroleumCapacity: 20f);
            BuildableDefinition buildable = CreateBuildable(drillDefinition);
            var regolith = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
            regolith.Configure("regolith", "Ordinary Regolith", 1, 1f, string.Empty);
            _created.Add(regolith);
            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            settings.Configure(0, 1f, 8, 1, regolith);
            _created.Add(settings);
            var terrain = new TerrainTileMap();
            terrain.Configure(123, settings);
            var inventory = new InventoryModel(30, 10);
            inventory.Add(_alloy, 8);
            var service = new BuildingService(inventory, new BuildGrid(), terrain);

            BuildResult result = service.CanPlace(
                buildable, new BuildFootprint(Vector2Int.zero, Vector2Int.one));

            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private MiningDrill CreateDrill(float productionPerSecond, int oreCapacity = 20,
            float outputPerSecond = 2f)
        {
            return new MiningDrill(CreateDefinition(productionPerSecond, oreCapacity, outputPerSecond));
        }

        private MiningDrillDefinition CreateDefinition(float productionPerSecond = 1f,
            int oreCapacity = 20, float outputPerSecond = 2f)
        {
            var definition = ScriptableObject.CreateInstance<MiningDrillDefinition>();
            definition.Configure(
                "iron", "iron", _ore, productionPerSecond, oreCapacity, outputPerSecond,
                electricityPerOre: 5f, electricityCapacity: 25f,
                petroleumItem: _petroleum, petroleumPerItem: 5f, petroleumPerOre: 1f,
                petroleumCapacity: 20f);
            _created.Add(definition);
            return definition;
        }

        private BuildableDefinition CreateBuildable(MiningDrillDefinition drillDefinition)
        {
            var buildable = ScriptableObject.CreateInstance<BuildableDefinition>();
            buildable.Configure("iron_mining_drill", "Iron Mining Drill", Vector2Int.one, 0f,
                new CraftingItemAmount(_alloy, 8));
            buildable.ConfigureMiningDrill(drillDefinition);
            _created.Add(buildable);
            return buildable;
        }

        private TerrainTileMap CreateCoveringTerrain(string terrainId)
        {
            var surface = ScriptableObject.CreateInstance<TerrainSurfaceDefinition>();
            surface.Configure(terrainId, terrainId, 1, 1f, string.Empty);
            _created.Add(surface);
            var settings = ScriptableObject.CreateInstance<TerrainPatchSettings>();
            settings.Configure(0, 1f, 8, 1, new TerrainPatchLayer(surface, 20f, 1f, 0));
            _created.Add(settings);
            var map = new TerrainTileMap();
            map.Configure(123, settings);
            return map;
        }

        private ItemDefinition CreateItem(string id, string displayName, int capacity, int stackSize)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.Configure(id, displayName, capacity, stackSize, false, true);
            _created.Add(item);
            return item;
        }
    }
}
