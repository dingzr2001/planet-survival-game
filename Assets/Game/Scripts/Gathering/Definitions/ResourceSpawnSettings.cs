using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.Gathering.Definitions
{
    [CreateAssetMenu(menuName = "Planet Survival/Gathering/Spawn Settings", fileName = "ResourceSpawnSettings")]
    public sealed class ResourceSpawnSettings : ScriptableObject
    {
        [SerializeField, Min(0)] private int _seedOffset = 7919;
        [SerializeField, Min(0f)] private float _minimumSpacing = 3f;
        [SerializeField] private ResourceSpawnEntry[] _entries = Array.Empty<ResourceSpawnEntry>();

        public int SeedOffset => _seedOffset;
        public float MinimumSpacing => _minimumSpacing;
        public IReadOnlyList<ResourceSpawnEntry> Entries => _entries;

        public void Configure(int seedOffset, float minimumSpacing, params ResourceSpawnEntry[] entries)
        {
            _seedOffset = seedOffset;
            _minimumSpacing = Mathf.Max(0f, minimumSpacing);
            _entries = entries ?? Array.Empty<ResourceSpawnEntry>();
        }
    }

    [Serializable]
    public struct ResourceSpawnEntry
    {
        [SerializeField] private ResourceNodeDefinition _definition;
        [SerializeField, Min(0)] private int _count;

        public ResourceSpawnEntry(ResourceNodeDefinition definition, int count)
        {
            _definition = definition;
            _count = Mathf.Max(0, count);
        }

        public ResourceNodeDefinition Definition => _definition;
        public int Count => _count;
    }
}
