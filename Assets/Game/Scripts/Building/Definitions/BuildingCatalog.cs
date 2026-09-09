using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.Building.Definitions
{
    /// <summary>Everything the player is allowed to build, in the order the build panel lists it.</summary>
    [CreateAssetMenu(menuName = "Planet Survival/Building/Catalog", fileName = "BuildingCatalog")]
    public sealed class BuildingCatalog : ScriptableObject
    {
        [SerializeField] private BuildableDefinition[] _buildables = Array.Empty<BuildableDefinition>();

        public IReadOnlyList<BuildableDefinition> Buildables => _buildables;

        public bool IsValid(out string error)
        {
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _buildables.Length; i++)
            {
                BuildableDefinition buildable = _buildables[i];
                if (buildable == null)
                {
                    error = $"Building catalog '{name}' lists a missing buildable.";
                    return false;
                }

                if (!buildable.IsValid(out string buildableError))
                {
                    error = $"Building catalog '{name}' lists an invalid buildable: {buildableError}";
                    return false;
                }

                if (!uniqueIds.Add(buildable.BuildableId))
                {
                    error = $"Building catalog '{name}' lists '{buildable.BuildableId}' twice.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public void Configure(params BuildableDefinition[] buildables)
        {
            _buildables = buildables ?? Array.Empty<BuildableDefinition>();
        }
    }
}
