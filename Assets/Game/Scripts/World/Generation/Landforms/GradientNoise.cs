using UnityEngine;

namespace PlanetSurvival.World.Generation.Landforms
{
    /// <summary>
    /// Seeded 2D gradient (Perlin-style) noise returning roughly [-1, 1]. Value noise, as used by the
    /// terrain patches, is fine for blobs, but ridges and long mountain chains need the zero crossings
    /// that only a signed gradient field has. <c>Mathf.PerlinNoise</c> is avoided because it cannot be
    /// seeded and mirrors itself around the origin, which would repeat terrain on the other side of spawn.
    /// </summary>
    public static class GradientNoise
    {
        // A quintic-faded gradient field with unit gradients peaks at about ±0.7; scaling by √2 brings the
        // usable range close to ±1 so thresholds read naturally.
        private const float OutputScale = 1.41421356f;

        // Octaves are decorrelated by mixing their index into the seed, otherwise every octave would share
        // one lattice and reinforce rather than add detail.
        private const int OctaveSeedMix = 0x5bd1e995;

        private static readonly float[] GradientX = { 1f, -1f, 0f, 0f, .70710678f, -.70710678f, .70710678f, -.70710678f };
        private static readonly float[] GradientZ = { 0f, 0f, 1f, -1f, .70710678f, .70710678f, -.70710678f, -.70710678f };

        /// <summary>One octave at unit feature size; divide coordinates by the desired feature size first.</summary>
        public static float Sample(int seed, float x, float z)
        {
            int cellX = Mathf.FloorToInt(x);
            int cellZ = Mathf.FloorToInt(z);
            float localX = x - cellX;
            float localZ = z - cellZ;
            float fadeX = Fade(localX);
            float fadeZ = Fade(localZ);

            float n00 = Corner(seed, cellX, cellZ, localX, localZ);
            float n10 = Corner(seed, cellX + 1, cellZ, localX - 1f, localZ);
            float n01 = Corner(seed, cellX, cellZ + 1, localX, localZ - 1f);
            float n11 = Corner(seed, cellX + 1, cellZ + 1, localX - 1f, localZ - 1f);
            float lower = n00 + (n10 - n00) * fadeX;
            float upper = n01 + (n11 - n01) * fadeX;
            return Mathf.Clamp((lower + (upper - lower) * fadeZ) * OutputScale, -1f, 1f);
        }

        /// <summary>
        /// Fractal sum of <paramref name="octaves"/> octaves, each at half the feature size and half the
        /// weight of the one before. Normalised by the total weight so the result stays within [-1, 1].
        /// </summary>
        public static float Fbm(int seed, float x, float z, int octaves)
        {
            float sum = 0f;
            float weight = 1f;
            float totalWeight = 0f;
            float frequency = 1f;
            int safeOctaves = Mathf.Max(1, octaves);
            for (int octave = 0; octave < safeOctaves; octave++)
            {
                sum += Sample(unchecked(seed + octave * OctaveSeedMix), x * frequency, z * frequency) * weight;
                totalWeight += weight;
                weight *= .5f;
                frequency *= 2f;
            }

            return sum / totalWeight;
        }

        /// <summary>
        /// Ridged noise in [0, 1]: one along the zero crossings of the field, falling away on both sides.
        /// Zero crossings of a gradient field form long connected lines, which is what makes ridges read as
        /// mountain chains rather than round hills.
        /// </summary>
        public static float Ridged(int seed, float x, float z)
        {
            return 1f - Mathf.Abs(Sample(seed, x, z));
        }

        private static float Corner(int seed, int cellX, int cellZ, float offsetX, float offsetZ)
        {
            int gradient = (int)(Hash(seed, cellX, cellZ) & 7u);
            return GradientX[gradient] * offsetX + GradientZ[gradient] * offsetZ;
        }

        /// <summary>Quintic fade; its zero second derivative at lattice lines hides the square grid.</summary>
        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        /// <summary>FNV-1a with an avalanche step, matching the terrain patch noise so seeds behave alike.</summary>
        internal static uint Hash(int seed, int x, int z)
        {
            unchecked
            {
                const uint prime = 16777619u;
                uint hash = 2166136261u;
                hash = (hash ^ (uint)seed) * prime;
                hash = (hash ^ (uint)x) * prime;
                hash = (hash ^ (uint)z) * prime;
                hash ^= hash >> 13;
                hash *= 0x85ebca6bu;
                hash ^= hash >> 16;
                return hash;
            }
        }

        /// <summary>A deterministic value in [0, 1) for one integer address.</summary>
        internal static float Hash01(int seed, int x, int z)
        {
            return (Hash(seed, x, z) >> 8) / 16777216f;
        }
    }
}
