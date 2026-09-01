using UnityEngine;

namespace PlanetSurvival.UI
{
    /// <summary>Shared translucent panel plate so every HUD surface reads as one system.</summary>
    public static class PanelBackground
    {
        private static readonly Color PlateColor = new(0.05f, 0.07f, 0.09f, 0.72f);
        private const float CornerRadius = 10f;

        public static void Draw(Rect area)
        {
            Draw(area, PlateColor);
        }

        public static void Draw(Rect area, Color color)
        {
            GUI.DrawTexture(area, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color,
                Vector4.zero, new Vector4(CornerRadius, CornerRadius, CornerRadius, CornerRadius));
        }
    }
}
