using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.World.Generation;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Gathering.Runtime
{
    /// <summary>Builds the runtime object of a single resource node, independent of how it was placed.</summary>
    public static class ResourceNodeFactory
    {
        public static ResourceNode Create(Transform parent, ResourceNodeDefinition definition, Vector3 position,
            WorldVisualSettings visuals)
        {
            if (definition == null)
            {
                throw new System.ArgumentNullException(nameof(definition));
            }

            var node = new GameObject();
            node.name = $"Resource - {definition.DisplayName}";
            node.transform.SetParent(parent);
            node.transform.position = position;

            var collider = node.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                Mathf.Max(.2f, definition.DisplayScale.x),
                Mathf.Max(.3f, definition.DisplayScale.y),
                Mathf.Max(.2f, definition.DisplayScale.z));
            collider.center = Vector3.up * collider.size.y * .5f;
            ResourceNode resourceNode = node.AddComponent<ResourceNode>();
            resourceNode.Configure(definition);

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(node.transform, false);
            visual.AddComponent<SpriteRenderer>();
            visual.AddComponent<WorldSpriteView>().Configure(
                definition.WorldSprite,
                Mathf.Max(.5f, definition.DisplayScale.y));

            Color shadowColor = visuals != null ? visuals.ShadowColor : new Color(0f, 0f, 0f, .4f);
            BlobShadow.Create(node.transform,
                new Vector2(collider.size.x * 1.1f, collider.size.z * .75f),
                shadowColor);
            return resourceNode;
        }
    }
}
