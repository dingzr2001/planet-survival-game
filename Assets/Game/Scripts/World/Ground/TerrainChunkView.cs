using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// Draws one terrain chunk as a fixed quad. Two control maps carry up to eight surface weights; the
    /// shared shader samples each gameplay tile at its centre and fits one complete terrain illustration
    /// into that tile. This keeps rendering batched without cutting artwork at a continuous patch edge.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainChunkView : MonoBehaviour
    {
        public const float SurfaceHeight = .006f;
        public const int SortingOrder = GroundDecalView.SortingOrder - 100;

        private static readonly int Control0Id = Shader.PropertyToID("_Control0");
        private static readonly int Control1Id = Shader.PropertyToID("_Control1");
        private static readonly int ControlUvId = Shader.PropertyToID("_ControlUv");
        private static readonly int TilesPerChunkId = Shader.PropertyToID("_TilesPerChunk");
        private static readonly int TileOriginId = Shader.PropertyToID("_TileOrigin");
        private static readonly int VariantSeedId = Shader.PropertyToID("_VariantSeed");

        private TerrainChunkRenderResources _resources;
        private bool _ownsResources;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Texture2D _control0;
        private Texture2D _control1;
        private Color32[] _control0Pixels = System.Array.Empty<Color32>();
        private Color32[] _control1Pixels = System.Array.Empty<Color32>();
        private MaterialPropertyBlock _propertyBlock;

        /// <summary>One when this block contains visible terrain, otherwise zero.</summary>
        public int LayerMeshCount => _meshRenderer != null && _meshRenderer.enabled ? 1 : 0;

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

            EnsureControlTextures(_resources.TextureSize);
            bool hasCoverage = TerrainControlMapBuilder.Fill(
                map, origin, sizeInTiles, _resources.ControlMapResolution, _resources.BlendDistance,
                _control0Pixels, _control1Pixels);
            _control0.SetPixels32(_control0Pixels);
            _control1.SetPixels32(_control1Pixels);
            _control0.Apply(false, false);
            _control1.Apply(false, false);

            _propertyBlock ??= new MaterialPropertyBlock();
            _meshRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetTexture(Control0Id, _control0);
            _propertyBlock.SetTexture(Control1Id, _control1);
            float scale = _resources.ControlMapResolution / (float)_resources.TextureSize;
            float offset = (TerrainControlMapBuilder.GutterSize + .5f) / _resources.TextureSize;
            _propertyBlock.SetVector(ControlUvId, new Vector4(scale, scale, offset, offset));
            _propertyBlock.SetFloat(TilesPerChunkId, sizeInTiles);
            _propertyBlock.SetVector(TileOriginId, new Vector4(origin.X, origin.Z, 0f, 0f));
            _propertyBlock.SetFloat(VariantSeedId, map.WorldSeed);
            _meshRenderer.SetPropertyBlock(_propertyBlock);
            _meshRenderer.enabled = hasCoverage;
        }

        private void EnsureResources(TerrainTileMap map, int sizeInTiles)
        {
            float chunkSize = map.TileSize * sizeInTiles;
            if (_resources != null
                && Mathf.Approximately(_resources.Mesh.bounds.size.x, chunkSize))
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

        private void EnsureControlTextures(int textureSize)
        {
            if (_control0 != null && _control0.width == textureSize)
            {
                return;
            }

            ReleaseControlTextures();
            _control0 = CreateControlTexture("Terrain Control 0", textureSize);
            _control1 = CreateControlTexture("Terrain Control 1", textureSize);
            int pixelCount = textureSize * textureSize;
            _control0Pixels = new Color32[pixelCount];
            _control1Pixels = new Color32[pixelCount];
        }

        private static Texture2D CreateControlTexture(string textureName, int textureSize)
        {
            var texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false, true)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            return texture;
        }

        private void OnDestroy()
        {
            ReleaseControlTextures();
            if (_ownsResources)
            {
                _resources?.Dispose();
            }

            _resources = null;
        }

        private void ReleaseControlTextures()
        {
            DestroyRuntimeObject(_control0);
            DestroyRuntimeObject(_control1);
            _control0 = null;
            _control1 = null;
            _control0Pixels = System.Array.Empty<Color32>();
            _control1Pixels = System.Array.Empty<Color32>();
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
