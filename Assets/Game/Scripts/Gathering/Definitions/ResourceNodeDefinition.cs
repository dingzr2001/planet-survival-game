using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.Gathering.Definitions
{
    [CreateAssetMenu(menuName = "Planet Survival/Gathering/Resource Node", fileName = "ResourceNodeDefinition")]
    public sealed class ResourceNodeDefinition : ScriptableObject
    {
        [SerializeField] private string _resourceId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField, Min(.1f)] private float _gatherDuration = 2f;
        [SerializeField, Min(.1f)] private float _gatherDistance = 2f;
        [SerializeField] private string _requiredToolItemId = string.Empty;
        [SerializeField] private ResourceYield[] _yields = Array.Empty<ResourceYield>();
        [SerializeField, Tooltip("Transparent cutouts used by the flat 2.5D world presentation. With more than one, each node picks a variant, so a stretch of surface does not repeat one silhouette. Every variant shares this resource's display scale, so they should have similar proportions.")]
        private Sprite[] _worldSprites = Array.Empty<Sprite>();
        [SerializeField] private Vector3 _displayScale = Vector3.one;
        [SerializeField, Tooltip("Whether the node is solid. Boulders block the way; something lying flat on the ground, such as an ice sheet, should be walked over instead. Either way the volume still carries the interaction.")]
        private bool _blocksMovement = true;
        [SerializeField, Tooltip("Draws the cutout on the ground using the X/Z footprint instead of as a camera-facing upright sprite.")]
        private bool _liesFlatOnGround;

        public string ResourceId => _resourceId;
        public string DisplayName => _displayName;
        public float GatherDuration => _gatherDuration;
        public float GatherDistance => _gatherDistance;
        public string RequiredToolItemId => _requiredToolItemId;
        public IReadOnlyList<ResourceYield> Yields => _yields;
        public IReadOnlyList<Sprite> WorldSprites => _worldSprites;

        /// <summary>The first cutout, or null when this resource has no artwork yet.</summary>
        public Sprite WorldSprite => _worldSprites.Length > 0 ? _worldSprites[0] : null;

        public Vector3 DisplayScale => _displayScale;

        /// <summary>False when the player walks straight over the node instead of around it.</summary>
        public bool BlocksMovement => _blocksMovement;
        public bool LiesFlatOnGround => _liesFlatOnGround;

        /// <summary>
        /// Picks the cutout for one node. The same seed always returns the same variant, which is what
        /// keeps a deposit looking like itself after its chunk is unloaded and streamed back in.
        /// </summary>
        public Sprite SelectWorldSprite(int variantSeed)
        {
            if (_worldSprites.Length == 0)
            {
                return null;
            }

            // The seed is a hash and may be negative; masking keeps the remainder in range.
            return _worldSprites[(variantSeed & int.MaxValue) % _worldSprites.Length];
        }

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(_resourceId) || string.IsNullOrWhiteSpace(_displayName))
            {
                error = "Resource ID and display name are required.";
                return false;
            }

            if (_gatherDuration <= 0f || _gatherDistance <= 0f || _yields == null || _yields.Length == 0)
            {
                error = $"Resource '{_resourceId}' requires positive timing, distance, and at least one yield.";
                return false;
            }

            for (int i = 0; i < _yields.Length; i++)
            {
                if (_yields[i].Item == null || _yields[i].Quantity <= 0)
                {
                    error = $"Resource '{_resourceId}' has an invalid yield at index {i}.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public void Configure(string resourceId, string displayName, float gatherDuration, float gatherDistance,
            string requiredToolItemId, Vector3 displayScale, params ResourceYield[] yields)
        {
            _resourceId = resourceId;
            _displayName = displayName;
            _gatherDuration = Mathf.Max(.1f, gatherDuration);
            _gatherDistance = Mathf.Max(.1f, gatherDistance);
            _requiredToolItemId = requiredToolItemId ?? string.Empty;
            _displayScale = displayScale;
            _yields = yields ?? Array.Empty<ResourceYield>();
        }

        /// <summary>
        /// Sets whether the node is solid. Kept separate from <see cref="Configure"/> so existing
        /// resources keep blocking the way unless they are explicitly made walkable.
        /// </summary>
        public void ConfigureCollision(bool blocksMovement)
        {
            _blocksMovement = blocksMovement;
        }

        /// <summary>Chooses between an upright cutout and a footprint-aligned surface.</summary>
        public void ConfigureGroundPresentation(bool liesFlatOnGround)
        {
            _liesFlatOnGround = liesFlatOnGround;
        }

        /// <summary>
        /// Replaces the cutout set. Order decides which seed maps to which look, so the caller must
        /// supply a stable order or nodes change appearance between runs.
        /// </summary>
        public void SetWorldSprites(params Sprite[] worldSprites)
        {
            _worldSprites = worldSprites ?? Array.Empty<Sprite>();
        }
    }
}
