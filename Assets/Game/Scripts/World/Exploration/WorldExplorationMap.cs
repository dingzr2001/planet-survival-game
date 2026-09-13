using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.World.Exploration
{
    /// <summary>
    /// Expedition-owned record of surface cells the player has seen. It contains no presentation state,
    /// so the explored area survives trips into the landing pod and can be reset with the expedition.
    /// </summary>
    public sealed class WorldExplorationMap
    {
        public const float DefaultCellSize = 2f;

        private readonly HashSet<Vector2Int> _exploredCells = new();

        public WorldExplorationMap(float cellSize = DefaultCellSize)
        {
            if (float.IsNaN(cellSize) || float.IsInfinity(cellSize) || cellSize <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), "Exploration cell size must be finite and positive.");
            }

            CellSize = cellSize;
        }

        public float CellSize { get; }
        public int ExploredCellCount => _exploredCells.Count;

        public bool Reveal(Vector3 worldPosition, float radius)
        {
            if (!IsFinite(worldPosition.x) || !IsFinite(worldPosition.z)
                || !IsFinite(radius) || radius < 0f)
            {
                return false;
            }

            int minimumX = Mathf.FloorToInt((worldPosition.x - radius) / CellSize);
            int maximumX = Mathf.FloorToInt((worldPosition.x + radius) / CellSize);
            int minimumY = Mathf.FloorToInt((worldPosition.z - radius) / CellSize);
            int maximumY = Mathf.FloorToInt((worldPosition.z + radius) / CellSize);
            float inclusionRadius = radius + CellSize * .5f;
            float squaredRadius = inclusionRadius * inclusionRadius;
            bool changed = false;

            for (int y = minimumY; y <= maximumY; y++)
            {
                float cellCenterZ = (y + .5f) * CellSize;
                for (int x = minimumX; x <= maximumX; x++)
                {
                    float cellCenterX = (x + .5f) * CellSize;
                    float deltaX = cellCenterX - worldPosition.x;
                    float deltaZ = cellCenterZ - worldPosition.z;
                    if (deltaX * deltaX + deltaZ * deltaZ <= squaredRadius)
                    {
                        changed |= _exploredCells.Add(new Vector2Int(x, y));
                    }
                }
            }

            return changed;
        }

        public bool IsExplored(float worldX, float worldZ)
        {
            if (!IsFinite(worldX) || !IsFinite(worldZ))
            {
                return false;
            }

            return _exploredCells.Contains(new Vector2Int(
                Mathf.FloorToInt(worldX / CellSize),
                Mathf.FloorToInt(worldZ / CellSize)));
        }

        public void Clear()
        {
            _exploredCells.Clear();
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
