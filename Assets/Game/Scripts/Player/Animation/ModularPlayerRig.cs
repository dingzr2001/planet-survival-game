using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Player.Animation
{
    /// <summary>
    /// Klei-style runtime cutout rig. Authored symbol rotations drive the silhouette; sprites overlap at
    /// their pivots and equipment remains an independent symbol owned by <see cref="PlayerAnimationController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModularPlayerRig : MonoBehaviour
    {
        private const float MinimumSpriteSize = .001f;
        // The generated symbols include dark flexible sleeves at both ends. As in Klei's cutout builds,
        // adjacent symbols deliberately overlap those sleeves; placing them end-to-end exposes the cut edge.
        private const float ArmJointSpacing = .56f;
        private const float ActionArmJointSpacing = .7f;
        private const float LegJointSpacing = .62f;

        private ModularPlayerRigDefinition _definition;
        private SpriteRenderer _sortingSource;
        private SpriteRenderer _torso;
        private Chain _armA;
        private Chain _armB;
        private Chain _legA;
        private Chain _legB;
        private float _characterHeight;
        private float _gaitTime;
        private SpriteFacingDirection _facing = (SpriteFacingDirection)(-1);

        public static ModularPlayerRig Create(Transform parent, ModularPlayerRigDefinition definition,
            float characterHeight, SpriteRenderer sortingSource)
        {
            if (parent == null || definition == null)
            {
                return null;
            }

            if (!definition.IsValid(out string error))
            {
                Debug.LogError(error, definition);
                return null;
            }

            var root = new GameObject("Modular Rig");
            root.transform.SetParent(parent, false);
            ModularPlayerRig rig = root.AddComponent<ModularPlayerRig>();
            rig.Build(definition, characterHeight, sortingSource);
            return rig;
        }

        public void Present(SpriteFacingDirection facing, float movementMagnitude, bool actionActive,
            PlayerRigActionPose actionPose, float elapsedSeconds)
        {
            if (_definition == null)
            {
                return;
            }

            if (_facing != facing)
            {
                _facing = facing;
                ApplySprites(_definition.SpritesFor(facing));
            }

            float movement = Mathf.Clamp01(movementMagnitude);
            if (!actionActive && movement > .01f)
            {
                _gaitTime += Mathf.Max(0f, elapsedSeconds) * Mathf.Lerp(5.5f, 9f, movement);
            }
            else if (!actionActive)
            {
                _gaitTime = Mathf.MoveTowards(_gaitTime, 0f, Mathf.Max(0f, elapsedSeconds) * 5f);
            }

            float phase = actionActive ? 0f : Mathf.Sin(_gaitTime) * movement;
            float bob = actionActive ? 0f : Mathf.Abs(Mathf.Sin(_gaitTime)) * movement * .006f;
            transform.localPosition = Vector3.up * bob * _characterHeight;
            PoseLegs(facing, phase);
            PoseArms(facing, phase, actionPose, actionActive);
            ApplySorting(facing, actionActive);
        }

        private void Build(ModularPlayerRigDefinition definition, float characterHeight,
            SpriteRenderer sortingSource)
        {
            _definition = definition;
            _sortingSource = sortingSource;
            _characterHeight = Mathf.Max(MinimumSpriteSize, characterHeight);
            _legA = CreateChain("Far Leg");
            _legB = CreateChain("Near Leg");
            _armA = CreateChain("Far Arm");
            _armB = CreateChain("Near Arm");
            _torso = CreateRenderer("Torso", transform);
            _facing = (SpriteFacingDirection)(-1);
            Present(SpriteFacingDirection.Down, 0f, false, PlayerRigActionPose.Rest, 0f);
        }

        private void ApplySprites(DirectionalRigSprites sprites)
        {
            SetSprite(_torso, sprites.Torso, _definition.TorsoHeight);
            if (_facing is SpriteFacingDirection.Left or SpriteFacingDirection.Right)
            {
                Vector3 torsoScale = _torso.transform.localScale;
                torsoScale.x *= 1.15f;
                _torso.transform.localScale = torsoScale;
            }
            SetChainSprites(_armA, sprites.UpperArm, sprites.Forearm, sprites.Hand,
                _definition.UpperArmLength, _definition.ForearmLength, _definition.HandHeight);
            SetChainSprites(_armB, sprites.UpperArm, sprites.Forearm, sprites.Hand,
                _definition.UpperArmLength, _definition.ForearmLength, _definition.HandHeight);
            SetChainSprites(_legA, sprites.Thigh, sprites.LowerLeg, sprites.Boot,
                _definition.ThighLength, _definition.LowerLegLength, _definition.BootHeight);
            SetChainSprites(_legB, sprites.Thigh, sprites.LowerLeg, sprites.Boot,
                _definition.ThighLength, _definition.LowerLegLength, _definition.BootHeight);
            bool mirrorSecondSide = _facing is SpriteFacingDirection.Down or SpriteFacingDirection.Up;
            SetFlipX(_armA, false);
            SetFlipX(_legA, false);
            SetFlipX(_armB, mirrorSecondSide);
            SetFlipX(_legB, mirrorSecondSide);
            _torso.transform.localPosition = Vector3.up * _definition.HipHeight * _characterHeight;
        }

        private void PoseLegs(SpriteFacingDirection facing, float phase)
        {
            bool sideView = facing is SpriteFacingDirection.Left or SpriteFacingDirection.Right;
            float hipSpread = sideView ? .012f : .08f;
            float facingSign = facing == SpriteFacingDirection.Left ? -1f : 1f;
            // HipHeight is the front/side seam. The rear artwork has visibly higher leg openings, so its
            // symbol roots receive a direction-specific offset instead of changing every leg's scale.
            float hipY = (_definition.HipHeight
                          + (facing == SpriteFacingDirection.Up ? .11f : 0f)) * _characterHeight;
            float liftA = Mathf.Max(0f, phase) * .018f * _characterHeight;
            float liftB = Mathf.Max(0f, -phase) * .018f * _characterHeight;
            Vector2 hipA = new(-hipSpread * _characterHeight, hipY + liftA);
            Vector2 hipB = new(hipSpread * _characterHeight, hipY + liftB);
            float angleA;
            float angleB;
            if (sideView)
            {
                angleA = phase * facingSign * 13f;
                angleB = -phase * facingSign * 13f;
            }
            else
            {
                float direction = facing == SpriteFacingDirection.Up ? -1f : 1f;
                angleA = phase * direction * 7f;
                angleB = -phase * direction * 7f;
            }

            // Klei's player build treats leg + foot as the animation symbols. Our two leg images remain
            // an internal composite, but the knee is deliberately locked so it reads as one continuous leg.
            ApplyAuthoredChain(_legA, hipA, _definition.ThighLength * _characterHeight,
                _definition.LowerLegLength * _characterHeight, angleA, 0f, 0f, LegJointSpacing);
            ApplyAuthoredChain(_legB, hipB, _definition.ThighLength * _characterHeight,
                _definition.LowerLegLength * _characterHeight, angleB, 0f, 0f, LegJointSpacing);
        }

        private void PoseArms(SpriteFacingDirection facing, float phase, PlayerRigActionPose actionPose,
            bool actionActive)
        {
            bool sideView = facing is SpriteFacingDirection.Left or SpriteFacingDirection.Right;
            float facingSign = facing == SpriteFacingDirection.Left ? -1f : 1f;
            float shoulderSpread = sideView
                ? .015f
                : facing == SpriteFacingDirection.Up ? .16f : .14f;
            Vector2 shoulderA = new(-shoulderSpread * _characterHeight,
                _definition.ShoulderHeight * _characterHeight);
            Vector2 shoulderB = new(shoulderSpread * _characterHeight,
                _definition.ShoulderHeight * _characterHeight);
            float mirror = facing == SpriteFacingDirection.Left ? -1f : 1f;
            float walkA = sideView ? -phase * facingSign * 11f : -phase * 8f;
            float walkB = sideView ? phase * facingSign * 11f : phase * 8f;
            ApplyAuthoredChain(_armA, shoulderA, _definition.UpperArmLength * _characterHeight,
                _definition.ForearmLength * _characterHeight,
                walkA + actionPose.FarUpperArm * mirror,
                actionPose.FarForearm * mirror,
                actionPose.FarHand * mirror, actionActive ? ActionArmJointSpacing : ArmJointSpacing);
            ApplyAuthoredChain(_armB, shoulderB, _definition.UpperArmLength * _characterHeight,
                _definition.ForearmLength * _characterHeight,
                walkB + actionPose.NearUpperArm * mirror,
                actionPose.NearForearm * mirror,
                actionPose.NearHand * mirror, actionActive ? ActionArmJointSpacing : ArmJointSpacing);
        }

        private void ApplySorting(SpriteFacingDirection facing, bool actionActive)
        {
            int bodyOrder = _sortingSource != null ? _sortingSource.sortingOrder : 0;
            bool backView = facing == SpriteFacingDirection.Up;
            SetOrder(_legA, bodyOrder - 4);
            _torso.sortingOrder = bodyOrder;
            SetOrder(_legB, bodyOrder + (backView ? -2 : 1));
            if (actionActive && !backView)
            {
                // Action clips may change symbol depth just like a Klei animation. Both hands must pass
                // in front of the held item during the down/side mining swing.
                SetOrder(_armA, bodyOrder + 2);
                SetOrder(_armB, bodyOrder + 4);
                return;
            }

            SetOrder(_armA, bodyOrder - (backView ? 2 : 3));
            SetOrder(_armB, bodyOrder + (backView ? -1 : 3));
        }

        private static void ApplyAuthoredChain(Chain chain, Vector2 root,
            float firstLength, float secondLength, float firstAngle, float secondAngle, float endAngle,
            float jointSpacing)
        {
            chain.Root.localPosition = root;
            chain.Root.localRotation = Quaternion.Euler(0f, 0f, firstAngle);
            chain.Middle.localPosition = Vector3.down * firstLength * jointSpacing;
            chain.Middle.localRotation = Quaternion.Euler(0f, 0f, secondAngle);
            chain.End.localPosition = Vector3.down * secondLength * jointSpacing;
            chain.End.localRotation = Quaternion.Euler(0f, 0f, endAngle - firstAngle - secondAngle);
        }

        private Chain CreateChain(string name)
        {
            Transform root = CreateNode(name, transform);
            Transform middle = CreateNode("Middle Joint", root);
            Transform end = CreateNode("End Joint", middle);
            return new Chain(root, middle, end,
                CreateRenderer("Upper", root), CreateRenderer("Lower", middle), CreateRenderer("End", end));
        }

        private void SetChainSprites(Chain chain, Sprite first, Sprite second, Sprite end,
            float firstHeight, float secondHeight, float endHeight)
        {
            SetSprite(chain.FirstRenderer, first, firstHeight);
            SetSprite(chain.SecondRenderer, second, secondHeight);
            SetSprite(chain.EndRenderer, end, endHeight);
        }

        private void SetSprite(SpriteRenderer renderer, Sprite sprite, float normalizedHeight,
            float verticalStretch = 1f, float horizontalScale = 1f)
        {
            renderer.sprite = sprite;
            if (sprite == null)
            {
                return;
            }

            float scale = normalizedHeight * _characterHeight
                          / Mathf.Max(MinimumSpriteSize, sprite.bounds.size.y);
            renderer.transform.localScale = new Vector3(scale * horizontalScale, scale * verticalStretch, scale);
        }

        private static void SetOrder(Chain chain, int sortingOrder)
        {
            chain.FirstRenderer.sortingOrder = sortingOrder;
            chain.SecondRenderer.sortingOrder = sortingOrder;
            chain.EndRenderer.sortingOrder = sortingOrder + 1;
        }

        private static void SetFlipX(Chain chain, bool flipX)
        {
            chain.FirstRenderer.flipX = flipX;
            chain.SecondRenderer.flipX = flipX;
            chain.EndRenderer.flipX = flipX;
        }

        private static Transform CreateNode(string name, Transform parent)
        {
            var node = new GameObject(name);
            node.transform.SetParent(parent, false);
            return node.transform;
        }

        private static SpriteRenderer CreateRenderer(string name, Transform parent)
        {
            var visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            return visual.AddComponent<SpriteRenderer>();
        }

        private readonly struct Chain
        {
            public Chain(Transform root, Transform middle, Transform end,
                SpriteRenderer firstRenderer, SpriteRenderer secondRenderer, SpriteRenderer endRenderer)
            {
                Root = root;
                Middle = middle;
                End = end;
                FirstRenderer = firstRenderer;
                SecondRenderer = secondRenderer;
                EndRenderer = endRenderer;
            }

            public Transform Root { get; }
            public Transform Middle { get; }
            public Transform End { get; }
            public SpriteRenderer FirstRenderer { get; }
            public SpriteRenderer SecondRenderer { get; }
            public SpriteRenderer EndRenderer { get; }
        }
    }
}
