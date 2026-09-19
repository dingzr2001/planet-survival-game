namespace PlanetSurvival.Mining.Domain
{
    /// <summary>Connection point for a power network. Returns the energy that fitted in the input buffer.</summary>
    public interface IElectricityInput
    {
        float ReceiveElectricity(float energyUnits);
    }
}
