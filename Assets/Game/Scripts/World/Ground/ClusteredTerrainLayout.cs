using System.Collections.Generic;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// Resolves which clustered layer, if any, covers a point. Layers are tried in order and the first
    /// match wins, so the terrain that should survive an overlap is listed first — hard rock before soft
    /// rock, otherwise a wide soft patch would erase the hard cores sitting inside it.
    /// </summary>
    public static class ClusteredTerrainLayout
    {
        /// <summary>The untouched base ground, returned wherever no layer reaches its threshold.</summary>
        public const int BaseLayerIndex = -1;

        public static int SelectLayer(IReadOnlyList<TerrainPatchLayer> layers, int worldSeed,
            float worldX, float worldZ)
        {
            if (layers == null)
            {
                return BaseLayerIndex;
            }

            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].Covers(worldSeed, worldX, worldZ))
                {
                    return i;
                }
            }

            return BaseLayerIndex;
        }
    }
}
