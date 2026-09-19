using System;
using System.Collections.Generic;
using PlanetSurvival.Gathering.Definitions;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// One kind of ground that covers tiles instead of standing on them. Unlike a resource node a surface
    /// has no object to remove: digging it wears it down until the tile falls back to the base regolith.
    /// </summary>
    [CreateAssetMenu(menuName = "Planet Survival/World/Terrain Surface", fileName = "TerrainSurfaceDefinition")]
    public sealed class TerrainSurfaceDefinition : ScriptableObject
    {
        public const int MaximumTextureVariantCount = 3;

        [SerializeField] private string _terrainId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField, Tooltip("Square surface artwork fitted once into every terrain tile of this type.")]
        private Texture2D _texture;
        [SerializeField, Tooltip("Optional alternate artwork chosen deterministically per terrain tile.")]
        private Texture2D[] _textureVariants = Array.Empty<Texture2D>();
        [SerializeField, Min(.1f), Tooltip("Legacy world-space repeat size retained for asset compatibility. Tile-aligned rendering uses the gameplay tile size instead.")]
        private float _textureTileSize = 8f;
        [SerializeField, Min(1), Tooltip("Digs needed to wear one tile back down to the base regolith.")]
        private int _digCount = 1;
        [SerializeField, Min(.1f)] private float _digDuration = 1.6f;
        [SerializeField, Min(.1f)] private float _digDistance = 2f;
        [SerializeField] private string _requiredToolItemId = string.Empty;
        [SerializeField, Tooltip("Handed over on every dig, not only the one that clears the tile, so harder ground is worth the extra swings.")]
        private ResourceYield[] _yields = Array.Empty<ResourceYield>();

        public string TerrainId => _terrainId;
        public string DisplayName => _displayName;
        public Texture2D Texture => _texture;
        public int TextureVariantCount => _texture == null
            ? 0
            : 1 + Mathf.Min(_textureVariants?.Length ?? 0, MaximumTextureVariantCount - 1);
        public float TextureTileSize => Mathf.Max(.1f, _textureTileSize);
        public int DigCount => Mathf.Max(1, _digCount);
        public float DigDuration => Mathf.Max(.1f, _digDuration);
        public float DigDistance => Mathf.Max(.1f, _digDistance);
        public string RequiredToolItemId => _requiredToolItemId;
        public IReadOnlyList<ResourceYield> Yields => _yields;

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(_terrainId) || string.IsNullOrWhiteSpace(_displayName))
            {
                error = "Terrain ID and display name are required.";
                return false;
            }

            if (_yields == null || _yields.Length == 0)
            {
                error = $"Terrain '{_terrainId}' requires at least one yield.";
                return false;
            }

            for (int i = 0; i < _yields.Length; i++)
            {
                if (_yields[i].Item == null || _yields[i].Quantity <= 0)
                {
                    error = $"Terrain '{_terrainId}' has an invalid yield at index {i}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public void Configure(string terrainId, string displayName, int digCount, float digDuration,
            string requiredToolItemId, params ResourceYield[] yields)
        {
            _terrainId = terrainId;
            _displayName = displayName;
            _digCount = Mathf.Max(1, digCount);
            _digDuration = Mathf.Max(.1f, digDuration);
            _requiredToolItemId = requiredToolItemId ?? string.Empty;
            _yields = yields ?? Array.Empty<ResourceYield>();
        }

        public void ConfigureTexture(Texture2D texture, float textureTileSize)
        {
            _texture = texture;
            _textureVariants = Array.Empty<Texture2D>();
            _textureTileSize = Mathf.Max(.1f, textureTileSize);
        }

        /// <summary>
        /// Configures up to three equally likely tile artworks. The first texture remains the primary
        /// texture so existing assets and renderers that do not understand variants still draw correctly.
        /// </summary>
        public void ConfigureTextureVariants(float textureTileSize, params Texture2D[] textures)
        {
            _textureTileSize = Mathf.Max(.1f, textureTileSize);
            if (textures == null || textures.Length == 0)
            {
                _texture = null;
                _textureVariants = Array.Empty<Texture2D>();
                return;
            }

            _texture = textures[0];
            int additionalCount = Mathf.Min(textures.Length - 1, MaximumTextureVariantCount - 1);
            _textureVariants = new Texture2D[additionalCount];
            for (int i = 0; i < additionalCount; i++)
            {
                _textureVariants[i] = textures[i + 1] != null ? textures[i + 1] : _texture;
            }
        }

        public Texture2D GetTextureVariant(int index)
        {
            if (index <= 0 || _textureVariants == null || index > _textureVariants.Length)
            {
                return _texture;
            }

            return _textureVariants[index - 1] != null ? _textureVariants[index - 1] : _texture;
        }
    }
}
