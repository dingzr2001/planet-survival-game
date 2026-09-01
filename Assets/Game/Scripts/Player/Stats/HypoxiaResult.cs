namespace PlanetSurvival.Player.Stats
{
    /// <summary>
    /// Outcome of a single <see cref="HypoxiaRule"/> evaluation. Callers apply the losses and may
    /// read the severities for feedback, but must never recreate the loss formula from the level.
    /// </summary>
    public readonly struct HypoxiaResult
    {
        public HypoxiaResult(float normalizedOxygen, float sanitySeverity, float healthSeverity,
            float sanityLoss, float healthLoss, HypoxiaLevel level)
        {
            NormalizedOxygen = normalizedOxygen;
            SanitySeverity = sanitySeverity;
            HealthSeverity = healthSeverity;
            SanityLoss = sanityLoss;
            HealthLoss = healthLoss;
            Level = level;
        }

        /// <summary>Normalized oxygen the evaluation used, clamped to [0, 1].</summary>
        public float NormalizedOxygen { get; }

        /// <summary>How far oxygen dropped below the sanity threshold, in [0, 1].</summary>
        public float SanitySeverity { get; }

        /// <summary>How far oxygen dropped below the health threshold, in [0, 1].</summary>
        public float HealthSeverity { get; }

        /// <summary>Sanity to remove for this time step. Never negative.</summary>
        public float SanityLoss { get; }

        /// <summary>Health to remove for this time step. Never negative.</summary>
        public float HealthLoss { get; }

        public HypoxiaLevel Level { get; }
    }
}
