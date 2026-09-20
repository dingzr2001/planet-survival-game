using PlanetSurvival.Farming.Domain;
using PlanetSurvival.UI.Farming;

namespace PlanetSurvival.Farming.Runtime
{
    public readonly struct PlanterBoxBinding
    {
        public PlanterBoxBinding(PlanterBox planterBox, PlanterBoxView view)
        {
            PlanterBox = planterBox;
            View = view;
        }

        public PlanterBox PlanterBox { get; }
        public PlanterBoxView View { get; }
        public bool IsComplete => PlanterBox != null && View != null;
    }
}
