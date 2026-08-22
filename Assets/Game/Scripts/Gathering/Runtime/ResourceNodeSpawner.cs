using System.Collections.Generic;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.World.Generation;
using PlanetSurvival.World.Grid;
using UnityEngine;

namespace PlanetSurvival.Gathering.Runtime
{
    public static class ResourceNodeSpawner
    {
        public static void Spawn(Transform parent, GridMap map, TerrainGenerationSettings terrain, ResourceSpawnSettings settings)
        {
            if (settings == null) return;
            var occupied = new List<Vector3>();
            int entrySeed = terrain.Seed + settings.SeedOffset;

            for (int entryIndex = 0; entryIndex < settings.Entries.Count; entryIndex++)
            {
                ResourceSpawnEntry entry = settings.Entries[entryIndex];
                if (entry.Definition == null || entry.Count <= 0) continue;
                IReadOnlyList<GridCoordinate> positions = ResourceSpawnPlanner.Plan(
                    map.Width, map.Length, entry.Count * 4, settings.MinimumSpacing / terrain.CellSize, entrySeed + entryIndex);

                int spawned = 0;
                for (int i = 0; i < positions.Count && spawned < entry.Count; i++)
                {
                    GridCoordinate coordinate = positions[i];
                    Vector3 position = new(coordinate.X * terrain.CellSize, map[coordinate.X, coordinate.Z].SurfaceHeight,
                        coordinate.Z * terrain.CellSize);
                    if (!IsFarEnough(position, occupied, settings.MinimumSpacing)) continue;
                    CreateNode(parent, entry.Definition, position);
                    occupied.Add(position);
                    spawned++;
                }

                if (spawned < entry.Count)
                    Debug.LogWarning($"Spawned {spawned}/{entry.Count} nodes for '{entry.Definition.ResourceId}' because spacing exhausted the map.");
            }
        }

        private static bool IsFarEnough(Vector3 candidate, List<Vector3> occupied, float spacing)
        {
            float sqrSpacing = spacing * spacing;
            for (int i = 0; i < occupied.Count; i++)
            {
                Vector3 delta = candidate - occupied[i];
                delta.y = 0f;
                if (delta.sqrMagnitude < sqrSpacing) return false;
            }
            return true;
        }

        private static void CreateNode(Transform parent, ResourceNodeDefinition definition, Vector3 position)
        {
            GameObject node = GameObject.CreatePrimitive(PrimitiveType.Cube);
            node.name = $"Resource - {definition.DisplayName}";
            node.transform.SetParent(parent);
            node.transform.localScale = definition.DisplayScale;
            node.transform.position = position + Vector3.up * definition.DisplayScale.y * .5f;
            node.GetComponent<Renderer>().material.color = definition.DisplayColor;
            node.AddComponent<ResourceNode>().Configure(definition);
        }
    }
}
