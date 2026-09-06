using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.Gathering.Definitions
{
    [CreateAssetMenu(menuName = "Planet Survival/Gathering/Spawn Settings", fileName = "ResourceSpawnSettings")]
    public sealed class ResourceSpawnSettings : ScriptableObject
    {
        [SerializeField, Min(0), Tooltip("Mixed into the world seed so resource layouts can be retuned without changing the terrain seed.")]
        private int _seedOffset = 7919;
        [SerializeField, Min(1f), Tooltip("Edge length of a resource chunk in world units. Every chunk is planned independently.")]
        private float _chunkSize = 32f;
        [SerializeField, Min(0), Tooltip("Chunks kept alive around the player, measured as a ring radius. 2 loads a 5x5 chunk area.")]
        private int _loadRadiusInChunks = 2;
        [SerializeField, Min(0f), Tooltip("Minimum distance in world units between two nodes of the same chunk.")]
        private float _minimumSpacing = 4f;
        [SerializeField, Min(0f), Tooltip("Radius in world units around the player start that stays free of nodes, so nobody spawns inside a rock. 0 disables the clearance.")]
        private float _spawnClearanceRadius = 3f;
        [SerializeField] private ResourceSpawnEntry[] _entries = Array.Empty<ResourceSpawnEntry>();

        public int SeedOffset => _seedOffset;
        public float ChunkSize => Mathf.Max(1f, _chunkSize);
        public int LoadRadiusInChunks => Mathf.Max(0, _loadRadiusInChunks);
        public float MinimumSpacing => _minimumSpacing;
        public float SpawnClearanceRadius => Mathf.Max(0f, _spawnClearanceRadius);
        public IReadOnlyList<ResourceSpawnEntry> Entries => _entries;

        public void Configure(int seedOffset, float chunkSize, int loadRadiusInChunks, float minimumSpacing,
            float spawnClearanceRadius, params ResourceSpawnEntry[] entries)
        {
            _seedOffset = seedOffset;
            _chunkSize = Mathf.Max(1f, chunkSize);
            _loadRadiusInChunks = Mathf.Max(0, loadRadiusInChunks);
            _minimumSpacing = Mathf.Max(0f, minimumSpacing);
            _spawnClearanceRadius = Mathf.Max(0f, spawnClearanceRadius);
            _entries = entries ?? Array.Empty<ResourceSpawnEntry>();
        }
    }

    [Serializable]
    public struct ResourceSpawnEntry
    {
        [SerializeField] private ResourceNodeDefinition _definition;
        [SerializeField, Min(0f), Tooltip("Average number of nodes per chunk. Values below 1 spawn the node in only some chunks.")]
        private float _nodesPerChunk;

        public ResourceSpawnEntry(ResourceNodeDefinition definition, float nodesPerChunk)
        {
            _definition = definition;
            _nodesPerChunk = Mathf.Max(0f, nodesPerChunk);
        }

        public ResourceNodeDefinition Definition => _definition;
        public float NodesPerChunk => Mathf.Max(0f, _nodesPerChunk);
    }
}
