using UnityEngine;

namespace PlanetSurvival.World.Presentation
{
    /// <summary>
    /// Keeps a cutout sprite facing the fixed game camera and plays texture animation frames.
    /// The owning GameObject remains responsible for gameplay position and collision.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldSpriteView : MonoBehaviour
    {
        private const float MinimumSpriteSize = .001f;

        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private Sprite[] _downFrames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] _leftFrames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] _rightFrames = System.Array.Empty<Sprite>();
        [SerializeField] private Sprite[] _upFrames = System.Array.Empty<Sprite>();
        [SerializeField, Min(1f), Tooltip("Walk-cycle playback speed.")]
        private float _framesPerSecond = 12f;

        private Camera _camera;
        private float _elapsed;
        private bool _isMoving;
        private SpriteFacingDirection _facing = SpriteFacingDirection.Down;
        private Sprite[] _runtimeSprites = System.Array.Empty<Sprite>();

        // Every sprite renderer under this view, drawn with the standing material. Re-collected when the
        // hierarchy changes, since equipment and colour layers are attached after the view is configured.
        private SpriteRenderer[] _standingRenderers = System.Array.Empty<SpriteRenderer>();
        private int _standingHierarchyCount = -1;
        private MaterialPropertyBlock _standingBlock;

        public SpriteRenderer Renderer => _renderer;
        public SpriteFacingDirection Facing => _facing;
        public float DisplayHeight { get; private set; } = 1f;

        /// <summary>
        /// Assigns the body renderer when it lives below the billboard root. This allows player-only
        /// motion and equipment layers without complicating ordinary one-sprite world props.
        /// </summary>
        public void SetRenderer(SpriteRenderer spriteRenderer)
        {
            _renderer = spriteRenderer;
        }

        public void Configure(Sprite sprite, float worldHeight)
        {
            ResolveRenderer();
            DisplayHeight = Mathf.Max(.1f, worldHeight);
            if (_renderer == null)
            {
                Debug.LogError($"{nameof(WorldSpriteView)} on '{name}' requires a {nameof(SpriteRenderer)}.", this);
                return;
            }

            ReleaseRuntimeSprites();
            _renderer.sprite = sprite;
            Sprite[] fallbackFrames = sprite == null ? System.Array.Empty<Sprite>() : new[] { sprite };
            _downFrames = fallbackFrames;
            _leftFrames = fallbackFrames;
            _rightFrames = fallbackFrames;
            _upFrames = fallbackFrames;
            FitHeight(DisplayHeight);
            transform.localPosition = Vector3.up * DisplayHeight * .5f;
        }

        /// <summary>
        /// Places a sprite whose import pivot represents its contact point directly on the owner's ground position.
        /// </summary>
        public void ConfigureGrounded(Sprite sprite, float worldHeight)
        {
            Configure(sprite, worldHeight);
            transform.localPosition = Vector3.zero;
        }

        /// <summary>
        /// Places a grounded sprite scaled so its artwork spans <paramref name="worldWidth"/>, keeping the
        /// aspect ratio of the source art. Used where a footprint, not an authored height, decides the size.
        /// </summary>
        public void ConfigureGroundedWidth(Sprite sprite, float worldWidth)
        {
            ConfigureGrounded(sprite, 1f);
            FitWidth(worldWidth);
        }

        /// <summary>Replaces static artwork without changing its authored world-space fit.</summary>
        public void SetStaticSprite(Sprite sprite)
        {
            ResolveRenderer();
            if (_renderer == null || sprite == null) return;
            ReleaseRuntimeSprites();
            _renderer.sprite = sprite;
            Sprite[] frame = { sprite };
            _downFrames = frame;
            _leftFrames = frame;
            _rightFrames = frame;
            _upFrames = frame;
        }

        public void ConfigureDirectional(Sprite fallbackSprite, float worldHeight,
            Texture2D animationSheet)
        {
            ConfigureDirectional(fallbackSprite, worldHeight, animationSheet, 4, null, null);
        }

        public void ConfigureDirectional(Sprite fallbackSprite, float worldHeight,
            Texture2D animationSheet,
            int framesPerDirection,
            System.Collections.Generic.IReadOnlyList<Rect> frameRects,
            System.Collections.Generic.IReadOnlyList<Vector2> framePivots)
        {
            Configure(fallbackSprite, worldHeight);
            if (_renderer == null)
            {
                return;
            }

            int safeFrameCount = Mathf.Max(1, framesPerDirection);
            bool hasAnchoredLayout = frameRects != null
                && framePivots != null
                && frameRects.Count == safeFrameCount * 4
                && framePivots.Count == safeFrameCount * 4;
            if (animationSheet == null
                || (!hasAnchoredLayout
                    && (animationSheet.width % safeFrameCount != 0 || animationSheet.height % 4 != 0)))
            {
                return;
            }

            int cellWidth = animationSheet.width / safeFrameCount;
            int cellHeight = animationSheet.height / 4;
            _runtimeSprites = new Sprite[safeFrameCount * 4];
            Sprite[][] directions = { _downFrames, _rightFrames, _leftFrames, _upFrames };
            for (int sourceRow = 0; sourceRow < 4; sourceRow++)
            {
                var frames = new Sprite[safeFrameCount];
                for (int column = 0; column < safeFrameCount; column++)
                {
                    int frameIndex = sourceRow * safeFrameCount + column;
                    Rect rect = hasAnchoredLayout
                        ? frameRects[frameIndex]
                        : new Rect(column * cellWidth, (3 - sourceRow) * cellHeight, cellWidth, cellHeight);
                    Vector2 pivot = hasAnchoredLayout ? framePivots[frameIndex] : new Vector2(.5f, .5f);
                    Sprite frame = Sprite.Create(
                        animationSheet,
                        rect,
                        pivot,
                        512f,
                        0,
                        SpriteMeshType.FullRect);
                    frame.name = $"Explorer_{sourceRow}_{column}";
                    frames[column] = frame;
                    _runtimeSprites[frameIndex] = frame;
                }

                directions[sourceRow] = frames;
            }

            _downFrames = directions[0];
            _rightFrames = directions[1];
            _leftFrames = directions[2];
            _upFrames = directions[3];
            _renderer.sprite = FirstFrame(_facing);
            FitHeight(worldHeight);
            if (hasAnchoredLayout)
            {
                transform.localPosition = Vector3.zero;
            }
        }

        public void ConfigureDirectional(Sprite fallbackSprite, float worldHeight,
            System.Collections.Generic.IReadOnlyList<Sprite> downFrames,
            System.Collections.Generic.IReadOnlyList<Sprite> leftFrames,
            System.Collections.Generic.IReadOnlyList<Sprite> rightFrames,
            System.Collections.Generic.IReadOnlyList<Sprite> upFrames)
        {
            Configure(fallbackSprite, worldHeight);
            if (_renderer == null)
            {
                return;
            }

            _downFrames = CopyValidFrames(downFrames, _downFrames);
            _leftFrames = CopyValidFrames(leftFrames, _downFrames);
            _rightFrames = CopyValidFrames(rightFrames, _downFrames);
            _upFrames = CopyValidFrames(upFrames, _downFrames);
            _renderer.sprite = FirstFrame(_facing);
            FitHeight(worldHeight);
        }

        /// <summary>
        /// Updates movement in camera-relative screen space. Positive Y faces away from the camera.
        /// </summary>
        public void SetMovement(Vector2 cameraRelativeMovement)
        {
            bool wasMoving = _isMoving;
            _isMoving = cameraRelativeMovement.sqrMagnitude > .001f;
            if (!_isMoving)
            {
                if (wasMoving)
                {
                    _elapsed = 0f;
                    ShowFrame(FirstFrame(_facing));
                }

                return;
            }

            SpriteFacingDirection facing = ResolveFacing(cameraRelativeMovement);
            if (!wasMoving || facing != _facing)
            {
                _elapsed = 0f;
                _facing = facing;
                ShowFrame(FirstFrame(_facing));
            }
        }

        private void Awake()
        {
            ResolveRenderer();
        }

        private void OnDestroy()
        {
            ReleaseRuntimeSprites();
        }

        private void LateUpdate()
        {
            ResolveCamera();
            FaceCamera();
            Animate();
            UpdateSortingOrder();
            UpdateStandingDepth();
        }

        /// <summary>
        /// Tells the standing material where this sprite meets the ground: the point on the ground behind
        /// the sprite's bottom edge along the camera's view, and the foot the sprite stands on. The shader
        /// depth-tests the whole sprite as an upright board there, so it stands in front of mountain walls
        /// behind it and is hidden by those in front of it, and draws it at the ground's height at the foot,
        /// so it stands on a crater floor.
        /// </summary>
        private void UpdateStandingDepth()
        {
            if (_camera == null || _renderer == null || _renderer.sprite == null)
            {
                return;
            }

            if (transform.hierarchyCount != _standingHierarchyCount)
            {
                _standingRenderers = GetComponentsInChildren<SpriteRenderer>(true);
                for (int i = 0; i < _standingRenderers.Length; i++)
                {
                    StandingSpriteMaterial.Apply(_standingRenderers[i]);
                }

                _standingHierarchyCount = transform.hierarchyCount;
            }

            // The sprite faces the camera, so its bottom edge is the lowest and nearest part of its bounds.
            Bounds bounds = _renderer.bounds;
            Vector3 forward = _camera.transform.forward;
            float travelToGround = forward.y < -1e-3f ? Mathf.Max(0f, bounds.min.y) / -forward.y : 0f;
            float groundZ = bounds.min.z + forward.z * travelToGround;
            Vector3 foot = transform.position;

            _standingBlock ??= new MaterialPropertyBlock();
            for (int i = 0; i < _standingRenderers.Length; i++)
            {
                SpriteRenderer standing = _standingRenderers[i];
                if (standing == null || standing.sharedMaterial != StandingSpriteMaterial.Shared)
                {
                    continue;
                }

                standing.GetPropertyBlock(_standingBlock);
                _standingBlock.SetFloat(StandingSpriteMaterial.GroundZId, groundZ);
                _standingBlock.SetFloat(StandingSpriteMaterial.EnabledId, 1f);
                _standingBlock.SetVector(StandingSpriteMaterial.FootId, new Vector4(foot.x, foot.z, 0f, 0f));
                standing.SetPropertyBlock(_standingBlock);
            }
        }

        private void ResolveCamera()
        {
            if (_camera == null || !_camera.isActiveAndEnabled)
            {
                _camera = Camera.main;
            }
        }

        private void FaceCamera()
        {
            if (_camera != null)
            {
                transform.rotation = _camera.transform.rotation;
            }
        }

        private void Animate()
        {
            Sprite[] frames = FramesFor(_facing);
            if (_renderer == null || frames.Length == 0)
            {
                return;
            }

            if (!_isMoving)
            {
                ShowFrame(frames[0]);
                return;
            }

            _elapsed += Time.deltaTime;
            int frame = Mathf.FloorToInt(_elapsed * _framesPerSecond) % frames.Length;
            ShowFrame(frames[frame]);
        }

        private static SpriteFacingDirection ResolveFacing(Vector2 movement)
        {
            if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
            {
                return movement.x < 0f ? SpriteFacingDirection.Left : SpriteFacingDirection.Right;
            }

            return movement.y < 0f ? SpriteFacingDirection.Down : SpriteFacingDirection.Up;
        }

        private Sprite[] FramesFor(SpriteFacingDirection facing)
        {
            return facing switch
            {
                SpriteFacingDirection.Left => _leftFrames,
                SpriteFacingDirection.Right => _rightFrames,
                SpriteFacingDirection.Up => _upFrames,
                _ => _downFrames
            };
        }

        private Sprite FirstFrame(SpriteFacingDirection facing)
        {
            Sprite[] frames = FramesFor(facing);
            return frames.Length > 0 ? frames[0] : null;
        }

        private void ShowFrame(Sprite sprite)
        {
            if (_renderer != null && sprite != null)
            {
                _renderer.sprite = sprite;
                _renderer.flipX = false;
            }
        }

        private static Sprite[] CopyValidFrames(
            System.Collections.Generic.IReadOnlyList<Sprite> source, Sprite[] fallback)
        {
            if (source == null || source.Count == 0)
            {
                return fallback;
            }

            var frames = new System.Collections.Generic.List<Sprite>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                {
                    frames.Add(source[i]);
                }
            }

            return frames.Count > 0 ? frames.ToArray() : fallback;
        }

        private void ReleaseRuntimeSprites()
        {
            for (int i = 0; i < _runtimeSprites.Length; i++)
            {
                if (_runtimeSprites[i] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(_runtimeSprites[i]);
                }
                else
                {
                    DestroyImmediate(_runtimeSprites[i]);
                }
            }

            _runtimeSprites = System.Array.Empty<Sprite>();
        }

        private void UpdateSortingOrder()
        {
            if (_camera == null || _renderer == null)
            {
                return;
            }

            float depth = Vector3.Dot(_camera.transform.forward, transform.position - _camera.transform.position);
            _renderer.sortingOrder = -Mathf.RoundToInt(depth * 100f);
        }

        private void FitWidth(float worldWidth)
        {
            if (_renderer == null || _renderer.sprite == null)
            {
                return;
            }

            Vector3 spriteSize = _renderer.sprite.bounds.size;
            float scale = Mathf.Max(.01f, worldWidth) / Mathf.Max(MinimumSpriteSize, spriteSize.x);
            transform.localScale = Vector3.one * scale;
            DisplayHeight = Mathf.Max(.1f, spriteSize.y * scale);
        }

        private void FitHeight(float worldHeight)
        {
            if (_renderer == null || _renderer.sprite == null)
            {
                return;
            }

            float spriteHeight = Mathf.Max(MinimumSpriteSize, _renderer.sprite.bounds.size.y);
            float scale = Mathf.Max(.1f, worldHeight) / spriteHeight;
            transform.localScale = Vector3.one * scale;
        }

        private void ResolveRenderer()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            if (_renderer == null)
            {
                _renderer = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

    }
}
