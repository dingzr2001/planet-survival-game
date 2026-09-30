using UnityEngine;

namespace PlanetSurvival.World.Presentation
{
    /// <summary>
    /// The material for sprites and overlays lying flat on the ground, drawn at the height of the drawn
    /// ground under each vertex so they follow craters (Resources/Shaders/SurfaceSprite). The shader lives
    /// under Resources so it ships in every build without a scene reference.
    /// </summary>
    public static class SurfaceSpriteMaterial
    {
        public const string ShaderResourcePath = "Shaders/SurfaceSprite";
        private const string DefaultSpriteShaderName = "Sprites/Default";

        private static Shader _shader;
        private static Material _shared;
        private static bool _missingReported;

        /// <summary>The surface sprite shader, or null when it could not be loaded.</summary>
        public static Shader Shader
        {
            get
            {
                if (_shader != null)
                {
                    return _shader;
                }

                _shader = Resources.Load<Shader>(ShaderResourcePath);
                if (_shader == null && !_missingReported)
                {
                    Debug.LogError($"Surface sprite shader was not found at Resources/{ShaderResourcePath}.");
                    _missingReported = true;
                }

                return _shader;
            }
        }

        /// <summary>The shared material for sprite renderers, or null when the shader could not be loaded.</summary>
        public static Material Shared
        {
            get
            {
                if (_shared != null)
                {
                    return _shared;
                }

                Shader shader = Shader;
                if (shader == null)
                {
                    return null;
                }

                _shared = new Material(shader) { name = "Surface Sprite", hideFlags = HideFlags.DontSave };
                return _shared;
            }
        }

        /// <summary>
        /// Gives the renderer the surface material unless it already uses a custom one. Returns whether it
        /// now draws with it.
        /// </summary>
        public static bool Apply(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            Material shared = Shared;
            if (shared == null)
            {
                return false;
            }

            Material current = renderer.sharedMaterial;
            if (current == shared)
            {
                return true;
            }

            if (current != null && current.shader != null && current.shader.name != DefaultSpriteShaderName)
            {
                return false;
            }

            renderer.sharedMaterial = shared;
            return true;
        }
    }
}
