using UnityEngine;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// A deterministic field over the XZ plane returning values in [0, 1]. Two points closer together than
    /// the feature size sample to similar values, which is the whole point: thresholding this field carves
    /// out connected patches instead of isolated speckles, so anything placed through it generates in
    /// runs rather than one cell at a time.
    /// </summary>
    /// <remarks>
    /// Nothing here knows about terrain. Any later feature that should appear in blobs — a mineral seam, a
    /// dust field, a crater cluster — can threshold the same field with its own seed and feature size.
    /// The value distribution is bell-shaped rather than flat, so a threshold is not a coverage fraction;
    /// <c>TerrainPatchLayerTests</c> records what the thresholds actually used in the game cover.
    /// </remarks>
    public static class ClusteredNoiseField
    {
        // A second octave at half the feature size, mixed in weakly. It ruffles patch borders so they do
        // not read as smooth ovals, while staying too quiet to break one patch into disconnected pieces.
        private const float DetailWeight = .3f;

        // Decorrelates the detail octave from the coarse one; two octaves off the same seed would share
        // their lattice values and reinforce each other instead of adding shape.
        private const int DetailSeedMix = 0x27d4eb2d;

        public static float Sample(int seed, float featureSize, float worldX, float worldZ)
        {
            float safeFeatureSize = Mathf.Max(.01f, featureSize);
            float coarse = SampleOctave(seed, safeFeatureSize, worldX, worldZ);
            float detail = SampleOctave(seed ^ DetailSeedMix, safeFeatureSize * .5f, worldX, worldZ);
            return Mathf.Clamp01(Mathf.Lerp(coarse, detail, DetailWeight));
        }

        private static float SampleOctave(int seed, float featureSize, float worldX, float worldZ)
        {
            float u = worldX / featureSize;
            float v = worldZ / featureSize;
            int cellX = Mathf.FloorToInt(u);
            int cellZ = Mathf.FloorToInt(v);
            float fadeX = Fade(u - cellX);
            float fadeZ = Fade(v - cellZ);

            float lowerEdge = Mathf.Lerp(
                LatticeValue(seed, cellX, cellZ),
                LatticeValue(seed, cellX + 1, cellZ),
                fadeX);
            float upperEdge = Mathf.Lerp(
                LatticeValue(seed, cellX, cellZ + 1),
                LatticeValue(seed, cellX + 1, cellZ + 1),
                fadeX);
            return Mathf.Lerp(lowerEdge, upperEdge, fadeZ);
        }

        /// <summary>Smoothstep. Flattening the interpolation at the lattice lines hides the square grid.</summary>
        private static float Fade(float t) => t * t * (3f - 2f * t);

        /// <summary>
        /// The field's value at one lattice corner. FNV-1a followed by an avalanche step, so neighbouring
        /// corners share no structure and the patches do not line up along world axes.
        /// </summary>
        private static float LatticeValue(int seed, int cellX, int cellZ)
        {
            unchecked
            {
                const uint prime = 16777619u;
                uint hash = 2166136261u;
                hash = (hash ^ (uint)seed) * prime;
                hash = (hash ^ (uint)cellX) * prime;
                hash = (hash ^ (uint)cellZ) * prime;
                hash ^= hash >> 13;
                hash *= 0x85ebca6bu;
                hash ^= hash >> 16;
                return hash / (float)uint.MaxValue;
            }
        }
    }
}
