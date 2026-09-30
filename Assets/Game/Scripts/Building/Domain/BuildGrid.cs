using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.Building.Domain
{
    /// <summary>
    /// The placement lattice of the world: it converts world positions to cells, snaps a footprint under
    /// the cursor, and remembers what is already taken. Snapped structures claim whole cells; free-standing
    /// objects claim an exact world rectangle, so small items can sit anywhere without wasting a cell.
    /// Occupancy is keyed by the object that claimed it, so releasing a site never has to walk the map.
    /// </summary>
    public sealed class BuildGrid
    {
        public const float DefaultCellSize = 1f;

        private readonly Dictionary<Vector2Int, object> _cells = new();
        private readonly Dictionary<object, BuildFootprint> _occupants = new();

        // Free rectangles are indexed by every cell they touch, so a query only inspects its own cells.
        private readonly Dictionary<Vector2Int, List<object>> _areaCells = new();
        private readonly Dictionary<object, Rect> _areaOccupants = new();
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
        public int FreeAreaCount => _areaOccupants.Count;

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

        /// <summary>The world position a footprint's object sits at: the centre of the covered rectangle.</summary>
        public Vector3 Center(in BuildFootprint footprint)
        {
            return new Vector3(
                _origin.x + (footprint.Origin.x + footprint.Size.x * .5f) * _cellSize,
                _origin.y,
                _origin.z + (footprint.Origin.y + footprint.Size.y * .5f) * _cellSize);
        }

        /// <summary>The footprint's extent on the XZ plane, as a rectangle whose y axis is world Z.</summary>
        public Rect WorldRect(in BuildFootprint footprint)
        {
            return new Rect(
                _origin.x + footprint.Origin.x * _cellSize,
                _origin.z + footprint.Origin.y * _cellSize,
                footprint.Size.x * _cellSize,
                footprint.Size.y * _cellSize);
        }

        /// <summary>
        /// Snaps a footprint of the given size under a world position, centred on the cursor the way
        /// Factorio does: an odd edge centres on the cell under the cursor, an even edge on the nearest grid
        /// line. Every size then tracks the cursor symmetrically instead of lagging towards one corner.
        /// </summary>
        public BuildFootprint CreateFootprint(Vector3 world, Vector2Int size)
        {
            Vector2Int safeSize = new(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
            float cursorX = (world.x - _origin.x) / _cellSize;
            float cursorZ = (world.z - _origin.z) / _cellSize;
            var origin = new Vector2Int(
                Mathf.FloorToInt(cursorX - safeSize.x * .5f + .5f),
                Mathf.FloorToInt(cursorZ - safeSize.y * .5f + .5f));
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
            return CoveringFootprint(new Rect(
                worldCenter.x - safeSize.x * .5f, worldCenter.z - safeSize.y * .5f, safeSize.x, safeSize.y));
        }

        public bool IsFree(in BuildFootprint footprint)
        {
            return IsFree(footprint, null);
        }

        /// <summary>Free for <paramref name="ignoredOccupant"/>, which may re-claim what it already holds.</summary>
        public bool IsFree(in BuildFootprint footprint, object ignoredOccupant)
        {
            for (int i = 0; i < footprint.CellCount; i++)
            {
                if (_cells.TryGetValue(footprint.CellAt(i), out object occupant) &&
                    !ReferenceEquals(occupant, ignoredOccupant))
                {
                    return false;
                }
            }

            return !OverlapsFreeArea(footprint, WorldRect(footprint), ignoredOccupant);
        }

        /// <summary>
        /// Whether a free-standing object may occupy <paramref name="worldArea"/> (x/y = world X/Z). Snapped
        /// structures fill their whole cells, so any claimed cell the area touches blocks it; other free
        /// objects block only where their rectangles actually overlap.
        /// </summary>
        public bool IsAreaFree(Rect worldArea, object ignoredOccupant = null)
        {
            BuildFootprint touched = CoveringFootprint(worldArea);
            for (int i = 0; i < touched.CellCount; i++)
            {
                if (_cells.TryGetValue(touched.CellAt(i), out object occupant) &&
                    !ReferenceEquals(occupant, ignoredOccupant))
                {
                    return false;
                }
            }

            return !OverlapsFreeArea(touched, worldArea, ignoredOccupant);
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

        public bool TryOccupyArea(Rect worldArea, object occupant)
        {
            if (occupant == null || worldArea.width <= 0f || worldArea.height <= 0f ||
                !IsAreaFree(worldArea, occupant))
            {
                return false;
            }

            Release(occupant);
            BuildFootprint touched = CoveringFootprint(worldArea);
            for (int i = 0; i < touched.CellCount; i++)
            {
                Vector2Int cell = touched.CellAt(i);
                if (!_areaCells.TryGetValue(cell, out List<object> occupants))
                {
                    occupants = new List<object>(1);
                    _areaCells.Add(cell, occupants);
                }

                occupants.Add(occupant);
            }

            _areaOccupants[occupant] = worldArea;
            return true;
        }

        public void Release(object occupant)
        {
            if (occupant == null)
            {
                return;
            }

            if (_areaOccupants.TryGetValue(occupant, out Rect area))
            {
                BuildFootprint touched = CoveringFootprint(area);
                for (int i = 0; i < touched.CellCount; i++)
                {
                    Vector2Int cell = touched.CellAt(i);
                    if (_areaCells.TryGetValue(cell, out List<object> occupants))
                    {
                        occupants.Remove(occupant);
                        if (occupants.Count == 0)
                        {
                            _areaCells.Remove(cell);
                        }
                    }
                }

                _areaOccupants.Remove(occupant);
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

        public bool TryGetFootprint(object occupant, out BuildFootprint footprint)
        {
            if (occupant != null)
            {
                return _occupants.TryGetValue(occupant, out footprint);
            }

            footprint = default;
            return false;
        }

        public bool TryGetArea(object occupant, out Rect area)
        {
            if (occupant != null)
            {
                return _areaOccupants.TryGetValue(occupant, out area);
            }

            area = default;
            return false;
        }

        public void Clear()
        {
            _cells.Clear();
            _occupants.Clear();
            _areaCells.Clear();
            _areaOccupants.Clear();
        }

        private BuildFootprint CoveringFootprint(Rect worldArea)
        {
            // Pulled in by a hair so a rectangle ending exactly on a grid line does not claim the next cell.
            float epsilon = _cellSize * .0001f;
            Vector2Int minimum = WorldToCell(new Vector3(worldArea.xMin + epsilon, 0f, worldArea.yMin + epsilon));
            Vector2Int maximum = WorldToCell(new Vector3(worldArea.xMax - epsilon, 0f, worldArea.yMax - epsilon));
            var size = new Vector2Int(
                Mathf.Max(1, maximum.x - minimum.x + 1),
                Mathf.Max(1, maximum.y - minimum.y + 1));
            return new BuildFootprint(minimum, size);
        }

        private bool OverlapsFreeArea(in BuildFootprint cells, Rect worldArea, object ignoredOccupant)
        {
            if (_areaOccupants.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < cells.CellCount; i++)
            {
                if (!_areaCells.TryGetValue(cells.CellAt(i), out List<object> occupants))
                {
                    continue;
                }

                for (int j = 0; j < occupants.Count; j++)
                {
                    object occupant = occupants[j];
                    if (!ReferenceEquals(occupant, ignoredOccupant) &&
                        _areaOccupants[occupant].Overlaps(worldArea))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
