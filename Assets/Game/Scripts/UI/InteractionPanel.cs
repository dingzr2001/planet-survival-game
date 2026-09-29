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
        /// <remarks>The hint wraps to two lines on a narrow panel, so the band has to fit both.</remarks>
        public const float FooterHeight = 82f;

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
            GUI.Label(new Rect(panel.x + 24f, panel.yMax - 76f, width, 22f), feedback ?? string.Empty,
                styles.Detail);
            GUI.Label(new Rect(panel.x + 24f, panel.yMax - 50f, width, 38f),
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


        /// <summary>
        /// The three bands a machine panel reads in: what goes in on the left, what the machine is doing in
        /// the middle, what comes out on the right. Separated by the same hairlines everywhere.
        /// </summary>
        public readonly struct MachineColumns
        {
            public MachineColumns(Rect inputs, Rect process, Rect outputs)
            {
                Inputs = inputs;
                Process = process;
                Outputs = outputs;
            }

            public Rect Inputs { get; }
            public Rect Process { get; }
            public Rect Outputs { get; }
        }

        /// <summary>Splits the body into input, process and output columns and draws the dividers.</summary>
        public static MachineColumns DrawColumns(Rect panel, in InteractionPanelTheme theme)
        {
            float top = ContentTop(panel);
            float bottom = ContentBottom(panel);
            float height = bottom - top;
            float inputWidth = panel.width * .275f;
            float processWidth = panel.width * .41f;
            float outputX = panel.x + inputWidth + processWidth;
            Divider(panel.x + inputWidth, top, bottom, theme);
            Divider(outputX, top, bottom, theme);
            return new MachineColumns(
                new Rect(panel.x, top, inputWidth, height),
                new Rect(panel.x + inputWidth, top, processWidth, height),
                new Rect(outputX, top, panel.xMax - outputX, height));
        }

        /// <summary>A slot centred in its column, with room below for its caption.</summary>
        public static Rect SlotAt(Rect column, float y, float size) =>
            new(column.x + (column.width - size) * .5f, y, size, size);

        /// <summary>
        /// Everything one slot occupies vertically: the artwork, its caption and level line, and the row of
        /// action buttons under it when it has any.
        /// </summary>
        public static float SlotBlockHeight(float slotSize, bool hasActions) =>
            slotSize + SlotCaptionHeight + (hasActions ? SlotActionHeight + 12f : 0f);

        /// <summary>
        /// The largest slot that still lets <paramref name="count"/> of them fit the column. The panel
        /// shrinks to fit a small game window, so slot geometry has to follow rather than be hand-placed.
        /// </summary>
        public static float SlotSizeForStack(Rect panel, Rect column, int count, bool hasActions,
            float topInset = 0f)
        {
            float overhead = SlotCaptionHeight + (hasActions ? SlotActionHeight + 12f : 0f);
            int slots = Mathf.Max(1, count);
            float perSlot = (column.height - topInset - MinimumSlotGap * (slots + 1)) / slots - overhead;
            return Mathf.Clamp(Mathf.Min(panel.width * .12f, perSlot), MinimumSlotSize, MaximumSlotSize);
        }

        /// <summary>
        /// Places one of <paramref name="count"/> slots stacked down a column, spread evenly under any
        /// <paramref name="topInset"/> the column reserves for a headline.
        /// </summary>
        public static Rect StackedSlot(Rect column, int index, int count, float slotSize, bool hasActions,
            float topInset = 0f)
        {
            float block = SlotBlockHeight(slotSize, hasActions);
            int slots = Mathf.Max(1, count);
            // One equal gap above, below and between every block, so the column stays balanced at any size.
            float gap = Mathf.Max(MinimumSlotGap,
                (column.height - topInset - slots * block) / (slots + 1));
            return SlotAt(column, column.y + topInset + gap + index * (block + gap), slotSize);
        }

        private const float MinimumSlotSize = 44f;
        private const float MaximumSlotSize = 110f;
        private const float MinimumSlotGap = 8f;

        /// <summary>
        /// The framed button a slot is built on. <paramref name="highlighted"/> marks the slot whose picker
        /// or menu is open.
        /// </summary>
        public static bool SlotButton(Rect rect, bool highlighted, in InteractionPanelTheme theme)
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = highlighted ? theme.Accent : theme.Frame;
            bool pressed = GUI.Button(rect, GUIContent.none);
            GUI.backgroundColor = previous;
            return pressed;
        }

        /// <summary>
        /// Artwork inside a slot: the item's sprite where it has one, otherwise a loose texture, otherwise a
        /// short glyph such as a chemical formula.
        /// </summary>
        public static void SlotIcon(Rect rect, Sprite icon, Texture2D texture, string glyph,
            InteractionPanelStyles styles)
        {
            var inner = new Rect(rect.x + 9f, rect.y + 9f, rect.width - 18f, rect.height - 18f);
            if (icon != null)
            {
                SpriteIcon.Draw(inner, icon);
            }
            else if (texture != null)
            {
                GUI.DrawTexture(inner, texture, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.Label(inner, glyph, styles.Glyph);
            }
        }

        /// <summary>The name and fill level under a slot, centred on it.</summary>
        public static void SlotCaption(Rect slot, string name, string level, InteractionPanelStyles styles)
        {
            var band = new Rect(slot.x - 28f, slot.yMax + 5f, slot.width + 56f, 22f);
            GUI.Label(band, name, styles.Caption);
            GUI.Label(new Rect(band.x, band.yMax, band.width, 19f), level, styles.Note);
        }

        /// <summary>Height a slot needs including its caption and level line.</summary>
        public const float SlotCaptionHeight = 46f;

        /// <summary>A slim level bar under a slot caption, for a tank rather than a stack of items.</summary>
        public static void SlotMeter(Rect slot, float value, float capacity, float minimum, Color fill,
            in InteractionPanelTheme theme)
        {
            var track = new Rect(slot.x - 12f, slot.yMax + SlotCaptionHeight + 2f, slot.width + 24f, 6f);
            Fill(track, theme.Track);
            float normalized = capacity > 0f ? Mathf.Clamp01(value / capacity) : 0f;
            if (normalized > 0f)
            {
                Fill(new Rect(track.x, track.y, track.width * normalized, track.height), fill);
            }

            if (minimum > 0f && capacity > 0f)
            {
                Fill(new Rect(track.x + track.width * Mathf.Clamp01(minimum / capacity) - 1f, track.y - 2f,
                    2f, track.height + 4f), theme.Accent);
            }
        }

        /// <summary>
        /// The inset display in the middle column that shows what the machine is doing. Returns the area
        /// inside it, below the title bar and above the status strip.
        /// </summary>
        public static Rect Chamber(Rect rect, string title, InteractionPanelStyles styles,
            in InteractionPanelTheme theme)
        {
            Fill(rect, theme.Recess);
            Frame(rect, theme.Frame);
            Fill(new Rect(rect.x + 1f, rect.y + 32f, rect.width - 2f, 1f), theme.Frame);
            GUI.Label(new Rect(rect.x + 12f, rect.y + 5f, rect.width - 24f, 22f), title, styles.Detail);
            return new Rect(rect.x + 8f, rect.y + 37f, rect.width - 16f, rect.height - 90f);
        }

        /// <summary>
        /// The strip along the bottom of a chamber: a lamp that is lit while the machine runs, what it is
        /// doing in words, and how far through the current cycle it is.
        /// </summary>
        public static void ChamberStatus(Rect chamber, bool active, string status, float progress,
            InteractionPanelStyles styles, in InteractionPanelTheme theme)
        {
            Fill(new Rect(chamber.x + 13f, chamber.yMax - 32f, 7f, 7f), active ? theme.Accent : theme.Muted);
            GUI.Label(new Rect(chamber.x + 27f, chamber.yMax - 37f, chamber.width - 39f, 20f), status,
                styles.Detail);
            var track = new Rect(chamber.x + 12f, chamber.yMax - 12f, chamber.width - 24f, 3f);
            Fill(track, theme.Track);
            float normalized = Mathf.Clamp01(progress);
            if (normalized > 0f)
            {
                Fill(new Rect(track.x, track.y, track.width * normalized, track.height), theme.Accent);
            }
        }

        /// <summary>The one number a machine exists to produce, at the top of the output column.</summary>
        public static void Headline(Rect column, string text, InteractionPanelStyles styles)
        {
            GUI.Label(new Rect(column.x + 13f, column.y + 13f, column.width - 26f, 33f), text, styles.Title);
        }

        /// <summary>Height one row of slot actions occupies.</summary>
        public const float SlotActionHeight = 26f;

        /// <summary>
        /// A compact action button under a slot, spanning its caption band. A slot that offers more than one
        /// action passes <paramref name="columns"/> and the button's <paramref name="column"/>.
        /// </summary>
        public static bool SlotAction(Rect slot, float offsetY, string label, bool enabled,
            int column = 0, int columns = 1)
        {
            const float Gap = 5f;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && enabled;
            float bandX = slot.x - 28f;
            float bandWidth = slot.width + 56f;
            float width = (bandWidth - Gap * (columns - 1)) / Mathf.Max(1, columns);
            bool pressed = GUI.Button(
                new Rect(bandX + column * (width + Gap), slot.yMax + offsetY, width, SlotActionHeight),
                label);
            GUI.enabled = previousEnabled;
            return pressed;
        }

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
