using PlanetSurvival.World.Grid;
using UnityEngine;

namespace PlanetSurvival.World.Generation
{
    public sealed class GridTerrainGenerator
    {
        public GridMap Generate(TerrainGenerationSettings settings)
        {
            var map = new GridMap(settings.Width, settings.Length);
            for (int x = 0; x < settings.Width; x++)
            {
                for (int z = 0; z < settings.Length; z++)
                {
                    map[x, z] = new GridCell(new GridCoordinate(x, z));
                }
            }

            return map;
        }
    }
}
