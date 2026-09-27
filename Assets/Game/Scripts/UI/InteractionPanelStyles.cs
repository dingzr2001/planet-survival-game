using UnityEngine;

namespace PlanetSurvival.UI
{
    /// <summary>
    /// The text styles every machine panel shares, built once per panel from its
    /// <see cref="InteractionPanelTheme"/>. Panels hold one instance and rebuild nothing while they are open.
    /// </summary>
    public sealed class InteractionPanelStyles
    {
        private InteractionPanelStyles(in InteractionPanelTheme theme)
        {
            Title = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            Title.normal.textColor = theme.Accent;

            Section = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            Section.normal.textColor = theme.Accent;

            Value = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            Value.normal.textColor = Color.white;

            Caption = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            Caption.normal.textColor = Color.white;

            // Vertically centred so a one-line readout sits level with the icons and bars beside it.
            Detail = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.MiddleLeft
            };
            Detail.normal.textColor = theme.Muted;

            Glyph = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            Glyph.normal.textColor = theme.Accent;
        }

        /// <summary>Panel name in the header.</summary>
        public GUIStyle Title { get; }

        /// <summary>Heading above a group of controls or readouts.</summary>
        public GUIStyle Section { get; }

        /// <summary>The one number or name a section exists to show.</summary>
        public GUIStyle Value { get; }

        /// <summary>Centred label under a slot or tile.</summary>
        public GUIStyle Caption { get; }

        /// <summary>Wrapping explanatory text, hints and action results.</summary>
        public GUIStyle Detail { get; }

        /// <summary>Stand-in mark where artwork is missing, such as a chemical formula.</summary>
        public GUIStyle Glyph { get; }

        /// <summary>
        /// Builds the styles for one theme. <see cref="GUI.skin"/> only exists during an IMGUI pass, so
        /// this must be called from <c>OnGUI</c> and never from a Unity lifecycle callback.
        /// </summary>
        public static InteractionPanelStyles Create(in InteractionPanelTheme theme) => new(theme);
    }
}
