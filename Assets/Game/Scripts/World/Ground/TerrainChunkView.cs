using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// Draws one terrain chunk: a ground quad over the base disc, the 3D mountain mesh standing on it, and
    /// the fallen rocks at the mountain feet. A landform map carries elevation, mountain height and border
    /// distance; a patch map carries the gameplay tiles. The ground shader turns them into lakes, basins,
    /// shadows and scattered patch artwork, so neither the tile nor the chunk grid shows on screen.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainChunkView : MonoBehaviour
    {
        public const float SurfaceHeight = .006f;
        public const int SortingOrder = GroundDecalView.SortingOrder - 100;

        /// <summary>Fallen rocks draw after the ground they lie on and before every standing sprite.</summary>
        public const int TalusSortingOrder = SortingOrder + 1;

        private static readonly int ElevationId = Shader.PropertyToID("_Elevation");
        private static readonly int ElevationUvId = Shader.PropertyToID("_ElevationUv");
        private static readonly int ElevationStepId = Shader.PropertyToID("_ElevationStep");
        private static readonly int VolcanicId = Shader.PropertyToID("_Volcanic");
        private static readonly int HasLandformsId = Shader.PropertyToID("_HasLandforms");
        private static readonly int BasinLevelId = Shader.PropertyToID("_BasinLevel");
        private static readonly int PatchIdsId = Shader.PropertyToID("_PatchIds");
        private static readonly int PatchTextureSizeId = Shader.PropertyToID("_PatchTextureSize");
        private static readonly int TilesPerChunkId = Shader.PropertyToID("_TilesPerChunk");
        private static readonly int TileOriginId = Shader.PropertyToID("_TileOrigin");
        private static readonly int TileSizeId = Shader.PropertyToID("_TileSize");
        private static readonly int ChunkOriginId = Shader.PropertyToID("_ChunkOrigin");
        private static readonly int ChunkSizeId = Shader.PropertyToID("_ChunkSize");
        private static readonly int VariantSeedId = Shader.PropertyToID("_VariantSeed");

        private readonly MountainMeshBuilder _mountainBuilder = new();
        private readonly TalusMeshBuilder _talusBuilder = new();
        private TerrainChunkRenderResources _resources;
        private bool _ownsResources;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Texture2D _elevation;
        private Texture2D _volcanic;
        private ushort[] _volcanicTexels = System.Array.Empty<ushort>();
        private Texture2D _patchIds;
        private ushort[] _elevationTexels = System.Array.Empty<ushort>();
        private float[] _elevationScratch = System.Array.Empty<float>();
        private float[] _borderDistance = System.Array.Empty<float>();
        private float[] _craterMarks = System.Array.Empty<float>();
        private Color32[] _patchTexels = System.Array.Empty<Color32>();
        private MaterialPropertyBlock _propertyBlock;
        private MeshRenderer _mountainRenderer;
        private Mesh _mountainMesh;
        private MeshRenderer _talusRenderer;
        private Mesh _talusMesh;

        // Landforms never change after generation, so a rebuild for a dug tile only refills the patch map.
        private TerrainTileMap _elevationMap;
        private TerrainTileCoordinate _elevationOrigin;
        private bool _hasLandforms;

        /// <summary>One when this block contains visible terrain, otherwise zero.</summary>
        public int LayerMeshCount => _meshRenderer != null && _meshRenderer.enabled ? 1 : 0;

        public Texture2D ElevationTexture => _elevation;
        public Texture2D PatchTexture => _patchIds;

        /// <summary>The chunk's mountain mesh, or null when no mountain reaches into it.</summary>
        public Mesh MountainMesh => _mountainRenderer != null && _mountainRenderer.enabled ? _mountainMesh : null;

        public int FallenRockCount => _talusRenderer != null && _talusRenderer.enabled ? _talusBuilder.RockCount : 0;

        public void Configure(TerrainChunkRenderResources resources)
        {
            if (_ownsResources)
            {
                _resources?.Dispose();
            }

            _resources = resources;
            _ownsResources = false;
            BindResources();
        }

        public void Rebuild(TerrainTileMap map, TerrainTileCoordinate origin, int sizeInTiles)
        {
            EnsureComponents();
            if (map == null || sizeInTiles <= 0)
            {
                _meshRenderer.enabled = false;
                return;
            }

            EnsureResources(map, sizeInTiles);
            if (_resources == null || !_resources.IsValid)
            {
                _meshRenderer.enabled = false;
                return;
            }

            float chunkSize = map.TileSize * sizeInTiles;
            float chunkOriginX = origin.MinX(map.TileSize);
            float chunkOriginZ = origin.MinZ(map.TileSize);
            EnsureTextures(_resources.ElevationTextureSize, TerrainControlMapBuilder.PatchTextureSizeFor(sizeInTiles));

            if (!ReferenceEquals(_elevationMap, map) || !_elevationOrigin.Equals(origin))
            {
                _hasLandforms = TerrainControlMapBuilder.FillLandformMaps(map, chunkOriginX, chunkOriginZ, chunkSize,
                    _resources.ControlMapResolution, _resources.ElevationGutter, _resources.Appearance,
                    _elevationScratch, _borderDistance, _craterMarks, _elevationTexels, _volcanicTexels,
                    out bool hasMountain);
                _elevation.SetPixelData(_elevationTexels, 0);
                _elevation.Apply(false, false);
                _volcanic.SetPixelData(_volcanicTexels, 0);
                _volcanic.Apply(false, false);
                RebuildMountainMeshes(hasMountain, map.WorldSeed, chunkSize, chunkOriginX, chunkOriginZ);
                _elevationMap = map;
                _elevationOrigin = origin;
            }

            bool hasLandforms = _hasLandforms;
            bool hasPatches = TerrainControlMapBuilder.FillPatchIds(map, origin, sizeInTiles,
                _resources.BlendDistance, _patchTexels);
            _patchIds.SetPixels32(_patchTexels);
            _patchIds.Apply(false, false);

            _propertyBlock ??= new MaterialPropertyBlock();
            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture(ElevationId, _elevation);
            // Laid out exactly like the landform map, so it shares its UV transform.
            _propertyBlock.SetTexture(VolcanicId, _volcanic);
            float elevationSize = _resources.ElevationTextureSize;
            float scale = _resources.ControlMapResolution / elevationSize;
            float offset = (_resources.ElevationGutter + .5f) / elevationSize;
            _propertyBlock.SetVector(ElevationUvId, new Vector4(scale, scale, offset, offset));
            _propertyBlock.SetFloat(ElevationStepId, chunkSize / _resources.ControlMapResolution);
            _propertyBlock.SetFloat(HasLandformsId, hasLandforms ? 1f : 0f);
            _propertyBlock.SetFloat(BasinLevelId,
                hasLandforms ? map.SampleLandform(chunkOriginX, chunkOriginZ).BasinLevel : 0f);
            _propertyBlock.SetTexture(PatchIdsId, _patchIds);
            _propertyBlock.SetFloat(PatchTextureSizeId, _patchIds.width);
            _propertyBlock.SetFloat(TilesPerChunkId, sizeInTiles);
            _propertyBlock.SetVector(TileOriginId, new Vector4(origin.X, origin.Z, 0f, 0f));
            _propertyBlock.SetFloat(TileSizeId, map.TileSize);
            _propertyBlock.SetVector(ChunkOriginId, new Vector4(chunkOriginX, chunkOriginZ, 0f, 0f));
            _propertyBlock.SetFloat(ChunkSizeId, chunkSize);
            _propertyBlock.SetFloat(VariantSeedId, map.WorldSeed);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
            _meshRenderer.enabled = hasLandforms || hasPatches;
            // The mountain shader marches the same height map for its self-shadowing.
            if (_mountainRenderer != null)
            {
                _mountainRenderer.SetPropertyBlock(_propertyBlock);
            }
        }

        private void RebuildMountainMeshes(bool hasMountain, int worldSeed, float chunkSize, float chunkOriginX,
            float chunkOriginZ)
        {
            bool mountainBuilt = false;
            bool rocksBuilt = false;
            // Rocks lie around mountains that may stand in the next chunk and around craters, so they are
            // built whenever the chunk has landforms, not only when a mountain reaches into it.
            if (_hasLandforms && _resources.MountainMaterial != null)
            {
                EnsureChildRenderer(ref _mountainRenderer, ref _mountainMesh, "Mountains", _resources.MountainMaterial);
                mountainBuilt = hasMountain && _mountainBuilder.Build(_mountainMesh, _borderDistance,
                    _elevationScratch, _resources.ElevationTextureSize, _resources.ElevationGutter,
                    _resources.ControlMapResolution, chunkSize, chunkOriginX, chunkOriginZ, _resources.Appearance);
            }

            if (_hasLandforms && _resources.TalusMaterial != null && _resources.TalusSliceCount > 0)
            {
                EnsureChildRenderer(ref _talusRenderer, ref _talusMesh, "Fallen Rocks", _resources.TalusMaterial);
                _talusRenderer.sortingOrder = TalusSortingOrder;
                rocksBuilt = _talusBuilder.Build(_talusMesh, _borderDistance, _craterMarks,
                    _resources.ElevationTextureSize, _resources.ElevationGutter, _resources.ControlMapResolution,
                    chunkSize, chunkOriginX, chunkOriginZ, _resources.Appearance, worldSeed,
                    _resources.TalusFirstSlice, _resources.TalusSliceCount);
            }

            if (_mountainRenderer != null)
            {
                _mountainRenderer.enabled = mountainBuilt;
            }

            if (_talusRenderer != null)
            {
                _talusRenderer.enabled = rocksBuilt;
            }
        }

        private void EnsureChildRenderer(ref MeshRenderer renderer, ref Mesh mesh, string childName, Material material)
        {
            if (renderer == null)
            {
                var child = new GameObject(childName);
                child.transform.SetParent(transform, false);
                // The chunk root sits a hair above the ground for the ground quad; meshes measure from y = 0.
                child.transform.localPosition = Vector3.down * SurfaceHeight;
                child.AddComponent<MeshFilter>();
                renderer = child.AddComponent<MeshRenderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            if (mesh == null)
            {
                mesh = new Mesh { name = $"{childName} Mesh" };
                mesh.MarkDynamic();
                renderer.GetComponent<MeshFilter>().sharedMesh = mesh;
            }

            renderer.sharedMaterial = material;
        }

        private void EnsureResources(TerrainTileMap map, int sizeInTiles)
        {
            float chunkSize = map.TileSize * sizeInTiles;
            if (_resources != null && Mathf.Approximately(_resources.ChunkSize, chunkSize))
            {
                BindResources();
                return;
            }

            if (_ownsResources)
            {
                _resources?.Dispose();
            }

            _resources = new TerrainChunkRenderResources(map.Layers, chunkSize);
            _ownsResources = true;
            BindResources();
        }

        private void BindResources()
        {
            EnsureComponents();
            _meshFilter.sharedMesh = _resources?.Mesh;
            _meshRenderer.sharedMaterial = _resources?.Material;
        }

        private void EnsureComponents()
        {
            if (_meshFilter == null)
            {
                _meshFilter = GetComponent<MeshFilter>();
                if (_meshFilter == null)
                {
                    _meshFilter = gameObject.AddComponent<MeshFilter>();
                }
            }

            if (_meshRenderer == null)
            {
                _meshRenderer = GetComponent<MeshRenderer>();
                if (_meshRenderer == null)
                {
                    _meshRenderer = gameObject.AddComponent<MeshRenderer>();
                }

                _meshRenderer.sortingOrder = SortingOrder;
                _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _meshRenderer.receiveShadows = false;
                _meshRenderer.enabled = false;
            }
        }

        private void EnsureTextures(int elevationSize, int patchSize)
        {
            if (_elevation == null || _elevation.width != elevationSize)
            {
                DestroyRuntimeObject(_elevation);
                // Half precision keeps borders placed to a few centimetres; eight bits would step them by
                // a quarter metre on gentle slopes.
                _elevation = new Texture2D(elevationSize, elevationSize, TextureFormat.RGBAHalf, false, true)
                {
                    name = "Terrain Landform Map",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                DestroyRuntimeObject(_volcanic);
                _volcanic = new Texture2D(elevationSize, elevationSize, TextureFormat.RGHalf, false, true)
                {
                    name = "Terrain Volcanic Map",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                int count = elevationSize * elevationSize;
                _volcanicTexels = new ushort[count * TerrainControlMapBuilder.VolcanicChannels];
                _elevationTexels = new ushort[count * TerrainControlMapBuilder.LandformChannels];
                _elevationScratch = new float[count];
                _borderDistance = new float[count];
                _craterMarks = new float[count];
                _elevationMap = null;
            }

            if (_patchIds == null || _patchIds.width != patchSize)
            {
                DestroyRuntimeObject(_patchIds);
                // Point sampled: these are identifiers, and blending two layer numbers would name a third.
                _patchIds = new Texture2D(patchSize, patchSize, TextureFormat.RGBA32, false, true)
                {
                    name = "Terrain Patch Ids",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                _patchTexels = new Color32[patchSize * patchSize];
            }
        }

        private void OnDestroy()
        {
            DestroyRuntimeObject(_elevation);
            DestroyRuntimeObject(_volcanic);
            DestroyRuntimeObject(_patchIds);
            DestroyRuntimeObject(_mountainMesh);
            DestroyRuntimeObject(_talusMesh);
            _elevation = null;
            _volcanic = null;
            _patchIds = null;
            _mountainMesh = null;
            _talusMesh = null;
            if (_ownsResources)
            {
                _resources?.Dispose();
            }

            _resources = null;
        }

        private static void DestroyRuntimeObject(Object instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }
    }
}
