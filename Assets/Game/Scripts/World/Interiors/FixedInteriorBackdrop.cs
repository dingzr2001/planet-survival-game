using UnityEngine;

namespace PlanetSurvival.World.Interiors
{
    [DisallowMultipleComponent]
    public sealed class FixedInteriorBackdrop : MonoBehaviour
    {
        private const float DistanceFromCamera = 30f;

        public static void Create(Camera camera, Sprite sprite)
        {
            if (camera == null || sprite == null)
            {
                Debug.LogError($"{nameof(FixedInteriorBackdrop)} requires a camera and background sprite.");
                return;
            }

            var backdrop = new GameObject("Fixed Interior Background");
            backdrop.transform.SetParent(camera.transform, false);
            backdrop.transform.localPosition = Vector3.forward * DistanceFromCamera;

            SpriteRenderer renderer = backdrop.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -10000;

            float spriteHeight = Mathf.Max(.01f, sprite.bounds.size.y);
            float cameraHeight = camera.orthographicSize * 2f;
            backdrop.transform.localScale = Vector3.one * (cameraHeight / spriteHeight);
            backdrop.AddComponent<FixedInteriorBackdrop>();
        }
    }
}
