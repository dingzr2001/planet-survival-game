using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.World.Generation.Landforms
{
    /// <summary>
    /// Measures how much open ground can be walked to from one point. Pass corridors and crater gaps are
    /// supposed to keep mountains from walling anything off; this is how the terrain preview and the tests
    /// check that promise for a given seed.
    /// </summary>
    public static class LandformReachability
    {
        /// <summary>
        /// Share of the open one-metre cells in a square of <paramref name="size"/> metres centred on
        /// <paramref name="start"/> that four-way steps can reach from the start cell. Zero when the start
        /// itself is blocked.
        /// </summary>
        public static float ReachableOpenShare(Func<float, float, bool> isBlocked, Vector2 start, int size)
        {
            if (isBlocked == null)
            {
                throw new ArgumentNullException(nameof(isBlocked));
            }

            int safeSize = Mathf.Max(1, size);
            float originX = Mathf.Floor(start.x - safeSize * .5f);
            float originZ = Mathf.Floor(start.y - safeSize * .5f);
            var open = new bool[safeSize * safeSize];
            int openCount = 0;
            for (int z = 0; z < safeSize; z++)
            {
                for (int x = 0; x < safeSize; x++)
                {
                    bool isOpen = !isBlocked(originX + x + .5f, originZ + z + .5f);
                    open[z * safeSize + x] = isOpen;
                    openCount += isOpen ? 1 : 0;
                }
            }

            int startX = Mathf.Clamp(Mathf.FloorToInt(start.x - originX), 0, safeSize - 1);
            int startZ = Mathf.Clamp(Mathf.FloorToInt(start.y - originZ), 0, safeSize - 1);
            int startIndex = startZ * safeSize + startX;
            if (openCount == 0 || !open[startIndex])
            {
                return 0f;
            }

            var visited = new bool[safeSize * safeSize];
            var frontier = new Queue<int>();
            frontier.Enqueue(startIndex);
            visited[startIndex] = true;
            int reached = 0;
            while (frontier.Count > 0)
            {
                int cell = frontier.Dequeue();
                reached++;
                int x = cell % safeSize;
                int z = cell / safeSize;
                Visit(x - 1, z);
                Visit(x + 1, z);
                Visit(x, z - 1);
                Visit(x, z + 1);
            }

            return reached / (float)openCount;

            void Visit(int x, int z)
            {
                if (x < 0 || z < 0 || x >= safeSize || z >= safeSize)
                {
                    return;
                }

                int index = z * safeSize + x;
                if (open[index] && !visited[index])
                {
                    visited[index] = true;
                    frontier.Enqueue(index);
                }
            }
        }
    }
}
