using UnityEngine;

namespace PlanetSurvival.Player.Animation
{
    /// <summary>A normalized, sprite-independent pose sampled from a player action.</summary>
    public readonly struct PlayerAnimationPose
    {
        public PlayerAnimationPose(Vector2 bodyOffset, float bodyRotation,
            Vector2 equipmentOffset, float equipmentRotation, float equipmentScale)
        {
            BodyOffset = bodyOffset;
            BodyRotation = bodyRotation;
            EquipmentOffset = equipmentOffset;
            EquipmentRotation = equipmentRotation;
            EquipmentScale = equipmentScale;
        }

        public Vector2 BodyOffset { get; }
        public float BodyRotation { get; }
        public Vector2 EquipmentOffset { get; }
        public float EquipmentRotation { get; }
        public float EquipmentScale { get; }

        public static PlayerAnimationPose Rest => new(Vector2.zero, 0f, Vector2.zero, 0f, 1f);
    }

    /// <summary>
    /// Additive rotations for the cutout symbols in one authored action pose. The animation asset owns
    /// these values, so equipment actions can move the character without requiring new character art.
    /// </summary>
    public readonly struct PlayerRigActionPose
    {
        public PlayerRigActionPose(float farUpperArm, float farForearm, float farHand,
            float nearUpperArm, float nearForearm, float nearHand)
        {
            FarUpperArm = farUpperArm;
            FarForearm = farForearm;
            FarHand = farHand;
            NearUpperArm = nearUpperArm;
            NearForearm = nearForearm;
            NearHand = nearHand;
        }

        public float FarUpperArm { get; }
        public float FarForearm { get; }
        public float FarHand { get; }
        public float NearUpperArm { get; }
        public float NearForearm { get; }
        public float NearHand { get; }

        public static PlayerRigActionPose Rest => default;
    }
}
