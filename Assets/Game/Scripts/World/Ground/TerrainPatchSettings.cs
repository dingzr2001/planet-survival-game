using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// How the surface is broken up into terrain patches. The layer list is the extension point: a new
    /// terrain that should generate in runs is one more <see cref="TerrainPatchLayer"/> here.
    /// </summary>
    [CreateAssetMenu(menuName = "Planet Survival/World/Terrain Patch Settings", fileName = "TerrainPatchSettings")]
    public sealed class TerrainPatchSettings : ScriptableObject
    {
        [SerializeField, Tooltip("Mixed into the world seed so terrain patches do not correlate with resource layouts.")]
        private int _seedOffset = 5231;
        [SerializeField, Min(.5f), Tooltip("World units across one terrain tile. This is the unit that is dug away, so it also decides how coarse a dug-out hole looks.")]
        private float _tileSize = 3f;
        [SerializeField, Min(1), Tooltip("Tiles per side of one streamed block. Every tile of a block shares a mesh, so larger blocks mean fewer draw calls but a more expensive rebuild after each dig.")]
        private int _chunkSizeInTiles = 8;
        [SerializeField, Min(0), Tooltip("Blocks kept loaded around the player, beyond the one they stand in.")]
        private int _loadRadiusInChunks = 1;
        [SerializeField, Tooltip("Hardest terrain first: the first layer that reaches its threshold wins the tile.")]
        private TerrainPatchLayer[] _layers = Array.Empty<TerrainPatchLayer>();

        public int SeedOffset => _seedOffset;
        public float TileSize => Mathf.Max(.5f, _tileSize);
        public int ChunkSizeInTiles => Mathf.Max(1, _chunkSizeInTiles);
        public int LoadRadiusInChunks => Mathf.Max(0, _loadRadiusInChunks);
        public float ChunkSize => TileSize * ChunkSizeInTiles;
        public IReadOnlyList<TerrainPatchLayer> Layers => _layers;

        public void Configure(int seedOffset, float tileSize, int chunkSizeInTiles, int loadRadiusInChunks,
            params TerrainPatchLayer[] layers)
        {
            _seedOffset = seedOffset;
            _tileSize = Mathf.Max(.5f, tileSize);
            _chunkSizeInTiles = Mathf.Max(1, chunkSizeInTiles);
            _loadRadiusInChunks = Mathf.Max(0, loadRadiusInChunks);
            _layers = layers ?? Array.Empty<TerrainPatchLayer>();
        }
    }
}
