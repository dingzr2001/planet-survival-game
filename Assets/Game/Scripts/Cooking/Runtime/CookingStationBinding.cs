using PlanetSurvival.Cooking.Definitions;
using PlanetSurvival.Cooking.Domain;
using PlanetSurvival.UI.Cooking;

namespace PlanetSurvival.Cooking.Runtime
{
    /// <summary>
    /// Everything a placed cooking station needs: what it can cook, the session-owned slot it cooks in,
    /// and the panel it opens. Bundled so scene builders pass one argument instead of three.
    /// </summary>
    public readonly struct CookingStationBinding
    {
        public CookingStationBinding(CookingStationDefinition definition, CookingProcess process, CookingView view)
        {
            Definition = definition;
            Process = process;
            View = view;
        }

        public CookingStationDefinition Definition { get; }
        public CookingProcess Process { get; }
        public CookingView View { get; }

        public bool IsComplete => Definition != null && Process != null && View != null;
    }
}
