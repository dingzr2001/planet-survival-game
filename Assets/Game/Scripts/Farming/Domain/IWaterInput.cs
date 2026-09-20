namespace PlanetSurvival.Farming.Domain
{
    /// <summary>Water-pipe endpoint. Returns the amount accepted, in milliliters.</summary>
    public interface IWaterInput
    {
        int ReceiveWater(int milliliters);
    }
}
