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

            Vector2Int patchFootprint = definition.VisualMode == ResourceVisualMode.GroundDecal
                ? definition.SelectGroundPatchFootprint(variantSeed)
                : Vector2Int.one;
            var collider = node.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                Mathf.Max(.2f, definition.DisplayScale.x * patchFootprint.x),
                Mathf.Max(.3f, definition.DisplayScale.y),
                Mathf.Max(.2f, definition.DisplayScale.z * patchFootprint.y));
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
                CreateGroundPatch(node.transform, definition, displayedSprite, cutout == null, patchFootprint,
                    variantSeed);
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

        private static void CreateGroundPatch(Transform parent, ResourceNodeDefinition definition, Sprite sprite,
            bool isPlaceholder, Vector2Int footprint, int variantSeed)
        {
            float tileWidth = definition.DisplayScale.x;
            float tileDepth = definition.DisplayScale.z;
            float originX = (footprint.x - 1) * tileWidth * -.5f;
            float originZ = (footprint.y - 1) * tileDepth * -.5f;

            for (int x = 0; x < footprint.x; x++)
            {
                for (int z = 0; z < footprint.y; z++)
                {
                    var visual = new GameObject($"Ground Tile {x + 1},{z + 1}");
                    visual.transform.SetParent(parent, false);
                    visual.transform.localPosition = new Vector3(originX + x * tileWidth, 0f,
                        originZ + z * tileDepth);
                    SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
                    GroundDecalView decal = visual.AddComponent<GroundDecalView>();
                    decal.Configure(sprite, new Vector2(tileWidth * 1.08f, tileDepth * 1.08f));
                    // Quarter turns break up repeated edge details while keeping every streamed reload deterministic.
                    decal.transform.Rotate(Vector3.forward, StableQuarterTurn(variantSeed, x, z), Space.Self);
                    if (isPlaceholder)
                    {
                        renderer.color = PlaceholderNodeColor;
                    }
                }
            }
        }

        private static float StableQuarterTurn(int variantSeed, int x, int z)
        {
            unchecked
            {
                int hash = variantSeed;
                hash = hash * 397 ^ x;
                hash = hash * 397 ^ z;
                return (hash & 3) * 90f;
            }
        }
    }
}
