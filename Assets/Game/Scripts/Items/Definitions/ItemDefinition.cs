using System;
using System.Collections.Generic;
using PlanetSurvival.Player.Stats;
using UnityEngine;

namespace PlanetSurvival.Items.Definitions
{
    [CreateAssetMenu(menuName = "Planet Survival/Items/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [SerializeField] private string _itemId = string.Empty;
        [SerializeField] private string _displayName = string.Empty;
        [SerializeField, TextArea] private string _description = string.Empty;
        [SerializeField] private Sprite _icon;
        [SerializeField, Min(1)] private int _capacityPerItem = 1;
        [SerializeField, Min(1)] private int _maximumStackSize = 1;
        [SerializeField] private bool _canUse;
        [SerializeField] private bool _canDrop = true;
        [SerializeField, Min(0)] private int _calories;
        [SerializeField] private VitalEffect[] _effects = Array.Empty<VitalEffect>();

        public string ItemId => _itemId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public int CapacityPerItem => _capacityPerItem;
        public int MaximumStackSize => _maximumStackSize;
        public bool CanUse => _canUse;
        public bool CanDrop => _canDrop;
        public int Calories => _calories;
        public IReadOnlyList<VitalEffect> Effects => _effects;

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(_itemId))
            {
                error = "Item ID must not be empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(_displayName))
            {
                error = $"Item '{_itemId}' must have a display name.";
                return false;
            }

            if (_capacityPerItem <= 0 || _maximumStackSize <= 0)
            {
                error = $"Item '{_itemId}' requires positive capacity and stack size.";
                return false;
            }

            if (_calories < 0)
            {
                error = $"Item '{_itemId}' cannot contain negative calories.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Configure(
            string itemId,
            string displayName,
            int capacityPerItem,
            int maximumStackSize,
            bool canUse,
            bool canDrop,
            params VitalEffect[] effects)
        {
            _itemId = itemId;
            _displayName = displayName;
            _capacityPerItem = capacityPerItem;
            _maximumStackSize = maximumStackSize;
            _canUse = canUse;
            _canDrop = canDrop;
            _calories = 0;
            _effects = effects ?? Array.Empty<VitalEffect>();
        }

        public void ConfigureNutrition(int calories)
        {
            _calories = Mathf.Max(0, calories);
        }

        public void ConfigureDescription(string description)
        {
            _description = description ?? string.Empty;
        }
    }

    [Serializable]
    public struct VitalEffect
    {
        [SerializeField] private VitalType _vitalType;
        [SerializeField] private float _amount;

        public VitalEffect(VitalType vitalType, float amount)
        {
            _vitalType = vitalType;
            _amount = amount;
        }

        public VitalType VitalType => _vitalType;
        public float Amount => _amount;
    }
}
