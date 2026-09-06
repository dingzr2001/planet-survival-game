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
        [SerializeField, Tooltip("Transparent cutout used by the flat 2.5D world presentation.")]
        private Sprite _worldSprite;
        [SerializeField] private Color _displayColor = Color.gray;
        [SerializeField] private Vector3 _displayScale = Vector3.one;

        public string ResourceId => _resourceId;
        public string DisplayName => _displayName;
        public float GatherDuration => _gatherDuration;
        public float GatherDistance => _gatherDistance;
        public string RequiredToolItemId => _requiredToolItemId;
        public IReadOnlyList<ResourceYield> Yields => _yields;
        public Sprite WorldSprite => _worldSprite;
        public Color DisplayColor => _displayColor;
        public Vector3 DisplayScale => _displayScale;

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
            string requiredToolItemId, Color displayColor, Vector3 displayScale, params ResourceYield[] yields)
        {
            _resourceId = resourceId;
            _displayName = displayName;
            _gatherDuration = Mathf.Max(.1f, gatherDuration);
            _gatherDistance = Mathf.Max(.1f, gatherDistance);
            _requiredToolItemId = requiredToolItemId ?? string.Empty;
            _displayColor = displayColor;
            _displayScale = displayScale;
            _yields = yields ?? Array.Empty<ResourceYield>();
        }

        public void SetWorldSprite(Sprite worldSprite)
        {
            _worldSprite = worldSprite;
        }
    }
}
