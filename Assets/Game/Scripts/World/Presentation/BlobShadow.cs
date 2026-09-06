using UnityEngine;

namespace PlanetSurvival.World.Presentation
{
    public static class BlobShadow
    {
        private const int TextureSize = 64;
        private static Sprite _sprite;

        public static void Create(Transform parent, Vector2 size, Color color)
        {
            var shadowObject = new GameObject("Blob Shadow");
            shadowObject.transform.SetParent(parent, false);
            shadowObject.transform.localPosition = new Vector3(0f, .012f, 0f);
            shadowObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            shadowObject.transform.localScale = new Vector3(Mathf.Max(.1f, size.x), Mathf.Max(.1f, size.y), 1f);

            SpriteRenderer renderer = shadowObject.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSprite();
            renderer.color = color;
            renderer.sortingOrder = -32000;
        }

        private static Sprite GetSprite()
        {
            if (_sprite != null)
            {
                return _sprite;
            }

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "BlobShadow",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float u = ((x + .5f) / TextureSize - .5f) * 2f;
                    float v = ((y + .5f) / TextureSize - .5f) * 2f;
                    float alpha = Mathf.Clamp01((1f - u * u - v * v) * 3f);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(.5f, .5f), TextureSize);
            _sprite.name = "BlobShadow";
            return _sprite;
        }
    }
}
