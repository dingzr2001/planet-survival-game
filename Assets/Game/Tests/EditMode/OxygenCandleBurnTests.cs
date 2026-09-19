using NUnit.Framework;
using PlanetSurvival.Oxygen.Domain;

namespace PlanetSurvival.Tests
{
    public sealed class OxygenCandleBurnTests
    {
        private const double GameHoursPerDay = 24d;

        [Test]
        public void Ignite_CanOnlyStartTheCandleOnce()
        {
            var candle = new OxygenCandleBurn();

            Assert.That(candle.Ignite(3d), Is.True);
            Assert.That(candle.Ignite(4d), Is.False);
            Assert.That(candle.IsBurning, Is.True);
            Assert.That(candle.SettledIntervals, Is.Zero);
        }

        [Test]
        public void Advance_ProducesConfiguredAmountForEveryWholeGameHour()
        {
            var reservoir = new OxygenReservoir(5000f);
            var candle = new OxygenCandleBurn();
            candle.Ignite(1d);

            float beforeInterval = candle.Advance(1d + .5d / GameHoursPerDay, reservoir);
            float threeIntervals = candle.Advance(1d + 3d / GameHoursPerDay, reservoir);

            Assert.That(beforeInterval, Is.Zero);
            Assert.That(threeIntervals,
                Is.EqualTo(3f * OxygenCandleBurn.OxygenLitersPerInterval));
            Assert.That(reservoir.CurrentLiters, Is.EqualTo(threeIntervals));
            Assert.That(candle.SettledIntervals, Is.EqualTo(3));
        }

        [Test]
        public void Advance_AfterSceneAbsence_SettlesRemainingCandleWithoutOverproducing()
        {
            var reservoir = new OxygenReservoir(10000f);
            var candle = new OxygenCandleBurn();
            candle.Ignite(2d);

            float produced = candle.Advance(12d, reservoir);

            Assert.That(produced, Is.EqualTo(
                OxygenCandleBurn.OxygenLitersPerInterval * OxygenCandleBurn.OutputIntervals));
            Assert.That(candle.IsBurning, Is.False);
            Assert.That(candle.IsSpent, Is.True);
            Assert.That(candle.Advance(13d, reservoir), Is.Zero);
        }

        [Test]
        public void Advance_WhenReservoirFills_VentsOverflowAndStillBurnsOut()
        {
            var reservoir = new OxygenReservoir(250f, 100f);
            var candle = new OxygenCandleBurn();
            candle.Ignite(0d);

            float accepted = candle.Advance(2d, reservoir);

            Assert.That(accepted, Is.EqualTo(150f));
            Assert.That(reservoir.CurrentLiters, Is.EqualTo(250f));
            Assert.That(candle.IsSpent, Is.True,
                "A chemical candle cannot pause merely because the reservoir is full.");
        }
    }
}
