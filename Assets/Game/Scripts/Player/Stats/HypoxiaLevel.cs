namespace PlanetSurvival.Player.Stats
{
    /// <summary>
    /// Discrete hypoxia stage. Stable input for future HUD and audio feedback; the continuous
    /// severities in <see cref="HypoxiaResult"/> drive the actual vital losses.
    /// </summary>
    public enum HypoxiaLevel
    {
        Safe,
        LowOxygen,
        Critical
    }
}
