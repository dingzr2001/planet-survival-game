using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.World.Generation.Landforms
{
    /// <summary>
    /// Merges a square mask of blocked cells into as few axis-aligned rectangles as a greedy sweep finds.
    /// Mountains cover whole regions, so one collider per cell would mean hundreds of colliders per chunk;
    /// merged rows and columns usually bring that down to a handful.
    /// </summary>
    public static class BlockedCellRectangles
    {
        /// <param name="blocked">Row-major mask, <paramref name="width"/> cells per row.</param>
        /// <param name="results">Cleared, then filled with rectangles in cell units (x, y, width, height).</param>
        public static void Merge(bool[] blocked, int width, int height, List<RectInt> results)
        {
            if (blocked == null)
            {
                throw new ArgumentNullException(nameof(blocked));
            }

            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (width <= 0 || height <= 0 || blocked.Length < width * height)
            {
                throw new ArgumentException(
                    $"A {width}×{height} mask needs {width * height} cells but {blocked.Length} were given.",
                    nameof(blocked));
            }

            results.Clear();
            var consumed = new bool[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!IsOpen(blocked, consumed, width, x, y))
                    {
                        continue;
                    }

                    int runWidth = 1;
                    while (x + runWidth < width && IsOpen(blocked, consumed, width, x + runWidth, y))
                    {
                        runWidth++;
                    }

                    int runHeight = 1;
                    while (y + runHeight < height && IsRowOpen(blocked, consumed, width, x, y + runHeight, runWidth))
                    {
                        runHeight++;
                    }

                    for (int markY = y; markY < y + runHeight; markY++)
                    {
                        for (int markX = x; markX < x + runWidth; markX++)
                        {
                            consumed[markY * width + markX] = true;
                        }
                    }

                    results.Add(new RectInt(x, y, runWidth, runHeight));
                }
            }
        }

        private static bool IsOpen(bool[] blocked, bool[] consumed, int width, int x, int y)
        {
            int index = y * width + x;
            return blocked[index] && !consumed[index];
        }

        private static bool IsRowOpen(bool[] blocked, bool[] consumed, int width, int x, int y, int runWidth)
        {
            for (int i = 0; i < runWidth; i++)
            {
                if (!IsOpen(blocked, consumed, width, x + i, y))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
