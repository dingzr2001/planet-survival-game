using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.Building.Domain
{
    /// <summary>
    /// The placement lattice of the world: it converts world positions to cells, snaps a footprint under
    /// the cursor, and remembers which cells are already taken. Occupancy is keyed by the object that
    /// claimed it, so releasing a site never has to walk the whole map.
    /// </summary>
    public sealed class BuildGrid
    {
        public const float DefaultCellSize = 1f;

        private readonly Dictionary<Vector2Int, object> _cells = new();
        private readonly Dictionary<Vector2Int, object> _quarterCells = new();
        private readonly Dictionary<object, BuildFootprint> _occupants = new();
        private readonly Dictionary<object, Vector2Int> _quarterOccupants = new();
        private readonly float _cellSize;
        private readonly Vector3 _origin;

        public BuildGrid(float cellSize = DefaultCellSize)
            : this(cellSize, Vector3.zero)
        {
        }

        public BuildGrid(float cellSize, Vector3 origin)
        {
            _cellSize = Mathf.Max(.05f, cellSize);
            _origin = origin;
        }

        public float CellSize => _cellSize;
        public Vector3 Origin => _origin;
        public int OccupiedCellCount => _cells.Count;
        public int OccupiedQuarterCellCount => _quarterCells.Count;

        public Vector2Int WorldToCell(Vector3 world)
        {
            return new Vector2Int(
                Mathf.FloorToInt((world.x - _origin.x) / _cellSize),
                Mathf.FloorToInt((world.z - _origin.z) / _cellSize));
        }

        /// <summary>The world position of a cell's centre, on the grid plane.</summary>
        public Vector3 CellCenter(Vector2Int cell)
        {
            return new Vector3(
                _origin.x + (cell.x + .5f) * _cellSize,
                _origin.y,
                _origin.z + (cell.y + .5f) * _cellSize);
        }

        /// <summary>Converts a position to one of the four half-size cells inside a normal build cell.</summary>
        public Vector2Int WorldToQuarterCell(Vector3 world)
        {
            float quarterSize = _cellSize * .5f;
            return new Vector2Int(
                Mathf.FloorToInt((world.x - _origin.x) / quarterSize),
                Mathf.FloorToInt((world.z - _origin.z) / quarterSize));
        }

        public Vector3 QuarterCellCenter(Vector2Int quarterCell)
        {
            float quarterSize = _cellSize * .5f;
            return new Vector3(
                _origin.x + (quarterCell.x + .5f) * quarterSize,
                _origin.y,
                _origin.z + (quarterCell.y + .5f) * quarterSize);
        }

        /// <summary>The world position a footprint's object sits at: the centre of the covered rectangle.</summary>
        public Vector3 Center(in BuildFootprint footprint)
        {
            return new Vector3(
                _origin.x + (footprint.Origin.x + footprint.Size.x * .5f) * _cellSize,
                _origin.y,
                _origin.z + (footprint.Origin.y + footprint.Size.y * .5f) * _cellSize);
        }

        /// <summary>
        /// Snaps a footprint of the given size under a world position. The cursor cell stays inside the
        /// footprint, biased towards its lower-left corner for even sizes, so dragging feels anchored to
        /// the cell the player is pointing at.
        /// </summary>
        public BuildFootprint CreateFootprint(Vector3 world, Vector2Int size)
        {
            Vector2Int safeSize = new(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            Vector2Int cursor = WorldToCell(world);
            var origin = new Vector2Int(
                cursor.x - (safeSize.x - 1) / 2,
                cursor.y - (safeSize.y - 1) / 2);
            return new BuildFootprint(origin, safeSize);
        }

        /// <summary>
        /// Projects a continuous world-space rectangle onto every construction cell it touches. Natural
        /// obstacles keep their free position and size; this projection exists only so snapped buildings
        /// cannot be placed through them.
        /// </summary>
        public BuildFootprint CreateCoveringFootprint(Vector3 worldCenter, Vector2 worldSize)
        {
            Vector2 safeSize = new(Mathf.Max(.01f, worldSize.x), Mathf.Max(.01f, worldSize.y));
            float epsilon = _cellSize * .0001f;
            var minimum = new Vector3(
                worldCenter.x - safeSize.x * .5f + epsilon,
                _origin.y,
                worldCenter.z - safeSize.y * .5f + epsilon);
            var maximum = new Vector3(
                worldCenter.x + safeSize.x * .5f - epsilon,
                _origin.y,
                worldCenter.z + safeSize.y * .5f - epsilon);
            Vector2Int minimumCell = WorldToCell(minimum);
            Vector2Int maximumCell = WorldToCell(maximum);
            var size = new Vector2Int(
                Mathf.Max(1, maximumCell.x - minimumCell.x + 1),
                Mathf.Max(1, maximumCell.y - minimumCell.y + 1));
            return new BuildFootprint(minimumCell, size);
        }

        public bool IsFree(in BuildFootprint footprint)
        {
            return IsFree(footprint, null);
        }

        /// <summary>Free for <paramref name="ignoredOccupant"/>, which may re-claim the cells it already holds.</summary>
        public bool IsFree(in BuildFootprint footprint, object ignoredOccupant)
        {
            for (int i = 0; i < footprint.CellCount; i++)
            {
                Vector2Int cell = footprint.CellAt(i);
                if (_cells.TryGetValue(cell, out object occupant) &&
                    !ReferenceEquals(occupant, ignoredOccupant))
                {
                    return false;
                }

                Vector2Int firstQuarter = cell * 2;
                for (int z = 0; z < 2; z++)
                {
                    for (int x = 0; x < 2; x++)
                    {
                        if (_quarterCells.TryGetValue(firstQuarter + new Vector2Int(x, z), out occupant) &&
                            !ReferenceEquals(occupant, ignoredOccupant))
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public bool IsQuarterCellFree(Vector2Int quarterCell, object ignoredOccupant = null)
        {
            if (_quarterCells.TryGetValue(quarterCell, out object quarterOccupant) &&
                !ReferenceEquals(quarterOccupant, ignoredOccupant))
            {
                return false;
            }

            var containingCell = new Vector2Int(
                Mathf.FloorToInt(quarterCell.x / 2f),
                Mathf.FloorToInt(quarterCell.y / 2f));
            return !_cells.TryGetValue(containingCell, out object occupant) ||
                   ReferenceEquals(occupant, ignoredOccupant);
        }

        public bool TryOccupyQuarterCell(Vector2Int quarterCell, object occupant)
        {
            if (occupant == null || !IsQuarterCellFree(quarterCell, occupant))
            {
                return false;
            }

            Release(occupant);
            _quarterCells[quarterCell] = occupant;
            _quarterOccupants[occupant] = quarterCell;
            return true;
        }

        public bool TryOccupy(in BuildFootprint footprint, object occupant)
        {
            if (occupant == null || !IsFree(footprint, occupant))
            {
                return false;
            }

            Release(occupant);
            for (int i = 0; i < footprint.CellCount; i++)
            {
                _cells[footprint.CellAt(i)] = occupant;
            }

            _occupants[occupant] = footprint;
            return true;
        }

        public void Release(object occupant)
        {
            if (occupant == null)
            {
                return;
            }

            if (_quarterOccupants.TryGetValue(occupant, out Vector2Int quarterCell))
            {
                if (_quarterCells.TryGetValue(quarterCell, out object current) && ReferenceEquals(current, occupant))
                {
                    _quarterCells.Remove(quarterCell);
                }

                _quarterOccupants.Remove(occupant);
            }

            if (!_occupants.TryGetValue(occupant, out BuildFootprint footprint))
            {
                return;
            }

            for (int i = 0; i < footprint.CellCount; i++)
            {
                Vector2Int cell = footprint.CellAt(i);
                if (_cells.TryGetValue(cell, out object current) && ReferenceEquals(current, occupant))
                {
                    _cells.Remove(cell);
                }
            }

            _occupants.Remove(occupant);
        }

        public object GetOccupant(Vector2Int cell)
        {
            return _cells.TryGetValue(cell, out object occupant) ? occupant : null;
        }

        public object GetQuarterCellOccupant(Vector2Int quarterCell)
        {
            return _quarterCells.TryGetValue(quarterCell, out object occupant) ? occupant : null;
        }

        public bool TryGetQuarterCell(object occupant, out Vector2Int quarterCell)
        {
            if (occupant != null)
            {
                return _quarterOccupants.TryGetValue(occupant, out quarterCell);
            }

            quarterCell = default;
            return false;
        }

        public bool TryGetFootprint(object occupant, out BuildFootprint footprint)
        {
            if (occupant != null)
            {
                return _occupants.TryGetValue(occupant, out footprint);
            }

            footprint = default;
            return false;
        }

        public void Clear()
        {
            _cells.Clear();
            _occupants.Clear();
            _quarterCells.Clear();
            _quarterOccupants.Clear();
        }
    }
}
