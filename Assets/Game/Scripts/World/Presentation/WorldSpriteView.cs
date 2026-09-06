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
        private enum FacingDirection
        {
            Down,
            Left,
            Right,
            Up
        }

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
        private FacingDirection _facing = FacingDirection.Down;
        private Sprite[] _runtimeSprites = System.Array.Empty<Sprite>();

        public SpriteRenderer Renderer => _renderer;

        public void Configure(Sprite sprite, float worldHeight)
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            _renderer.sprite = sprite;
            Sprite[] fallbackFrames = sprite == null ? System.Array.Empty<Sprite>() : new[] { sprite };
            _downFrames = fallbackFrames;
            _leftFrames = fallbackFrames;
            _rightFrames = fallbackFrames;
            _upFrames = fallbackFrames;
            FitHeight(worldHeight);
            transform.localPosition = Vector3.up * Mathf.Max(.1f, worldHeight) * .5f;
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
            int safeFrameCount = Mathf.Max(1, framesPerDirection);
            if (animationSheet == null
                || animationSheet.width % safeFrameCount != 0
                || animationSheet.height % 4 != 0)
            {
                return;
            }

            ReleaseRuntimeSprites();
            int cellWidth = animationSheet.width / safeFrameCount;
            int cellHeight = animationSheet.height / 4;
            bool hasAnchoredLayout = frameRects != null
                && framePivots != null
                && frameRects.Count == safeFrameCount * 4
                && framePivots.Count == safeFrameCount * 4;
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

            FacingDirection facing = ResolveFacing(cameraRelativeMovement);
            if (!wasMoving || facing != _facing)
            {
                _elapsed = 0f;
                _facing = facing;
                ShowFrame(FirstFrame(_facing));
            }
        }

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }
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

        private static FacingDirection ResolveFacing(Vector2 movement)
        {
            if (Mathf.Abs(movement.x) > Mathf.Abs(movement.y))
            {
                return movement.x < 0f ? FacingDirection.Left : FacingDirection.Right;
            }

            return movement.y < 0f ? FacingDirection.Down : FacingDirection.Up;
        }

        private Sprite[] FramesFor(FacingDirection facing)
        {
            return facing switch
            {
                FacingDirection.Left => _leftFrames,
                FacingDirection.Right => _rightFrames,
                FacingDirection.Up => _upFrames,
                _ => _downFrames
            };
        }

        private Sprite FirstFrame(FacingDirection facing)
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
    }
}
