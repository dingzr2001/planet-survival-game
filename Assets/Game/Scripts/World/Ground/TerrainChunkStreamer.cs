using System.Collections.Generic;
using PlanetSurvival.World.Chunks;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// Keeps the terrain blocks around a target drawn and drops the ones it left behind. Terrain is
    /// generated from the world seed and the dug tiles the <see cref="TerrainTileMap"/> remembers, so an
    /// unloaded block comes back exactly as the player left it, holes included.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainChunkStreamer : MonoBehaviour
    {
        // Blocks are only released one ring beyond the load radius so walking a border does not thrash them.
        private const int UnloadMargin = 1;

        [SerializeField] private TerrainPatchSettings _settings;

        private readonly Dictionary<ChunkCoordinate, TerrainChunkView> _loadedChunks = new();
        private readonly HashSet<ChunkCoordinate> _dirtyChunks = new();
        private readonly List<ChunkCoordinate> _chunkBuffer = new();
        private TerrainTileMap _map;
        private Transform _target;
        private ChunkCoordinate _center;
        private bool _hasCenter;

        public int LoadedChunkCount => _loadedChunks.Count;

        public void Configure(TerrainPatchSettings settings, TerrainTileMap map)
        {
            UnsubscribeFromMap();
            UnloadAll();
            _settings = settings;
            _map = map;
            if (_map != null)
            {
                _map.TileChanged += OnTileChanged;
            }

            RefreshAroundTarget(true);
        }

        /// <summary>Sets the transform the drawn area follows; passing null suspends streaming.</summary>
        public void SetTarget(Transform target)
        {
            _target = target;
            RefreshAroundTarget(true);
        }

        private void Update()
        {
            RefreshAroundTarget(false);
            RebuildDirtyChunks();
        }

        private void OnDestroy()
        {
            UnsubscribeFromMap();
        }

        private void OnTileChanged(TerrainTileCoordinate tile)
        {
            // Tiles carry their own quads rather than sharing vertices, so a changed tile only ever
            // invalidates the block it belongs to, even on a block border.
            _dirtyChunks.Add(ChunkOf(tile));
        }

        private void RefreshAroundTarget(bool force)
        {
            if (_target == null || _settings == null || _map == null)
            {
                return;
            }

            Vector3 position = _target.position;
            ChunkCoordinate center = ChunkCoordinate.FromWorld(position.x, position.z, _settings.ChunkSize);
            if (!force && _hasCenter && center.Equals(_center))
            {
                return;
            }

            _center = center;
            _hasCenter = true;
            int loadRadius = _settings.LoadRadiusInChunks;
            UnloadBeyond(center, loadRadius + UnloadMargin);

            for (int x = center.X - loadRadius; x <= center.X + loadRadius; x++)
            {
                for (int z = center.Z - loadRadius; z <= center.Z + loadRadius; z++)
                {
                    var chunk = new ChunkCoordinate(x, z);
                    if (!_loadedChunks.ContainsKey(chunk))
                    {
                        Load(chunk);
                    }
                }
            }
        }

        private void RebuildDirtyChunks()
        {
            if (_dirtyChunks.Count == 0)
            {
                return;
            }

            _chunkBuffer.Clear();
            _chunkBuffer.AddRange(_dirtyChunks);
            _dirtyChunks.Clear();
            for (int i = 0; i < _chunkBuffer.Count; i++)
            {
                if (_loadedChunks.TryGetValue(_chunkBuffer[i], out TerrainChunkView view) && view != null)
                {
                    view.Rebuild(_map, OriginTileOf(_chunkBuffer[i]), _settings.ChunkSizeInTiles);
                }
            }

            _chunkBuffer.Clear();
        }

        private void Load(ChunkCoordinate chunk)
        {
            var root = new GameObject($"Terrain Chunk {chunk}");
            root.transform.SetParent(transform);
            root.transform.position = new Vector3(
                chunk.OriginX(_settings.ChunkSize),
                TerrainChunkView.SurfaceHeight,
                chunk.OriginZ(_settings.ChunkSize));
            TerrainChunkView view = root.AddComponent<TerrainChunkView>();
            view.Rebuild(_map, OriginTileOf(chunk), _settings.ChunkSizeInTiles);
            _loadedChunks.Add(chunk, view);
        }

        private void UnloadBeyond(ChunkCoordinate center, int unloadRadius)
        {
            _chunkBuffer.Clear();
            foreach (KeyValuePair<ChunkCoordinate, TerrainChunkView> entry in _loadedChunks)
            {
                if (entry.Key.RingDistanceTo(center) > unloadRadius)
                {
                    _chunkBuffer.Add(entry.Key);
                }
            }

            for (int i = 0; i < _chunkBuffer.Count; i++)
            {
                Unload(_chunkBuffer[i]);
            }

            _chunkBuffer.Clear();
        }

        private void Unload(ChunkCoordinate chunk)
        {
            if (!_loadedChunks.TryGetValue(chunk, out TerrainChunkView view))
            {
                return;
            }

            _loadedChunks.Remove(chunk);
            _dirtyChunks.Remove(chunk);
            if (view != null)
            {
                DestroyRuntimeObject(view.gameObject);
            }
        }

        private void UnloadAll()
        {
            _chunkBuffer.Clear();
            _chunkBuffer.AddRange(_loadedChunks.Keys);
            for (int i = 0; i < _chunkBuffer.Count; i++)
            {
                Unload(_chunkBuffer[i]);
            }

            _chunkBuffer.Clear();
            _hasCenter = false;
        }

        private void UnsubscribeFromMap()
        {
            if (_map != null)
            {
                _map.TileChanged -= OnTileChanged;
            }
        }

        private ChunkCoordinate ChunkOf(TerrainTileCoordinate tile)
        {
            int size = _settings.ChunkSizeInTiles;
            return new ChunkCoordinate(FloorDivide(tile.X, size), FloorDivide(tile.Z, size));
        }

        private TerrainTileCoordinate OriginTileOf(ChunkCoordinate chunk)
        {
            int size = _settings.ChunkSizeInTiles;
            return new TerrainTileCoordinate(chunk.X * size, chunk.Z * size);
        }

        private static int FloorDivide(int value, int size)
        {
            int quotient = value / size;
            return value % size < 0 ? quotient - 1 : quotient;
        }

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
