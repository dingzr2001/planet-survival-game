using System;
using System.Collections.Generic;
using PlanetSurvival.Cooking.Definitions;
using PlanetSurvival.Crafting.Definitions;
using UnityEngine;

namespace PlanetSurvival.Building.Definitions
{
    /// <summary>
    /// One entry of the build menu: what it costs, how many grid cells it covers, how long it takes to
    /// raise, and what the finished structure looks like. A buildable that names a cooking station turns
    /// into a working appliance once construction ends.
    /// </summary>
    [CreateAssetMenu(menuName = "Planet Survival/Building/Buildable", fileName = "Buildable")]
    public sealed class BuildableDefinition : ScriptableObject
    {
        [SerializeField] private string _buildableId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField, TextArea] private string _description = string.Empty;
        [SerializeField, Tooltip("Menu artwork. Falls back to the first cost item's icon when empty.")]
        private Sprite _icon;
        [SerializeField, Tooltip("Footprint in grid cells. Every cell must be free before it can be placed.")]
        private Vector2Int _footprint = Vector2Int.one;
        [SerializeField, Min(0f), Tooltip("Real seconds between placing the site and the finished building. Zero completes instantly.")]
        private float _buildSeconds = 10f;
        [SerializeField, Tooltip("Materials consumed the moment the site is placed. Cancelling refunds them.")]
        private CraftingItemAmount[] _cost = Array.Empty<CraftingItemAmount>();

        [Header("Presentation")]
        [SerializeField, Tooltip("Optional world artwork. Without it the building is drawn as a tinted block.")]
        private Sprite _worldSprite;
        [SerializeField, Min(.1f)] private float _worldHeight = 1.6f;
        [SerializeField] private Color _bodyColor = new(.62f, .6f, .56f);

        [Header("Function")]
        [SerializeField, Tooltip("Optional: the finished building serves as this cooking station.")]
        private CookingStationDefinition _cookingStation;

        public string BuildableId => _buildableId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Vector2Int Footprint => new(Mathf.Max(1, _footprint.x), Mathf.Max(1, _footprint.y));
        public float BuildSeconds => Mathf.Max(0f, _buildSeconds);
        public IReadOnlyList<CraftingItemAmount> Cost => _cost;
        public Sprite WorldSprite => _worldSprite;
        public float WorldHeight => Mathf.Max(.1f, _worldHeight);
        public Color BodyColor => _bodyColor;
        public CookingStationDefinition CookingStation => _cookingStation;

        /// <summary>The menu icon, falling back to the artwork of the material the structure is mostly made of.</summary>
        public Sprite MenuIcon
        {
            get
            {
                if (_icon != null)
                {
                    return _icon;
                }

                return _cost.Length > 0 && _cost[0].Item != null ? _cost[0].Item.Icon : null;
            }
        }

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(_buildableId) || string.IsNullOrWhiteSpace(_displayName))
            {
                error = "A buildable requires a non-empty ID and display name.";
                return false;
            }

            if (_footprint.x <= 0 || _footprint.y <= 0)
            {
                error = $"Buildable '{_buildableId}' needs a footprint of at least one cell per axis.";
                return false;
            }

            if (_cost == null || _cost.Length == 0)
            {
                error = $"Buildable '{_buildableId}' must cost at least one material.";
                return false;
            }

            var uniqueItems = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _cost.Length; i++)
            {
                CraftingItemAmount amount = _cost[i];
                if (amount.Item == null)
                {
                    error = $"Buildable '{_buildableId}' lists a missing material.";
                    return false;
                }

                if (!amount.Item.IsValid(out string itemError))
                {
                    error = $"Buildable '{_buildableId}' lists an invalid material: {itemError}";
                    return false;
                }

                if (amount.Quantity <= 0 || !uniqueItems.Add(amount.Item.ItemId))
                {
                    error = $"Buildable '{_buildableId}' repeats or empties the material '{amount.Item.ItemId}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        public void Configure(string buildableId, string displayName, Vector2Int footprint,
            float buildSeconds, params CraftingItemAmount[] cost)
        {
            _buildableId = buildableId;
            _displayName = displayName;
            _footprint = footprint;
            _buildSeconds = Mathf.Max(0f, buildSeconds);
            _cost = cost ?? Array.Empty<CraftingItemAmount>();
        }

        public void ConfigurePresentation(Sprite worldSprite, float worldHeight, Color bodyColor)
        {
            _worldSprite = worldSprite;
            _worldHeight = Mathf.Max(.1f, worldHeight);
            _bodyColor = bodyColor;
        }

        public void ConfigureDescription(string description)
        {
            _description = description ?? string.Empty;
        }

        public void ConfigureCookingStation(CookingStationDefinition cookingStation)
        {
            _cookingStation = cookingStation;
        }
    }
}
