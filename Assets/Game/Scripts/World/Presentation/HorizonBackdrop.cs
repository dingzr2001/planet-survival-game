using UnityEngine;

namespace PlanetSurvival.World.Presentation
{
    /// <summary>
    /// Keeps a panoramic space sky locked to the camera while the foreground ground plane
    /// continues to participate in depth testing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HorizonBackdrop : MonoBehaviour
    {
        private const float Distance = 1000f;
        private const float Overscan = 1.03f;

        [SerializeField, HideInInspector] private Camera _camera;
        [SerializeField] private SpriteRenderer _renderer;

        private void OnEnable()
        {
            if (_camera == null)
            {
                _camera = GetComponentInParent<Camera>();
            }

            if (_camera != null && _renderer != null && _renderer.sprite != null)
            {
                FitCameraFrustum(_camera);
            }
        }

        public static HorizonBackdrop Create(Camera camera, Sprite sprite)
        {
            var backdropObject = new GameObject("Panoramic Space Sky");
            backdropObject.transform.SetParent(camera.transform, false);
            HorizonBackdrop backdrop = backdropObject.AddComponent<HorizonBackdrop>();
            backdrop.Configure(camera, sprite);
            return backdrop;
        }

        public void Configure(Camera camera, Sprite sprite)
        {
            if (camera == null)
            {
                Debug.LogError($"{nameof(HorizonBackdrop)} requires a camera.", this);
                enabled = false;
                return;
            }

            _camera = camera;

            _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null)
            {
                _renderer = gameObject.AddComponent<SpriteRenderer>();
            }

            _renderer.sprite = sprite;
            _renderer.sortingOrder = short.MinValue;
            transform.localRotation = Quaternion.identity;
            FitCameraFrustum(camera);
        }

        private void FitCameraFrustum(Camera camera)
        {
            if (_renderer.sprite == null)
            {
                return;
            }

            float viewHeight = 2f * Distance * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            float viewWidth = viewHeight * camera.aspect;
            Vector2 spriteSize = _renderer.sprite.bounds.size;
            float scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y) * Overscan;
            transform.localPosition = Vector3.forward * Distance;
            transform.localScale = Vector3.one * scale;
        }
    }
}
