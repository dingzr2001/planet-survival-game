using System;
using System.Collections.Generic;
using PlanetSurvival.Building.Domain;
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
        private Transform _target;
        private BuildGrid _buildGrid;
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
            Vector3 spawnClearanceCenter, BuildGrid buildGrid = null)
        {
            ReleaseBuildReservations();
            _settings = settings;
            _visuals = visuals;
            _worldSeed = worldSeed;
            _buildGrid = buildGrid;
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

        private void Update()
        {
            RefreshAroundTarget(false);
        }

        private void OnDestroy()
        {
            ReleaseBuildReservations();
            _loadedChunks.Clear();
        }

        private void RefreshAroundTarget(bool force)
        {
            if (_target == null || _settings == null || _settings.Entries.Count == 0)
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
            IReadOnlyList<ChunkResourcePlacement> placements = ChunkResourcePlanner.PlanResources(
                chunk, _worldSeed, _settings.ChunkSize, _settings.MinimumSpacing, _settings.Entries);

            var root = new GameObject($"Resource Chunk {chunk}");
            root.transform.SetParent(transform);
            var loaded = new LoadedChunk(root);
            IReadOnlyList<ResourceSpawnEntry> entries = _settings.Entries;

            for (int i = 0; i < placements.Count; i++)
            {
                ChunkResourcePlacement placement = placements[i];
                ResourceNodeDefinition definition = entries[placement.EntryIndex].Definition;
                if (definition == null || IsInsideSpawnClearance(placement, definition) ||
                    _gatheredNodes.Contains(new NodeKey(chunk, placement.PlacementId)))
                {
                    continue;
                }

                var position = new Vector3(placement.WorldX, 0f, placement.WorldZ);
                ResourceNode node = ResourceNodeFactory.Create(
                    root.transform, definition, position, _visuals, placement.VariantSeed);
                if (!TryReserveBuildCells(node, position, definition.SelectWorldFootprint(placement.VariantSeed)))
                {
                    Destroy(node.gameObject);
                    continue;
                }

                loaded.Add(placement.PlacementId, node);
            }

            _loadedChunks.Add(chunk, loaded);
        }

        private bool IsInsideSpawnClearance(ChunkResourcePlacement placement, ResourceNodeDefinition definition)
        {
            if (_squaredSpawnClearance <= 0f)
            {
                return false;
            }

            Vector2 footprint = definition.SelectWorldFootprint(placement.VariantSeed);
            float deltaX = Mathf.Max(0f,
                Mathf.Abs(placement.WorldX - _spawnClearanceCenter.x) - footprint.x * .5f);
            float deltaZ = Mathf.Max(0f,
                Mathf.Abs(placement.WorldZ - _spawnClearanceCenter.y) - footprint.y * .5f);
            return deltaX * deltaX + deltaZ * deltaZ < _squaredSpawnClearance;
        }

        private bool TryReserveBuildCells(ResourceNode node, Vector3 position, Vector2 worldFootprint)
        {
            if (_buildGrid == null)
            {
                return true;
            }

            BuildFootprint occupiedCells = _buildGrid.CreateCoveringFootprint(position, worldFootprint);
            if (!_buildGrid.TryOccupy(occupiedCells, node))
            {
                return false;
            }

            node.Depleted += OnNodeDepleted;
            return true;
        }

        private void OnNodeDepleted(ResourceNode node)
        {
            if (_buildGrid != null)
            {
                _buildGrid.Release(node);
            }
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
                ReleaseBuildReservation(spawned.Node);
                if (spawned.Node != null && spawned.Node.IsDepleted)
                {
                    _gatheredNodes.Add(new NodeKey(chunk, spawned.PlacementId));
                }
            }

            _loadedChunks.Remove(chunk);
            if (loaded.Root != null)
            {
                Destroy(loaded.Root);
            }
        }

        private void ReleaseBuildReservations()
        {
            foreach (LoadedChunk loaded in _loadedChunks.Values)
            {
                IReadOnlyList<LoadedChunk.SpawnedNode> nodes = loaded.Nodes;
                for (int i = 0; i < nodes.Count; i++)
                {
                    ReleaseBuildReservation(nodes[i].Node);
                }
            }
        }

        private void ReleaseBuildReservation(ResourceNode node)
        {
            if (ReferenceEquals(node, null))
            {
                return;
            }

            if (_buildGrid != null)
            {
                _buildGrid.Release(node);
            }

            if (node != null)
            {
                node.Depleted -= OnNodeDepleted;
            }
        }

        private sealed class LoadedChunk
        {
            private readonly List<SpawnedNode> _nodes = new();

            public LoadedChunk(GameObject root) => Root = root;

            public GameObject Root { get; }
            public IReadOnlyList<SpawnedNode> Nodes => _nodes;

            public void Add(int placementId, ResourceNode node) => _nodes.Add(new SpawnedNode(placementId, node));

            public readonly struct SpawnedNode
            {
                public SpawnedNode(int placementId, ResourceNode node)
                {
                    PlacementId = placementId;
                    Node = node;
                }

                public int PlacementId { get; }
                public ResourceNode Node { get; }
            }
        }

        private readonly struct NodeKey : IEquatable<NodeKey>
        {
            private readonly ChunkCoordinate _chunk;
            private readonly int _placementId;

            public NodeKey(ChunkCoordinate chunk, int placementId)
            {
                _chunk = chunk;
                _placementId = placementId;
            }

            public bool Equals(NodeKey other) => _chunk.Equals(other._chunk) && _placementId == other._placementId;
            public override bool Equals(object obj) => obj is NodeKey other && Equals(other);
            public override int GetHashCode() => (_chunk.GetHashCode() * 397) ^ _placementId;
        }
    }
}
