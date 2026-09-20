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
        /// <summary>
        /// World units across one terrain tile. Construction cells use the same size, so one structure
        /// covers exactly one square of ground artwork.
        /// </summary>
        public const float DefaultTileSize = 2.75f;

        [SerializeField, Tooltip("Mixed into the world seed so terrain patches do not correlate with resource layouts.")]
        private int _seedOffset = 5231;
        [SerializeField, Min(.5f), Tooltip("World units across one terrain tile. One complete terrain illustration is fitted into this square, and digging clears the same square.")]
        private float _tileSize = DefaultTileSize;
        [SerializeField, Min(1), Tooltip("Tiles per side of one streamed block. Every block is one quad and one control-map pair; larger blocks reduce streaming objects but make each mask rebuild more expensive.")]
        private int _chunkSizeInTiles = 8;
        [SerializeField, Min(0), Tooltip("Blocks kept loaded around the player, beyond the one they stand in.")]
        private int _loadRadiusInChunks = 1;
        [SerializeField, Tooltip("Shader that blends the per-chunk terrain control maps. Keep an asset reference so player builds cannot strip it.")]
        private Shader _blendShader;
        [SerializeField, Min(16), Tooltip("Control-map samples per chunk edge. Kept dense enough that every gameplay tile centre has a stable terrain sample.")]
        private int _controlMapResolution = TerrainChunkRenderResources.DefaultControlMapResolution;
        [SerializeField, Min(.05f), Tooltip("Width of the underlying coverage field. Tile rendering thresholds this field at each tile centre to avoid cropped artwork.")]
        private float _blendDistance = TerrainChunkRenderResources.DefaultBlendDistance;
        [SerializeField, Tooltip("Highest-priority terrain first: the first matching layer wins. Each layer controls its own approximate coverage and patch size.")]
        private TerrainPatchLayer[] _layers = Array.Empty<TerrainPatchLayer>();
        [SerializeField, Tooltip("Optional diggable material available where no terrain patch covers the base ground.")]
        private TerrainSurfaceDefinition _baseSurface;

        public int SeedOffset => _seedOffset;
        public float TileSize => Mathf.Max(.5f, _tileSize);
        public int ChunkSizeInTiles => Mathf.Max(1, _chunkSizeInTiles);
        public int LoadRadiusInChunks => Mathf.Max(0, _loadRadiusInChunks);
        public float ChunkSize => TileSize * ChunkSizeInTiles;
        public Shader BlendShader => _blendShader;
        public int ControlMapResolution => Mathf.Max(16, _controlMapResolution);
        public float BlendDistance => Mathf.Max(.05f, _blendDistance);
        public IReadOnlyList<TerrainPatchLayer> Layers => _layers;
        public TerrainSurfaceDefinition BaseSurface => _baseSurface;

        public void Configure(int seedOffset, float tileSize, int chunkSizeInTiles, int loadRadiusInChunks,
            params TerrainPatchLayer[] layers)
        {
            Configure(seedOffset, tileSize, chunkSizeInTiles, loadRadiusInChunks, null, layers);
        }

        public void Configure(int seedOffset, float tileSize, int chunkSizeInTiles, int loadRadiusInChunks,
            TerrainSurfaceDefinition baseSurface, params TerrainPatchLayer[] layers)
        {
            _seedOffset = seedOffset;
            _tileSize = Mathf.Max(.5f, tileSize);
            _chunkSizeInTiles = Mathf.Max(1, chunkSizeInTiles);
            _loadRadiusInChunks = Mathf.Max(0, loadRadiusInChunks);
            _baseSurface = baseSurface;
            _layers = layers ?? Array.Empty<TerrainPatchLayer>();
        }

        public void ConfigureRendering(Shader blendShader, int controlMapResolution, float blendDistance)
        {
            _blendShader = blendShader;
            _controlMapResolution = Mathf.Max(16, controlMapResolution);
            _blendDistance = Mathf.Max(.05f, blendDistance);
        }

        private void OnValidate()
        {
            if (_layers == null)
            {
                _layers = Array.Empty<TerrainPatchLayer>();
                return;
            }

            for (int i = 0; i < _layers.Length; i++)
            {
                TerrainPatchLayer layer = _layers[i];
                if (layer.UpgradeLegacyCoverage())
                {
                    _layers[i] = layer;
                }
            }
        }
    }
}
