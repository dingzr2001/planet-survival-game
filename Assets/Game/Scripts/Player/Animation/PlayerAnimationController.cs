using System;
using System.Collections.Generic;
using PlanetSurvival.World.Presentation;
using UnityEngine;

namespace PlanetSurvival.Player.Animation
{
    /// <summary>
    /// Composes the existing directional body animation with transform-driven actions and an independent
    /// equipment cutout. Gameplay talks to this component and never needs to know how many render layers exist.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class PlayerAnimationController : MonoBehaviour
    {
        private const float MinimumSpriteSize = .001f;

        [SerializeField] private WorldSpriteView _body;
        [SerializeField] private Transform _characterMotion;
        [SerializeField] private Transform _equipmentMotion;
        [SerializeField] private SpriteRenderer _equipmentRenderer;
        [SerializeField] private ModularPlayerRig _modularRig;
        [SerializeField] private PlayerToolAnimationDefinition[] _toolAnimations =
            Array.Empty<PlayerToolAnimationDefinition>();

        private PlayerEquipmentVisualDefinition _equipment;
        private PlayerActionAnimationDefinition _action;
        private float _actionElapsed;
        private float _movementMagnitude;
        private string _activeToolItemId = string.Empty;

        public WorldSpriteView Body => _body;
        public Transform CharacterMotion => _characterMotion;
        public Transform EquipmentMotion => _equipmentMotion;
        public SpriteRenderer EquipmentRenderer => _equipmentRenderer;
        public ModularPlayerRig ModularRig => _modularRig;
        public PlayerEquipmentVisualDefinition Equipment => _equipment;
        public PlayerActionAnimationDefinition CurrentAction => _action;
        public bool IsPlayingAction => _action != null;

        public event Action<PlayerActionAnimationDefinition> ActionCompleted;

        public static PlayerAnimationController Create(Transform parent, Sprite fallbackSprite, float worldHeight,
            Texture2D animationSheet = null, int framesPerDirection = 4,
            IReadOnlyList<Rect> frameRects = null, IReadOnlyList<Vector2> framePivots = null)
        {
            var visual = new GameObject("Player Visual");
            visual.transform.SetParent(parent, false);

            var characterMotion = new GameObject("Character Motion");
            characterMotion.transform.SetParent(visual.transform, false);

            var bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(characterMotion.transform, false);
            SpriteRenderer bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();

            var equipmentObject = new GameObject("Held Equipment");
            equipmentObject.transform.SetParent(characterMotion.transform, false);
            SpriteRenderer equipmentRenderer = equipmentObject.AddComponent<SpriteRenderer>();
            equipmentRenderer.enabled = false;

            WorldSpriteView body = visual.AddComponent<WorldSpriteView>();
            body.SetRenderer(bodyRenderer);
            body.ConfigureDirectional(fallbackSprite, worldHeight, animationSheet,
                framesPerDirection, frameRects, framePivots);

            PlayerAnimationController controller = visual.AddComponent<PlayerAnimationController>();
            controller.Configure(body, characterMotion.transform, equipmentObject.transform, equipmentRenderer);
            return controller;
        }

        public void Configure(WorldSpriteView body, Transform characterMotion,
            Transform equipmentMotion, SpriteRenderer equipmentRenderer)
        {
            _body = body;
            _characterMotion = characterMotion;
            _equipmentMotion = equipmentMotion;
            _equipmentRenderer = equipmentRenderer;
            ResetPose();
            RefreshEquipmentPose(PlayerAnimationPose.Rest);
        }

        public void ConfigureTools(IReadOnlyList<PlayerToolAnimationDefinition> toolAnimations)
        {
            if (toolAnimations == null || toolAnimations.Count == 0)
            {
                _toolAnimations = Array.Empty<PlayerToolAnimationDefinition>();
                return;
            }

            var validAnimations = new List<PlayerToolAnimationDefinition>(toolAnimations.Count);
            for (int i = 0; i < toolAnimations.Count; i++)
            {
                PlayerToolAnimationDefinition toolAnimation = toolAnimations[i];
                if (toolAnimation != null && toolAnimation.IsValid(out _))
                {
                    validAnimations.Add(toolAnimation);
                }
            }

            _toolAnimations = validAnimations.ToArray();
        }

        public bool ConfigureModularRig(ModularPlayerRigDefinition definition)
        {
            if (_body == null || _body.Renderer == null || _characterMotion == null || definition == null)
            {
                return false;
            }

            if (!definition.IsValid(out string error))
            {
                Debug.LogError(error, definition);
                return false;
            }

            if (_modularRig != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_modularRig.gameObject);
                }
                else
                {
                    DestroyImmediate(_modularRig.gameObject);
                }
            }

            _modularRig = ModularPlayerRig.Create(_characterMotion, definition,
                CharacterLocalHeight(), _body.Renderer);
            _body.Renderer.enabled = _modularRig == null;
            PresentRig(0f);
            return _modularRig != null;
        }

        public void SetMovement(Vector2 cameraRelativeMovement)
        {
            _movementMagnitude = Mathf.Clamp01(cameraRelativeMovement.magnitude);
            if (_body == null)
            {
                return;
            }

            _body.SetMovement(cameraRelativeMovement);
            RefreshEquipmentPose(CurrentPose());
            PresentRig(0f);
        }

        public bool Equip(PlayerEquipmentVisualDefinition equipment)
        {
            _equipment = equipment;
            if (_equipmentRenderer == null)
            {
                return false;
            }

            if (equipment == null)
            {
                _equipmentRenderer.sprite = null;
                _equipmentRenderer.enabled = false;
                ResetEquipmentPose();
                PresentRig(0f);
                return true;
            }

            if (!equipment.IsValid(out string error))
            {
                Debug.LogError(error, equipment);
                _equipmentRenderer.sprite = null;
                _equipmentRenderer.enabled = false;
                return false;
            }

            _equipmentRenderer.sprite = equipment.Sprite;
            _equipmentRenderer.enabled = true;
            RefreshEquipmentPose(CurrentPose());
            PresentRig(0f);
            return true;
        }

        public bool PlayAction(PlayerActionAnimationDefinition action, bool restartIfAlreadyPlaying = true)
        {
            if (action == null || (!restartIfAlreadyPlaying && _action == action))
            {
                return false;
            }

            _action = action;
            _actionElapsed = 0f;
            ApplyPose(_action.Evaluate(0f));
            PresentRig(0f);
            return true;
        }

        public bool TryPlayToolAction(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            for (int i = 0; i < _toolAnimations.Length; i++)
            {
                PlayerToolAnimationDefinition toolAnimation = _toolAnimations[i];
                if (!string.Equals(toolAnimation.ItemId, itemId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!Equip(toolAnimation.EquipmentVisual) || !PlayAction(toolAnimation.ActionAnimation))
                {
                    Equip(null);
                    return false;
                }

                _activeToolItemId = itemId;
                return true;
            }

            return false;
        }

        public bool StopToolAction(string itemId)
        {
            if (string.IsNullOrEmpty(_activeToolItemId)
                || !string.Equals(_activeToolItemId, itemId, StringComparison.Ordinal))
            {
                return false;
            }

            _activeToolItemId = string.Empty;
            StopAction();
            Equip(null);
            return true;
        }

        public void StopAction()
        {
            _action = null;
            _actionElapsed = 0f;
            ApplyPose(PlayerAnimationPose.Rest);
            PresentRig(0f);
        }

        /// <summary>Advances action playback explicitly; exposed so gameplay-independent tests need no frame loop.</summary>
        public void Advance(float elapsedSeconds)
        {
            if (elapsedSeconds < 0f || float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds))
            {
                RefreshEquipmentPose(CurrentPose());
                PresentRig(0f);
                return;
            }

            if (_action == null)
            {
                RefreshEquipmentPose(PlayerAnimationPose.Rest);
                PresentRig(elapsedSeconds);
                return;
            }

            PlayerActionAnimationDefinition playingAction = _action;
            _actionElapsed += elapsedSeconds;
            if (playingAction.Loop)
            {
                _actionElapsed %= playingAction.Duration;
                ApplyPose(playingAction.Evaluate(_actionElapsed / playingAction.Duration));
                PresentRig(elapsedSeconds);
                return;
            }

            if (_actionElapsed >= playingAction.Duration)
            {
                _action = null;
                _actionElapsed = 0f;
                ApplyPose(PlayerAnimationPose.Rest);
                if (!string.IsNullOrEmpty(_activeToolItemId))
                {
                    _activeToolItemId = string.Empty;
                    Equip(null);
                }
                ActionCompleted?.Invoke(playingAction);
                PresentRig(elapsedSeconds);
                return;
            }

            ApplyPose(playingAction.Evaluate(_actionElapsed / playingAction.Duration));
            PresentRig(elapsedSeconds);
        }

        private void LateUpdate()
        {
            Advance(Time.deltaTime);
        }

        private void OnDisable()
        {
            _activeToolItemId = string.Empty;
            StopAction();
            Equip(null);
        }

        private PlayerAnimationPose CurrentPose()
        {
            return _action == null
                ? PlayerAnimationPose.Rest
                : _action.Evaluate(_actionElapsed / _action.Duration);
        }

        private void ApplyPose(PlayerAnimationPose pose)
        {
            float characterHeight = CharacterLocalHeight();
            if (_characterMotion != null)
            {
                _characterMotion.localPosition = new Vector3(
                    pose.BodyOffset.x * characterHeight,
                    pose.BodyOffset.y * characterHeight,
                    0f);
                _characterMotion.localRotation = Quaternion.Euler(0f, 0f, pose.BodyRotation);
                _characterMotion.localScale = Vector3.one;
            }

            RefreshEquipmentPose(pose);
        }

        private void RefreshEquipmentPose(PlayerAnimationPose actionPose)
        {
            if (_body == null || _body.Renderer == null || _equipment == null || _equipmentMotion == null
                || _equipmentRenderer == null
                || _equipmentRenderer.sprite == null)
            {
                return;
            }

            float characterHeight = CharacterLocalHeight();
            DirectionalEquipmentPose gripPose = _equipment.PoseFor(_body.Facing);
            Vector2 normalizedPosition = gripPose.NormalizedPosition + actionPose.EquipmentOffset;
            _equipmentMotion.localPosition = new Vector3(
                normalizedPosition.x * characterHeight,
                normalizedPosition.y * characterHeight,
                0f);
            _equipmentMotion.localRotation = Quaternion.Euler(
                0f, 0f, gripPose.Rotation + actionPose.EquipmentRotation);

            float spriteHeight = Mathf.Max(MinimumSpriteSize, _equipmentRenderer.sprite.bounds.size.y);
            float scale = characterHeight * _equipment.HeightRatio / spriteHeight * actionPose.EquipmentScale;
            _equipmentMotion.localScale = Vector3.one * scale;
            _equipmentRenderer.sortingOrder = _body.Renderer.sortingOrder + (gripPose.BehindBody ? -1 : 2);
        }

        private float CharacterLocalHeight()
        {
            return _body != null && _body.Renderer != null && _body.Renderer.sprite != null
                ? Mathf.Max(MinimumSpriteSize, _body.Renderer.sprite.bounds.size.y)
                : 1f;
        }

        private void ResetPose()
        {
            if (_characterMotion != null)
            {
                _characterMotion.localPosition = Vector3.zero;
                _characterMotion.localRotation = Quaternion.identity;
                _characterMotion.localScale = Vector3.one;
            }
        }

        private void ResetEquipmentPose()
        {
            if (_equipmentMotion == null)
            {
                return;
            }

            _equipmentMotion.localPosition = Vector3.zero;
            _equipmentMotion.localRotation = Quaternion.identity;
            _equipmentMotion.localScale = Vector3.one;
        }

        private void PresentRig(float elapsedSeconds)
        {
            if (_modularRig == null || _body == null)
            {
                return;
            }

            _modularRig.Present(_body.Facing, _movementMagnitude, _action != null,
                CurrentRigPose(), elapsedSeconds);
        }

        private PlayerRigActionPose CurrentRigPose()
        {
            return _action == null
                ? PlayerRigActionPose.Rest
                : _action.EvaluateRigPose(_actionElapsed / _action.Duration);
        }
    }
}
