using NUnit.Framework;
using PlanetSurvival.Core.Time;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class GameClockTests
    {
        [Test]
        public void Advance_UpdatesDayTimeAndRescueCountdown()
        {
            var gameObject = new GameObject("Clock Test");
            GameClock clock = gameObject.AddComponent<GameClock>();
            clock.Configure(240f, 3);

            clock.Advance(150f);

            Assert.That(clock.CurrentDay, Is.EqualTo(1));
            Assert.That(clock.Hour, Is.EqualTo(15));
            Assert.That(clock.Minute, Is.EqualTo(0));
            Assert.That(clock.DaysUntilRescue, Is.EqualTo(2));
            Assert.That(clock.RescueAvailable, Is.False);
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void Advance_WhenMinuteChanges_RaisesTimeChangedOnce()
        {
            var gameObject = new GameObject("Clock Test");
            GameClock clock = gameObject.AddComponent<GameClock>();
            clock.Configure(1440f, 30);
            int changes = 0;
            clock.TimeChanged += () => changes++;

            clock.Advance(0.5f);
            clock.Advance(0.25f);
            clock.Advance(0.25f);

            Assert.That(changes, Is.EqualTo(2));
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void Model_PauseAndTimeScale_ProduceDeterministicElapsedTime()
        {
            var oneStep = new GameTimeModel(240d, 30, 2d);
            var splitSteps = new GameTimeModel(240d, 30, 2d);

            oneStep.Advance(60d);
            for (int i = 0; i < 60; i++) splitSteps.Advance(1d);
            splitSteps.SetPaused(true);
            Assert.That(splitSteps.Advance(100d), Is.Zero);

            Assert.That(splitSteps.ElapsedDays, Is.EqualTo(oneStep.ElapsedDays).Within(0.0000001d));
            Assert.That(oneStep.ElapsedDays, Is.EqualTo(0.5d).Within(0.0000001d));
        }

        [Test]
        public void Model_CrossingSeveralDays_ReportsFinalDayAndAccurateRemainder()
        {
            var model = new GameTimeModel(24d, 4);
            int reportedDay = 0;
            model.DayChanged += day => reportedDay = day;

            model.Advance(54d);

            Assert.That(model.CurrentDay, Is.EqualTo(3));
            Assert.That(model.Hour, Is.EqualTo(6));
            Assert.That(reportedDay, Is.EqualTo(3));
            Assert.That(model.DaysUntilRescue, Is.EqualTo(1));
        }
    }
}
