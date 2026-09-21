using UnityEngine;

namespace PlanetSurvival.Power.Runtime
{
    /// <summary>A selectable endpoint shown in a power-pole configuration panel.</summary>
    public readonly struct PowerEndpointOption
    {
        public PowerEndpointOption(string id, string label, Sprite icon)
        {
            Id = id;
            Label = label;
            Icon = icon;
        }

        public string Id { get; }
        public string Label { get; }
        public Sprite Icon { get; }
    }
}
