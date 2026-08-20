using PlanetSurvival.World.Grid;
using UnityEngine;

namespace PlanetSurvival.World.Generation
{
    public sealed class GridTerrainGenerator
    {
        public GridMap Generate(TerrainGenerationSettings settings)
        {
            var map = new GridMap(settings.Width, settings.Length);
            var random = new System.Random(settings.Seed);
            float offsetX = random.Next(-100000, 100000);
            float offsetZ = random.Next(-100000, 100000);

            for (int x = 0; x < settings.Width; x++)
            {
                for (int z = 0; z < settings.Length; z++)
                {
                    float sampleX = offsetX + x * settings.NoiseScale;
                    float sampleZ = offsetZ + z * settings.NoiseScale;
                    float normalizedHeight = Mathf.PerlinNoise(sampleX, sampleZ);
                    float height = normalizedHeight * settings.HeightAmplitude;
                    map[x, z] = new GridCell(new GridCoordinate(x, z), height, settings.UndergroundDepth);
                }
            }

            return map;
        }
    }
}
