using System;
using System.Collections.Generic;
using PlanetSurvival.World.Grid;

namespace PlanetSurvival.Gathering.Runtime
{
    public static class ResourceSpawnPlanner
    {
        public static IReadOnlyList<GridCoordinate> Plan(int width, int length, int count, float minimumSpacingInCells, int seed)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            if (count <= 0) return Array.Empty<GridCoordinate>();

            var candidates = new List<GridCoordinate>(width * length);
            for (int x = 0; x < width; x++)
            for (int z = 0; z < length; z++)
                candidates.Add(new GridCoordinate(x, z));

            var random = new Random(seed);
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                (candidates[i], candidates[swapIndex]) = (candidates[swapIndex], candidates[i]);
            }

            float minimumSqrDistance = Math.Max(0f, minimumSpacingInCells) * Math.Max(0f, minimumSpacingInCells);
            var result = new List<GridCoordinate>(Math.Min(count, candidates.Count));
            for (int i = 0; i < candidates.Count && result.Count < count; i++)
            {
                GridCoordinate candidate = candidates[i];
                bool valid = true;
                for (int j = 0; j < result.Count; j++)
                {
                    float dx = candidate.X - result[j].X;
                    float dz = candidate.Z - result[j].Z;
                    if (dx * dx + dz * dz < minimumSqrDistance)
                    {
                        valid = false;
                        break;
                    }
                }

                if (valid) result.Add(candidate);
            }

            return result;
        }
    }
}
