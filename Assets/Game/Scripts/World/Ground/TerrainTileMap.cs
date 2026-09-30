using System;
using System.Collections.Generic;
using PlanetSurvival.World.Generation.Landforms;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// The authority on what covers each terrain tile. Generation is deterministic from the world seed, so
    /// only the tiles the player has dug need remembering. It holds no presentation state and lives on the
    /// expedition, which is what keeps a half-dug rock face half dug across a trip into the landing pod.
    /// </summary>
    public sealed class TerrainTileMap
    {
        // Only tiles that have taken at least one dig appear here; the value is the digs still needed, so
        // zero means the tile has fallen back to base regolith.
        private readonly Dictionary<TerrainTileCoordinate, int> _remainingDigs = new();
        private readonly List<TerrainTileCoordinate> _changeBuffer = new();
        private IReadOnlyList<TerrainPatchLayer> _layers = Array.Empty<TerrainPatchLayer>();
        private TerrainSurfaceDefinition _baseSurface;
        private TerrainSurfaceDefinition _iceLakeSurface;
        private LandformSettings _landformSettings;
        private LandformSampler _landforms;
        private Vector2 _landingSite;

        public float TileSize { get; private set; } = 1f;
        public int WorldSeed { get; private set; }
        public float CoverageMultiplier { get; private set; } = 1f;
        public IReadOnlyList<TerrainPatchLayer> Layers => _layers;
        public int DugTileCount => _remainingDigs.Count;

        /// <summary>The landform field beneath the patches, or null when the surface is flat open ground.</summary>
        public LandformSampler Landforms => _landforms;

        /// <summary>Raised for each tile whose terrain or remaining digs changed.</summary>
        public event Action<TerrainTileCoordinate> TileChanged;

        public void Configure(int worldSeed, TerrainPatchSettings settings, float coverageMultiplier = 1f)
        {
            Configure(worldSeed, settings, coverageMultiplier, Vector2.zero);
        }

        /// <param name="landingSite">
        /// World XZ of the landing site. Landforms keep it flat and open and place the starter lake near it.
        /// </param>
        public void Configure(int worldSeed, TerrainPatchSettings settings, float coverageMultiplier,
            Vector2 landingSite)
        {
            float tileSize = settings != null ? settings.TileSize : 1f;
            float safeCoverageMultiplier = Mathf.Max(0f, coverageMultiplier);
            LandformSettings landformSettings = settings != null ? settings.Landforms : null;
            // A tile address only means something against one seed, grid and landform layout, so changing
            // any of them would leave the recorded holes sitting on unrelated ground. Dropping them is the
            // honest answer.
            if (worldSeed != WorldSeed || !Mathf.Approximately(tileSize, TileSize)
                || !Mathf.Approximately(safeCoverageMultiplier, CoverageMultiplier)
                || landformSettings != _landformSettings || landingSite != _landingSite)
            {
                Clear();
            }

            WorldSeed = worldSeed;
            TileSize = tileSize;
            CoverageMultiplier = safeCoverageMultiplier;
            _layers = settings != null ? settings.Layers : Array.Empty<TerrainPatchLayer>();
            _baseSurface = settings != null ? settings.BaseSurface : null;
            _iceLakeSurface = settings != null ? settings.IceLakeSurface : null;
            _landformSettings = landformSettings;
            _landingSite = landingSite;
            _landforms = landformSettings != null
                ? new LandformSampler(worldSeed, landformSettings, landingSite)
                : null;
        }

        public LandformKind GetLandformKind(float worldX, float worldZ)
        {
            return _landforms != null ? _landforms.KindAt(worldX, worldZ) : LandformKind.Plain;
        }

        /// <summary>The full landform sample; flat open ground at mid elevation when landforms are off.</summary>
        public LandformSample SampleLandform(float worldX, float worldZ)
        {
            return _landforms != null
                ? _landforms.Sample(worldX, worldZ)
                : new LandformSample(.5f, 0f, LandformKind.Plain);
        }

        /// <summary>True where a landform forbids walking and building: a mountain or a lava lake.</summary>
        public bool IsBlockedAt(float worldX, float worldZ)
        {
            return LandformSample.Blocks(GetLandformKind(worldX, worldZ));
        }

        public TerrainTileCoordinate TileAt(Vector3 worldPosition)
        {
            return TerrainTileCoordinate.FromWorld(worldPosition.x, worldPosition.z, TileSize);
        }

        /// <summary>The layer covering a tile, or <see cref="ClusteredTerrainLayout.BaseLayerIndex"/>.</summary>
        public int GetLayerIndex(TerrainTileCoordinate tile)
        {
            if (_remainingDigs.TryGetValue(tile, out int remaining) && remaining <= 0)
            {
                return ClusteredTerrainLayout.BaseLayerIndex;
            }

            float centerX = tile.CenterX(TileSize);
            float centerZ = tile.CenterZ(TileSize);
            return SelectPatchLayer(GetLandformKind(centerX, centerZ), centerX, centerZ);
        }

        /// <summary>
        /// The diggable terrain on a tile. An optional base surface represents material that can be
        /// collected from ordinary ground without changing its rendered appearance.
        /// </summary>
        public TerrainSurfaceDefinition GetSurface(TerrainTileCoordinate tile)
        {
            if (_remainingDigs.TryGetValue(tile, out int remaining) && remaining <= 0)
            {
                return null;
            }

            float centerX = tile.CenterX(TileSize);
            float centerZ = tile.CenterZ(TileSize);
            LandformKind landform = GetLandformKind(centerX, centerZ);
            switch (landform)
            {
                case LandformKind.Mountain:
                case LandformKind.LavaLake:
                    return null;
                case LandformKind.IceLake:
                    return _iceLakeSurface;
            }

            int layerIndex = SelectPatchLayer(landform, centerX, centerZ);
            return layerIndex == ClusteredTerrainLayout.BaseLayerIndex ? _baseSurface : _layers[layerIndex].Surface;
        }

        /// <summary>
        /// Patches are detail on open ground only. A mountain or lake owns its ground outright, otherwise an
        /// iron patch would punch a hole in a mountain or float on a frozen lake.
        /// </summary>
        private int SelectPatchLayer(LandformKind landform, float worldX, float worldZ)
        {
            if (!AllowsPatches(landform))
            {
                return ClusteredTerrainLayout.BaseLayerIndex;
            }

            int layer = ClusteredTerrainLayout.SelectLayer(_layers, WorldSeed, worldX, worldZ, CoverageMultiplier);
            return IsSheetOnSlope(layer, worldX, worldZ) || IsIceOnRockGround(layer, worldX, worldZ)
                ? ClusteredTerrainLayout.BaseLayerIndex
                : layer;
        }

        /// <summary>Rock ground never holds ice, lakes or patches alike.</summary>
        private bool IsIceOnRockGround(int layer, float worldX, float worldZ)
        {
            return layer != ClusteredTerrainLayout.BaseLayerIndex && _landforms != null &&
                   _iceLakeSurface != null && _layers[layer].Surface == _iceLakeSurface &&
                   _landforms.Volcanic.IsRockGround(worldX, worldZ);
        }

        /// <summary>
        /// Sheet patches (continuous ice) are level frozen water; on a crater's tilted wall or rim they give
        /// way to bare ground, like a lake does.
        /// </summary>
        private bool IsSheetOnSlope(int layer, float worldX, float worldZ)
        {
            if (layer == ClusteredTerrainLayout.BaseLayerIndex || _landforms == null)
            {
                return false;
            }

            TerrainSurfaceDefinition surface = _layers[layer].Surface;
            return surface != null && surface.PatchRendering == TerrainPatchRendering.Continuous &&
                   _landforms.Craters.IsOnSlope(worldX, worldZ);
        }

        private static bool AllowsPatches(LandformKind landform)
        {
            return landform == LandformKind.Plain || landform == LandformKind.Basin;
        }

        /// <summary>Digs still needed to clear the tile; zero on ground that cannot be dug any further.</summary>
        public int GetRemainingDigs(TerrainTileCoordinate tile)
        {
            if (_remainingDigs.TryGetValue(tile, out int remaining))
            {
                return Mathf.Max(0, remaining);
            }

            TerrainSurfaceDefinition surface = GetSurface(tile);
            return surface != null ? surface.DigCount : 0;
        }

        public bool IsDiggable(TerrainTileCoordinate tile) => GetRemainingDigs(tile) > 0;

        /// <summary>True once the tile has been worn all the way down to the base ground.</summary>
        public bool IsClearedByDigging(TerrainTileCoordinate tile)
        {
            return _remainingDigs.TryGetValue(tile, out int remaining) && remaining <= 0;
        }

        /// <summary>
        /// How solidly terrain covers a point, from 1 well inside a patch down to 0 at its rim, falling
        /// off over <paramref name="featherDistance"/> metres. The drawn outline can only cut on cell
        /// boundaries, and rocks in the artwork are wider than a cell, so a hard outline slices boulders
        /// down the middle; fading the rim instead lets them thin out.
        /// <para>
        /// The distance is taken to the rim of the covered ground as a whole, not of one grade. Two
        /// grades meeting are still rock against rock and need no fade — fading there would open a seam
        /// of bare ground between them. A tile dug away drops to zero outright, so its hole stays crisp.
        /// </para>
        /// </summary>
        public float GetCoverFade(float worldX, float worldZ, float featherDistance)
        {
            if (IsClearedByDigging(TerrainTileCoordinate.FromWorld(worldX, worldZ, TileSize)))
            {
                return 0f;
            }

            if (!AllowsPatches(GetLandformKind(worldX, worldZ)))
            {
                return 0f;
            }

            if (featherDistance <= 0f)
            {
                return 1f;
            }

            float deepest = float.NegativeInfinity;
            for (int i = 0; i < _layers.Count; i++)
            {
                float distance = _layers[i].SignedDistanceToEdge(
                    WorldSeed, worldX, worldZ, CoverageMultiplier);
                if (distance > deepest)
                {
                    deepest = distance;
                }
            }

            return Mathf.Clamp01(deepest / featherDistance);
        }

        /// <summary>
        /// The layer to draw at one point, which is asked at a finer resolution than the dig grid. Tiles
        /// are what the player digs, but a patch whose outline followed those tiles would end in straight
        /// three-metre steps that cut boulders in half, so the drawn edge follows the field itself. The
        /// two only disagree in the band where the field crosses its threshold, which is exactly the
        /// border that should look ragged; a tile dug away still clears completely.
        /// </summary>
        public int GetVisualLayerIndex(float worldX, float worldZ)
        {
            TerrainTileCoordinate tile = TerrainTileCoordinate.FromWorld(worldX, worldZ, TileSize);
            if (IsClearedByDigging(tile))
            {
                return ClusteredTerrainLayout.BaseLayerIndex;
            }

            return SelectPatchLayer(GetLandformKind(worldX, worldZ), worldX, worldZ);
        }

        /// <summary>
        /// Takes one dig off a tile. The caller has already granted the yields, so a dig that finds
        /// nothing to break returns <see cref="TerrainDigOutcome.Nothing"/> and changes no state.
        /// </summary>
        public TerrainDigOutcome Dig(TerrainTileCoordinate tile)
        {
            TerrainSurfaceDefinition surface = GetSurface(tile);
            if (surface == null)
            {
                return TerrainDigOutcome.Nothing;
            }

            int remaining = Mathf.Max(0, GetRemainingDigs(tile) - 1);
            _remainingDigs[tile] = remaining;
            TileChanged?.Invoke(tile);
            return new TerrainDigOutcome(surface, remaining);
        }

        /// <summary>Restores every dug tile to its generated terrain, for the start of a new expedition.</summary>
        public void Clear()
        {
            if (_remainingDigs.Count == 0)
            {
                return;
            }

            // Buffered because a listener rebuilding its view may not enumerate the dictionary safely.
            _changeBuffer.Clear();
            _changeBuffer.AddRange(_remainingDigs.Keys);
            _remainingDigs.Clear();
            for (int i = 0; i < _changeBuffer.Count; i++)
            {
                TileChanged?.Invoke(_changeBuffer[i]);
            }

            _changeBuffer.Clear();
        }
    }
}
