using System;
using System.Collections.Generic;
using PlanetSurvival.World.Generation.Landforms;
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
        /// World units across one terrain tile: the square a dig clears and a patch layer claims. It is
        /// independent of the one-metre construction grid; patch artwork is scattered across tile borders
        /// so the tile lattice never shows.
        /// </summary>
        public const float DefaultTileSize = 2.75f;

        [SerializeField, Tooltip("Mixed into the world seed so terrain patches do not correlate with resource layouts.")]
        private int _seedOffset = 5231;
        [SerializeField, Min(.5f), Tooltip("World units across one terrain tile: the square one dig clears and one patch deposit occupies.")]
        private float _tileSize = DefaultTileSize;
        [SerializeField, Min(1), Tooltip("Tiles per side of one streamed block. Every block is one quad and one control-map pair; larger blocks reduce streaming objects but make each mask rebuild more expensive.")]
        private int _chunkSizeInTiles = 8;
        [SerializeField, Min(0), Tooltip("Blocks kept loaded around the player, beyond the one they stand in.")]
        private int _loadRadiusInChunks = 1;
        [SerializeField, Tooltip("Shader that blends the per-chunk terrain control maps. Keep an asset reference so player builds cannot strip it.")]
        private Shader _blendShader;
        [SerializeField, Tooltip("Shader of the 3D mountain meshes. Keep an asset reference so player builds cannot strip it.")]
        private Shader _mountainShader;
        [SerializeField, Tooltip("Shader of the fallen rocks at cliff feet. Keep an asset reference so player builds cannot strip it.")]
        private Shader _talusShader;
        [SerializeField, Min(16), Tooltip("Elevation samples per chunk edge. The shader interpolates between them and derives borders in metres, so a few samples per metre is plenty; more only lengthens chunk loads.")]
        private int _controlMapResolution = TerrainChunkRenderResources.DefaultControlMapResolution;
        [SerializeField, Min(.05f), Tooltip("Metres over which patch artwork shrinks towards the rim of its patch, so deposits thin out instead of ending in a hard row.")]
        private float _blendDistance = TerrainChunkRenderResources.DefaultBlendDistance;
        [SerializeField, Tooltip("Highest-priority terrain first: the first matching layer wins. Each layer controls its own approximate coverage and patch size.")]
        private TerrainPatchLayer[] _layers = Array.Empty<TerrainPatchLayer>();
        [SerializeField, Tooltip("Optional diggable material available where no terrain patch covers the base ground.")]
        private TerrainSurfaceDefinition _baseSurface;

        [Header("Landforms")]
        [SerializeField, Tooltip("Mountains, basins, craters and ice lakes laid out beneath the patches. Without it the surface is flat open ground.")]
        private LandformSettings _landforms;
        [SerializeField, Tooltip("Diggable surface of ice lakes. Sharing the ice terrain ID lets ice drills and pickaxes work on lakes.")]
        private TerrainSurfaceDefinition _iceLakeSurface;
        [SerializeField] private LandformAppearance _landformAppearance = new();

        public int SeedOffset => _seedOffset;
        public float TileSize => Mathf.Max(.5f, _tileSize);
        public int ChunkSizeInTiles => Mathf.Max(1, _chunkSizeInTiles);
        public int LoadRadiusInChunks => Mathf.Max(0, _loadRadiusInChunks);
        public float ChunkSize => TileSize * ChunkSizeInTiles;
        public Shader BlendShader => _blendShader;
        public Shader MountainShader => _mountainShader;
        public Shader TalusShader => _talusShader;
        public int ControlMapResolution => Mathf.Max(16, _controlMapResolution);
        public float BlendDistance => Mathf.Max(.05f, _blendDistance);
        public IReadOnlyList<TerrainPatchLayer> Layers => _layers;
        public TerrainSurfaceDefinition BaseSurface => _baseSurface;
        public LandformSettings Landforms => _landforms;
        public TerrainSurfaceDefinition IceLakeSurface => _iceLakeSurface;
        public LandformAppearance LandformAppearance => _landformAppearance ??= new LandformAppearance();

        public void ConfigureLandforms(LandformSettings landforms, TerrainSurfaceDefinition iceLakeSurface)
        {
            _landforms = landforms;
            _iceLakeSurface = iceLakeSurface;
        }

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

        public void ConfigureLandformShaders(Shader mountainShader, Shader talusShader)
        {
            _mountainShader = mountainShader;
            _talusShader = talusShader;
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
