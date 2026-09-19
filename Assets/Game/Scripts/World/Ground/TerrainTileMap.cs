using System;
using System.Collections.Generic;
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

        public float TileSize { get; private set; } = 1f;
        public int WorldSeed { get; private set; }
        public float CoverageMultiplier { get; private set; } = 1f;
        public IReadOnlyList<TerrainPatchLayer> Layers => _layers;
        public int DugTileCount => _remainingDigs.Count;

        /// <summary>Raised for each tile whose terrain or remaining digs changed.</summary>
        public event Action<TerrainTileCoordinate> TileChanged;

        public void Configure(int worldSeed, TerrainPatchSettings settings, float coverageMultiplier = 1f)
        {
            float tileSize = settings != null ? settings.TileSize : 1f;
            float safeCoverageMultiplier = Mathf.Max(0f, coverageMultiplier);
            // A tile address only means something against one seed and grid, so changing either would
            // leave the recorded holes sitting on unrelated ground. Dropping them is the honest answer.
            if (worldSeed != WorldSeed || !Mathf.Approximately(tileSize, TileSize)
                || !Mathf.Approximately(safeCoverageMultiplier, CoverageMultiplier))
            {
                Clear();
            }

            WorldSeed = worldSeed;
            TileSize = tileSize;
            CoverageMultiplier = safeCoverageMultiplier;
            _layers = settings != null ? settings.Layers : Array.Empty<TerrainPatchLayer>();
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

            return ClusteredTerrainLayout.SelectLayer(
                _layers, WorldSeed, tile.CenterX(TileSize), tile.CenterZ(TileSize), CoverageMultiplier);
        }

        /// <summary>The terrain covering a tile, or null where the base regolith shows through.</summary>
        public TerrainSurfaceDefinition GetSurface(TerrainTileCoordinate tile)
        {
            int layerIndex = GetLayerIndex(tile);
            return layerIndex == ClusteredTerrainLayout.BaseLayerIndex ? null : _layers[layerIndex].Surface;
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

            return ClusteredTerrainLayout.SelectLayer(
                _layers, WorldSeed, worldX, worldZ, CoverageMultiplier);
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
