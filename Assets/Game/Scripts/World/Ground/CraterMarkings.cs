using PlanetSurvival.World.Generation.Landforms;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// The surface marks around impact craters, as one signed value: negative on a floor (how strongly its
    /// settled dust shows), positive outside a rim (how thick the ejecta lies, continuous near the rim and
    /// breaking into rays further out), zero elsewhere. Ejecta rocks are scattered by it.
    /// </summary>
    /// <remarks>
    /// The ground shader draws the floor and the ejecta blanket from the relative crater distance stored in
    /// the landform map, with the same radii as here (<see cref="FloorFull"/> to <see cref="EjectaFull"/>);
    /// keep the two in step.
    /// </remarks>
    public static class CraterMarkings
    {
        /// <summary>Share of the radius out to which dust covers the floor fully.</summary>
        public const float FloorFull = .5f;

        /// <summary>Share of the radius by which the floor dust has faded out.</summary>
        public const float FloorEnd = .85f;

        /// <summary>Share of the radius, on the outer slope of the rim, where ejecta begins.</summary>
        public const float EjectaStart = .9f;

        /// <summary>Share of the radius, just past the crest, where ejecta is full strength.</summary>
        public const float EjectaFull = 1.05f;

        // Between these radii the continuous blanket gives way to rays.
        private const float RaysBegin = 1.15f;
        private const float RaysFull = 1.5f;

        // Radius of the circle the angular noise is sampled on, which sets roughly how many rays a crater has.
        private const float RayFrequency = 2.2f;
        private const int RaySeed = 0x6a09e667;

        /// <summary>The signed crater mark at a point, from the nearest crater.</summary>
        public static float Sample(CraterField craters, float x, float z, LandformAppearance appearance)
        {
            if (craters == null || appearance == null ||
                !craters.TryGetNearest(x, z, out Crater crater, out float relative))
            {
                return 0f;
            }

            return Mark(crater, relative, x, z, appearance);
        }

        /// <summary>
        /// The signed crater mark at a point <paramref name="relative"/> crater radii from the centre of
        /// <paramref name="crater"/>, its nearest crater.
        /// </summary>
        public static float Mark(in Crater crater, float relative, float x, float z, LandformAppearance appearance)
        {
            if (relative < EjectaStart)
            {
                return -(1f - Smoothstep(FloorFull, FloorEnd, relative));
            }

            float reach = appearance.EjectaReach;
            if (relative >= reach)
            {
                return 0f;
            }

            float blanket = Smoothstep(EjectaStart, EjectaFull, relative)
                            * (1f - Smoothstep(EjectaFull, reach, relative));
            return blanket * Mathf.Lerp(1f, RayStrength(crater, x, z), Smoothstep(RaysBegin, RaysFull, relative));
        }

        /// <summary>From zero between rays to one along a ray, by direction from the crater's centre.</summary>
        private static float RayStrength(in Crater crater, float x, float z)
        {
            float angle = Mathf.Atan2(z - crater.CenterZ, x - crater.CenterX);
            // Each crater gets its own rays; its centre is as good a key as any and needs no extra state.
            int seed = RaySeed ^ (Mathf.RoundToInt(crater.CenterX) * 73856093)
                                ^ (Mathf.RoundToInt(crater.CenterZ) * 19349663);
            float noise = GradientNoise.Sample(seed, Mathf.Cos(angle) * RayFrequency, Mathf.Sin(angle) * RayFrequency);
            return Mathf.Clamp01(.35f + .9f * noise);
        }

        private static float Smoothstep(float edge0, float edge1, float value)
        {
            float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
