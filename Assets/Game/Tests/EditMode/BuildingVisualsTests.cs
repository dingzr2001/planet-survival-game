using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Building.Application;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Building.Runtime;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    /// <summary>
    /// How large a structure is drawn. Artwork is authored at whatever resolution suits it, so the
    /// footprint — not the pixel size or an authored height — has to decide what a building covers.
    /// </summary>
    public sealed class BuildingVisualsTests
    {
        private const float CellSize = 2.75f;

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
        public void Body_FillsTheCellItOccupies()
        {
            BuildableDefinition drill = CreateBuildable(Vector2Int.one, CreateSprite(48, 64));
            var parent = Track(new GameObject("Site"));

            Transform body = BuildingVisuals.CreateBody(parent.transform, drill, CellSize);

            Assert.That(WorldWidth(body), Is.EqualTo(CellSize).Within(.001f),
                "A one-cell structure has to span its whole cell of ground.");
        }

        [Test]
        public void Body_GrowsWithTheFootprintRatherThanTheArtwork()
        {
            BuildableDefinition oven = CreateBuildable(new Vector2Int(2, 2), CreateSprite(16, 16));
            var parent = Track(new GameObject("Site"));

            Transform body = BuildingVisuals.CreateBody(parent.transform, oven, CellSize);

            Assert.That(WorldWidth(body), Is.EqualTo(2f * CellSize).Within(.001f));
        }

        [Test]
        public void Body_KeepsTheAspectRatioOfItsArtwork()
        {
            // Twice as tall as it is wide, so filling one cell must leave it two cells tall.
            BuildableDefinition tower = CreateBuildable(Vector2Int.one, CreateSprite(32, 64));
            var parent = Track(new GameObject("Site"));

            Transform body = BuildingVisuals.CreateBody(parent.transform, tower, CellSize);

            Assert.That(BuildingVisuals.BodyHeight(body, 1f), Is.EqualTo(2f * CellSize).Within(.001f));
        }

        [Test]
        public void Body_TouchesTheGroundAtTheNearEdgeOfItsFootprint()
        {
            BuildableDefinition crate = CreateBuildable(Vector2Int.one, CreateSprite(64, 64));
            var parent = Track(new GameObject("Site"));

            Transform body = BuildingVisuals.CreateBody(parent.transform, crate, CellSize);

            // The camera looks along +Z, so the cells a structure occupies run away from it: art anchored
            // at the centre of the footprint is drawn half a cell up the screen from the cell it stands on.
            Assert.That(body.localPosition.z, Is.EqualTo(-CellSize * .5f).Within(.001f));
            Assert.That(body.localPosition.x, Is.EqualTo(0f).Within(.001f));
        }

        [Test]
        public void Body_AnchorsDeepFootprintsByTheirWholeDepth()
        {
            BuildableDefinition oven = CreateBuildable(new Vector2Int(2, 2), CreateSprite(64, 64));
            var parent = Track(new GameObject("Site"));

            Transform body = BuildingVisuals.CreateBody(parent.transform, oven, CellSize);

            Assert.That(body.localPosition.z, Is.EqualTo(-CellSize).Within(.001f));
        }

        [Test]
        public void PlaceholderBlock_AlsoFillsTheCell()
        {
            BuildableDefinition unfinished = CreateBuildable(Vector2Int.one, null);
            var parent = Track(new GameObject("Site"));

            Transform body = BuildingVisuals.CreateBody(parent.transform, unfinished, CellSize);

            Assert.That(WorldWidth(body), Is.EqualTo(CellSize).Within(.001f),
                "A buildable still waiting for its artwork has to read at the size it will ship at.");
        }

        [Test]
        public void FootprintOutline_OnlyMarksCellsAStructureHasNotFilledYet()
        {
            ItemDefinition stone = Track(ScriptableObject.CreateInstance<ItemDefinition>());
            stone.Configure("raw_stone", "Raw Stone", 1, 20, false, true);
            BuildableDefinition crate = CreateBuildable(Vector2Int.one, CreateSprite(64, 64));
            crate.Configure("crate", "Crate", Vector2Int.one, 10f, new CraftingItemAmount(stone, 1));
            var inventory = new InventoryModel(30, 20);
            inventory.Add(stone, 1);
            var service = new BuildingService(inventory, new BuildGrid(CellSize));
            service.TryPlace(crate, new BuildFootprint(Vector2Int.zero, Vector2Int.one), out BuildSite site);
            var siteObject = Track(new GameObject("Site"));
            BuildSiteView view = siteObject.AddComponent<BuildSiteView>();

            view.Bind(site, service, CellSize, null);
            SpriteRenderer outline = siteObject.transform.Find("Footprint").GetComponent<SpriteRenderer>();
            Assert.That(outline.enabled, Is.True, "An unfinished site has to show which cells it has claimed.");

            service.Advance(site.TotalSeconds);

            Assert.That(outline.enabled, Is.False,
                "A finished structure draws its own cells; the outline would frame it in white.");
        }

        private static float WorldWidth(Transform body)
        {
            WorldSpriteView view = body.GetComponentInChildren<WorldSpriteView>(true);
            return view.Renderer.sprite.bounds.size.x * view.transform.lossyScale.x;
        }

        private BuildableDefinition CreateBuildable(Vector2Int footprint, Sprite worldSprite)
        {
            var buildable = Track(ScriptableObject.CreateInstance<BuildableDefinition>());
            buildable.Configure("test_buildable", "Test Buildable", footprint, 1f);
            buildable.ConfigurePresentation(worldSprite, 1.2f, Color.white);
            return buildable;
        }

        private Sprite CreateSprite(int width, int height)
        {
            var texture = Track(new Texture2D(width, height));
            Sprite sprite = Track(Sprite.Create(texture,
                new Rect(0f, 0f, width, height), new Vector2(.5f, 0f), width));
            return sprite;
        }

        private T Track<T>(T target) where T : Object
        {
            _created.Add(target);
            return target;
        }
    }
}
