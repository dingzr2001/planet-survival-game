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
    }
}
