using UnityEngine;

namespace PlanetSurvival.World.Presentation
{
    /// <summary>
    /// The stand-in artwork used wherever a piece of content is playable before an artist has drawn it.
    /// A tinted block reads as provisional at a glance, which is the point: content missing its art must
    /// still be findable and usable, but must never be mistaken for finished.
    /// </summary>
    public static class PlaceholderArt
    {
        private const int TextureSize = 4;

        private static Sprite _solidSprite;

        /// <summary>An opaque white sprite. Tint the renderer to tell one placeholder from another.</summary>
        public static Sprite SolidSprite()
        {
            if (_solidSprite != null)
            {
                return _solidSprite;
            }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "PlaceholderBlock",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[TextureSize * TextureSize];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _solidSprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(.5f, .5f), TextureSize);
            _solidSprite.name = texture.name;
            return _solidSprite;
        }
    }
}
