namespace PlanetSurvival.Crafting.Domain
{
    public interface ICraftingConditionProvider
    {
        bool IsConditionMet(string conditionId);
    }
}
