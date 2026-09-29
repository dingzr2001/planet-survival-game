using UnityEngine;

namespace PlanetSurvival.UI
{
    /// <summary>
    /// The three colours one machine panel is drawn from: the plate behind it, the accent its titles and
    /// readouts use, and the muted tone supporting text falls back to. Machine families keep different
    /// accents so a panel is recognisable before it is read, while the plate weight and the structure
    /// around it stay identical everywhere.
    /// </summary>
    public readonly struct InteractionPanelTheme
    {
        public InteractionPanelTheme(Color plate, Color accent, Color muted)
        {
            Plate = plate;
            Accent = accent;
            Muted = muted;
        }

        /// <summary>Fill of the panel plate, including its opacity over the dimmed world.</summary>
        public Color Plate { get; }

        /// <summary>Titles, section headings and the values the player is here to read.</summary>
        public Color Accent { get; }

        /// <summary>Explanations, hints and the latest action result.</summary>
        public Color Muted { get; }

        /// <summary>Outline of an inset instrument area, such as a slot or a process chamber.</summary>
        public Color Frame => Opaque(Color.Lerp(Plate, Accent, .35f));

        /// <summary>Inside of an inset instrument area: darker than the plate, so it reads as recessed.</summary>
        public Color Recess => Opaque(Color.Lerp(Color.black, Plate, .45f));

        /// <summary>The empty part of a meter or progress track.</summary>
        public Color Track => Opaque(Color.Lerp(Plate, Accent, .18f));

        /// <summary>Plate of a popup that floats above the panel.</summary>
        public Color Popup => new(
            Mathf.Lerp(Plate.r, Accent.r, .06f) + .045f,
            Mathf.Lerp(Plate.g, Accent.g, .06f) + .043f,
            Mathf.Lerp(Plate.b, Accent.b, .06f) + .038f,
            .99f);

        /// <summary>The panel's colours over a transparent plate would tint the world; instruments are solid.</summary>
        private static Color Opaque(Color color) => new(color.r, color.g, color.b, 1f);

        /// <summary>Combustion, mining and cooking: anything that burns fuel or heats material.</summary>
        public static InteractionPanelTheme Thermal { get; } = new(
            new Color(.065f, .052f, .037f, .98f),
            new Color(1f, .72f, .27f),
            new Color(.79f, .78f, .72f));

        /// <summary>Water, gas and electrolysis machinery.</summary>
        public static InteractionPanelTheme Fluid { get; } = new(
            new Color(.02f, .055f, .08f, .98f),
            new Color(.52f, .86f, 1f),
            new Color(.8f, .86f, .9f));

        /// <summary>Planters, hydroponics and anything else that grows a crop.</summary>
        public static InteractionPanelTheme Growth { get; } = new(
            new Color(.025f, .075f, .045f, .98f),
            new Color(.5f, .95f, .62f),
            new Color(.82f, .88f, .82f));

        /// <summary>Storage, item transfer and power distribution: the networks between machines.</summary>
        public static InteractionPanelTheme Logistics { get; } = new(
            new Color(.035f, .05f, .065f, .98f),
            new Color(.45f, .82f, 1f),
            new Color(.82f, .88f, .92f));
    }
}
