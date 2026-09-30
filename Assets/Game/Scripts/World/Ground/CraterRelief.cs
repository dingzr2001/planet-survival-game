using PlanetSurvival.World.Generation.Landforms;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// The drawn shape of a crater, in its own radii from the centre: a flat floor, an inner wall climbing
    /// from <see cref="WallStart"/> to a narrow rim, and an outer slope back down to the plain. Heights
    /// scale with the radius, so every crater has the same proportions.
    /// </summary>
    /// <remarks>
    /// Presentation only. Gameplay stays on the flat plane; the ground, the sprites standing on it and the
    /// pointer are all drawn or resolved at this height (see <see cref="TerrainSurface"/>). The shaders
    /// evaluate the same profile from constants uploaded by <see cref="TerrainSurfaceShaderGlobals"/>, so
    /// keep this and TerrainSurface.cginc in step.
    /// </remarks>
    public static class CraterRelief
    {
        /// <summary>
        /// Depth of the floor below the plain, in radii: about a real simple crater's proportion. Much
        /// shallower and, from almost straight above, the walls are too gentle to show a pit at all.
        /// </summary>
        public const float Depth = .2f;

        /// <summary>Height of the rim crest above the plain, in radii.</summary>
        public const float RimHeight = .04f;

        /// <summary>Width of the rim, in radii.</summary>
        public const float RimWidth = .07f;

        /// <summary>Distance from the centre, in radii, where the inner wall leaves the floor.</summary>
        public const float WallStart = CraterField.WallStart;

        /// <summary>Distance from the centre, in radii, beyond which the ground is untouched.</summary>
        public const float Reach = CraterField.SlopeReach;

        /// <summary>Height at <paramref name="relative"/> radii from a crater's centre, in radii.</summary>
        public static float Profile(float relative)
        {
            if (relative >= Reach)
            {
                return 0f;
            }

            float t = Mathf.Clamp01((relative - WallStart) / (1f - WallStart));
            float bowl = -Depth * (1f - t * t * (3f - 2f * t));
            float rimOffset = (relative - 1f) / RimWidth;
            return bowl + RimHeight * Mathf.Exp(-rimOffset * rimOffset);
        }

        /// <summary>True height in metres at <paramref name="relative"/> radii from the centre of a crater.</summary>
        public static float HeightAt(in Crater crater, float relative) => Profile(relative) * crater.Radius;
    }
}
