using System.Collections.Generic;
using PlanetSurvival.World.Generation.Landforms;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// Samples the terrain into the two per-chunk textures the terrain shader reads.
    /// <list type="bullet">
    /// <item>A landform map: the normalised elevation, from which the shader derives lake and basin
    /// borders and relief shading; the mountain mesh height, for shadows; and the distance to the mountain
    /// border, for the rubble at the cliff foot. One continuous field is what lets every border follow the
    /// landform instead of the tile grid.</item>
    /// <item>A patch map: one texel per gameplay tile holding which patch layer owns it, how deep inside
    /// its patch it lies, and whether it has been dug away.</item>
    /// </list>
    /// Both carry a gutter beyond the chunk edge, sampled from the same world positions the neighbouring
    /// chunk uses, so filtering and neighbour look-ups never show a seam.
    /// </summary>
    public static class TerrainControlMapBuilder
    {
        /// <summary>Patch layers the shader can address. Bounded by its uniform slice tables.</summary>
        public const int MaximumLayerCount = 16;

        /// <summary>Tiles of patch data beyond each chunk edge: scattered artwork may overlap one neighbour.</summary>
        public const int PatchGutter = 1;

        public const byte ClearedFlag = byte.MaxValue;

        public static int PatchTextureSizeFor(int tilesPerChunk) => Mathf.Max(1, tilesPerChunk) + PatchGutter * 2;

        /// <summary>Half-precision channels per landform texel: elevation, mountain height, border distance.</summary>
        public const int LandformChannels = 4;

        // Border distances are stored clamped: beyond a few dozen metres nothing looks at them.
        private const float MaximumStoredDistance = 64f;

        /// <summary>
        /// Crater distance, in radii, stored where no crater is near. Beyond every crater's ejecta, so the
        /// shader draws nothing there.
        /// </summary>
        public const float MaximumCraterDistance = 3f;

        /// <summary>Half-precision channels per volcanic texel: rock ground, lava activity.</summary>
        public const int VolcanicChannels = 2;

        // The volcanic fields are clamped before storing; far from any border their exact value is moot.
        private const float MaximumVolcanic = 8f;

        /// <summary>
        /// Landform texels beyond each chunk edge. The shader looks up to <paramref name="sampleReach"/>
        /// metres away from a pixel (a shadow march or a rubble cell), plus one texel for its gradient.
        /// </summary>
        public static int ElevationGutterFor(float chunkSize, int resolution, float sampleReach)
        {
            float step = chunkSize / Mathf.Max(1, resolution);
            return Mathf.CeilToInt(Mathf.Max(0f, sampleReach) / step) + 1;
        }

        public static int ElevationTextureSizeFor(int resolution, int gutter) =>
            Mathf.Max(1, resolution) + 1 + Mathf.Max(0, gutter) * 2;

        /// <summary>
        /// Fills the landform map, one texel per sample and the first texel <paramref name="gutter"/> steps
        /// outside the chunk's minimum corner. Channels, in half precision:
        /// R = normalised elevation (lakes, basins, relief shading), G = mountain mesh height in metres
        /// (shadows), B = signed metres inside the mountain border, roughened (the cliff foot and its rubble),
        /// A = distance to the nearest crater's centre in its radii, up to <see cref="MaximumCraterDistance"/>
        /// (the crater's relief shading, floor dust and ejecta blanket).
        /// </summary>
        /// <param name="elevation">Receives the normalised elevation per texel.</param>
        /// <param name="inside">Receives the signed border distance per texel, for building the mountain mesh.</param>
        /// <param name="craterMarks">Receives the crater mark per texel, for scattering ejecta rocks.</param>
        /// <param name="volcanicTexels">
        /// Receives two half-precision channels per texel: R = <see cref="VolcanicField.RockGround"/> (0 on the
        /// border of rock ground), for the rock texture laid over the regolith; G = <see cref="VolcanicField.Lava"/>
        /// (1 on a lava shore), for lava lakes, their glow and fissures.
        /// </param>
        /// <param name="hasMountain">True when any sample inside the chunk lies inside a mountain.</param>
        /// <returns>False when the map has no landforms, so there is nothing to shade.</returns>
        public static bool FillLandformMaps(TerrainTileMap map, float chunkOriginX, float chunkOriginZ,
            float chunkSize, int resolution, int gutter, LandformAppearance appearance, float[] elevation,
            float[] inside, float[] craterMarks, ushort[] texels, ushort[] volcanicTexels, out bool hasMountain)
        {
            hasMountain = false;
            if (map == null || map.Landforms == null || chunkSize <= 0f)
            {
                return false;
            }

            int safeResolution = Mathf.Max(1, resolution);
            int size = ElevationTextureSizeFor(safeResolution, gutter);
            int count = size * size;
            if (elevation == null || inside == null || craterMarks == null || texels == null ||
                volcanicTexels == null || elevation.Length < count || inside.Length < count ||
                craterMarks.Length < count || texels.Length < count * LandformChannels ||
                volcanicTexels.Length < count * VolcanicChannels)
            {
                return false;
            }

            float step = chunkSize / safeResolution;
            LandformSampler landforms = map.Landforms;
            for (int z = 0; z < size; z++)
            {
                float worldZ = chunkOriginZ + (z - gutter) * step;
                for (int x = 0; x < size; x++)
                {
                    elevation[z * size + x] = landforms.Sample(chunkOriginX + (x - gutter) * step, worldZ).Elevation;
                }
            }

            for (int z = 0; z < size; z++)
            {
                float worldZ = chunkOriginZ + (z - gutter) * step;
                for (int x = 0; x < size; x++)
                {
                    float worldX = chunkOriginX + (x - gutter) * step;
                    int index = z * size + x;
                    float e = elevation[index];

                    // Metres to the mountain border: how far above the mountain level this sample sits,
                    // divided by how fast the field climbs there. Accurate near the border, which is the only
                    // place the cliff profile and the rubble care about.
                    float slopeX = (elevation[z * size + Mathf.Min(x + 1, size - 1)]
                                    - elevation[z * size + Mathf.Max(x - 1, 0)]) / (2f * step);
                    float slopeZ = (elevation[Mathf.Min(z + 1, size - 1) * size + x]
                                    - elevation[Mathf.Max(z - 1, 0) * size + x]) / (2f * step);
                    float slope = Mathf.Max(Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ), 1e-4f);
                    float distance = (e - 1f) / slope + MountainHeightProfile.BorderRoughness(worldX, worldZ, appearance);
                    distance = Mathf.Clamp(distance, -MaximumStoredDistance, MaximumStoredDistance);
                    inside[index] = distance;

                    float height = Mathf.Max(0f, MountainHeightProfile.Height(distance, e - 1f, worldX, worldZ, appearance));
                    int texel = index * LandformChannels;
                    texels[texel] = Mathf.FloatToHalf(e);
                    texels[texel + 1] = Mathf.FloatToHalf(height);
                    texels[texel + 2] = Mathf.FloatToHalf(distance);
                    float craterDistance = MaximumCraterDistance;
                    float craterMark = 0f;
                    if (landforms.Craters.TryGetNearest(worldX, worldZ, out Crater crater, out float relative))
                    {
                        craterDistance = Mathf.Min(relative, MaximumCraterDistance);
                        craterMark = CraterMarkings.Mark(crater, relative, worldX, worldZ, appearance);
                    }

                    craterMarks[index] = craterMark;
                    texels[texel + 3] = Mathf.FloatToHalf(craterDistance);
                    landforms.Volcanic.Sample(worldX, worldZ, out float rockGround, out float lava);
                    int volcanicTexel = index * VolcanicChannels;
                    volcanicTexels[volcanicTexel] =
                        Mathf.FloatToHalf(Mathf.Clamp(rockGround, -MaximumVolcanic, MaximumVolcanic));
                    volcanicTexels[volcanicTexel + 1] =
                        Mathf.FloatToHalf(Mathf.Clamp(lava, -MaximumVolcanic, MaximumVolcanic));

                    bool inner = x >= gutter && z >= gutter && x <= gutter + safeResolution && z <= gutter + safeResolution;
                    hasMountain |= inner && distance > 0f;
                }
            }

            return true;
        }

        /// <summary>
        /// Fills one RGBA texel per tile, gutter included: R = owning patch layer + 1 (0 for none), G = how
        /// far inside its patch the tile lies over <paramref name="depthDistance"/> metres, B =
        /// <see cref="ClearedFlag"/> when dug away. Patch ownership follows the gameplay rules exactly, so
        /// the art drawn over a tile is always what digging it yields.
        /// </summary>
        /// <returns>True when a tile inside the chunk carries a patch.</returns>
        public static bool FillPatchIds(TerrainTileMap map, TerrainTileCoordinate origin, int sizeInTiles,
            float depthDistance, Color32[] pixels)
        {
            if (map == null || sizeInTiles <= 0)
            {
                return false;
            }

            int textureSize = PatchTextureSizeFor(sizeInTiles);
            if (pixels == null || pixels.Length < textureSize * textureSize)
            {
                return false;
            }

            IReadOnlyList<TerrainPatchLayer> layers = map.Layers;
            float safeDepthDistance = Mathf.Max(.01f, depthDistance);
            bool hasCoverage = false;
            for (int z = 0; z < textureSize; z++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    var tile = new TerrainTileCoordinate(origin.X + x - PatchGutter, origin.Z + z - PatchGutter);
                    int index = z * textureSize + x;
                    if (map.IsClearedByDigging(tile))
                    {
                        pixels[index] = new Color32(0, 0, ClearedFlag, 0);
                        continue;
                    }

                    int layerIndex = map.GetLayerIndex(tile);
                    if (layerIndex == ClusteredTerrainLayout.BaseLayerIndex || layerIndex >= MaximumLayerCount)
                    {
                        pixels[index] = default;
                        continue;
                    }

                    float distance = layers[layerIndex].SignedDistanceToEdge(
                        map.WorldSeed, tile.CenterX(map.TileSize), tile.CenterZ(map.TileSize),
                        map.CoverageMultiplier);
                    byte depth = (byte)Mathf.RoundToInt(Mathf.Clamp01(distance / safeDepthDistance) * byte.MaxValue);
                    pixels[index] = new Color32((byte)(layerIndex + 1), depth, 0, 0);

                    bool inner = x >= PatchGutter && z >= PatchGutter &&
                                 x < textureSize - PatchGutter && z < textureSize - PatchGutter;
                    hasCoverage |= inner;
                }
            }

            return hasCoverage;
        }
    }
}
