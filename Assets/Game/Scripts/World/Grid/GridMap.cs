using System;
using System.Collections.Generic;

namespace PlanetSurvival.World.Grid
{
    public sealed class GridMap
    {
        private readonly GridCell[,] _cells;

        public GridMap(int width, int length)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            Width = width;
            Length = length;
            _cells = new GridCell[width, length];
        }

        public int Width { get; }
        public int Length { get; }

        public GridCell this[int x, int z]
        {
            get => _cells[x, z];
            set => _cells[x, z] = value;
        }

        public bool Contains(GridCoordinate coordinate)
        {
            return coordinate.X >= 0 && coordinate.X < Width && coordinate.Z >= 0 && coordinate.Z < Length;
        }

        public IEnumerable<GridCell> GetCells()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int z = 0; z < Length; z++)
                {
                    yield return _cells[x, z];
                }
            }
        }
    }
}
