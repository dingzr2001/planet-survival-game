namespace PlanetSurvival.Farming.Domain
{
    /// <summary>Carbon-dioxide pipe endpoint. Returns the amount accepted, in liters.</summary>
    public interface ICarbonDioxideInput
    {
        float ReceiveCarbonDioxide(float liters);
    }
}
