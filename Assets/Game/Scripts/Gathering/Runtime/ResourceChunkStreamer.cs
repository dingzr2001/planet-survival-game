using System;
using System.Collections.Generic;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.World.Chunks;
using PlanetSurvival.World.Generation;
using UnityEngine;

namespace PlanetSurvival.Gathering.Runtime
{
    /// <summary>
    /// Keeps the resource chunks around a target loaded and drops the ones the target left behind. Chunk layouts
    /// come from <see cref="ChunkResourcePlanner"/>, so an unloaded chunk rebuilds exactly as the player left it,
    /// minus the nodes that were already gathered.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ResourceChunkStreamer : MonoBehaviour
    {
        // Chunks are only released one ring beyond the load radius so walking along a border does not thrash them.
        private const int UnloadMargin = 1;

        [SerializeField] private ResourceSpawnSettings _settings;
        [SerializeField] private WorldVisualSettings _visuals;

        private readonly Dictionary<ChunkCoordinate, LoadedChunk> _loadedChunks = new();
        private readonly HashSet<NodeKey> _gatheredNodes = new();
        private readonly List<ChunkCoordinate> _unloadBuffer = new();
        private float[] _densities = Array.Empty<float>();
        private Transform _target;
        private int _worldSeed;
        private ChunkCoordinate _center;
        private bool _hasCenter;
        private Vector2 _spawnClearanceCenter;
        private float _squaredSpawnClearance;

        /// <param name="spawnClearanceCenter">
        /// Where the player starts. Nodes planned within the configured clearance radius of it are skipped, so the
        /// player never wakes up inside a rock. The point is fixed for the run and does not follow the target.
        /// </param>
        public void Configure(ResourceSpawnSettings settings, WorldVisualSettings visuals, int worldSeed,
            Vector3 spawnClearanceCenter)
        {
            _settings = settings;
            _visuals = visuals;
            _worldSeed = worldSeed;
            _densities = BuildDensities(settings);
            _spawnClearanceCenter = new Vector2(spawnClearanceCenter.x, spawnClearanceCenter.z);
            float clearance = settings != null ? settings.SpawnClearanceRadius : 0f;
            _squaredSpawnClearance = clearance * clearance;
            RefreshAroundTarget(true);
        }

        /// <summary>Sets the transform the loaded area follows; passing null suspends streaming.</summary>
        public void SetTarget(Transform target)
        {
            _target = target;
            RefreshAroundTarget(true);
        }

        private void Awake()
        {
            if (_densities.Length == 0)
            {
                _densities = BuildDensities(_settings);
            }
        }

        private void Update()
        {
            RefreshAroundTarget(false);
        }

        private void OnDestroy()
        {
            _loadedChunks.Clear();
        }

        private void RefreshAroundTarget(bool force)
        {
            if (_target == null || _settings == null || _densities.Length == 0)
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

        private void Load(ChunkCoordinate chunk)
        {
            IReadOnlyList<ChunkResourcePlacement> placements = ChunkResourcePlanner.Plan(
                chunk, _worldSeed, _settings.ChunkSize, _settings.MinimumSpacing, _densities);

            var root = new GameObject($"Resource Chunk {chunk}");
            root.transform.SetParent(transform);
            var loaded = new LoadedChunk(root);
            IReadOnlyList<ResourceSpawnEntry> entries = _settings.Entries;

            for (int i = 0; i < placements.Count; i++)
            {
                ChunkResourcePlacement placement = placements[i];
                // Skipping by placement index keeps the remaining indices stable, so gathered nodes stay identified.
                if (IsInsideSpawnClearance(placement) || _gatheredNodes.Contains(new NodeKey(chunk, i)))
                {
                    continue;
                }

                ResourceNodeDefinition definition = entries[placement.EntryIndex].Definition;
                ResourceNode node = ResourceNodeFactory.Create(root.transform, definition,
                    new Vector3(placement.WorldX, 0f, placement.WorldZ), _visuals);
                loaded.Add(i, node);
            }

            _loadedChunks.Add(chunk, loaded);
        }

        private bool IsInsideSpawnClearance(ChunkResourcePlacement placement)
        {
            if (_squaredSpawnClearance <= 0f)
            {
                return false;
            }

            float deltaX = placement.WorldX - _spawnClearanceCenter.x;
            float deltaZ = placement.WorldZ - _spawnClearanceCenter.y;
            return deltaX * deltaX + deltaZ * deltaZ < _squaredSpawnClearance;
        }

        private void UnloadBeyond(ChunkCoordinate center, int unloadRadius)
        {
            _unloadBuffer.Clear();
            foreach (KeyValuePair<ChunkCoordinate, LoadedChunk> entry in _loadedChunks)
            {
                if (entry.Key.RingDistanceTo(center) > unloadRadius)
                {
                    _unloadBuffer.Add(entry.Key);
                }
            }

            for (int i = 0; i < _unloadBuffer.Count; i++)
            {
                Unload(_unloadBuffer[i]);
            }

            _unloadBuffer.Clear();
        }

        private void Unload(ChunkCoordinate chunk)
        {
            if (!_loadedChunks.TryGetValue(chunk, out LoadedChunk loaded))
            {
                return;
            }

            // Remember the gathered nodes before the objects go away, otherwise revisiting the chunk would
            // hand the player the same resources again.
            IReadOnlyList<LoadedChunk.SpawnedNode> nodes = loaded.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                LoadedChunk.SpawnedNode spawned = nodes[i];
                if (spawned.Node != null && spawned.Node.IsDepleted)
                {
                    _gatheredNodes.Add(new NodeKey(chunk, spawned.PlacementIndex));
                }
            }

            _loadedChunks.Remove(chunk);
            if (loaded.Root != null)
            {
                Destroy(loaded.Root);
            }
        }

        /// <summary>Entries without a definition keep their index but never spawn, so layouts stay stable.</summary>
        private static float[] BuildDensities(ResourceSpawnSettings settings)
        {
            if (settings == null)
            {
                return Array.Empty<float>();
            }

            IReadOnlyList<ResourceSpawnEntry> entries = settings.Entries;
            var densities = new float[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                densities[i] = entries[i].Definition != null ? entries[i].NodesPerChunk : 0f;
            }

            return densities;
        }

        private sealed class LoadedChunk
        {
            private readonly List<SpawnedNode> _nodes = new();

            public LoadedChunk(GameObject root) => Root = root;

            public GameObject Root { get; }
            public IReadOnlyList<SpawnedNode> Nodes => _nodes;

            public void Add(int placementIndex, ResourceNode node) => _nodes.Add(new SpawnedNode(placementIndex, node));

            public readonly struct SpawnedNode
            {
                public SpawnedNode(int placementIndex, ResourceNode node)
                {
                    PlacementIndex = placementIndex;
                    Node = node;
                }

                public int PlacementIndex { get; }
                public ResourceNode Node { get; }
            }
        }

        private readonly struct NodeKey : IEquatable<NodeKey>
        {
            private readonly ChunkCoordinate _chunk;
            private readonly int _placementIndex;

            public NodeKey(ChunkCoordinate chunk, int placementIndex)
            {
                _chunk = chunk;
                _placementIndex = placementIndex;
            }

            public bool Equals(NodeKey other) => _chunk.Equals(other._chunk) && _placementIndex == other._placementIndex;
            public override bool Equals(object obj) => obj is NodeKey other && Equals(other);
            public override int GetHashCode() => (_chunk.GetHashCode() * 397) ^ _placementIndex;
        }
    }
}
