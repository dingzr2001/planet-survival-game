using System;

namespace PlanetSurvival.Player.Stats
{
    /// <summary>Maps real-world food energy to the game's 0-100 satiety scale.</summary>
    public static class NutritionBalance
    {
        // A completely full hunger meter represents roughly one adult daily energy budget.
        public const float CaloriesPerHungerPoint = 25f;

        public static float ToHungerPoints(int calories)
        {
            return Math.Max(0, calories) / CaloriesPerHungerPoint;
        }
    }
}
