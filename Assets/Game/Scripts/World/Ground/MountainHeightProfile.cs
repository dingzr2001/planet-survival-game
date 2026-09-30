using PlanetSurvival.World.Generation.Landforms;
using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// The height of the mountain mesh. The foot rises from the collision border as rock cliff along some
    /// stretches of the edge and as steep scree along others, so the outline is not one even wall around a
    /// lid; behind it the massif swells with the landform elevation, so large ranges build broad
    /// summits while thin ones stay low, and ridged noise adds crests and valleys on top. A cliff and a flat
    /// plateau alone read as a slab from the steep camera, whatever the shading.
    /// The same heights drive the mesh, its self-shadowing and the ground shader's shadows, which is what
    /// keeps them aligned.
    /// </summary>
    public static class MountainHeightProfile
    {
        /// <summary>Metres below the ground at which mesh vertices outside the mountain are buried.</summary>
        public const float BuriedFoot = -.05f;

        /// <summary>Peak amplitude, in metres, of the rocky relief on mountain tops.</summary>
        public const float BumpAmplitude = .15f;

        // Horizontal metres over which a rock cliff climbs to its height; steep enough to read as rock face.
        private const float CliffRun = .6f;

        // Horizontal metres over which a scree stretch climbs, about 50° at its steepest: still clearly a
        // barrier at the collision border, but a slope rather than a wall.
        private const float ScreeRun = 3.2f;

        // Weight of the fine octave that breaks up the lip, relative to the broad wander.
        private const float LipRaggedness = .45f;

        // Share of the local cliff height a scree stretch reaches before the top begins.
        private const float ScreeHeightShare = .65f;

        // Metres along the edge over which the foot turns between cliff and scree.
        private const float FootFeatureSize = 11f;

        // How strongly a ridge running into the edge pulls the foot towards cliff: spurs end in rock faces,
        // gullies in scree.
        private const float SpurSteepening = 1.5f;

        // The top approaches its full plateau rise over roughly this many metres inland.
        private const float PlateauFalloff = 8f;

        // Relief on the top only starts once past the brink, so the lip stays clean.
        private const float BumpFadeIn = 1.5f;

        // Metres behind the brink over which the ridges grow to full strength. Short, so spurs and gullies
        // run right up to the brink: relief that ramps up with distance from the edge follows the outline,
        // and every south rim would become one even strip facing away from the light.
        private const float ReliefFadeIn = 3f;

        /// <summary>
        /// Steepest rise, in metres per metre, the relief may climb from the brink. Where a massif wants to be
        /// tall right behind its wall this cap is what shapes it, as a ramp that follows the outline. The
        /// camera stretches a slope facing it by its own height, so behind every south wall a steep ramp
        /// would show as a wide strip turned away from the light: a dark second wall. Kept gentle.
        /// </summary>
        public const float MaximumReliefSlope = .35f;

        // Metres over which the slope cap rounds into the free relief.
        private const float CapSoftening = 1.5f;

        /// <summary>Landform elevation above the mountain level beyond which massifs stop swelling.</summary>
        public const float MaximumElevationExcess = .8f;

        private const int CliffSeed = 0x3c6ef372;
        private const int FootSeed = 0x4f1bbcdc;
        private const int BumpSeed = 0x1b873593;
        private const int RoughnessSeed = 0x7f4a7c15;
        private const int RidgeSeed = 0x2545f491;
        private const int RidgeOctaves = 3;

        // Domain warp of the ridge field, in ridge spacings, and the turn between its octaves, in radians.
        private const float RidgeWarp = .45f;
        private const float RidgeOctaveTurn = .83f;

        /// <summary>Mesh height at a point <paramref name="inside"/> metres inside the mountain border.</summary>
        /// <param name="elevationExcess">
        /// Landform elevation above the mountain level at the point (<c>Elevation - 1</c>); zero on the border.
        /// </param>
        public static float Height(float inside, float elevationExcess, float worldX, float worldZ,
            LandformAppearance appearance)
        {
            if (inside <= 0f || appearance == null)
            {
                return BuriedFoot;
            }

            float ridge = RidgeAt(worldX, worldZ, appearance);
            float steepness = FootSteepness(worldX, worldZ, ridge);
            float run = Mathf.Lerp(ScreeRun, CliffRun, steepness);
            float footHeight = CliffHeightAt(worldX, worldZ, appearance)
                               * Mathf.Lerp(ScreeHeightShare, 1f, steepness);
            float foot = footHeight * Smootherstep(Mathf.Clamp01(inside / run));
            float beyond = Mathf.Max(0f, inside - run);
            float plateau = appearance.PlateauRise * (1f - Mathf.Exp(-beyond / PlateauFalloff));
            float bumps = GradientNoise.Sample(BumpSeed, worldX * .35f, worldZ * .35f) * BumpAmplitude
                          * Mathf.Clamp01(beyond / BumpFadeIn);
            return foot + plateau + bumps + Relief(beyond, elevationExcess, ridge, appearance);
        }

        /// <summary>
        /// Massif swell plus ridges, faded in behind the brink and capped to <see cref="MaximumReliefSlope"/>
        /// so it can only climb as fast as the distance from the edge allows.
        /// </summary>
        private static float Relief(float beyond, float elevationExcess, float ridge, LandformAppearance appearance)
        {
            if (beyond <= 0f)
            {
                return 0f;
            }

            float excess = Mathf.Clamp(elevationExcess, 0f, MaximumElevationExcess);
            float swell = appearance.MassifRise * excess;
            float ridges = appearance.RidgeHeight * ridge;

            // The swell needs no fade: the elevation above the mountain level is already zero on the border.
            float fade = Smootherstep(Mathf.Clamp01(beyond / ReliefFadeIn));
            // A hard min leaves a crease where the cap lets go, which the exaggerated lighting draws as a line.
            return Mathf.Max(0f, SmoothMin(swell + ridges * fade, beyond * MaximumReliefSlope, CapSoftening));
        }

        /// <summary>Polynomial smooth minimum: equal to min() away from the crossing, rounded within k of it.</summary>
        private static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(.5f + .5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        /// <summary>
        /// How cliff-like the foot is at a point on the edge, from 0 (scree) to 1 (rock cliff). Mostly one or
        /// the other, with short transitions, so the outline alternates between faces and slopes.
        /// </summary>
        public static float FootSteepness(float worldX, float worldZ, LandformAppearance appearance)
        {
            return appearance == null ? 1f : FootSteepness(worldX, worldZ, RidgeAt(worldX, worldZ, appearance));
        }

        /// <summary>Horizontal metres over which the foot climbs from the border to the brink.</summary>
        public static float FootRunAt(float worldX, float worldZ, LandformAppearance appearance)
        {
            return Mathf.Lerp(ScreeRun, CliffRun, FootSteepness(worldX, worldZ, appearance));
        }

        /// <summary>Metres the foot rises to at its brink.</summary>
        public static float FootHeightAt(float worldX, float worldZ, LandformAppearance appearance)
        {
            return CliffHeightAt(worldX, worldZ, appearance)
                   * Mathf.Lerp(ScreeHeightShare, 1f, FootSteepness(worldX, worldZ, appearance));
        }

        private static float FootSteepness(float worldX, float worldZ, float ridge)
        {
            float along = GradientNoise.Sample(FootSeed, worldX / FootFeatureSize, worldZ / FootFeatureSize);
            float raw = along + SpurSteepening * (ridge - .52f);
            float t = Mathf.Clamp01((raw + .35f) / .7f);
            return t * t * (3f - 2f * t);
        }

        private static float RidgeAt(float worldX, float worldZ, LandformAppearance appearance)
        {
            return appearance.RidgeHeight > 0f
                ? RidgedMultifractal(worldX / appearance.RidgeSpacing, worldZ / appearance.RidgeSpacing)
                : 0f;
        }

        /// <summary>
        /// Crest lines with branching side ridges, in [0, 1]. Each octave is weighted by the one above it, so
        /// fine ridges only grow on the flanks of big ones and valley floors stay smooth, the shape of an
        /// eroded range on a shaded relief map. A single ridged octave only makes rounded blobs.
        /// </summary>
        /// <remarks>
        /// Gradient noise is zero on every lattice point, so a ridged octave crests on a square grid; warping
        /// the input and turning each octave keeps those crests from lining up into straight lines.
        /// </remarks>
        private static float RidgedMultifractal(float x, float z)
        {
            float warpedX = x + RidgeWarp * GradientNoise.Sample(RidgeSeed + 16, x * .5f, z * .5f);
            float warpedZ = z + RidgeWarp * GradientNoise.Sample(RidgeSeed + 17, x * .5f, z * .5f);
            float sum = 0f;
            float amplitude = 1f;
            float total = 0f;
            float weight = 1f;
            float frequency = 1f;
            for (int octave = 0; octave < RidgeOctaves; octave++)
            {
                float angle = RidgeOctaveTurn * (octave + 1);
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                float octaveX = (warpedX * cos - warpedZ * sin) * frequency + octave * 17.3f;
                float octaveZ = (warpedX * sin + warpedZ * cos) * frequency - octave * 9.1f;
                float crest = GradientNoise.Ridged(RidgeSeed + octave, octaveX, octaveZ);
                crest *= crest * weight;
                weight = Mathf.Clamp01(crest * 2f);
                sum += crest * amplitude;
                total += amplitude;
                amplitude *= .5f;
                frequency *= 2.1f;
            }

            return sum / total;
        }

        /// <summary>
        /// Cliff height along the border. It wanders slowly, so neighbouring stretches of one wall differ,
        /// and a finer octave breaks the lip so it never runs as one smooth line.
        /// </summary>
        public static float CliffHeightAt(float worldX, float worldZ, LandformAppearance appearance)
        {
            float broad = GradientNoise.Sample(CliffSeed, worldX * .07f, worldZ * .07f);
            float ragged = GradientNoise.Sample(CliffSeed + 1, worldX * .35f, worldZ * .35f);
            // Normalised so the variation setting still bounds the height.
            float wander = (broad + LipRaggedness * ragged) / (1f + LipRaggedness);
            return appearance.CliffHeight * (1f + appearance.CliffHeightVariation * wander);
        }

        /// <summary>
        /// Offset, in metres, added to the distance from the border so the drawn foot wanders a little
        /// around the smooth collision outline instead of tracing it exactly.
        /// </summary>
        public static float BorderRoughness(float worldX, float worldZ, LandformAppearance appearance)
        {
            if (appearance == null || appearance.EdgeRoughness <= 0f)
            {
                return 0f;
            }

            float coarse = GradientNoise.Sample(RoughnessSeed, worldX * .45f, worldZ * .45f);
            float fine = GradientNoise.Sample(RoughnessSeed + 1, worldX * 1.7f, worldZ * 1.7f) * .3f;
            return (coarse + fine) * appearance.EdgeRoughness;
        }

        private static float Smootherstep(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
