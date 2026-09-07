using System;
using UnityEngine;

namespace PlanetSurvival.Player.Stats
{
    /// <summary>
    /// Turns a normalized oxygen ratio and a game time step into sanity and health losses.
    /// Plain C# so the curve can be verified without a scene; knows nothing about where the
    /// oxygen came from or how it is consumed.
    /// </summary>
    public sealed class HypoxiaRule
    {
        public const float DefaultSanityThreshold = .5f;
        public const float DefaultHealthThreshold = .2f;
        public const float DefaultMaximumSanityLossPerGameHour = 30f;
        public const float DefaultMaximumHealthLossPerGameHour = 60f;
        public const float DefaultSanityExponent = 1.5f;
        public const float DefaultHealthExponent = 1.5f;

        /// <summary>Thresholds must stay above zero so severity never divides by zero.</summary>
        public const float MinimumThreshold = .0001f;

        public HypoxiaRule()
            : this(DefaultSanityThreshold, DefaultHealthThreshold, DefaultMaximumSanityLossPerGameHour,
                DefaultMaximumHealthLossPerGameHour, DefaultSanityExponent, DefaultHealthExponent)
        {
        }

        public HypoxiaRule(float sanityThreshold, float healthThreshold,
            float maximumSanityLossPerGameHour, float maximumHealthLossPerGameHour,
            float sanityExponent, float healthExponent)
        {
            RequireFinite(sanityThreshold, nameof(sanityThreshold));
            RequireFinite(healthThreshold, nameof(healthThreshold));
            RequireFinite(maximumSanityLossPerGameHour, nameof(maximumSanityLossPerGameHour));
            RequireFinite(maximumHealthLossPerGameHour, nameof(maximumHealthLossPerGameHour));
            RequireFinite(sanityExponent, nameof(sanityExponent));
            RequireFinite(healthExponent, nameof(healthExponent));

            if (sanityThreshold < MinimumThreshold || sanityThreshold > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(sanityThreshold), sanityThreshold,
                    $"Sanity threshold must be within [{MinimumThreshold}, 1].");
            }

            if (healthThreshold < MinimumThreshold || healthThreshold > sanityThreshold)
            {
                throw new ArgumentOutOfRangeException(nameof(healthThreshold), healthThreshold,
                    $"Health threshold must be within [{MinimumThreshold}, {sanityThreshold}] (0 < Th <= Ts <= 1).");
            }

            RequireNotNegative(maximumSanityLossPerGameHour, nameof(maximumSanityLossPerGameHour));
            RequireNotNegative(maximumHealthLossPerGameHour, nameof(maximumHealthLossPerGameHour));
            RequireNotNegative(sanityExponent, nameof(sanityExponent));
            RequireNotNegative(healthExponent, nameof(healthExponent));

            SanityThreshold = sanityThreshold;
            HealthThreshold = healthThreshold;
            MaximumSanityLossPerGameHour = maximumSanityLossPerGameHour;
            MaximumHealthLossPerGameHour = maximumHealthLossPerGameHour;
            SanityExponent = sanityExponent;
            HealthExponent = healthExponent;
        }

        public float SanityThreshold { get; }
        public float HealthThreshold { get; }
        public float MaximumSanityLossPerGameHour { get; }
        public float MaximumHealthLossPerGameHour { get; }
        public float SanityExponent { get; }
        public float HealthExponent { get; }

        /// <summary>
        /// Evaluates the losses for one time step. A step of zero or less produces no loss but still
        /// reports the current severities and level, so callers can refresh feedback without ticking.
        /// </summary>
        /// <exception cref="ArgumentException">A non-finite input was supplied.</exception>
        public HypoxiaResult Evaluate(float normalizedOxygen, float elapsedGameHours)
        {
            RequireFinite(normalizedOxygen, nameof(normalizedOxygen));
            RequireFinite(elapsedGameHours, nameof(elapsedGameHours));

            float oxygen = Mathf.Clamp01(normalizedOxygen);
            float sanitySeverity = Severity(oxygen, SanityThreshold);
            float healthSeverity = Severity(oxygen, HealthThreshold);
            float step = Mathf.Max(0f, elapsedGameHours);

            return new HypoxiaResult(
                sanitySeverity,
                healthSeverity,
                Loss(MaximumSanityLossPerGameHour, sanitySeverity, SanityExponent, step),
                Loss(MaximumHealthLossPerGameHour, healthSeverity, HealthExponent, step),
                GetLevel(oxygen));
        }

        public HypoxiaLevel GetLevel(float normalizedOxygen)
        {
            float oxygen = Mathf.Clamp01(normalizedOxygen);
            if (oxygen >= SanityThreshold)
            {
                return HypoxiaLevel.Safe;
            }

            return oxygen >= HealthThreshold ? HypoxiaLevel.LowOxygen : HypoxiaLevel.Critical;
        }

        private static float Severity(float oxygen, float threshold)
        {
            return Mathf.Clamp01((threshold - oxygen) / threshold);
        }

        private static float Loss(float maximumRate, float severity, float exponent, float elapsedGameHours)
        {
            // Severity zero must always mean zero loss, including for an exponent of zero.
            if (severity <= 0f || elapsedGameHours <= 0f)
            {
                return 0f;
            }

            return maximumRate * Mathf.Pow(severity, exponent) * elapsedGameHours;
        }

        private static void RequireFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentException($"'{parameterName}' must be a finite number but was {value}.", parameterName);
            }
        }

        private static void RequireNotNegative(float value, string parameterName)
        {
            if (value < 0f)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, $"'{parameterName}' must be zero or greater.");
            }
        }
    }
}
