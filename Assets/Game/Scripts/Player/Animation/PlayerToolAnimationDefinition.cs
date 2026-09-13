using UnityEngine;

namespace PlanetSurvival.Player.Animation
{
    /// <summary>Connects a gameplay item ID to its independent visual and reusable action motion.</summary>
    [CreateAssetMenu(menuName = "Planet Survival/Player/Tool Animation", fileName = "PlayerToolAnimation")]
    public sealed class PlayerToolAnimationDefinition : ScriptableObject
    {
        [SerializeField] private string _itemId = string.Empty;
        [SerializeField] private PlayerEquipmentVisualDefinition _equipmentVisual;
        [SerializeField] private PlayerActionAnimationDefinition _actionAnimation;

        public string ItemId => _itemId;
        public PlayerEquipmentVisualDefinition EquipmentVisual => _equipmentVisual;
        public PlayerActionAnimationDefinition ActionAnimation => _actionAnimation;

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(_itemId))
            {
                error = $"Tool animation '{name}' requires an item ID.";
                return false;
            }

            if (_equipmentVisual == null)
            {
                error = $"Tool animation '{name}' requires an equipment visual.";
                return false;
            }

            if (!_equipmentVisual.IsValid(out string visualError))
            {
                error = $"Tool animation '{name}' has an invalid equipment visual. {visualError}";
                return false;
            }

            if (_actionAnimation == null)
            {
                error = $"Tool animation '{name}' requires an action animation.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Configure(string itemId, PlayerEquipmentVisualDefinition equipmentVisual,
            PlayerActionAnimationDefinition actionAnimation)
        {
            _itemId = itemId ?? string.Empty;
            _equipmentVisual = equipmentVisual;
            _actionAnimation = actionAnimation;
        }
    }
}
