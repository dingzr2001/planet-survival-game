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

            Vector2 worldFootprint = definition.SelectWorldFootprint(variantSeed);
            var collider = node.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                worldFootprint.x,
                Mathf.Max(.3f, definition.DisplayScale.y),
                worldFootprint.y);
            collider.center = Vector3.up * collider.size.y * .5f;
            // A walkable node keeps its volume as a trigger: the interactor's overlap query includes
            // triggers, so gathering still works while the player walks straight over the node.
            collider.isTrigger = !definition.BlocksMovement;
            ResourceNode resourceNode = node.AddComponent<ResourceNode>();
            resourceNode.Configure(definition);

            float displayHeight = Mathf.Max(.5f, definition.DisplayScale.y);
            Sprite cutout = definition.SelectWorldSprite(variantSeed);
            Sprite displayedSprite = cutout != null ? cutout : PlaceholderArt.SolidSprite();
            if (definition.VisualMode == ResourceVisualMode.GroundDecal)
            {
                CreateGroundPatch(node.transform, displayedSprite, cutout == null, worldFootprint, variantSeed);
            }
            else
            {
                var visual = new GameObject("Sprite");
                visual.transform.SetParent(node.transform, false);
                SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
                WorldSpriteView spriteView = visual.AddComponent<WorldSpriteView>();
                spriteView.Configure(displayedSprite, displayHeight);
                if (cutout == null)
                {
                    renderer.color = PlaceholderNodeColor;
                    spriteView.transform.localScale = new Vector3(
                        Mathf.Max(.2f, definition.DisplayScale.x) / displayHeight * spriteView.transform.localScale.x,
                        spriteView.transform.localScale.y,
                        spriteView.transform.localScale.z);
                }
            }

            if (definition.VisualMode == ResourceVisualMode.Billboard && definition.CastsBlobShadow)
            {
                Color shadowColor = visuals != null ? visuals.ShadowColor : new Color(0f, 0f, 0f, .4f);
                BlobShadow.Create(node.transform,
                    new Vector2(collider.size.x * 1.1f, collider.size.z * .75f),
                    shadowColor);
            }
            return resourceNode;
        }

        private static void CreateGroundPatch(Transform parent, Sprite sprite, bool isPlaceholder,
            Vector2 footprint, int variantSeed)
        {
            var visual = new GameObject("Ground Decal");
            visual.transform.SetParent(parent, false);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            GroundDecalView decal = visual.AddComponent<GroundDecalView>();
            decal.Configure(sprite, footprint);
            // Half turns add variation without swapping the physical axes of a rectangular deposit.
            decal.transform.Rotate(Vector3.forward, StableHalfTurn(variantSeed), Space.Self);
            if (isPlaceholder)
            {
                renderer.color = PlaceholderNodeColor;
            }
        }

        private static float StableHalfTurn(int variantSeed)
        {
            return (variantSeed & 1) * 180f;
        }
    }
}
