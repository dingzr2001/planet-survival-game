using UnityEngine;

namespace PlanetSurvival.World.Presentation
{
    /// <summary>
    /// Draws a top-down cutout on the X/Z plane. Ground decals deliberately stay outside the
    /// camera-depth sorting used by upright sprites, so actors can never disappear behind a floor mark.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GroundDecalView : MonoBehaviour
    {
        public const int SortingOrder = -30000;
        private const float GroundOffset = .015f;
        private const float MinimumSize = .001f;

        [SerializeField] private SpriteRenderer _renderer;

        public SpriteRenderer Renderer => _renderer;

        public void Configure(Sprite sprite, Vector2 footprint)
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }

            _renderer.sprite = sprite;
            _renderer.sortingOrder = SortingOrder;
            transform.localPosition = Vector3.up * GroundOffset;
            transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            FitFootprint(footprint);
        }

        private void Awake()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<SpriteRenderer>();
            }
        }

        private void FitFootprint(Vector2 footprint)
        {
            if (_renderer == null || _renderer.sprite == null)
            {
                return;
            }

            Vector2 spriteSize = _renderer.sprite.bounds.size;
            float widthScale = Mathf.Max(.1f, footprint.x) / Mathf.Max(MinimumSize, spriteSize.x);
            float depthScale = Mathf.Max(.1f, footprint.y) / Mathf.Max(MinimumSize, spriteSize.y);
            transform.localScale = Vector3.one * Mathf.Min(widthScale, depthScale);
        }
    }
}
