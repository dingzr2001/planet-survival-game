using PlanetSurvival.Mining.Domain;

namespace PlanetSurvival.Power.Domain
{
    /// <summary>Power-consuming endpoint contract for current and future electric buildings.</summary>
    public interface IPowerInput : IElectricityInput
    {
        float RequestedElectricity(float elapsedSeconds);
    }
}
