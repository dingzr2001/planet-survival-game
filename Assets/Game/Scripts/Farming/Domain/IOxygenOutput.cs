namespace PlanetSurvival.Farming.Domain
{
    /// <summary>Oxygen-pipe endpoint. Returns the amount removed from the output buffer, in liters.</summary>
    public interface IOxygenOutput
    {
        float ExtractOxygen(float maximumLiters);
    }
}
