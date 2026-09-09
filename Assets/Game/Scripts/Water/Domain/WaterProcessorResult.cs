namespace PlanetSurvival.Water.Domain
{
    /// <summary>
    /// Why a processor operation did or did not happen. The panel turns these into text, so the domain
    /// stays free of wording and the same outcome can be phrased differently per fixture.
    /// </summary>
    public enum WaterProcessorResult
    {
        Succeeded,
        InvalidRequest,
        AlreadyProcessing,
        OutputWaiting,
        NotEnoughIce,
        NothingReady,
        TankFull
    }
}
