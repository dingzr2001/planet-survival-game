using UnityEngine;

namespace PlanetSurvival.UI
{
    /// <summary>
    /// Shared IMGUI item-icon drawing, so every panel renders a sprite through its texture rectangle
    /// and keeps the aspect ratio of packed or trimmed art.
    /// </summary>
    public static class SpriteIcon
    {
        public static void Draw(Rect area, Sprite icon)
        {
            if (icon == null)
            {
                return;
            }

            Texture2D texture = icon.texture;
            if (texture == null)
            {
                return;
            }

            Rect textureRect = icon.textureRect;
            var coordinates = new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height);
            GUI.DrawTextureWithTexCoords(
                FitAspect(area, textureRect.width / textureRect.height), texture, coordinates, true);
        }

        public static Rect FitAspect(Rect area, float aspect)
        {
            if (aspect <= 0f)
            {
                return area;
            }

            float width = area.width;
            float height = width / aspect;
            if (height > area.height)
            {
                height = area.height;
                width = height * aspect;
            }

            return new Rect(area.x + (area.width - width) * 0.5f, area.y + (area.height - height) * 0.5f, width, height);
        }
    }
}
