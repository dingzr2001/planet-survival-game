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
        public void TemporaryOverflow_AllowsValueAboveEffectiveMaximum()
        {
            var vital = new Vital(100f, 100f);
            vital.SetTemporaryOverflow(10f);

            vital.Change(20f);

            Assert.That(vital.Current, Is.EqualTo(110f));
            Assert.That(vital.EffectiveMaximum, Is.EqualTo(100f));
        }

        [Test]
        public void NegativeMaximumModifier_RestrictsCurrentValue()
        {
            var vital = new Vital(100f, 100f);

            vital.SetMaximumModifier(-30f);

            Assert.That(vital.Current, Is.EqualTo(70f));
            Assert.That(vital.EffectiveMaximum, Is.EqualTo(70f));
        }
    }
}
