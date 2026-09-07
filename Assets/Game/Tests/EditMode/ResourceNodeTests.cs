using NUnit.Framework;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.Gathering.Runtime;
using PlanetSurvival.Inventory.Application;
using PlanetSurvival.Items.Definitions;
using PlanetSurvival.Player.Interaction;
using PlanetSurvival.Player.Stats;
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
    }
}
