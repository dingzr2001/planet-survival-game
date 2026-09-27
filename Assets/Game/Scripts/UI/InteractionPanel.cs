using UnityEngine;

namespace PlanetSurvival.UI
{
    /// <summary>
    /// The chrome every full-screen machine panel is built from: a dimmed world, a centred plate, a header
    /// that names the machine and reports its state, and a footer carrying the last action result. Panels
    /// draw their own body between <see cref="ContentTop"/> and <see cref="ContentBottom"/>, so they differ
    /// only where the machine differs.
    /// </summary>
    /// <remarks>
    /// Every method here draws immediately and must be called from <c>OnGUI</c>. The header and footer use
    /// absolute rectangles so a panel is free to lay its body out with either <c>GUI</c> or <c>GUILayout</c>.
    /// </remarks>
    public static class InteractionPanel
    {
        /// <summary>Distance from the header's top edge to the first row of body content.</summary>
        public const float HeaderHeight = 98f;

        /// <summary>Height reserved at the bottom for the feedback and hint lines.</summary>
        public const float FooterHeight = 72f;

        /// <summary>Horizontal padding between the plate edge and body content.</summary>
        public const float ContentPadding = 22f;

        private const float ScrimAlpha = .7f;
        private const float ScreenMargin = 24f;
        private const float IconSize = 32f;
        private const float CloseWidth = 68f;
        private const float CloseHeight = 28f;
        private const string EscapeHint = "ESC closes this panel.";

        /// <summary>
        /// Dims the world and lays out the centred plate, shrinking it to fit small screens. Call this first
        /// in <c>OnGUI</c>; the rectangle it returns anchors everything else.
        /// </summary>
        public static Rect Begin(float preferredWidth, float preferredHeight, in InteractionPanelTheme theme)
        {
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, ScrimAlpha));
            float width = Mathf.Min(preferredWidth, Screen.width - ScreenMargin);
            float height = Mathf.Min(preferredHeight, Screen.height - ScreenMargin);
            var panel = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            PanelBackground.Draw(panel, theme.Plate);
            return panel;
        }

        /// <summary>
        /// Draws the machine's icon, name, one-line state and close button.
        /// </summary>
        /// <param name="icon">The machine's menu artwork, or <c>null</c> to leave the title flush left.</param>
        /// <param name="state">Why the machine is or is not running, in the player's words.</param>
        /// <returns>True on the frame the close button was pressed.</returns>
        public static bool DrawHeader(Rect panel, Sprite icon, string title, string state,
            InteractionPanelStyles styles)
        {
            bool close = GUI.Button(
                new Rect(panel.xMax - CloseWidth - 20f, panel.y + 17f, CloseWidth, CloseHeight), "CLOSE");
            float titleX = panel.x + ContentPadding;
            if (icon != null)
            {
                SpriteIcon.Draw(new Rect(panel.x + 19f, panel.y + 15f, IconSize, IconSize), icon);
                titleX = panel.x + 60f;
            }

            GUI.Label(new Rect(titleX, panel.y + 16f, panel.xMax - CloseWidth - 32f - titleX, 28f),
                title, styles.Title);
            if (!string.IsNullOrEmpty(state))
            {
                GUI.Label(new Rect(panel.x + ContentPadding, panel.y + 51f, panel.width - ContentPadding * 2f, 38f),
                    state, styles.Detail);
            }

            return close;
        }

        /// <summary>
        /// Draws the result of the last action and a standing hint. Escape is always spelled out, so every
        /// panel advertises the same way out.
        /// </summary>
        public static void DrawFooter(Rect panel, string feedback, string hint, InteractionPanelStyles styles)
        {
            float width = panel.width - 48f;
            GUI.Label(new Rect(panel.x + 24f, panel.yMax - 58f, width, 24f), feedback ?? string.Empty,
                styles.Detail);
            GUI.Label(new Rect(panel.x + 24f, panel.yMax - 33f, width, 20f),
                string.IsNullOrEmpty(hint) ? EscapeHint : $"{hint}  ·  {EscapeHint}", styles.Detail);
        }

        /// <summary>First line of body content, below the header.</summary>
        public static float ContentTop(Rect panel) => panel.y + HeaderHeight;

        /// <summary>Last line of body content, above the footer.</summary>
        public static float ContentBottom(Rect panel) => panel.yMax - FooterHeight;

        /// <summary>The whole body area, ready to hand to <see cref="GUILayout.BeginArea(Rect)"/>.</summary>
        public static Rect ContentArea(Rect panel) => new(
            panel.x + ContentPadding,
            ContentTop(panel),
            panel.width - ContentPadding * 2f,
            ContentBottom(panel) - ContentTop(panel));

        /// <summary>A hairline that separates two body columns.</summary>
        public static void Divider(float x, float top, float bottom, in InteractionPanelTheme theme)
        {
            Color accent = theme.Accent;
            Fill(new Rect(x, top, 1f, bottom - top), new Color(accent.r, accent.g, accent.b, .35f));
        }

        /// <summary>Solid colour fill; the building block for meters, frames and the scrim.</summary>
        public static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        /// <summary>One-pixel outline around a sub-area such as a reaction chamber or a tile.</summary>
        public static void Frame(Rect rect, Color color)
        {
            Fill(new Rect(rect.x, rect.y, rect.width, 1f), color);
            Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
            Fill(new Rect(rect.x, rect.y, 1f, rect.height), color);
            Fill(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
        }

        /// <summary>
        /// Fills a level bar inside <paramref name="track"/>. A positive <paramref name="minimum"/> marks the
        /// level the machine needs before it will run.
        /// </summary>
        public static void Meter(Rect track, float value, float capacity, Color fill, float minimum = 0f)
        {
            GUI.DrawTexture(track, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f,
                new Color(.08f, .09f, .1f), Vector4.zero, new Vector4(4f, 4f, 4f, 4f));
            float normalized = capacity > 0f ? Mathf.Clamp01(value / capacity) : 0f;
            if (normalized > 0f)
            {
                GUI.DrawTexture(
                    new Rect(track.x + 2f, track.y + 2f, (track.width - 4f) * normalized, track.height - 4f),
                    Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, fill, Vector4.zero,
                    new Vector4(3f, 3f, 3f, 3f));
            }

            if (minimum > 0f && capacity > 0f)
            {
                float x = track.x + track.width * Mathf.Clamp01(minimum / capacity);
                Fill(new Rect(x - 1f, track.y, 2f, track.height), new Color(1f, 1f, 1f, .75f));
            }
        }

        /// <summary>
        /// A labelled level bar in the current <c>GUILayout</c> flow: optional icon, the value against its
        /// capacity, then the bar itself.
        /// </summary>
        public static void LayoutMeter(string label, float value, float capacity, string unit, Color fill,
            InteractionPanelStyles styles, Texture2D icon = null, float minimum = 0f)
        {
            const float IconColumn = 26f;
            GUILayout.BeginHorizontal();
            if (icon != null)
            {
                Rect iconRect = GUILayoutUtility.GetRect(IconColumn, IconColumn,
                    GUILayout.Width(IconColumn), GUILayout.Height(IconColumn));
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
                GUILayout.Space(6f);
            }

            GUILayout.BeginVertical();
            string minimumText = minimum > 0f ? $"  ·  minimum {minimum:0.#}" : string.Empty;
            GUILayout.Label($"{label}  {value:0.#} / {capacity:0.#} {unit}{minimumText}", styles.Detail);
            Meter(GUILayoutUtility.GetRect(10f, 14f, GUILayout.ExpandWidth(true)),
                value, capacity, fill, minimum);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        /// <summary>A bare progress bar in the current <c>GUILayout</c> flow, for timers and growth.</summary>
        public static void LayoutProgress(float normalized, Color fill, float height = 14f)
        {
            Meter(GUILayoutUtility.GetRect(10f, height, GUILayout.ExpandWidth(true)),
                Mathf.Clamp01(normalized), 1f, fill);
        }
    }
}
