using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.World.Generation;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Gathering.Runtime
{
    /// <summary>Builds the runtime object of a single resource node, independent of how it was placed.</summary>
    public static class ResourceNodeFactory
    {
        /// <summary>Tint of a node whose cutout artwork is still missing.</summary>
        private static readonly Color PlaceholderNodeColor = new(.72f, .82f, .88f);


        /// <param name="variantSeed">
        /// Chooses the cutout when the resource has several. It comes from the chunk plan, so the same
        /// node always looks the same.
        /// </param>
        public static ResourceNode Create(Transform parent, ResourceNodeDefinition definition, Vector3 position,
            WorldVisualSettings visuals, int variantSeed = 0)
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
            // A walkable node keeps its volume as a trigger: the interactor's overlap query includes
            // triggers, so gathering still works while the player walks straight over the node.
            collider.isTrigger = !definition.BlocksMovement;
            ResourceNode resourceNode = node.AddComponent<ResourceNode>();
            resourceNode.Configure(definition);

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(node.transform, false);
            visual.AddComponent<SpriteRenderer>();
            WorldSpriteView spriteView = visual.AddComponent<WorldSpriteView>();
            float displayHeight = Mathf.Max(.5f, definition.DisplayScale.y);
            Sprite cutout = definition.SelectWorldSprite(variantSeed);
            Sprite displayedSprite = cutout != null ? cutout : PlaceholderArt.SolidSprite();
            if (definition.LiesFlatOnGround)
            {
                spriteView.ConfigureGroundPlane(displayedSprite,
                    new Vector2(definition.DisplayScale.x, definition.DisplayScale.z));
            }
            else
            {
                spriteView.Configure(displayedSprite, displayHeight);
            }
            if (cutout == null)
            {
                // Without a cutout the node would be invisible and only its shadow would hint at it, so
                // it is drawn as a footprint-wide block until its artwork exists.
                spriteView.Renderer.color = PlaceholderNodeColor;
                visual.transform.localScale = new Vector3(
                    Mathf.Max(.2f, definition.DisplayScale.x) / displayHeight * visual.transform.localScale.x,
                    visual.transform.localScale.y,
                    visual.transform.localScale.z);
            }

            if (!definition.LiesFlatOnGround)
            {
                Color shadowColor = visuals != null ? visuals.ShadowColor : new Color(0f, 0f, 0f, .4f);
                BlobShadow.Create(node.transform,
                    new Vector2(collider.size.x * 1.1f, collider.size.z * .75f),
                    shadowColor);
            }
            return resourceNode;
        }
    }
}
