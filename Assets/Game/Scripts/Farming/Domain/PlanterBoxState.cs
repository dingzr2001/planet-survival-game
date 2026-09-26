namespace PlanetSurvival.Farming.Domain
{
    public enum PlanterBoxState
    {
        Empty,
        NeedsWater,
        NeedsCarbonDioxide,
        Growing,
        Mature,
        Dead,
        OxygenStorageFull,
        Producing = Growing
    }
}
