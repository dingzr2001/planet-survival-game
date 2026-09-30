using System.Collections.Generic;
using PlanetSurvival.World.Generation.Landforms;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// Solid colliders for the impassable landforms (mountains and lava lakes) inside one streamed terrain
    /// chunk. Neither is ever dug, so the colliders are built once when the chunk loads and live exactly as
    /// long as it does.
    /// </summary>
    /// <remarks>
    /// Blocking is decided on one-metre cells, the same lattice construction snaps to, so the wall the
    /// player walks into and the cells the build preview refuses are the same squares.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class TerrainChunkObstacles : MonoBehaviour
    {
        public const float CellSize = 1f;

        /// <summary>Taller than anything that walks, low enough to stay out of the camera's sort order.</summary>
        private const float ColliderHeight = 3f;

        private readonly List<RectInt> _rectangles = new();
        private readonly List<BoxCollider> _colliders = new();
        private bool[] _blocked = System.Array.Empty<bool>();

        public int ColliderCount => _colliders.Count;

        /// <summary>
        /// Rebuilds the colliders for the cells whose centres fall inside the chunk. The chunk's own transform
        /// is left untouched; colliders are placed in world space under it.
        /// </summary>
        public void Build(TerrainTileMap map, float chunkOriginX, float chunkOriginZ, float chunkSize)
        {
            Clear();
            if (map == null || map.Landforms == null || chunkSize <= 0f)
            {
                return;
            }

            int firstCellX = FirstCellIn(chunkOriginX);
            int firstCellZ = FirstCellIn(chunkOriginZ);
            int width = FirstCellIn(chunkOriginX + chunkSize) - firstCellX;
            int height = FirstCellIn(chunkOriginZ + chunkSize) - firstCellZ;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            if (_blocked.Length < width * height)
            {
                _blocked = new bool[width * height];
            }

            bool anyBlocked = false;
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool blocked = map.IsBlockedAt(
                        (firstCellX + x + .5f) * CellSize, (firstCellZ + z + .5f) * CellSize);
                    _blocked[z * width + x] = blocked;
                    anyBlocked |= blocked;
                }
            }

            if (!anyBlocked)
            {
                return;
            }

            BlockedCellRectangles.Merge(_blocked, width, height, _rectangles);
            for (int i = 0; i < _rectangles.Count; i++)
            {
                RectInt rectangle = _rectangles[i];
                var colliderObject = new GameObject("Impassable Ground Collider");
                colliderObject.transform.SetParent(transform, false);
                colliderObject.transform.position = new Vector3(
                    (firstCellX + rectangle.x + rectangle.width * .5f) * CellSize,
                    ColliderHeight * .5f,
                    (firstCellZ + rectangle.y + rectangle.height * .5f) * CellSize);
                colliderObject.transform.rotation = Quaternion.identity;
                colliderObject.transform.localScale = Vector3.one;
                BoxCollider box = colliderObject.AddComponent<BoxCollider>();
                box.size = new Vector3(rectangle.width * CellSize, ColliderHeight, rectangle.height * CellSize);
                _colliders.Add(box);
            }
        }

        private void Clear()
        {
            for (int i = 0; i < _colliders.Count; i++)
            {
                if (_colliders[i] != null)
                {
                    DestroyRuntimeObject(_colliders[i].gameObject);
                }
            }

            _colliders.Clear();
        }

        /// <summary>The first one-metre cell whose centre is at or beyond <paramref name="edge"/>.</summary>
        private static int FirstCellIn(float edge) => Mathf.CeilToInt(edge / CellSize - .5f);

        private static void DestroyRuntimeObject(GameObject instance)
        {
            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }
    }
}
