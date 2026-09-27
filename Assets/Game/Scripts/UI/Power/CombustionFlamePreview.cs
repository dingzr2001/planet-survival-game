using UnityEngine;

namespace PlanetSurvival.UI.Power
{
    /// <summary>Renders the flame effect prefab into the IMGUI generator panel.</summary>
    internal sealed class CombustionFlamePreview
    {
        private const string PreviewLayerName = "UIEffectPreview";
        private const int TextureSize = 512;
        private static readonly Vector3 PreviewPosition = new(10000f, 10000f, 10000f);

        private readonly GameObject _effect;
        private readonly GameObject _cameraObject;
        private readonly Camera _camera;
        private readonly RenderTexture _texture;
        private readonly ParticleSystem[] _particles;
        private readonly ParticleSystem _outerFlames;
        private readonly ParticleSystem _glow;
        private bool _burning;

        private CombustionFlamePreview(GameObject prefab, Transform owner, int layer)
        {
            _effect = Object.Instantiate(prefab, owner);
            _effect.name = "Combustion Flame Preview";
            _effect.transform.SetPositionAndRotation(PreviewPosition, Quaternion.identity);
            SetLayerRecursively(_effect.transform, layer);
            _particles = _effect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem particle in _particles)
            {
                switch (particle.name)
                {
                    case "Outer Flames":
                        _outerFlames = particle;
                        ConfigureFlames(particle, .38f, .68f, .66f, 1.08f,
                            new Color(1f, .65f, .48f, .58f), new Color(1f, .9f, .7f, .78f));
                        break;
                    case "Inner Flames":
                        ConfigureFlames(particle, .17f, .32f, .44f, .73f,
                            new Color(1f, .88f, .67f, .76f), new Color(1f, 1f, .9f, .9f));
                        break;
                    case "Fire Glow":
                        _glow = particle;
                        ParticleSystem.MainModule glowMain = particle.main;
                        glowMain.startColor = new Color(1f, .28f, .035f, .2f);
                        glowMain.startSize = new ParticleSystem.MinMaxCurve(.75f, 1.05f);
                        break;
                }
            }
            _effect.SetActive(false);

            _texture = new RenderTexture(TextureSize, TextureSize, 16, RenderTextureFormat.ARGB32)
            {
                name = "Combustion Flame Preview",
                filterMode = FilterMode.Bilinear
            };
            _texture.Create();

            _cameraObject = new GameObject("Combustion Flame Camera");
            _cameraObject.transform.SetParent(owner, false);
            _cameraObject.transform.position = PreviewPosition + new Vector3(0f, -.52f, -10f);
            _cameraObject.transform.rotation = Quaternion.identity;
            _camera = _cameraObject.AddComponent<Camera>();
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.orthographicSize = .68f;
            _camera.nearClipPlane = .1f;
            _camera.farClipPlane = 20f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.clear;
            _camera.cullingMask = 1 << layer;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.targetTexture = _texture;
        }

        public Texture Texture => _texture;

        public static CombustionFlamePreview Create(GameObject prefab, Transform owner)
        {
            int layer = LayerMask.NameToLayer(PreviewLayerName);
            if (prefab == null || layer < 0)
            {
                Debug.LogError($"Combustion flame preview requires its prefab and {PreviewLayerName} layer.", owner);
                return null;
            }

            return new CombustionFlamePreview(prefab, owner, layer);
        }

        public void SetBurning(bool burning)
        {
            if (_burning == burning) return;
            _burning = burning;
            _effect.SetActive(burning);
            foreach (ParticleSystem particle in _particles)
            {
                if (burning) particle.Play();
                else particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        public void Render()
        {
            if (!_burning) return;

            // Two gentle frequencies keep the glow alive without flashing the whole panel.
            float time = Time.unscaledTime;
            float breath = 1f + .12f * Mathf.Sin(time * 7.3f) + .06f * Mathf.Sin(time * 12.7f);
            if (_outerFlames != null)
            {
                ParticleSystem.EmissionModule emission = _outerFlames.emission;
                emission.rateOverTime = 28f * breath;
            }
            if (_glow != null)
            {
                ParticleSystem.EmissionModule emission = _glow.emission;
                emission.rateOverTime = 9f * breath;
            }
            _camera.Render();
        }

        private static void ConfigureFlames(ParticleSystem particle, float minWidth, float maxWidth,
            float minHeight, float maxHeight, Color dark, Color bright)
        {
            ParticleSystem.MainModule main = particle.main;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(minWidth, maxWidth);
            main.startSizeY = new ParticleSystem.MinMaxCurve(minHeight, maxHeight);
            main.startColor = new ParticleSystem.MinMaxGradient(dark, bright);

            ParticleSystem.ShapeModule shape = particle.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(particle.name == "Inner Flames" ? .28f : .54f, .02f, .02f);
            if (particle.name == "Inner Flames")
            {
                ParticleSystem.EmissionModule emission = particle.emission;
                emission.rateOverTime = 18f;
            }
            ParticleSystem.NoiseModule noise = particle.noise;
            noise.enabled = true;
            noise.strength = particle.name == "Inner Flames" ? .035f : .08f;
            noise.frequency = .6f;
        }

        public void Dispose()
        {
            _camera.targetTexture = null;
            _texture.Release();
            Object.Destroy(_texture);
            Object.Destroy(_cameraObject);
            Object.Destroy(_effect);
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayerRecursively(child, layer);
        }
    }
}
