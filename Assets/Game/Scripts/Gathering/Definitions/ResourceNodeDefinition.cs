using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
        [SerializeField, Tooltip("Controls whether the cutout is an upright camera-facing prop or a floor decal below all actors.")]
        private ResourceVisualMode _visualMode = ResourceVisualMode.Billboard;
        [SerializeField, Tooltip("Disables the generic oval shadow when the artwork already represents a surface touching the ground.")]
        private bool _suppressBlobShadow;
        [SerializeField, Tooltip("Possible physical X/Z sizes in world units for one ground decal. Values may be fractional; repeating a size weights it more heavily. Non-ground resources ignore this setting.")]
        private Vector2[] _groundPatchSizes = Array.Empty<Vector2>();
        [SerializeField, HideInInspector, FormerlySerializedAs("_groundPatchFootprints")]
        private Vector2Int[] _legacyGroundPatchFootprints = Array.Empty<Vector2Int>();

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
        public ResourceVisualMode VisualMode => _visualMode;
        public bool CastsBlobShadow => !_suppressBlobShadow;

        /// <summary>
        /// Selects a stable physical size for a ground patch. An empty list uses the resource display scale.
        /// </summary>
        public Vector2 SelectGroundPatchSize(int variantSeed)
        {
            if (_groundPatchSizes == null || _groundPatchSizes.Length == 0)
            {
                return DisplayScaleFootprint;
            }

            Vector2 size = _groundPatchSizes[(variantSeed & int.MaxValue) % _groundPatchSizes.Length];
            return SanitizeFootprint(size);
        }

        /// <summary>The physical X/Z area occupied by this particular visual variant.</summary>
        public Vector2 SelectWorldFootprint(int variantSeed)
        {
            return _visualMode == ResourceVisualMode.GroundDecal
                ? SelectGroundPatchSize(variantSeed)
                : DisplayScaleFootprint;
        }

        /// <summary>Largest configured footprint, used to inspect neighbouring chunks during generation.</summary>
        public Vector2 MaximumWorldFootprint
        {
            get
            {
                Vector2 maximum = DisplayScaleFootprint;
                if (_visualMode != ResourceVisualMode.GroundDecal || _groundPatchSizes == null)
                {
                    return maximum;
                }

                for (int i = 0; i < _groundPatchSizes.Length; i++)
                {
                    Vector2 size = SanitizeFootprint(_groundPatchSizes[i]);
                    maximum = new Vector2(Mathf.Max(maximum.x, size.x), Mathf.Max(maximum.y, size.y));
                }

                return maximum;
            }
        }

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

        /// <summary>Chooses how this resource is rendered without coupling presentation to collision.</summary>
        public void ConfigurePresentation(ResourceVisualMode visualMode)
        {
            _visualMode = visualMode;
        }

        public void ConfigureBlobShadow(bool castsBlobShadow)
        {
            _suppressBlobShadow = !castsBlobShadow;
        }

        /// <summary>
        /// Configures possible physical X/Z sizes in world units. Duplicate entries provide simple,
        /// inspectable weighting without introducing separate probability data.
        /// </summary>
        public void ConfigureGroundPatchSizes(params Vector2[] sizes)
        {
            _groundPatchSizes = sizes ?? Array.Empty<Vector2>();
            _legacyGroundPatchFootprints = Array.Empty<Vector2Int>();
        }

        /// <summary>
        /// Replaces the cutout set. Order decides which seed maps to which look, so the caller must
        /// supply a stable order or nodes change appearance between runs.
        /// </summary>
        public void SetWorldSprites(params Sprite[] worldSprites)
        {
            _worldSprites = worldSprites ?? Array.Empty<Sprite>();
        }

        private Vector2 DisplayScaleFootprint => SanitizeFootprint(new Vector2(_displayScale.x, _displayScale.z));

        private void OnEnable()
        {
            UpgradeLegacyGroundPatchFootprints();
        }

        private void OnValidate()
        {
            UpgradeLegacyGroundPatchFootprints();
        }

        private void UpgradeLegacyGroundPatchFootprints()
        {
            if (_legacyGroundPatchFootprints == null || _legacyGroundPatchFootprints.Length == 0)
            {
                return;
            }

            _groundPatchSizes = new Vector2[_legacyGroundPatchFootprints.Length];
            Vector2 unitSize = DisplayScaleFootprint;
            for (int i = 0; i < _legacyGroundPatchFootprints.Length; i++)
            {
                Vector2Int footprint = _legacyGroundPatchFootprints[i];
                _groundPatchSizes[i] = new Vector2(
                    Mathf.Max(1, footprint.x) * unitSize.x,
                    Mathf.Max(1, footprint.y) * unitSize.y);
            }

            _legacyGroundPatchFootprints = Array.Empty<Vector2Int>();
        }

        private static Vector2 SanitizeFootprint(Vector2 footprint)
        {
            return new Vector2(Mathf.Max(.1f, footprint.x), Mathf.Max(.1f, footprint.y));
        }
    }
}
