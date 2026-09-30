using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// The meshes, materials and artwork shared by every loaded terrain chunk: the ground quad and its
    /// material, the mountain material, the fallen-rock material, and the texture array of patch and rock
    /// artwork. Owned by the streamer, released on <see cref="Dispose"/>; chunks only hold their own control
    /// textures and meshes.
    /// </summary>
    public sealed class TerrainChunkRenderResources : IDisposable
    {
        public const int DefaultControlMapResolution = 64;
        public const float DefaultBlendDistance = 1.2f;
        public const string ShaderName = "Planet Survival/Terrain Blend";
        // Spacing of the ground grid: a crater's rim, the narrowest part of the sunken ground, spans about
        // two of these on the smallest craters.
        private const float ChunkMeshCellSize = .5f;

        // Metres below the plane the ground bounds reach, past the deepest crater at the mountains' scale.
        private const float SunkenBoundsDepth = 60f;

        public const string MountainShaderName = "Planet Survival/Mountain";
        public const string TalusShaderName = "Planet Survival/Talus";

        /// <summary>
        /// Edge length of each artwork slice. Patches and rocks are drawn a few metres wide, a few hundred
        /// pixels on screen at most, so 512 keeps them sharp at a fraction of the memory of the source art.
        /// </summary>
        private const int PatchSliceSize = 512;

        private static readonly int LayerCountId = Shader.PropertyToID("_LayerCount");
        private static readonly int LayerFirstSliceId = Shader.PropertyToID("_LayerFirstSlice");
        private static readonly int LayerVariantCountId = Shader.PropertyToID("_LayerVariantCount");
        private static readonly int LayerContinuousId = Shader.PropertyToID("_LayerContinuous");
        private static readonly int LayerTextureScaleId = Shader.PropertyToID("_LayerTextureScale");
        private static readonly int PatchArrayId = Shader.PropertyToID("_PatchArray");
        private static readonly int MountainTexId = Shader.PropertyToID("_MountainTex");
        private static readonly int MountainTexScaleId = Shader.PropertyToID("_MountainTexScale");
        private static readonly int MountainTintId = Shader.PropertyToID("_MountainTint");
        private static readonly int CliffColorId = Shader.PropertyToID("_CliffColor");
        private static readonly int CliffTexId = Shader.PropertyToID("_CliffTex");
        private static readonly int CliffTexScaleId = Shader.PropertyToID("_CliffTexScale");
        private static readonly int TalusTintId = Shader.PropertyToID("_TalusTint");
        private static readonly int VerticalScaleId = Shader.PropertyToID("_VerticalScale");
        private static readonly int ShadowReachId = Shader.PropertyToID("_ShadowReach");
        private static readonly int ReliefShadingId = Shader.PropertyToID("_ReliefShading");
        private static readonly int ShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
        private static readonly int IceTexId = Shader.PropertyToID("_IceTex");
        private static readonly int IceTexScaleId = Shader.PropertyToID("_IceTexScale");
        private static readonly int BasinTintId = Shader.PropertyToID("_BasinTint");
        private static readonly int FissureFirstSliceId = Shader.PropertyToID("_FissureFirstSlice");
        private static readonly int FissureSliceCountId = Shader.PropertyToID("_FissureSliceCount");
        private static readonly int FissureDensityId = Shader.PropertyToID("_FissureDensity");
        private static readonly int FissureSizeRangeId = Shader.PropertyToID("_FissureSizeRange");
        private static readonly int LavaTexId = Shader.PropertyToID("_LavaTex");
        private static readonly int LavaTexScaleId = Shader.PropertyToID("_LavaTexScale");
        private static readonly int LavaFlowSpeedId = Shader.PropertyToID("_LavaFlowSpeed");
        private static readonly int LavaGlowColorId = Shader.PropertyToID("_LavaGlowColor");
        private static readonly int LavaGlowReachId = Shader.PropertyToID("_LavaGlowReach");
        private static readonly int LavaCrustColorId = Shader.PropertyToID("_LavaCrustColor");
        private static readonly int RockGroundTexId = Shader.PropertyToID("_RockGroundTex");
        private static readonly int RockGroundTexScaleId = Shader.PropertyToID("_RockGroundTexScale");
        private static readonly int RockGroundTintId = Shader.PropertyToID("_RockGroundTint");
        private static readonly int CraterReliefStrengthId = Shader.PropertyToID("_CraterReliefStrength");
        private static readonly int EjectaReachId = Shader.PropertyToID("_EjectaReach");
        private static readonly int CraterFloorTexId = Shader.PropertyToID("_CraterFloorTex");
        private static readonly int CraterFloorTexScaleId = Shader.PropertyToID("_CraterFloorTexScale");
        private static readonly int CraterFloorTintId = Shader.PropertyToID("_CraterFloorTint");
        private static readonly int CraterEjectaTintId = Shader.PropertyToID("_CraterEjectaTint");
        private static readonly int HillshadeStrengthId = Shader.PropertyToID("_HillshadeStrength");
        private static readonly int EdgeRoughnessId = Shader.PropertyToID("_EdgeRoughness");

        private readonly float[] _layerFirstSlice = new float[TerrainControlMapBuilder.MaximumLayerCount];
        private readonly float[] _layerVariantCount = new float[TerrainControlMapBuilder.MaximumLayerCount];
        private readonly float[] _layerContinuous = new float[TerrainControlMapBuilder.MaximumLayerCount];
        private readonly float[] _layerTextureScale = new float[TerrainControlMapBuilder.MaximumLayerCount];
        private RenderTexture _patchArray;

        /// <summary>Resources for a streamer configured from terrain settings.</summary>
        public TerrainChunkRenderResources(TerrainPatchSettings settings)
            : this(settings != null ? settings.Layers : null,
                settings != null ? settings.ChunkSize : 1f,
                settings != null ? settings.BlendShader : null,
                settings != null ? settings.ControlMapResolution : DefaultControlMapResolution,
                settings != null ? settings.BlendDistance : DefaultBlendDistance,
                settings != null ? settings.LandformAppearance : null,
                settings != null ? settings.IceLakeSurface : null,
                settings != null ? settings.MountainShader : null,
                settings != null ? settings.TalusShader : null)
        {
        }

        public TerrainChunkRenderResources(IReadOnlyList<TerrainPatchLayer> layers, float chunkSize,
            Shader shader = null, int controlMapResolution = DefaultControlMapResolution,
            float blendDistance = DefaultBlendDistance, LandformAppearance appearance = null,
            TerrainSurfaceDefinition iceLakeSurface = null, Shader mountainShader = null, Shader talusShader = null)
        {
            ChunkSize = Mathf.Max(.1f, chunkSize);
            ControlMapResolution = Mathf.Max(16, controlMapResolution);
            BlendDistance = Mathf.Max(.05f, blendDistance);
            Appearance = appearance ?? new LandformAppearance();
            ElevationGutter = TerrainControlMapBuilder.ElevationGutterFor(
                ChunkSize, ControlMapResolution, Appearance.SampleReach);
            ElevationTextureSize = TerrainControlMapBuilder.ElevationTextureSizeFor(
                ControlMapResolution, ElevationGutter);
            Mesh = CreateChunkMesh(ChunkSize);

            Material = CreateMaterial(shader, ShaderName, "Runtime Terrain Blend");
            if (Material == null)
            {
                return;
            }

            MountainMaterial = CreateMaterial(mountainShader, MountainShaderName, "Runtime Mountain");
            TalusMaterial = CreateMaterial(talusShader, TalusShaderName, "Runtime Fallen Rocks");
            LayerCount = layers != null ? Mathf.Min(layers.Count, TerrainControlMapBuilder.MaximumLayerCount) : 0;
            if (layers != null && layers.Count > TerrainControlMapBuilder.MaximumLayerCount)
            {
                Debug.LogWarning(
                    $"Terrain rendering supports {TerrainControlMapBuilder.MaximumLayerCount} patch layers; "
                    + $"{layers.Count - TerrainControlMapBuilder.MaximumLayerCount} lower-priority layers will not draw.");
            }

            BindArtwork(layers);
            BindAppearance(iceLakeSurface);
        }

        /// <summary>Grid shared by every chunk's ground; see <see cref="CreateChunkMesh"/>.</summary>
        public Mesh Mesh { get; }

        /// <summary>The ground material, drawn on each chunk's quad.</summary>
        public Material Material { get; }

        /// <summary>Material of the 3D mountain meshes; null when its shader is unavailable.</summary>
        public Material MountainMaterial { get; }

        /// <summary>Material of the fallen-rock billboards; null when its shader is unavailable.</summary>
        public Material TalusMaterial { get; }

        public float ChunkSize { get; }
        public int ControlMapResolution { get; }
        public float BlendDistance { get; }
        public int LayerCount { get; }
        public LandformAppearance Appearance { get; }
        public int ElevationGutter { get; }
        public int ElevationTextureSize { get; }

        /// <summary>First slice of the fallen-rock cutouts, which follow the patch layers in the array.</summary>
        public int TalusFirstSlice { get; private set; }

        public int TalusSliceCount { get; private set; }

        /// <summary>Artwork-array slice of the first lava fissure cutout, after the fallen rocks.</summary>
        public int FissureFirstSlice { get; private set; }

        /// <summary>Number of lava fissure cutouts in the artwork array.</summary>
        public int FissureSliceCount { get; private set; }

        /// <summary>Slices in the artwork array: every patch layer variant, then the fallen rocks.</summary>
        public int PatchSliceCount { get; private set; }

        public bool IsValid => Mesh != null && Material != null;

        public void Dispose()
        {
            if (_patchArray != null)
            {
                _patchArray.Release();
            }

            DestroyRuntimeObject(_patchArray);
            DestroyRuntimeObject(Material);
            DestroyRuntimeObject(MountainMaterial);
            DestroyRuntimeObject(TalusMaterial);
            DestroyRuntimeObject(Mesh);
            _patchArray = null;
        }

        public bool IsContinuous(int layerIndex) =>
            layerIndex >= 0 && layerIndex < LayerCount && _layerContinuous[layerIndex] > .5f;

        /// <summary>Where a layer's variants start in the artwork array, and how many there are.</summary>
        public Vector2Int SliceRangeOf(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= LayerCount)
            {
                return Vector2Int.zero;
            }

            return new Vector2Int((int)_layerFirstSlice[layerIndex], (int)_layerVariantCount[layerIndex]);
        }

        private static Material CreateMaterial(Shader shader, string shaderName, string materialName)
        {
            Shader resolved = shader != null ? shader : Shader.Find(shaderName);
            if (resolved == null)
            {
                Debug.LogError($"Terrain shader '{shaderName}' could not be found.");
                return null;
            }

            return new Material(resolved) { name = materialName };
        }

        /// <summary>
        /// Copies every layer's artwork, then the fallen rocks, into one texture array, so the shaders can
        /// pick any layer, variant or rock from a single sampler instead of branching over many.
        /// </summary>
        private void BindArtwork(IReadOnlyList<TerrainPatchLayer> layers)
        {
            var sources = new List<Texture>();
            for (int i = 0; i < LayerCount; i++)
            {
                TerrainSurfaceDefinition surface = layers[i].Surface;
                int variants = surface != null ? surface.TextureVariantCount : 0;
                _layerFirstSlice[i] = sources.Count;
                _layerVariantCount[i] = Mathf.Max(1, variants);
                _layerContinuous[i] =
                    surface != null && surface.PatchRendering == TerrainPatchRendering.Continuous ? 1f : 0f;
                _layerTextureScale[i] = surface != null ? 1f / surface.TextureTileSize : 1f;
                if (variants == 0)
                {
                    // A layer without artwork still owns a slice so its indices stay valid; clear shows nothing.
                    sources.Add(null);
                    continue;
                }

                for (int variant = 0; variant < variants; variant++)
                {
                    sources.Add(surface.GetTextureVariant(variant));
                }
            }

            TalusFirstSlice = sources.Count;
            IReadOnlyList<Texture2D> stones = Appearance.TalusStones;
            for (int i = 0; i < stones.Count; i++)
            {
                if (stones[i] != null)
                {
                    sources.Add(stones[i]);
                }
            }

            TalusSliceCount = sources.Count - TalusFirstSlice;
            FissureFirstSlice = sources.Count;
            IReadOnlyList<Texture2D> fissures = Appearance.LavaFissures;
            for (int i = 0; i < fissures.Count; i++)
            {
                if (fissures[i] != null)
                {
                    sources.Add(fissures[i]);
                }
            }

            FissureSliceCount = sources.Count - FissureFirstSlice;
            PatchSliceCount = sources.Count;
            Material.SetFloat(FissureFirstSliceId, FissureFirstSlice);
            Material.SetFloat(FissureSliceCountId, FissureSliceCount);
            Material.SetFloat(LayerCountId, LayerCount);
            Material.SetFloatArray(LayerFirstSliceId, _layerFirstSlice);
            Material.SetFloatArray(LayerVariantCountId, _layerVariantCount);
            Material.SetFloatArray(LayerContinuousId, _layerContinuous);
            Material.SetFloatArray(LayerTextureScaleId, _layerTextureScale);
            if (sources.Count == 0)
            {
                return;
            }

            _patchArray = new RenderTexture(PatchSliceSize, PatchSliceSize, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                name = "Terrain Artwork",
                dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray,
                volumeDepth = sources.Count,
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Trilinear,
                // Continuous surfaces tile in world space; scattered cutouts never sample outside 0-1.
                wrapMode = TextureWrapMode.Repeat,
                anisoLevel = 4
            };
            if (!_patchArray.Create())
            {
                // Headless editors have no graphics device; the terrain simply draws without artwork.
                return;
            }

            for (int slice = 0; slice < sources.Count; slice++)
            {
                Graphics.Blit(sources[slice] != null ? sources[slice] : Texture2D.blackTexture, _patchArray, 0, slice);
            }

            _patchArray.GenerateMips();
            Material.SetTexture(PatchArrayId, _patchArray);
            if (TalusMaterial != null)
            {
                TalusMaterial.SetTexture(PatchArrayId, _patchArray);
            }
        }

        /// <remarks>
        /// Ice lakes are drawn exactly like the sheet-ice patches of the same surface, so the two never read
        /// as different kinds of ice.
        /// </remarks>
        private void BindAppearance(TerrainSurfaceDefinition iceLakeSurface)
        {
            LandformAppearance appearance = Appearance;
            Material.SetFloat(ShadowReachId, appearance.ShadowReach);
            Material.SetFloat(ShadowStrengthId, appearance.ShadowStrength);
            Texture2D iceTexture = iceLakeSurface != null ? iceLakeSurface.Texture : null;
            Material.SetTexture(IceTexId, iceTexture != null ? iceTexture : Texture2D.whiteTexture);
            Material.SetFloat(IceTexScaleId, iceLakeSurface != null ? 1f / iceLakeSurface.TextureTileSize : 1f);
            Material.SetColor(BasinTintId, appearance.BasinTint);
            Material.SetTexture(LavaTexId,
                appearance.LavaTexture != null ? appearance.LavaTexture : Texture2D.blackTexture);
            Material.SetFloat(LavaTexScaleId, 1f / appearance.LavaTextureSize);
            Material.SetFloat(LavaFlowSpeedId, appearance.LavaFlowSpeed);
            Material.SetColor(LavaGlowColorId, appearance.LavaGlowColor);
            Material.SetFloat(LavaGlowReachId, appearance.LavaGlowReach);
            Material.SetColor(LavaCrustColorId, appearance.LavaCrustColor);
            Material.SetTexture(RockGroundTexId,
                appearance.RockGroundTexture != null ? appearance.RockGroundTexture : Texture2D.grayTexture);
            Material.SetFloat(RockGroundTexScaleId, 1f / appearance.RockGroundTextureSize);
            Material.SetColor(RockGroundTintId, appearance.RockGroundTint);
            Material.SetFloat(FissureDensityId, appearance.FissureDensity);
            Material.SetVector(FissureSizeRangeId,
                new Vector4(appearance.MinimumFissureSize, appearance.MaximumFissureSize, 0f, 0f));
            Material.SetFloat(CraterReliefStrengthId, appearance.CraterReliefStrength);
            Material.SetFloat(EjectaReachId, appearance.EjectaReach);
            Material.SetTexture(CraterFloorTexId,
                appearance.CraterFloorTexture != null ? appearance.CraterFloorTexture : Texture2D.whiteTexture);
            Material.SetFloat(CraterFloorTexScaleId, 1f / appearance.CraterFloorTextureSize);
            Material.SetColor(CraterFloorTintId, appearance.CraterFloorTint);
            Material.SetColor(CraterEjectaTintId, appearance.CraterEjectaTint);
            Material.SetFloat(HillshadeStrengthId, appearance.HillshadeStrength);
            Material.SetFloat(EdgeRoughnessId, appearance.EdgeRoughness);

            if (MountainMaterial != null)
            {
                MountainMaterial.SetTexture(MountainTexId,
                    appearance.MountainTexture != null ? appearance.MountainTexture : Texture2D.whiteTexture);
                MountainMaterial.SetFloat(MountainTexScaleId, 1f / appearance.MountainTextureSize);
                MountainMaterial.SetColor(MountainTintId, appearance.MountainTint);
                MountainMaterial.SetTexture(CliffTexId,
                    appearance.CliffTexture != null ? appearance.CliffTexture : Texture2D.whiteTexture);
                MountainMaterial.SetFloat(CliffTexScaleId, 1f / appearance.CliffTextureSize);
                MountainMaterial.SetColor(CliffColorId, appearance.CliffColor);
                MountainMaterial.SetFloat(VerticalScaleId, appearance.VerticalExaggeration);
                MountainMaterial.SetFloat(ReliefShadingId, appearance.ReliefShading);
            }

            if (TalusMaterial != null)
            {
                TalusMaterial.SetColor(TalusTintId, appearance.TalusTint);
            }
        }

        /// <summary>
        /// One grid shared by every chunk, fine enough for the vertex shader to sink it smoothly into craters
        /// (TerrainSurface.cginc). Its bounds reach down past the deepest crater so a sunken chunk is never
        /// culled while in view.
        /// </summary>
        private static Mesh CreateChunkMesh(float size)
        {
            int cells = Mathf.Max(1, Mathf.CeilToInt(size / ChunkMeshCellSize));
            int perAxis = cells + 1;
            var vertices = new Vector3[perAxis * perAxis];
            var uv = new Vector2[vertices.Length];
            for (int z = 0; z < perAxis; z++)
            {
                for (int x = 0; x < perAxis; x++)
                {
                    float u = (float)x / cells;
                    float v = (float)z / cells;
                    vertices[z * perAxis + x] = new Vector3(u * size, 0f, v * size);
                    uv[z * perAxis + x] = new Vector2(u, v);
                }
            }

            var triangles = new int[cells * cells * 6];
            int triangle = 0;
            for (int z = 0; z < cells; z++)
            {
                for (int x = 0; x < cells; x++)
                {
                    int a = z * perAxis + x;
                    int c = a + perAxis;
                    triangles[triangle++] = a;
                    triangles[triangle++] = c + 1;
                    triangles[triangle++] = a + 1;
                    triangles[triangle++] = a;
                    triangles[triangle++] = c;
                    triangles[triangle++] = c + 1;
                }
            }

            var mesh = new Mesh
            {
                name = "Shared Terrain Chunk Grid",
                indexFormat = vertices.Length > ushort.MaxValue
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
                vertices = vertices,
                uv = uv,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(size * .5f, -SunkenBoundsDepth * .5f, size * .5f),
                new Vector3(size, SunkenBoundsDepth + 2f, size));
            return mesh;
        }

        private static void DestroyRuntimeObject(UnityEngine.Object instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(instance);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
