using System.Collections.Generic;
using UnityEngine;

namespace PlanetSurvival.World.Generation
{
    [CreateAssetMenu(menuName = "Planet Survival/World/Visual Settings")]
    public sealed class WorldVisualSettings : ScriptableObject
    {
        [Header("Ground")]
        [SerializeField] private Texture2D _groundTexture;
        [SerializeField] private Color _groundTint = Color.white;
        [SerializeField, Min(.1f), Tooltip("World units covered by one repeat of the ground texture.")]
        private float _groundTileSize = 8f;

        [Header("Player")]
        [SerializeField] private Sprite _playerSprite;
        [SerializeField, Min(.1f)] private float _playerHeight = 1.8f;
        [SerializeField, Tooltip("Four direction rows: down, right, left, then up.")]
        private Texture2D _playerAnimationSheet;
        [SerializeField, Min(1)] private int _playerFramesPerDirection = 4;
        [SerializeField, HideInInspector] private Rect[] _playerFrameRects = System.Array.Empty<Rect>();
        [SerializeField, HideInInspector] private Vector2[] _playerFramePivots = System.Array.Empty<Vector2>();

        [Header("Landing Pod")]
        [SerializeField, Tooltip("Camera-facing exterior artwork used by the surface landing pod.")]
        private Sprite _landingPodExteriorSprite;
        [SerializeField, Min(.1f), Tooltip("Displayed height of the landing pod exterior in world units.")]
        private float _landingPodExteriorHeight = 5.4f;

        [Header("Presentation")]
        [SerializeField, Tooltip("Camera-relative distant sky and horizon backdrop.")]
        private Sprite _horizonSprite;
        [SerializeField] private Color _shadowColor = new(0.08f, 0.035f, 0.02f, .48f);

        public Texture2D GroundTexture => _groundTexture;
        public Color GroundTint => _groundTint;
        public float GroundTileSize => _groundTileSize;
        public Sprite PlayerSprite => _playerSprite;
        public float PlayerHeight => _playerHeight;
        public Texture2D PlayerAnimationSheet => _playerAnimationSheet;
        public int PlayerFramesPerDirection => _playerFramesPerDirection;
        public IReadOnlyList<Rect> PlayerFrameRects => _playerFrameRects;
        public IReadOnlyList<Vector2> PlayerFramePivots => _playerFramePivots;
        public Sprite LandingPodExteriorSprite => _landingPodExteriorSprite;
        public float LandingPodExteriorHeight => _landingPodExteriorHeight;
        public Sprite HorizonSprite => _horizonSprite;
        public Color ShadowColor => _shadowColor;

        public void Configure(Texture2D groundTexture, Sprite playerSprite, Sprite horizonSprite = null)
        {
            _groundTexture = groundTexture;
            _playerSprite = playerSprite;
            _horizonSprite = horizonSprite;
        }

        public void ConfigurePlayerAnimation(Texture2D animationSheet, int framesPerDirection,
            Rect[] frameRects, Vector2[] framePivots)
        {
            _playerAnimationSheet = animationSheet;
            _playerFramesPerDirection = Mathf.Max(1, framesPerDirection);
            _playerFrameRects = frameRects ?? System.Array.Empty<Rect>();
            _playerFramePivots = framePivots ?? System.Array.Empty<Vector2>();
        }

        public void ConfigureLandingPod(Sprite exteriorSprite, float exteriorHeight)
        {
            _landingPodExteriorSprite = exteriorSprite;
            _landingPodExteriorHeight = Mathf.Max(.1f, exteriorHeight);
        }
    }
}
