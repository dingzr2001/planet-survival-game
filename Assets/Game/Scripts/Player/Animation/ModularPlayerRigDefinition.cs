using System;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Player.Animation
{
    /// <summary>
    /// Directional artwork for a transform-driven character rig. The same limb sprites are reused by
    /// both sides of the body; animation comes from authored symbol transforms rather than baked character frames.
    /// </summary>
    [CreateAssetMenu(menuName = "Planet Survival/Player/Modular Rig", fileName = "ModularPlayerRig")]
    public sealed class ModularPlayerRigDefinition : ScriptableObject
    {
        [SerializeField] private DirectionalRigSprites _down;
        [SerializeField] private DirectionalRigSprites _left;
        [SerializeField] private DirectionalRigSprites _right;
        [SerializeField] private DirectionalRigSprites _up;

        [Header("Proportions (fraction of character height)")]
        [SerializeField, Range(.3f, .7f)] private float _hipHeight = .37f;
        [SerializeField, Range(.45f, .75f)] private float _torsoHeight = .63f;
        [SerializeField, Range(.5f, .9f)] private float _shoulderHeight = .7f;
        [SerializeField, Range(.1f, .35f)] private float _upperArmLength = .165f;
        [SerializeField, Range(.1f, .35f)] private float _forearmLength = .15f;
        [SerializeField, Range(.04f, .16f)] private float _handHeight = .1f;
        [SerializeField, Range(.1f, .35f)] private float _thighLength = .2f;
        [SerializeField, Range(.1f, .35f)] private float _lowerLegLength = .18f;
        [SerializeField, Range(.05f, .18f)] private float _bootHeight = .11f;

        public float HipHeight => _hipHeight;
        public float TorsoHeight => _torsoHeight;
        public float ShoulderHeight => _shoulderHeight;
        public float UpperArmLength => _upperArmLength;
        public float ForearmLength => _forearmLength;
        public float HandHeight => _handHeight;
        public float ThighLength => _thighLength;
        public float LowerLegLength => _lowerLegLength;
        public float BootHeight => _bootHeight;

        public DirectionalRigSprites SpritesFor(SpriteFacingDirection facing)
        {
            return facing switch
            {
                SpriteFacingDirection.Left => _left,
                SpriteFacingDirection.Right => _right,
                SpriteFacingDirection.Up => _up,
                _ => _down
            };
        }

        public bool IsValid(out string error)
        {
            if (!_down.IsComplete || !_left.IsComplete || !_right.IsComplete || !_up.IsComplete)
            {
                error = $"Modular player rig '{name}' requires torso, arm, hand, leg and boot sprites for all four directions.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public void Configure(DirectionalRigSprites down, DirectionalRigSprites left,
            DirectionalRigSprites right, DirectionalRigSprites up)
        {
            _down = down;
            _left = left;
            _right = right;
            _up = up;
        }
    }

    [Serializable]
    public struct DirectionalRigSprites
    {
        [SerializeField] private Sprite _torso;
        [SerializeField] private Sprite _upperArm;
        [SerializeField] private Sprite _forearm;
        [SerializeField] private Sprite _hand;
        [SerializeField] private Sprite _thigh;
        [SerializeField] private Sprite _lowerLeg;
        [SerializeField] private Sprite _boot;

        public DirectionalRigSprites(Sprite torso, Sprite upperArm, Sprite forearm, Sprite hand,
            Sprite thigh, Sprite lowerLeg, Sprite boot)
        {
            _torso = torso;
            _upperArm = upperArm;
            _forearm = forearm;
            _hand = hand;
            _thigh = thigh;
            _lowerLeg = lowerLeg;
            _boot = boot;
        }

        public Sprite Torso => _torso;
        public Sprite UpperArm => _upperArm;
        public Sprite Forearm => _forearm;
        public Sprite Hand => _hand;
        public Sprite Thigh => _thigh;
        public Sprite LowerLeg => _lowerLeg;
        public Sprite Boot => _boot;
        public bool IsComplete => _torso != null && _upperArm != null && _forearm != null
                                  && _hand != null && _thigh != null && _lowerLeg != null && _boot != null;
    }
}
