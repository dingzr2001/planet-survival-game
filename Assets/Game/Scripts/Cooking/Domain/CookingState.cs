namespace PlanetSurvival.Cooking.Domain
{
    public enum CookingState
    {
        /// <summary>Nothing is loaded; the station accepts a new recipe.</summary>
        Idle,

        /// <summary>Ingredients are consumed and the timer is running.</summary>
        Cooking,

        /// <summary>The dish is finished and waits inside the station until it is collected.</summary>
        Ready
    }
}
