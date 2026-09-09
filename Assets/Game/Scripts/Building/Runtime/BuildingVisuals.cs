using PlanetSurvival.Building.Definitions;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Building.Runtime
{
    /// <summary>
    /// Shared presentation of anything that occupies build cells: the body of a structure and the
    /// footprint patch drawn on the ground. Structures without artwork fall back to a tinted block, so a
    /// new buildable is playable before its sprite exists.
    /// </summary>
    public static class BuildingVisuals
    {
        private const int TextureSize = 32;
        private const int BorderThickness = 2;
        private const float GroundOffset = .015f;

        private static Sprite _blockSprite;
        private static Sprite _footprintSprite;

        /// <summary>
        /// Builds the visible body under <paramref name="parent"/>. The returned transform is scaled, not
        /// the sprite itself, so callers can grow a building out of the ground while it is under construction.
        /// </summary>
        public static Transform CreateBody(Transform parent, BuildableDefinition buildable, float cellSize)
        {
            var body = new GameObject("Body");
            body.transform.SetParent(parent, false);

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(body.transform, false);
            visual.AddComponent<SpriteRenderer>();
            WorldSpriteView view = visual.AddComponent<WorldSpriteView>();

            if (buildable.WorldSprite != null)
            {
                view.ConfigureGrounded(buildable.WorldSprite, buildable.WorldHeight);
            }
            else
            {
                // The placeholder block is as wide as the footprint so the silhouette still reads as a 2×2 oven.
                float width = Mathf.Max(buildable.Footprint.x, buildable.Footprint.y) * cellSize;
                view.Configure(BlockSprite(), buildable.WorldHeight);
                visual.transform.localScale = new Vector3(
                    width / Mathf.Max(.01f, buildable.WorldHeight) * visual.transform.localScale.x,
                    visual.transform.localScale.y,
                    visual.transform.localScale.z);
                view.Renderer.color = buildable.BodyColor;
            }

            return body.transform;
        }

        /// <summary>The translucent patch that shows which cells a footprint covers.</summary>
        public static SpriteRenderer CreateFootprintPatch(Transform parent, Vector2Int footprint, float cellSize)
        {
            var patch = new GameObject("Footprint");
            patch.transform.SetParent(parent, false);
            patch.transform.localPosition = Vector3.up * GroundOffset;
            patch.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            patch.transform.localScale = new Vector3(
                Mathf.Max(1, footprint.x) * cellSize,
                Mathf.Max(1, footprint.y) * cellSize,
                1f);

            SpriteRenderer renderer = patch.AddComponent<SpriteRenderer>();
            renderer.sprite = FootprintSprite();
            renderer.sortingOrder = -16000;
            return renderer;
        }

        /// <summary>Tints every sprite of a body, used for the ghost and for unfinished sites.</summary>
        public static void Tint(Transform body, Color color)
        {
            if (body == null)
            {
                return;
            }

            SpriteRenderer[] renderers = body.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].color = color;
            }
        }

        private static Sprite BlockSprite()
        {
            if (_blockSprite == null)
            {
                _blockSprite = CreateSprite("BuildBlock", false);
            }

            return _blockSprite;
        }

        private static Sprite FootprintSprite()
        {
            if (_footprintSprite == null)
            {
                _footprintSprite = CreateSprite("BuildFootprint", true);
            }

            return _footprintSprite;
        }

        private static Sprite CreateSprite(string name, bool hollow)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    bool border = x < BorderThickness || y < BorderThickness ||
                                  x >= TextureSize - BorderThickness || y >= TextureSize - BorderThickness;
                    byte alpha = hollow ? (byte)(border ? 255 : 64) : (byte)255;
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture,
                new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(.5f, .5f), TextureSize);
            sprite.name = name;
            return sprite;
        }
    }
}
