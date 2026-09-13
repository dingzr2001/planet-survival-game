using UnityEngine;

namespace PlanetSurvival.Player.Animation
{
    /// <summary>
    /// Transform-only action animation shared by any held item. Offsets are measured as a fraction of
    /// character height, so one mining or repair motion can be reused with different player artwork.
    /// </summary>
    [CreateAssetMenu(menuName = "Planet Survival/Player/Action Animation", fileName = "PlayerActionAnimation")]
    public sealed class PlayerActionAnimationDefinition : ScriptableObject
    {
        [SerializeField, Min(.01f)] private float _duration = .45f;
        [SerializeField] private bool _loop;

        [Header("Body")]
        [SerializeField] private AnimationCurve _bodyHorizontalOffset = Constant(0f);
        [SerializeField] private AnimationCurve _bodyVerticalOffset = Constant(0f);
        [SerializeField] private AnimationCurve _bodyRotation = Constant(0f);

        [Header("Held equipment")]
        [SerializeField] private AnimationCurve _equipmentHorizontalOffset = Constant(0f);
        [SerializeField] private AnimationCurve _equipmentVerticalOffset = Constant(0f);
        [SerializeField] private AnimationCurve _equipmentRotation = Constant(0f);
        [SerializeField] private AnimationCurve _equipmentScale = Constant(1f);

        [Header("Cutout symbol rotations (additive degrees)")]
        [SerializeField] private AnimationCurve _farUpperArmRotation = Constant(0f);
        [SerializeField] private AnimationCurve _farForearmRotation = Constant(0f);
        [SerializeField] private AnimationCurve _farHandRotation = Constant(0f);
        [SerializeField] private AnimationCurve _nearUpperArmRotation = Constant(0f);
        [SerializeField] private AnimationCurve _nearForearmRotation = Constant(0f);
        [SerializeField] private AnimationCurve _nearHandRotation = Constant(0f);

        public float Duration => Mathf.Max(.01f, _duration);
        public bool Loop => _loop;

        public PlayerAnimationPose Evaluate(float normalizedTime)
        {
            float time = Mathf.Clamp01(normalizedTime);
            return new PlayerAnimationPose(
                new Vector2(Evaluate(_bodyHorizontalOffset, time), Evaluate(_bodyVerticalOffset, time)),
                Evaluate(_bodyRotation, time),
                new Vector2(Evaluate(_equipmentHorizontalOffset, time),
                    Evaluate(_equipmentVerticalOffset, time)),
                Evaluate(_equipmentRotation, time),
                Mathf.Max(0f, Evaluate(_equipmentScale, time, 1f)));
        }

        public PlayerRigActionPose EvaluateRigPose(float normalizedTime)
        {
            float time = Mathf.Clamp01(normalizedTime);
            return new PlayerRigActionPose(
                Evaluate(_farUpperArmRotation, time),
                Evaluate(_farForearmRotation, time),
                Evaluate(_farHandRotation, time),
                Evaluate(_nearUpperArmRotation, time),
                Evaluate(_nearForearmRotation, time),
                Evaluate(_nearHandRotation, time));
        }

        public void Configure(float duration, bool loop,
            AnimationCurve bodyHorizontalOffset, AnimationCurve bodyVerticalOffset, AnimationCurve bodyRotation,
            AnimationCurve equipmentHorizontalOffset, AnimationCurve equipmentVerticalOffset,
            AnimationCurve equipmentRotation, AnimationCurve equipmentScale)
        {
            _duration = Mathf.Max(.01f, duration);
            _loop = loop;
            _bodyHorizontalOffset = bodyHorizontalOffset ?? Constant(0f);
            _bodyVerticalOffset = bodyVerticalOffset ?? Constant(0f);
            _bodyRotation = bodyRotation ?? Constant(0f);
            _equipmentHorizontalOffset = equipmentHorizontalOffset ?? Constant(0f);
            _equipmentVerticalOffset = equipmentVerticalOffset ?? Constant(0f);
            _equipmentRotation = equipmentRotation ?? Constant(0f);
            _equipmentScale = equipmentScale ?? Constant(1f);
        }

        public void ConfigureRig(AnimationCurve farUpperArmRotation, AnimationCurve farForearmRotation,
            AnimationCurve farHandRotation, AnimationCurve nearUpperArmRotation,
            AnimationCurve nearForearmRotation, AnimationCurve nearHandRotation)
        {
            _farUpperArmRotation = farUpperArmRotation ?? Constant(0f);
            _farForearmRotation = farForearmRotation ?? Constant(0f);
            _farHandRotation = farHandRotation ?? Constant(0f);
            _nearUpperArmRotation = nearUpperArmRotation ?? Constant(0f);
            _nearForearmRotation = nearForearmRotation ?? Constant(0f);
            _nearHandRotation = nearHandRotation ?? Constant(0f);
        }

        private static float Evaluate(AnimationCurve curve, float time, float fallback = 0f)
        {
            return curve == null || curve.length == 0 ? fallback : curve.Evaluate(time);
        }

        private static AnimationCurve Constant(float value)
        {
            return AnimationCurve.Constant(0f, 1f, value);
        }
    }
}
