using UnityEngine;

namespace PlanetSurvival.World.Presentation
{
    /// <summary>
    /// The shared material that depth-tests camera-facing sprites as upright objects standing on the ground,
    /// so they sort correctly against the 3D mountain meshes. The shader lives under Resources so it ships
    /// in every build without a scene reference.
    /// </summary>
    public static class StandingSpriteMaterial
    {
        public const string ShaderResourcePath = "Shaders/StandingSprite";
        private const string DefaultSpriteShaderName = "Sprites/Default";

        public static readonly int GroundZId = Shader.PropertyToID("_StandingGroundZ");
        public static readonly int EnabledId = Shader.PropertyToID("_StandingEnabled");

        /// <summary>World (x, z) of the sprite's foot; the sprite is drawn at the ground's height there.</summary>
        public static readonly int FootId = Shader.PropertyToID("_StandingFoot");

        private static Material _shared;
        private static bool _missingReported;

        /// <summary>The shared material, or null when the shader could not be loaded.</summary>
        public static Material Shared
        {
            get
            {
                if (_shared != null)
                {
                    return _shared;
                }

                var shader = Resources.Load<Shader>(ShaderResourcePath);
                if (shader == null)
                {
                    if (!_missingReported)
                    {
                        Debug.LogError($"Standing sprite shader was not found at Resources/{ShaderResourcePath}.");
                        _missingReported = true;
                    }

                    return null;
                }

                _shared = new Material(shader) { name = "Standing Sprite", hideFlags = HideFlags.DontSave };
                return _shared;
            }
        }

        /// <summary>
        /// Gives the renderer the standing material unless it already uses a custom one. Returns whether it
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
