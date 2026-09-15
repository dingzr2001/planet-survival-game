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
    /// The value distribution is bell-shaped rather than flat, so coverage is converted through measured
    /// quantiles instead of pretending a raw threshold is a percentage.
    /// </remarks>
    public static class ClusteredNoiseField
    {
        // Raw field values are not uniformly distributed, so (1 - coverage) is not a usable threshold.
        // These quantiles were measured from 500,000 deterministic, decorrelated field samples. Keeping
        // the conversion here means every clustered feature speaks in an intuitive fraction of ground
        // rather than each feature author having to rediscover noise-specific threshold values.
        private static readonly float[] ThresholdsByCoveragePercent =
        {
            1.000000f, .849942f, .818980f, .797379f, .781062f, .767013f, .754948f, .744136f,
            .734490f, .725517f, .716864f, .708839f, .701217f, .693880f, .686909f, .679986f,
            .673562f, .667326f, .661135f, .654988f, .649061f, .643224f, .637645f, .632125f,
            .626602f, .621189f, .615917f, .610593f, .605326f, .600146f, .595036f, .589901f,
            .584898f, .579913f, .575074f, .570155f, .565340f, .560541f, .555735f, .551036f,
            .546342f, .541682f, .537067f, .532298f, .527746f, .523131f, .518499f, .513777f,
            .509144f, .504520f, .499812f, .495218f, .490571f, .486022f, .481425f, .476791f,
            .472141f, .467656f, .463056f, .458501f, .453743f, .449102f, .444387f, .439630f,
            .434813f, .429994f, .425153f, .420285f, .415374f, .410266f, .405341f, .400152f,
            .394860f, .389748f, .384448f, .379051f, .373573f, .368191f, .362522f, .356904f,
            .351088f, .345229f, .339243f, .332920f, .326562f, .320179f, .313682f, .306648f,
            .299216f, .291763f, .283654f, .275087f, .265960f, .256190f, .245467f, .233048f,
            .219359f, .203097f, .181860f, .150088f, 0f
        };

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

        /// <summary>
        /// Converts a desired fraction of covered ground to the raw value this field must reach. The
        /// result is statistical rather than a quota: a finite region can vary, which is what lets the
        /// generated patches remain organic and deterministic.
        /// </summary>
        public static float ThresholdForCoverage(float coverage)
        {
            float scaledCoverage = Mathf.Clamp01(coverage) * (ThresholdsByCoveragePercent.Length - 1);
            int lowerIndex = Mathf.FloorToInt(scaledCoverage);
            if (lowerIndex >= ThresholdsByCoveragePercent.Length - 1)
            {
                return ThresholdsByCoveragePercent[^1];
            }

            return Mathf.Lerp(ThresholdsByCoveragePercent[lowerIndex],
                ThresholdsByCoveragePercent[lowerIndex + 1], scaledCoverage - lowerIndex);
        }

        /// <summary>Inverse of <see cref="ThresholdForCoverage"/>, used to migrate old threshold assets.</summary>
        public static float CoverageForThreshold(float threshold)
        {
            float clampedThreshold = Mathf.Clamp01(threshold);
            int low = 0;
            int high = ThresholdsByCoveragePercent.Length - 1;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (ThresholdsByCoveragePercent[middle] >= clampedThreshold)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            float highThreshold = ThresholdsByCoveragePercent[low];
            float lowThreshold = ThresholdsByCoveragePercent[high];
            float fraction = Mathf.Approximately(highThreshold, lowThreshold)
                ? 0f
                : Mathf.InverseLerp(highThreshold, lowThreshold, clampedThreshold);
            return (low + fraction) / (ThresholdsByCoveragePercent.Length - 1);
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
