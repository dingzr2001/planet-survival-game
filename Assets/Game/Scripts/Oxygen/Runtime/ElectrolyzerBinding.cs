using PlanetSurvival.Oxygen.Domain;
using PlanetSurvival.UI.Oxygen;

namespace PlanetSurvival.Oxygen.Runtime
{
    /// <summary>Everything the scene half of an electrolyzer needs: the machine and the panel it opens.</summary>
    public readonly struct ElectrolyzerBinding
    {
        public ElectrolyzerBinding(Electrolyzer electrolyzer, ElectrolyzerView view)
        {
            Electrolyzer = electrolyzer;
            View = view;
        }

        public Electrolyzer Electrolyzer { get; }
        public ElectrolyzerView View { get; }
        public bool IsComplete => Electrolyzer != null && View != null;
    }
}
