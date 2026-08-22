using NUnit.Framework;
using PlanetSurvival.World.Generation;

namespace PlanetSurvival.Tests
{
    public sealed class AtmosphereModelTests
    {
        [Test]
        public void OxygenRatio_DecreasesPredictablyWithAltitude()
        {
            var atmosphere = new AtmosphereModel(.2f, 1000f);

            Assert.That(atmosphere.GetOxygenRatio(0f), Is.EqualTo(.2f).Within(.0001f));
            Assert.That(atmosphere.GetOxygenRatio(1000f), Is.EqualTo(.2f * UnityEngine.Mathf.Exp(-1f)).Within(.0001f));
            Assert.That(atmosphere.GetOxygenRatio(-10f), Is.EqualTo(.2f).Within(.0001f));
        }
    }
}
