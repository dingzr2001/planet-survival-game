using NUnit.Framework;
using PlanetSurvival.World.Exploration;
using UnityEngine;

namespace PlanetSurvival.Tests
{
    public sealed class WorldExplorationMapTests
    {
        [Test]
        public void Reveal_OnlyDiscoversCellsInsideThePlayersVision()
        {
            var exploration = new WorldExplorationMap(2f);

            bool changed = exploration.Reveal(Vector3.zero, 10f);

            Assert.That(changed, Is.True);
            Assert.That(exploration.IsExplored(0f, 0f), Is.True);
            Assert.That(exploration.IsExplored(30f, 0f), Is.False);
        }

        [Test]
        public void Reveal_AfterMoving_KeepsPreviouslyExploredCells()
        {
            var exploration = new WorldExplorationMap(2f);
            exploration.Reveal(Vector3.zero, 6f);

            exploration.Reveal(new Vector3(40f, 0f, 0f), 6f);

            Assert.That(exploration.IsExplored(0f, 0f), Is.True);
            Assert.That(exploration.IsExplored(40f, 0f), Is.True);
            Assert.That(exploration.IsExplored(20f, 0f), Is.False);
        }

        [Test]
        public void Clear_ForANewExpedition_RemovesExplorationHistory()
        {
            var exploration = new WorldExplorationMap();
            exploration.Reveal(Vector3.zero, 10f);

            exploration.Clear();

            Assert.That(exploration.ExploredCellCount, Is.Zero);
            Assert.That(exploration.IsExplored(0f, 0f), Is.False);
        }
    }
}
