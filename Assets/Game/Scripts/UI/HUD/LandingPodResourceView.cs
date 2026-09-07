using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.Water.Domain;
using UnityEngine;

namespace PlanetSurvival.UI.HUD
{
    [DisallowMultipleComponent]
    public sealed class LandingPodResourceView : MonoBehaviour
    {
        private const string OxygenTextureResource = "Oxygen/Oxygen";
        private const string WaterTextureResource = "Water/WaterDrop";
        private const float PanelX = 18f;
        private const float PanelY = 92f;
        private const float PanelWidth = 270f;
        private const float PanelHeight = 126f;
        private OxygenReservoir _oxygen;
        private LiquidContainer _water;
        private Texture2D _oxygenTexture;
        private Texture2D _waterTexture;
        private GUIStyle _headerStyle;
        private GUIStyle _valueStyle;

        private void Awake()
        {
            _oxygenTexture = Resources.Load<Texture2D>(OxygenTextureResource);
            _waterTexture = Resources.Load<Texture2D>(WaterTextureResource);
        }

        public void Bind(OxygenReservoir oxygen, LiquidContainer water)
        {
            _oxygen = oxygen;
            _water = water;
            if (_oxygen != null && _water != null)
            {
                return;
            }

            Debug.LogError($"{nameof(LandingPodResourceView)} on '{name}' requires oxygen and water reserves.", this);
            enabled = false;
        }

        private void OnGUI()
        {
            if (_oxygen == null || _water == null)
            {
                return;
            }

            EnsureStyles();
            var panel = new Rect(PanelX, PanelY, PanelWidth, PanelHeight);
            PanelBackground.Draw(panel);
            GUI.Label(new Rect(panel.x + 14f, panel.y + 8f, panel.width - 28f, 22f),
                "LANDING POD LIFE SUPPORT", _headerStyle);
            DrawResource(panel.y + 36f, _oxygenTexture, _oxygen.CurrentLiters, _oxygen.CapacityLiters,
                "L", new Color(.38f, .88f, 1f));
            DrawResource(panel.y + 78f, _waterTexture, _water.CurrentMilliliters / 1000f,
                _water.CapacityMilliliters / 1000f, "L", new Color(.3f, .62f, 1f));
        }

        private void DrawResource(float y, Texture2D icon, float current, float capacity, string unit, Color color)
        {
            const float padding = 14f;
            const float iconSize = 34f;
            if (icon != null)
            {
                GUI.DrawTexture(new Rect(PanelX + padding, y, iconSize, iconSize), icon,
                    ScaleMode.ScaleToFit, true);
            }

            var valueRect = new Rect(PanelX + padding + iconSize + 8f, y,
                PanelWidth - padding * 2f - iconSize - 8f, 18f);
            GUI.Label(valueRect, $"{current:0.0} / {capacity:0.0} {unit}", _valueStyle);

            var track = new Rect(valueRect.x, y + 23f, valueRect.width, 8f);
            GUI.DrawTexture(track, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                new Color(1f, 1f, 1f, .12f), Vector4.zero, new Vector4(4f, 4f, 4f, 4f));
            float normalized = capacity <= 0f ? 0f : Mathf.Clamp01(current / capacity);
            if (normalized > 0f)
            {
                var fill = new Rect(track.x, track.y, track.width * normalized, track.height);
                GUI.DrawTexture(fill, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                    color, Vector4.zero, new Vector4(4f, 4f, 4f, 4f));
            }
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null)
            {
                return;
            }

            _headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            _headerStyle.normal.textColor = new Color(.75f, .92f, 1f);
            _valueStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleRight };
            _valueStyle.normal.textColor = new Color(.75f, .82f, .87f);
        }
    }
}
