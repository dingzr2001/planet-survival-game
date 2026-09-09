using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Building.Application;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Items.Definitions;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    public sealed class BuildingSystemTests
    {
        private const float WallSeconds = 6f;

        private readonly List<Object> _createdAssets = new();
        private ItemDefinition _stone;
        private BuildableDefinition _wall;
        private BuildableDefinition _oven;

        [SetUp]
        public void SetUp()
        {
            _stone = CreateItem("raw_stone", "Raw Stone");
            _wall = CreateBuildable("stone_wall", "Stone Wall", Vector2Int.one, WallSeconds,
                new CraftingItemAmount(_stone, 4));
            _oven = CreateBuildable("field_oven", "Field Oven", new Vector2Int(2, 2), 20f,
                new CraftingItemAmount(_stone, 6));
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _createdAssets.Count; i++)
            {
                Object.DestroyImmediate(_createdAssets[i]);
            }

            _createdAssets.Clear();
        }

        [Test]
        public void Grid_SnapsAPositionToTheCellUnderIt()
        {
            var grid = new BuildGrid(2f);

            BuildFootprint footprint = grid.CreateFootprint(new Vector3(5.3f, 0f, -1.2f), Vector2Int.one);

            Assert.That(footprint.Origin, Is.EqualTo(new Vector2Int(2, -1)));
            Assert.That(grid.Center(footprint), Is.EqualTo(new Vector3(5f, 0f, -1f)));
        }

        [Test]
        public void Grid_KeepsTheCursorCellInsideAnEvenFootprint()
        {
            var grid = new BuildGrid();

            BuildFootprint footprint = grid.CreateFootprint(new Vector3(4.6f, 0f, 4.2f), new Vector2Int(2, 2));

            Assert.That(footprint.Contains(new Vector2Int(4, 4)), Is.True);
            Assert.That(footprint.Origin, Is.EqualTo(new Vector2Int(4, 4)));
            Assert.That(footprint.CellCount, Is.EqualTo(4));
        }

        [Test]
        public void Place_SpendsTheMaterialsAndClaimsEveryCell()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_stone, 10);
            var service = new BuildingService(inventory, new BuildGrid());

            BuildResult result = service.TryPlace(_oven, new BuildFootprint(Vector2Int.zero, new Vector2Int(2, 2)),
                out BuildSite site);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(inventory.GetQuantity("raw_stone"), Is.EqualTo(4));
            Assert.That(site.State, Is.EqualTo(BuildState.UnderConstruction));
            Assert.That(service.Grid.OccupiedCellCount, Is.EqualTo(4));
            Assert.That(service.Grid.GetOccupant(new Vector2Int(1, 1)), Is.SameAs(site));
        }

        [Test]
        public void Place_RejectsCellsAnotherStructureAlreadyCovers()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_stone, 20);
            var service = new BuildingService(inventory, new BuildGrid());
            service.TryPlace(_oven, new BuildFootprint(Vector2Int.zero, new Vector2Int(2, 2)), out BuildSite _);

            BuildResult overlapping = service.TryPlace(_wall, new BuildFootprint(Vector2Int.one, Vector2Int.one),
                out BuildSite blocked);

            Assert.That(overlapping.Succeeded, Is.False);
            Assert.That(overlapping.Failure, Is.EqualTo(BuildFailure.Blocked));
            Assert.That(blocked, Is.Null);
            Assert.That(inventory.GetQuantity("raw_stone"), Is.EqualTo(14), "A rejected placement must not charge materials.");
            Assert.That(service.Sites.Count, Is.EqualTo(1));
        }

        [Test]
        public void Place_RejectsWhatTheBackpackCannotPayFor()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_stone, 3);
            var service = new BuildingService(inventory, new BuildGrid());

            BuildResult result = service.TryPlace(_wall, new BuildFootprint(Vector2Int.zero, Vector2Int.one),
                out BuildSite site);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure, Is.EqualTo(BuildFailure.MissingResources));
            Assert.That(site, Is.Null);
            Assert.That(service.Grid.OccupiedCellCount, Is.Zero);
        }

        [Test]
        public void Advance_FinishesTheSiteWhenTheTimerRunsOut()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_stone, 4);
            var service = new BuildingService(inventory, new BuildGrid());
            service.TryPlace(_wall, new BuildFootprint(Vector2Int.zero, Vector2Int.one), out BuildSite site);
            BuildSite completed = null;
            service.SiteCompleted += finished => completed = finished;

            service.Advance(WallSeconds * .5f);
            Assert.That(site.State, Is.EqualTo(BuildState.UnderConstruction));
            Assert.That(site.Progress, Is.EqualTo(.5f).Within(.001f));

            service.Advance(WallSeconds * .5f);

            Assert.That(site.State, Is.EqualTo(BuildState.Completed));
            Assert.That(site.Progress, Is.EqualTo(1f).Within(.001f));
            Assert.That(completed, Is.SameAs(site));
            Assert.That(service.Grid.OccupiedCellCount, Is.EqualTo(1), "A finished building keeps its cells.");
        }

        [Test]
        public void Cancel_RefundsAnUnfinishedSiteAndFreesItsCells()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_stone, 4);
            var service = new BuildingService(inventory, new BuildGrid());
            service.TryPlace(_wall, new BuildFootprint(Vector2Int.zero, Vector2Int.one), out BuildSite site);
            BuildSite removed = null;
            service.SiteRemoved += dropped => removed = dropped;

            BuildResult result = service.Cancel(site);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(inventory.GetQuantity("raw_stone"), Is.EqualTo(4));
            Assert.That(service.Sites.Count, Is.Zero);
            Assert.That(service.Grid.OccupiedCellCount, Is.Zero);
            Assert.That(removed, Is.SameAs(site));
        }

        [Test]
        public void Cancel_RefusesAFinishedBuilding()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_stone, 4);
            var service = new BuildingService(inventory, new BuildGrid());
            service.TryPlace(_wall, new BuildFootprint(Vector2Int.zero, Vector2Int.one), out BuildSite site);
            service.Advance(WallSeconds);

            BuildResult result = service.Cancel(site);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure, Is.EqualTo(BuildFailure.NotUnderConstruction));
            Assert.That(inventory.GetQuantity("raw_stone"), Is.Zero);
            Assert.That(service.Sites.Count, Is.EqualTo(1));
        }

        [Test]
        public void Clear_EmptiesTheWorldAndTheGrid()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_stone, 8);
            var service = new BuildingService(inventory, new BuildGrid());
            service.TryPlace(_wall, new BuildFootprint(Vector2Int.zero, Vector2Int.one), out BuildSite _);
            service.TryPlace(_wall, new BuildFootprint(new Vector2Int(3, 3), Vector2Int.one), out BuildSite _);

            service.Clear();

            Assert.That(service.Sites.Count, Is.Zero);
            Assert.That(service.Grid.OccupiedCellCount, Is.Zero);
            Assert.That(service.CanAfford(_wall), Is.False, "Clearing refunds nothing.");
        }

        private ItemDefinition CreateItem(string itemId, string displayName)
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            item.name = displayName;
            item.Configure(itemId, displayName, 1, 20, false, true);
            _createdAssets.Add(item);
            return item;
        }

        private BuildableDefinition CreateBuildable(string buildableId, string displayName, Vector2Int footprint,
            float buildSeconds, params CraftingItemAmount[] cost)
        {
            var buildable = ScriptableObject.CreateInstance<BuildableDefinition>();
            buildable.name = displayName;
            buildable.Configure(buildableId, displayName, footprint, buildSeconds, cost);
            _createdAssets.Add(buildable);
            return buildable;
        }
    }
}
