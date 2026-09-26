using PlanetSurvival.Farming.Domain;
using PlanetSurvival.UI.Farming;
using PlanetSurvival.Core.Time;

namespace PlanetSurvival.Farming.Runtime
{
    public readonly struct PlanterBoxBinding
    {
        public PlanterBoxBinding(PlanterBox planterBox, PlanterBoxView view, GameClock clock)
        {
            PlanterBox = planterBox;
            View = view;
            Clock = clock;
        }

        public PlanterBox PlanterBox { get; }
        public PlanterBoxView View { get; }
        public GameClock Clock { get; }
        public bool IsComplete => PlanterBox != null && View != null && Clock != null;
    }
}
