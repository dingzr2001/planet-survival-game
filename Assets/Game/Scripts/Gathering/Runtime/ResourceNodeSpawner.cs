using System.Collections.Generic;
using PlanetSurvival.Gathering.Definitions;
using PlanetSurvival.World.Generation;
using PlanetSurvival.World.Grid;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Gathering.Runtime
{
    public static class ResourceNodeSpawner
    {
        public static void Spawn(Transform parent, GridMap map, TerrainGenerationSettings terrain,
            ResourceSpawnSettings settings, WorldVisualSettings visuals)
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
                    Vector3 position = new(coordinate.X * terrain.CellSize, 0f, coordinate.Z * terrain.CellSize);
                    if (!IsFarEnough(position, occupied, settings.MinimumSpacing)) continue;
                    CreateNode(parent, entry.Definition, position, visuals);
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

        private static void CreateNode(Transform parent, ResourceNodeDefinition definition, Vector3 position,
            WorldVisualSettings visuals)
        {
            var node = new GameObject();
            node.name = $"Resource - {definition.DisplayName}";
            node.transform.SetParent(parent);
            node.transform.position = position;

            var collider = node.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                Mathf.Max(.2f, definition.DisplayScale.x),
                Mathf.Max(.3f, definition.DisplayScale.y),
                Mathf.Max(.2f, definition.DisplayScale.z));
            collider.center = Vector3.up * collider.size.y * .5f;
            node.AddComponent<ResourceNode>().Configure(definition);

            var visual = new GameObject("Sprite");
            visual.transform.SetParent(node.transform, false);
            visual.AddComponent<SpriteRenderer>();
            visual.AddComponent<WorldSpriteView>().Configure(
                definition.WorldSprite,
                Mathf.Max(.5f, definition.DisplayScale.y));

            Color shadowColor = visuals != null ? visuals.ShadowColor : new Color(0f, 0f, 0f, .4f);
            BlobShadow.Create(node.transform,
                new Vector2(collider.size.x * 1.1f, collider.size.z * .75f),
                shadowColor);
        }
    }
}
