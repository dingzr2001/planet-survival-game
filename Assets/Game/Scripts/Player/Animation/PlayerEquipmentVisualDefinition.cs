using System;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Player.Animation
{
    /// <summary>
    /// One held-item cutout plus its four grip poses. Adding a tool uses this asset instead of baking the
    /// tool into every frame of every player direction.
    /// </summary>
    [CreateAssetMenu(menuName = "Planet Survival/Player/Equipment Visual", fileName = "PlayerEquipmentVisual")]
    public sealed class PlayerEquipmentVisualDefinition : ScriptableObject
    {
        [SerializeField] private Sprite _sprite;
        [SerializeField, Min(.01f), Tooltip("Displayed equipment height as a fraction of player height.")]
        private float _heightRatio = .35f;
        [SerializeField] private DirectionalEquipmentPose _downPose;
        [SerializeField] private DirectionalEquipmentPose _leftPose;
        [SerializeField] private DirectionalEquipmentPose _rightPose;
        [SerializeField] private DirectionalEquipmentPose _upPose;

        public Sprite Sprite => _sprite;
        public float HeightRatio => Mathf.Max(.01f, _heightRatio);

        public DirectionalEquipmentPose PoseFor(SpriteFacingDirection facing)
        {
            return facing switch
            {
                SpriteFacingDirection.Left => _leftPose,
                SpriteFacingDirection.Right => _rightPose,
                SpriteFacingDirection.Up => _upPose,
                _ => _downPose
            };
        }

        public bool IsValid(out string error)
        {
            if (_sprite == null)
            {
                error = $"Equipment visual '{name}' requires a sprite.";
                return false;
            }

            if (_heightRatio <= 0f)
            {
                error = $"Equipment visual '{name}' requires a positive height ratio.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Configure(Sprite sprite, float heightRatio,
            DirectionalEquipmentPose downPose, DirectionalEquipmentPose leftPose,
            DirectionalEquipmentPose rightPose, DirectionalEquipmentPose upPose)
        {
            _sprite = sprite;
            _heightRatio = Mathf.Max(.01f, heightRatio);
            _downPose = downPose;
            _leftPose = leftPose;
            _rightPose = rightPose;
            _upPose = upPose;
        }
    }

    [Serializable]
    public struct DirectionalEquipmentPose
    {
        [SerializeField, Tooltip("Grip position measured as a fraction of player height.")]
        private Vector2 _normalizedPosition;
        [SerializeField] private float _rotation;
        [SerializeField, Tooltip("Render behind the body for directions where the far hand holds the item.")]
        private bool _behindBody;
        [SerializeField, Tooltip("Second hand relative to the primary grip, in equipment-local axes and player-height units.")]
        private Vector2 _secondaryGripOffset;

        public DirectionalEquipmentPose(Vector2 normalizedPosition, float rotation, bool behindBody = false)
        {
            _normalizedPosition = normalizedPosition;
            _rotation = rotation;
            _behindBody = behindBody;
            _secondaryGripOffset = new Vector2(0f, .1f);
        }

        public DirectionalEquipmentPose(Vector2 normalizedPosition, float rotation,
            Vector2 secondaryGripOffset, bool behindBody = false)
        {
            _normalizedPosition = normalizedPosition;
            _rotation = rotation;
            _behindBody = behindBody;
            _secondaryGripOffset = secondaryGripOffset;
        }

        public Vector2 NormalizedPosition => _normalizedPosition;
        public float Rotation => _rotation;
        public bool BehindBody => _behindBody;
        public Vector2 SecondaryGripOffset => _secondaryGripOffset.sqrMagnitude > .0001f
            ? _secondaryGripOffset
            : new Vector2(0f, .1f);
    }
}
