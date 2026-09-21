using NUnit.Framework;
using PlanetSurvival.Power.Domain;

namespace PlanetSurvival.Tests.EditMode
{
    public sealed class PowerPoleTests
    {
        [Test]
        public void OutputOrder_CanBeReprioritizedWithoutChangingConfiguredEndpoints()
        {
            var pole = new PowerPole();
            pole.AddOutput("drill:first");
            pole.AddOutput("drill:second");
            pole.AddOutput("drill:third");

            Assert.That(pole.MoveOutputEarlier("drill:third"), Is.True);
            Assert.That(pole.OutputEndpointIds, Is.EqualTo(new[]
            {
                "drill:first", "drill:third", "drill:second"
            }));
            Assert.That(pole.MoveOutputLater("drill:first"), Is.True);
            Assert.That(pole.OutputEndpointIds, Is.EqualTo(new[]
            {
                "drill:third", "drill:first", "drill:second"
            }));
        }

        [Test]
        public void Endpoints_AreUniqueAndFlowStateIsReported()
        {
            var pole = new PowerPole();
            Assert.That(pole.AddInput("solar:a"), Is.True);
            Assert.That(pole.AddInput("solar:a"), Is.False);
            Assert.That(pole.AddOutput("drill:a"), Is.True);
            Assert.That(pole.AddOutput("drill:a"), Is.False);

            pole.SetFlow(2f, 1f, 1);

            Assert.That(pole.LastInputPower, Is.EqualTo(2f));
            Assert.That(pole.LastDeliveredPower, Is.EqualTo(1f));
            Assert.That(pole.LastPoweredOutputs, Is.EqualTo(1));
        }
    }
}
