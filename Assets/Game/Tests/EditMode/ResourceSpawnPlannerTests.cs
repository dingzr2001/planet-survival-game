using System.Collections.Generic;
using NUnit.Framework;
using PlanetSurvival.Gathering.Runtime;
using PlanetSurvival.World.Grid;

namespace PlanetSurvival.Tests
{
    public sealed class ResourceSpawnPlannerTests
    {
        [Test]
        public void Plan_SameSeed_ProducesSameCoordinates()
        {
            IReadOnlyList<GridCoordinate> first = ResourceSpawnPlanner.Plan(20, 12, 15, 2f, 1234);
            IReadOnlyList<GridCoordinate> second = ResourceSpawnPlanner.Plan(20, 12, 15, 2f, 1234);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Plan_RespectsMinimumSpacing()
        {
            IReadOnlyList<GridCoordinate> result = ResourceSpawnPlanner.Plan(20, 20, 30, 3f, 5);

            for (int i = 0; i < result.Count; i++)
            for (int j = i + 1; j < result.Count; j++)
            {
                float dx = result[i].X - result[j].X;
                float dz = result[i].Z - result[j].Z;
                Assert.That(dx * dx + dz * dz, Is.GreaterThanOrEqualTo(9f));
            }
        }
    }
}
