using NUnit.Framework;
using PlanetSurvival.Core.Flow;
using PlanetSurvival.Core.Time;
using PlanetSurvival.Water.Domain;

namespace PlanetSurvival.Tests
{
    public sealed class GameSessionStateTests
    {
        private const float RealSecondsPerGameDay = 600f;
        private const int RescueDay = 30;

        [Test]
        public void ConfigureTime_AcrossSceneChanges_KeepsOneRunningExpeditionClock()
        {
            var session = new GameSessionState();
            GameTimeModel surfaceClock = session.ConfigureTime(RealSecondsPerGameDay, RescueDay);
            surfaceClock.Advance(RealSecondsPerGameDay * 3d);

            // Walking into the landing pod builds a second scene, which configures the clock again.
            GameTimeModel interiorClock = session.ConfigureTime(RealSecondsPerGameDay, RescueDay);

            Assert.That(interiorClock, Is.SameAs(surfaceClock),
                "Every scene must drive the same clock, or entering the pod would rewind the expedition.");
            Assert.That(interiorClock.CurrentDay, Is.EqualTo(4));
            Assert.That(interiorClock.DaysUntilRescue, Is.EqualTo(RescueDay - 4));
        }

        [Test]
        public void Reset_StartsANewExpeditionOnDayOneWithTheSameDayLength()
        {
            var session = new GameSessionState();
            session.ConfigureTime(RealSecondsPerGameDay, RescueDay).Advance(RealSecondsPerGameDay * 5d);

            session.Reset();

            Assert.That(session.Time.CurrentDay, Is.EqualTo(1));
            Assert.That(session.Time.RescueDay, Is.EqualTo(RescueDay));
            Assert.That(session.Time.ElapsedDays, Is.Zero);
        }

        [Test]
        public void LandingStock_LeavesWaterAsTheFirstResourceToRunOut()
        {
            var session = new GameSessionState();

            Assert.That(session.LandingPodWaterSupply.CurrentMilliliters,
                Is.EqualTo(GameSessionState.InitialLandingPodWaterMilliliters));
            Assert.That(session.LandingPodWaterSupply.CapacityMilliliters,
                Is.EqualTo(GameSessionState.LandingPodWaterCapacityMilliliters));
            Assert.That(session.LandingPodWaterSupply.RemainingCapacityMilliliters, Is.GreaterThan(0),
                "The reserve must have room left, or potable water could never be poured into it.");
        }

        [Test]
        public void Reset_RefillsTheReserveAndLeavesTheLoopFixturesEmpty()
        {
            var session = new GameSessionState();
            session.LandingPodWaterSupply.TryConsume(1000);

            session.Reset();

            Assert.That(session.WaterProcessor.State, Is.EqualTo(WaterProcessorState.Idle));
            Assert.That(session.Hydroponics.Slots.Count, Is.EqualTo(GameSessionState.HydroponicsSlotCount));
            Assert.That(session.Hydroponics.FirstEmptySlot(), Is.SameAs(session.Hydroponics.Slots[0]));
            Assert.That(session.LandingPodWaterSupply.CurrentMilliliters,
                Is.EqualTo(GameSessionState.InitialLandingPodWaterMilliliters));
        }
    }
}
