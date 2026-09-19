namespace PlanetSurvival.Mining.Domain
{
    /// <summary>Connection point for a petroleum pipe. Returns the liquid volume accepted by the tank.</summary>
    public interface IPetroleumInput
    {
        float ReceivePetroleum(float volume);
    }
}
