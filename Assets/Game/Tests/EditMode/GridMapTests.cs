using NUnit.Framework;
using PlanetSurvival.World.Grid;

namespace PlanetSurvival.Tests
{
    public sealed class GridMapTests
    {
        [Test]
        public void Contains_RejectsCoordinatesOutsideSquareBoundary()
        {
            var map = new GridMap(10, 10);

            Assert.That(map.Contains(new GridCoordinate(0, 0)), Is.True);
            Assert.That(map.Contains(new GridCoordinate(9, 9)), Is.True);
            Assert.That(map.Contains(new GridCoordinate(-1, 0)), Is.False);
            Assert.That(map.Contains(new GridCoordinate(10, 5)), Is.False);
        }
    }
}
