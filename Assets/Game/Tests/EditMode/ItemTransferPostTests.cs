using NUnit.Framework;
using PlanetSurvival.Building.Application;
using PlanetSurvival.Building.Definitions;
using PlanetSurvival.Building.Domain;
using PlanetSurvival.Crafting.Definitions;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Transport.Domain;
using PlanetSurvival.Transport.Runtime;
using PlanetSurvival.UI.Transport;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    using InventoryModel = PlanetSurvival.Inventory.Domain.Inventory;

    public sealed class ItemTransferPostTests
    {
        private ItemDefinition _alloy;
        private BuildableDefinition _postDefinition;

        [SetUp]
        public void SetUp()
        {
            _alloy = ScriptableObject.CreateInstance<ItemDefinition>();
            _alloy.Configure("alloy", "Alloy", 1, 20, false, true);
            _postDefinition = ScriptableObject.CreateInstance<BuildableDefinition>();
            _postDefinition.Configure("transfer_post", "Transfer Post", Vector2Int.one, 0f,
                new CraftingItemAmount(_alloy, 2));
            _postDefinition.ConfigureItemTransferPost(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_postDefinition);
            Object.DestroyImmediate(_alloy);
        }

        [Test]
        public void QuarterGrid_AllowsFourPostsInOneBuildCell_AndBlocksAFullBuilding()
        {
            var grid = new BuildGrid();
            for (int z = 0; z < 2; z++)
            {
                for (int x = 0; x < 2; x++)
                {
                    Assert.That(grid.TryOccupyQuarterCell(new Vector2Int(x, z), new object()), Is.True);
                }
            }

            Assert.That(grid.OccupiedQuarterCellCount, Is.EqualTo(4));
            Assert.That(grid.IsFree(new BuildFootprint(Vector2Int.zero, Vector2Int.one)), Is.False);
        }

        [Test]
        public void FullBuilding_BlocksAllFourQuarterCells()
        {
            var grid = new BuildGrid();
            Assert.That(grid.TryOccupy(new BuildFootprint(Vector2Int.zero, Vector2Int.one), new object()), Is.True);

            Assert.That(grid.IsQuarterCellFree(new Vector2Int(0, 0)), Is.False);
            Assert.That(grid.IsQuarterCellFree(new Vector2Int(1, 0)), Is.False);
            Assert.That(grid.IsQuarterCellFree(new Vector2Int(0, 1)), Is.False);
            Assert.That(grid.IsQuarterCellFree(new Vector2Int(1, 1)), Is.False);
        }

        [Test]
        public void PlaceTransferPost_UsesQuarterCellAndOwnsTransferState()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_alloy, 4);
            var service = new BuildingService(inventory, new BuildGrid());

            BuildResult result = service.TryPlaceTransferPost(
                _postDefinition, new Vector2Int(3, -2), out BuildSite site);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(site.QuarterCell, Is.EqualTo(new Vector2Int(3, -2)));
            Assert.That(site.ItemTransferPost, Is.Not.Null);
            Assert.That(service.Grid.GetQuarterCellOccupant(new Vector2Int(3, -2)), Is.SameAs(site));
            Assert.That(inventory.GetQuantity(_alloy.ItemId), Is.EqualTo(2));
        }

        [Test]
        public void TransferPosts_AreNumberedByPlacementOrder()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_alloy, 6);
            var service = new BuildingService(inventory, new BuildGrid());

            Assert.That(service.TryPlaceTransferPost(
                _postDefinition, Vector2Int.zero, out BuildSite first).Succeeded, Is.True);
            Assert.That(service.TryPlaceTransferPost(
                _postDefinition, Vector2Int.right, out BuildSite second).Succeeded, Is.True);
            Assert.That(service.TryPlaceTransferPost(
                _postDefinition, Vector2Int.up, out BuildSite third).Succeeded, Is.True);

            Assert.That(first.ItemTransferPost.PostNumber, Is.EqualTo(1));
            Assert.That(second.ItemTransferPost.PostNumber, Is.EqualTo(2));
            Assert.That(third.ItemTransferPost.PostNumber, Is.EqualTo(3));
        }

        [Test]
        public void Buffer_RejectsMixedItemsAndNeverExceedsCapacity()
        {
            ItemDefinition ore = ScriptableObject.CreateInstance<ItemDefinition>();
            ore.Configure("ore", "Ore", 1, 20, false, true);
            try
            {
                var post = new ItemTransferPost(4, Color.cyan);

                Assert.That(post.Insert(_alloy, ItemTransferPost.BufferCapacity + 4),
                    Is.EqualTo(ItemTransferPost.BufferCapacity));
                Assert.That(post.AcceptableQuantity(ore, 1), Is.Zero);
                Assert.That(post.BufferedQuantity, Is.EqualTo(ItemTransferPost.BufferCapacity));
                Assert.That(post.Extract(3), Is.EqualTo(3));
                Assert.That(post.BufferedQuantity, Is.EqualTo(ItemTransferPost.BufferCapacity - 3));
            }
            finally
            {
                Object.DestroyImmediate(ore);
            }
        }

        [Test]
        public void ConfigureGroup_ForcesOpaqueColorAndPublishesNumber()
        {
            var post = new ItemTransferPost(1, Color.white);

            post.ConfigureGroup(12, new Color(.2f, .4f, .8f, .1f));

            Assert.That(post.GroupNumber, Is.EqualTo(12));
            Assert.That(post.GroupColor.a, Is.EqualTo(1f));
        }

        [Test]
        public void RightClickSelectionFallback_OpensPanelForSmallPostWithoutColliderHit()
        {
            var inventory = new InventoryModel(30, 20);
            inventory.Add(_alloy, 2);
            var service = new BuildingService(inventory, new BuildGrid());
            Assert.That(service.TryPlaceTransferPost(
                _postDefinition, Vector2Int.zero, out BuildSite site).Succeeded, Is.True);

            var postObject = new GameObject("Transfer Post");
            var bodyObject = new GameObject("Body");
            var cameraObject = new GameObject("Camera");
            var viewObject = new GameObject("Transfer UI");
            var systemObject = new GameObject("Transfer System");
            var playerObject = new GameObject("Player");
            try
            {
                postObject.transform.position = service.Grid.QuarterCellCenter(Vector2Int.zero);
                bodyObject.transform.SetParent(postObject.transform, false);
                postObject.AddComponent<BoxCollider>();
                ItemTransferPostStation station = postObject.AddComponent<ItemTransferPostStation>();
                station.Bind(site, bodyObject.transform);
                postObject.GetComponent<Collider>().enabled = false;

                Camera camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.transform.position = new Vector3(postObject.transform.position.x, 10f,
                    postObject.transform.position.z);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                ItemTransferPostView view = viewObject.AddComponent<ItemTransferPostView>();
                ItemTransferSystem system = systemObject.AddComponent<ItemTransferSystem>();
                system.Bind(service, null, view, playerObject.transform, null, camera);
                system.Register(station);

                Vector3 screenPosition = camera.WorldToScreenPoint(postObject.transform.position);
                Assert.That(system.TryOpenPostAtScreenPosition(screenPosition), Is.True);
                Assert.That(view.IsOpen, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(systemObject);
                Object.DestroyImmediate(viewObject);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(playerObject);
                Object.DestroyImmediate(postObject);
            }
        }
    }
}
