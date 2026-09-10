using NUnit.Framework;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Gathering.Runtime;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Stats;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class ResourceNodeTests
    {
        private GameObject _actor;
        private GameObject _nodeObject;
        private ItemDefinition _item;
        private ResourceNodeDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _item = ScriptableObject.CreateInstance<ItemDefinition>();
            _item.Configure("test_resource", "Test Resource", 1, 20, false, true);
            _definition = ScriptableObject.CreateInstance<ResourceNodeDefinition>();
            _definition.Configure("test_node", "Test Node", 1f, 2f, string.Empty,
                Vector3.one, new ResourceYield(_item, 2));

            _actor = new GameObject("Gatherer");
            _actor.AddComponent<PlayerSurvival>();
            _actor.AddComponent<PlayerInventory>();
            _nodeObject = new GameObject("Node");
            _nodeObject.AddComponent<BoxCollider>();
            _nodeObject.AddComponent<ResourceNode>().Configure(_definition);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_nodeObject);
            Object.DestroyImmediate(_actor);
            Object.DestroyImmediate(_definition);
            Object.DestroyImmediate(_item);
        }

        [Test]
        public void Advance_CompletesGathering_AddsYieldAndDepletesNode()
        {
            ResourceNode node = _nodeObject.GetComponent<ResourceNode>();
            PlayerInventory inventory = _actor.GetComponent<PlayerInventory>();
            var context = new InteractionContext(_actor, _actor.GetComponent<PlayerSurvival>(), inventory);

            node.Interact(context);
            node.Advance(1f);

            Assert.That(inventory.Inventory.Stacks[0].Quantity, Is.EqualTo(2));
            Assert.That(node.IsDepleted, Is.True);
            Assert.That(_nodeObject.GetComponent<Collider>().enabled, Is.False);
        }

        [Test]
        public void Advance_WhenGathererLeavesRange_CancelsWithoutYield()
        {
            ResourceNode node = _nodeObject.GetComponent<ResourceNode>();
            PlayerInventory inventory = _actor.GetComponent<PlayerInventory>();
            var context = new InteractionContext(_actor, _actor.GetComponent<PlayerSurvival>(), inventory);

            node.Interact(context);
            _actor.transform.position = Vector3.right * 3f;
            node.Advance(.5f);

            Assert.That(node.IsGathering, Is.False);
            Assert.That(node.IsDepleted, Is.False);
            Assert.That(inventory.Inventory.Stacks, Is.Empty);
        }

        [Test]
        public void Advance_WhenActorIsNearColliderButFarFromNodeCenter_DoesNotCancel()
        {
            ResourceNode node = _nodeObject.GetComponent<ResourceNode>();
            PlayerInventory inventory = _actor.GetComponent<PlayerInventory>();
            var context = new InteractionContext(_actor, _actor.GetComponent<PlayerSurvival>(), inventory);
            _nodeObject.transform.localScale = new Vector3(4f, 1f, 1f);
            _actor.transform.position = new Vector3(2.4f, 0f, 0f);
            Physics.SyncTransforms();

            node.Interact(context);
            node.Advance(.5f);

            Assert.That(node.IsGathering, Is.True);
            Assert.That(node.IsDepleted, Is.False);
        }

        /// <summary>
        /// A node that lies flat on the ground must not be a wall, but it still needs its volume: the
        /// interactor's overlap query and the gathering distance both measure against the collider.
        /// </summary>
        [Test]
        public void Create_WalkableResource_KeepsItsVolumeAsATriggerInsteadOfBlockingTheWay()
        {
            _definition.ConfigureCollision(false);
            var parent = new GameObject("Chunk");

            ResourceNode node = ResourceNodeFactory.Create(
                parent.transform, _definition, Vector3.zero, null);

            var collider = node.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.isTrigger, Is.True, "A walkable node must not block the character controller.");
            Assert.That(collider.enabled, Is.True, "The volume still carries the interaction.");
            Object.DestroyImmediate(parent);
        }

        [Test]
        public void Create_GroundResource_LiesFlatAndDoesNotCastABlobShadow()
        {
            _definition.ConfigurePresentation(ResourceVisualMode.GroundDecal);
            _definition.ConfigureBlobShadow(false);
            var parent = new GameObject("Chunk");

            ResourceNode node = ResourceNodeFactory.Create(
                parent.transform, _definition, Vector3.zero, null);

            GroundDecalView view = node.GetComponentInChildren<GroundDecalView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.transform.localEulerAngles.x, Is.EqualTo(90f).Within(.01f));
            Assert.That(view.Renderer.sortingOrder, Is.EqualTo(GroundDecalView.SortingOrder));
            Assert.That(node.GetComponentInChildren<WorldSpriteView>(), Is.Null,
                "A floor surface must not participate in billboard depth sorting.");
            Assert.That(node.transform.Find("Blob Shadow"), Is.Null,
                "A surface already touching the ground must not receive a floating-object shadow.");
            Object.DestroyImmediate(parent);
        }

        [Test]
        public void Create_SolidResource_StaysAnObstacle()
        {
            _definition.ConfigureCollision(true);
            var parent = new GameObject("Chunk");

            ResourceNode node = ResourceNodeFactory.Create(
                parent.transform, _definition, Vector3.zero, null);

            Assert.That(node.GetComponent<BoxCollider>().isTrigger, Is.False);
            Object.DestroyImmediate(parent);
        }
    }
}
