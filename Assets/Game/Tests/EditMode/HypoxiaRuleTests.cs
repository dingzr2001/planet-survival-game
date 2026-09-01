using System;
using NUnit.Framework;
using PlanetSurvival.Player.Stats;

namespace PlanetSurvival.Tests
{
    public sealed class HypoxiaRuleTests
    {
        private const float Tolerance = .0001f;

        [Test]
        public void Evaluate_AtOrAboveSanityThreshold_LosesNothing()
        {
            var rule = new HypoxiaRule();

            HypoxiaResult safe = rule.Evaluate(1f, 1f);
            HypoxiaResult boundary = rule.Evaluate(.5f, 1f);

            Assert.That(safe.SanityLoss, Is.EqualTo(0f));
            Assert.That(safe.HealthLoss, Is.EqualTo(0f));
            Assert.That(boundary.SanityLoss, Is.EqualTo(0f));
            Assert.That(boundary.HealthLoss, Is.EqualTo(0f));
            Assert.That(boundary.Level, Is.EqualTo(HypoxiaLevel.Safe));
        }

        [Test]
        public void Evaluate_JustBelowSanityThreshold_LosesSanityOnly()
        {
            var rule = new HypoxiaRule();

            HypoxiaResult result = rule.Evaluate(.4999f, 1f);

            Assert.That(result.SanityLoss, Is.GreaterThan(0f));
            Assert.That(result.HealthLoss, Is.EqualTo(0f));
            Assert.That(result.Level, Is.EqualTo(HypoxiaLevel.LowOxygen));
        }

        [Test]
        public void Evaluate_AtHealthThreshold_LosesSanityButNoHealth()
        {
            var rule = new HypoxiaRule();

            HypoxiaResult boundary = rule.Evaluate(.2f, 1f);
            HypoxiaResult belowBoundary = rule.Evaluate(.1999f, 1f);

            Assert.That(boundary.SanityLoss, Is.GreaterThan(0f));
            Assert.That(boundary.HealthLoss, Is.EqualTo(0f));
            Assert.That(boundary.Level, Is.EqualTo(HypoxiaLevel.LowOxygen));
            Assert.That(belowBoundary.HealthLoss, Is.GreaterThan(0f));
            Assert.That(belowBoundary.Level, Is.EqualTo(HypoxiaLevel.Critical));
        }

        [Test]
        public void Evaluate_AtZeroOxygen_ReachesConfiguredMaximumRates()
        {
            var rule = new HypoxiaRule();

            HypoxiaResult result = rule.Evaluate(0f, 1f);

            Assert.That(result.SanitySeverity, Is.EqualTo(1f));
            Assert.That(result.HealthSeverity, Is.EqualTo(1f));
            Assert.That(result.SanityLoss, Is.EqualTo(30f).Within(Tolerance));
            Assert.That(result.HealthLoss, Is.EqualTo(60f).Within(Tolerance));
        }

        [Test]
        public void Evaluate_MatchesTheDocumentedDefaultCurve()
        {
            var rule = new HypoxiaRule();

            Assert.That(rule.Evaluate(.4f, 1f).SanityLoss, Is.EqualTo(2.7f).Within(.05f));
            Assert.That(rule.Evaluate(.3f, 1f).SanityLoss, Is.EqualTo(7.6f).Within(.05f));
            Assert.That(rule.Evaluate(.2f, 1f).SanityLoss, Is.EqualTo(13.9f).Within(.05f));
            Assert.That(rule.Evaluate(.1f, 1f).SanityLoss, Is.EqualTo(21.5f).Within(.05f));
            Assert.That(rule.Evaluate(.1f, 1f).HealthLoss, Is.EqualTo(21.2f).Within(.05f));
        }

        [Test]
        public void Evaluate_AsOxygenFalls_LossRatesNeverDecrease()
        {
            var rule = new HypoxiaRule();
            float previousSanityLoss = 0f;
            float previousHealthLoss = 0f;

            for (int step = 100; step >= 0; step--)
            {
                HypoxiaResult result = rule.Evaluate(step / 100f, 1f);

                Assert.That(result.SanityLoss, Is.GreaterThanOrEqualTo(previousSanityLoss - Tolerance));
                Assert.That(result.HealthLoss, Is.GreaterThanOrEqualTo(previousHealthLoss - Tolerance));
                previousSanityLoss = result.SanityLoss;
                previousHealthLoss = result.HealthLoss;
            }
        }

        [Test]
        public void Evaluate_OneLargeStep_MatchesManySmallSteps()
        {
            var rule = new HypoxiaRule();

            HypoxiaResult combined = rule.Evaluate(.1f, 1f);
            float splitSanityLoss = 0f;
            float splitHealthLoss = 0f;
            for (int i = 0; i < 10; i++)
            {
                HypoxiaResult step = rule.Evaluate(.1f, .1f);
                splitSanityLoss += step.SanityLoss;
                splitHealthLoss += step.HealthLoss;
            }

            Assert.That(splitSanityLoss, Is.EqualTo(combined.SanityLoss).Within(Tolerance));
            Assert.That(splitHealthLoss, Is.EqualTo(combined.HealthLoss).Within(Tolerance));
        }

        [Test]
        public void Evaluate_WithNonPositiveTimeStep_ReportsSeverityWithoutLoss()
        {
            var rule = new HypoxiaRule();

            HypoxiaResult zeroStep = rule.Evaluate(0f, 0f);
            HypoxiaResult negativeStep = rule.Evaluate(0f, -5f);

            Assert.That(zeroStep.SanityLoss, Is.EqualTo(0f));
            Assert.That(zeroStep.HealthLoss, Is.EqualTo(0f));
            Assert.That(zeroStep.SanitySeverity, Is.EqualTo(1f));
            Assert.That(zeroStep.Level, Is.EqualTo(HypoxiaLevel.Critical));
            Assert.That(negativeStep.SanityLoss, Is.EqualTo(0f));
            Assert.That(negativeStep.HealthLoss, Is.EqualTo(0f));
        }

        [Test]
        public void Evaluate_OutOfRangeOxygen_IsClamped()
        {
            var rule = new HypoxiaRule();

            Assert.That(rule.Evaluate(-1f, 1f).SanityLoss, Is.EqualTo(30f).Within(Tolerance));
            Assert.That(rule.Evaluate(4f, 1f).SanityLoss, Is.EqualTo(0f));
        }

        [Test]
        public void Evaluate_WithNonFiniteInput_Throws()
        {
            var rule = new HypoxiaRule();

            Assert.Throws<ArgumentException>(() => rule.Evaluate(float.NaN, 1f));
            Assert.Throws<ArgumentException>(() => rule.Evaluate(.1f, float.NaN));
            Assert.Throws<ArgumentException>(() => rule.Evaluate(float.PositiveInfinity, 1f));
            Assert.Throws<ArgumentException>(() => rule.Evaluate(.1f, float.PositiveInfinity));
        }

        [Test]
        public void Evaluate_WithCustomConfiguration_UsesIt()
        {
            var rule = new HypoxiaRule(.8f, .4f, 10f, 20f, 1f, 2f);

            HypoxiaResult result = rule.Evaluate(.4f, 1f);

            // Ss = (0.8 - 0.4) / 0.8 = 0.5, Sh = 0 at the health threshold.
            Assert.That(result.SanityLoss, Is.EqualTo(5f).Within(Tolerance));
            Assert.That(result.HealthLoss, Is.EqualTo(0f));
            Assert.That(rule.Evaluate(.2f, 1f).HealthLoss, Is.EqualTo(20f * .25f).Within(Tolerance));
            Assert.That(rule.Evaluate(.6f, 1f).Level, Is.EqualTo(HypoxiaLevel.LowOxygen));
        }

        [Test]
        public void Constructor_WithIllegalConfiguration_IsRejected()
        {
            // Health threshold above sanity threshold.
            Assert.Throws<ArgumentOutOfRangeException>(() => new HypoxiaRule(.3f, .5f, 30f, 60f, 1.5f, 1.5f));
            // Zero and out of range thresholds.
            Assert.Throws<ArgumentOutOfRangeException>(() => new HypoxiaRule(.5f, 0f, 30f, 60f, 1.5f, 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HypoxiaRule(1.5f, .2f, 30f, 60f, 1.5f, 1.5f));
            // Negative rates and exponents.
            Assert.Throws<ArgumentOutOfRangeException>(() => new HypoxiaRule(.5f, .2f, -1f, 60f, 1.5f, 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HypoxiaRule(.5f, .2f, 30f, 60f, 1.5f, -1.5f));
            // Non-finite configuration.
            Assert.Throws<ArgumentException>(() => new HypoxiaRule(float.NaN, .2f, 30f, 60f, 1.5f, 1.5f));
        }

        [Test]
        public void Evaluate_WithZeroExponent_StillLosesNothingAboveThreshold()
        {
            var rule = new HypoxiaRule(.5f, .2f, 30f, 60f, 0f, 0f);

            Assert.That(rule.Evaluate(.6f, 1f).SanityLoss, Is.EqualTo(0f));
            Assert.That(rule.Evaluate(.4f, 1f).SanityLoss, Is.EqualTo(30f).Within(Tolerance));
        }
    }
}
