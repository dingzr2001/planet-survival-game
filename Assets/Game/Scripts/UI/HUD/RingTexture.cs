using System;
using UnityEngine;

namespace PlanetSurvival.UI.HUD
{
    /// <summary>
    /// Bakes an anti-aliased ring (donut arc) into a white alpha texture so IMGUI can tint and draw it.
    /// The texture is only rebuilt when the quantized fill amount actually changes.
    /// </summary>
    internal sealed class RingTexture : IDisposable
    {
        private const int Resolution = 128;
        private const int FillSteps = 240;
        private const float OuterRadius = 0.5f;
        private const float Thickness = 0.15f;
        private const float EdgeSoftness = 1.6f / Resolution;

        private Texture2D _texture;
        private Color32[] _pixels;
        private int _bakedStep = -1;

        public Texture2D Get(float fill)
        {
            int step = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(fill) * FillSteps), 0, FillSteps);
            if (_texture != null && _bakedStep == step)
            {
                return _texture;
            }

            EnsureTexture();
            Bake(step / (float)FillSteps);
            _bakedStep = step;
            return _texture;
        }

        public void Dispose()
        {
            if (_texture != null)
            {
                UnityEngine.Object.Destroy(_texture);
                _texture = null;
            }

            _pixels = null;
            _bakedStep = -1;
        }

        private void EnsureTexture()
        {
            if (_texture != null)
            {
                return;
            }

            _texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false)
            {
                name = "RingGauge",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _pixels = new Color32[Resolution * Resolution];
        }

        private void Bake(float fill)
        {
            float innerRadius = OuterRadius - Thickness;
            const float twoPi = Mathf.PI * 2f;

            for (int y = 0; y < Resolution; y++)
            {
                // Texture space is bottom-up; flip so the arc starts at the top of the drawn rect.
                float v = 0.5f - (y + 0.5f) / Resolution;
                int rowStart = y * Resolution;

                for (int x = 0; x < Resolution; x++)
                {
                    float u = (x + 0.5f) / Resolution - 0.5f;
                    float radius = Mathf.Sqrt(u * u + v * v);
                    float alpha = Mathf.Clamp01((OuterRadius - radius) / EdgeSoftness)
                                  * Mathf.Clamp01((radius - innerRadius) / EdgeSoftness);

                    if (alpha > 0f && fill < 1f)
                    {
                        alpha *= fill <= 0f ? 0f : ArcCoverage(u, v, fill, radius, twoPi);
                    }

                    _pixels[rowStart + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
                }
            }

            _texture.SetPixels32(_pixels);
            _texture.Apply(false, false);
        }

        /// <summary>Coverage of a clockwise arc that starts at twelve o'clock, softened at both cut edges.</summary>
        private static float ArcCoverage(float u, float v, float fill, float radius, float twoPi)
        {
            float angle = Mathf.Atan2(u, -v);
            if (angle < 0f)
            {
                angle += twoPi;
            }

            float normalized = angle / twoPi;
            float arcLength = Mathf.Max(radius, 0.0001f) * twoPi;
            float toEnd = (fill - normalized) * arcLength;
            float fromStart = normalized * arcLength;
            return Mathf.Min(Mathf.Clamp01(toEnd / EdgeSoftness), Mathf.Clamp01(fromStart / EdgeSoftness));
        }
    }
}
