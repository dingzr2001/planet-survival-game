using System;
using NUnit.Framework;
using PlanetSurvival.Oxygen.Domain;

namespace PlanetSurvival.Tests
{
    public sealed class OxygenReservoirTests
    {
        [Test]
        public void Consume_ClampsAtEmptyAndReportsActualAmount()
        {
            var reservoir = new OxygenReservoir(100f, 40f);

            float consumed = reservoir.Consume(75f);

            Assert.That(consumed, Is.EqualTo(40f));
            Assert.That(reservoir.CurrentLiters, Is.Zero);
            Assert.That(reservoir.Normalized, Is.Zero);
        }

        [Test]
        public void Consume_RejectsInvalidAmountsWithoutChangingState()
        {
            var reservoir = new OxygenReservoir(100f, 80f);

            Assert.That(reservoir.Consume(0f), Is.Zero);
            Assert.That(reservoir.Consume(-5f), Is.Zero);
            Assert.That(reservoir.Consume(float.NaN), Is.Zero);
            Assert.That(reservoir.CurrentLiters, Is.EqualTo(80f));
        }

        [Test]
        public void Constructor_RejectsInvalidCapacityAndInitialState()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OxygenReservoir(0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OxygenReservoir(100f, 101f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OxygenReservoir(float.PositiveInfinity));
        }
    }
}
