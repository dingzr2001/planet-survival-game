using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace PlanetSurvival.World.Ground
{
    /// <summary>
    /// One clustered terrain type and where it appears. The layer thresholds its own
    /// <see cref="ClusteredNoiseField"/>, which is what makes its tiles come out as connected patches
    /// rather than scattered singles. Adding a terrain type is one more entry in
    /// <see cref="TerrainPatchSettings"/>; no generation code changes.
    /// </summary>
    [Serializable]
    public struct TerrainPatchLayer
    {
        // Spacing of the finite difference used to measure the field's slope. Wide enough that the
        // sampled difference is not lost in float noise, narrow next to the smallest patch size.
        private const float GradientStep = .4f;

        [SerializeField] private TerrainSurfaceDefinition _surface;
        [SerializeField, Min(1f), Tooltip("World units across one patch of this terrain. Larger values make fewer, wider patches.")]
        private float _patchSize;
        [SerializeField, Range(0f, 1f), Tooltip("Approximate fraction of ground this terrain may cover before overlaps. For example, 0.02 is very rare and 0.2 is common.")]
        private float _targetCoverage;
        [SerializeField, Tooltip("Offsets this layer's field from the other layers. Two layers sharing an offset and patch size sample the same field, which nests them into rings instead of scattering them independently.")]
        private int _seedOffset;

        // Assets authored before target coverage existed keep their exact layout until OnValidate migrates
        // the old threshold to its equivalent coverage. Runtime fallback also makes an unmigrated asset safe.
        [FormerlySerializedAs("_threshold"), SerializeField, HideInInspector]
        private float _legacyThreshold;
        [SerializeField, HideInInspector] private bool _usesTargetCoverage;

        public TerrainPatchLayer(TerrainSurfaceDefinition surface, float patchSize, float targetCoverage,
            int seedOffset)
        {
            _surface = surface;
            _patchSize = patchSize;
            _targetCoverage = Mathf.Clamp01(targetCoverage);
            _seedOffset = seedOffset;
            _legacyThreshold = 0f;
            _usesTargetCoverage = true;
        }

        public TerrainSurfaceDefinition Surface => _surface;
        public float PatchSize => Mathf.Max(1f, _patchSize);
        public float TargetCoverage => _usesTargetCoverage
            ? Mathf.Clamp01(_targetCoverage)
            : ClusteredNoiseField.CoverageForThreshold(_legacyThreshold);
        public float Threshold => _usesTargetCoverage
            ? ClusteredNoiseField.ThresholdForCoverage(_targetCoverage)
            : Mathf.Clamp01(_legacyThreshold);
        public int SeedOffset => _seedOffset;

        internal bool UpgradeLegacyCoverage()
        {
            if (_usesTargetCoverage)
            {
                return false;
            }

            _targetCoverage = ClusteredNoiseField.CoverageForThreshold(_legacyThreshold);
            _usesTargetCoverage = true;
            return true;
        }

        /// <summary>Whether this layer reaches the given point. A layer without a surface covers nothing.</summary>
        public bool Covers(int worldSeed, float worldX, float worldZ)
        {
            return _surface != null
                   && ClusteredNoiseField.Sample(worldSeed ^ _seedOffset, PatchSize, worldX, worldZ)
                   >= Threshold;
        }

        /// <summary>
        /// Roughly how many metres inside its own patch a point lies, negative outside it. The field
        /// value alone has no unit, so it is divided by how fast the field is changing there; that turns
        /// "how far above the threshold" into a distance the presentation can feather over a fixed width.
        /// </summary>
        public float SignedDistanceToEdge(int worldSeed, float worldX, float worldZ)
        {
            if (_surface == null)
            {
                return float.NegativeInfinity;
            }

            int seed = worldSeed ^ _seedOffset;
            float patchSize = PatchSize;
            float depth = ClusteredNoiseField.Sample(seed, patchSize, worldX, worldZ) - Threshold;
            float slopeX = ClusteredNoiseField.Sample(seed, patchSize, worldX + GradientStep, worldZ)
                           - ClusteredNoiseField.Sample(seed, patchSize, worldX - GradientStep, worldZ);
            float slopeZ = ClusteredNoiseField.Sample(seed, patchSize, worldX, worldZ + GradientStep)
                           - ClusteredNoiseField.Sample(seed, patchSize, worldX, worldZ - GradientStep);
            float slope = Mathf.Sqrt(slopeX * slopeX + slopeZ * slopeZ) / (2f * GradientStep);
            if (slope < 1e-6f)
            {
                // A flat spot in the field is nowhere near an edge either way.
                return depth >= 0f ? float.PositiveInfinity : float.NegativeInfinity;
            }

            return depth / slope;
        }
    }
}
