using NUnit.Framework;
using PlanetSurvival.Player.Stats;

namespace PlanetSurvival.Tests
{
    public sealed class VitalTests
    {
        [Test]
        public void Change_ClampsAtZero()
        {
            var vital = new Vital(100f, 50f);

            vital.Change(-75f);

            Assert.That(vital.Current, Is.EqualTo(0f));
        }

        [Test]
        public void Change_ClampsAtTheMaximum()
        {
            var vital = new Vital(100f, 90f);

            vital.Change(50f);

            Assert.That(vital.Current, Is.EqualTo(100f));
        }

        [Test]
        public void SetMaximum_PullsAnOversizedCurrentValueDown()
        {
            var vital = new Vital(100f, 100f);

            vital.SetMaximum(70f);

            Assert.That(vital.Current, Is.EqualTo(70f));
            Assert.That(vital.Maximum, Is.EqualTo(70f));
        }

        [Test]
        public void Normalized_WithZeroMaximum_IsEmptyRatherThanFull()
        {
            var vital = new Vital(0f, 0f);

            Assert.That(vital.Normalized, Is.EqualTo(0f));
        }
    }
}
