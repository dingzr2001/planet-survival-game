using UnityEngine;

namespace PlanetSurvival.Building.Domain
{
    /// <summary>
    /// A rectangle of grid cells, anchored at its lower-left cell. Placement, blocking checks, and the
    /// preview highlight all speak in footprints, so a 2×2 oven is handled exactly like a 1×1 wall.
    /// </summary>
    public readonly struct BuildFootprint
    {
        public BuildFootprint(Vector2Int origin, Vector2Int size)
        {
            Origin = origin;
            Size = new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
        }

        public Vector2Int Origin { get; }
        public Vector2Int Size { get; }

        /// <summary>The last cell the footprint covers, inclusive.</summary>
        public Vector2Int Max => new(Origin.x + Size.x - 1, Origin.y + Size.y - 1);

        public int CellCount => Size.x * Size.y;

        public bool Contains(Vector2Int cell)
        {
            return cell.x >= Origin.x && cell.x <= Max.x && cell.y >= Origin.y && cell.y <= Max.y;
        }

        public bool Overlaps(in BuildFootprint other)
        {
            return Origin.x <= other.Max.x && other.Origin.x <= Max.x &&
                   Origin.y <= other.Max.y && other.Origin.y <= Max.y;
        }

        /// <summary>
        /// True when the two rectangles share at least one cell edge without overlapping. Corner contact
        /// does not count: a pole touching a machine only diagonally is not wired to it.
        /// </summary>
        public bool IsEdgeAdjacentTo(in BuildFootprint other)
        {
            if (Overlaps(other))
            {
                return false;
            }

            bool columnsOverlap = Origin.x <= other.Max.x && other.Origin.x <= Max.x;
            bool rowsOverlap = Origin.y <= other.Max.y && other.Origin.y <= Max.y;
            bool touchesVertically = Max.y + 1 == other.Origin.y || other.Max.y + 1 == Origin.y;
            bool touchesHorizontally = Max.x + 1 == other.Origin.x || other.Max.x + 1 == Origin.x;
            return (columnsOverlap && touchesVertically) || (rowsOverlap && touchesHorizontally);
        }

        /// <summary>Enumerates the covered cells in row order, so callers can index without allocating.</summary>
        public Vector2Int CellAt(int index)
        {
            int clamped = Mathf.Clamp(index, 0, CellCount - 1);
            return new Vector2Int(Origin.x + clamped % Size.x, Origin.y + clamped / Size.x);
        }

        public override string ToString()
        {
            return $"({Origin.x},{Origin.y}) {Size.x}×{Size.y}";
        }
    }
}
