using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// Samples continuous terrain fields into two RGBA control maps. Gameplay remains tile-based, while
    /// these weights let the renderer blend each surface per pixel without deriving a geometric outline.
    /// </summary>
    public static class TerrainControlMapBuilder
    {
        public const int MaximumLayerCount = 8;
        public const int GutterSize = 1;

        public static int TextureSizeFor(int resolution) => Mathf.Max(1, resolution) + 1 + GutterSize * 2;

        /// <summary>
        /// Fills control maps for a chunk, including one sample beyond every edge. The gutter lets
        /// bilinear filtering see the same world samples as the neighbouring chunk instead of clamped
        /// edge pixels, which prevents seams.
        /// </summary>
        public static bool Fill(TerrainTileMap map, TerrainTileCoordinate origin, int sizeInTiles,
            int resolution, float blendDistance, Color32[] control0, Color32[] control1)
        {
            if (map == null || sizeInTiles <= 0)
            {
                return false;
            }

            int safeResolution = Mathf.Max(1, resolution);
            int textureSize = TextureSizeFor(safeResolution);
            int requiredLength = textureSize * textureSize;
            if (control0 == null || control1 == null
                || control0.Length < requiredLength || control1.Length < requiredLength)
            {
                return false;
            }

            IReadOnlyList<TerrainPatchLayer> layers = map.Layers;
            int layerCount = Mathf.Min(layers.Count, MaximumLayerCount);
            float chunkSize = map.TileSize * sizeInTiles;
            float sampleStep = chunkSize / safeResolution;
            float originX = origin.MinX(map.TileSize);
            float originZ = origin.MinZ(map.TileSize);
            float safeBlendDistance = Mathf.Max(.01f, blendDistance);
            bool hasCoverage = false;

            for (int z = 0; z < textureSize; z++)
            {
                float worldZ = originZ + (z - GutterSize) * sampleStep;
                bool innerZ = z >= GutterSize && z <= GutterSize + safeResolution;
                for (int x = 0; x < textureSize; x++)
                {
                    float worldX = originX + (x - GutterSize) * sampleStep;
                    int pixelIndex = z * textureSize + x;
                    if (map.IsClearedByDigging(
                            TerrainTileCoordinate.FromWorld(worldX, worldZ, map.TileSize)))
                    {
                        control0[pixelIndex] = default;
                        control1[pixelIndex] = default;
                        continue;
                    }

                    byte r0 = 0;
                    byte g0 = 0;
                    byte b0 = 0;
                    byte a0 = 0;
                    byte r1 = 0;
                    byte g1 = 0;
                    byte b1 = 0;
                    byte a1 = 0;
                    // Gameplay resolves overlapping terrain by priority: the first matching layer owns
                    // the tile. Keep the render data subject to the same rule. This matters for cutout
                    // artwork because otherwise transparent pixels in iron would reveal a lower rock
                    // layer and make one tile look as though it contains both resources.
                    for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
                    {
                        float distance = layers[layerIndex].SignedDistanceToEdge(
                            map.WorldSeed, worldX, worldZ, map.CoverageMultiplier);
                        if (distance < 0f)
                        {
                            continue;
                        }

                        byte weight = ToByte(SmoothTransition(distance, safeBlendDistance));
                        switch (layerIndex)
                        {
                            case 0: r0 = weight; break;
                            case 1: g0 = weight; break;
                            case 2: b0 = weight; break;
                            case 3: a0 = weight; break;
                            case 4: r1 = weight; break;
                            case 5: g1 = weight; break;
                            case 6: b1 = weight; break;
                            case 7: a1 = weight; break;
                        }

                        break;
                    }

                    control0[pixelIndex] = new Color32(r0, g0, b0, a0);
                    control1[pixelIndex] = new Color32(r1, g1, b1, a1);
                    bool innerX = x >= GutterSize && x <= GutterSize + safeResolution;
                    if (innerX && innerZ && (r0 | g0 | b0 | a0 | r1 | g1 | b1 | a1) != 0)
                    {
                        hasCoverage = true;
                    }
                }
            }

            return hasCoverage;
        }

        private static float SmoothTransition(float signedDistance, float blendDistance)
        {
            float t = Mathf.Clamp01(signedDistance / blendDistance + .5f);
            return t * t * (3f - 2f * t);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * byte.MaxValue);
        }
    }
}
