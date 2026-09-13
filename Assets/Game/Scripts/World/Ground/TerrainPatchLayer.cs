using System;
using UnityEngine;

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
        [SerializeField] private TerrainSurfaceDefinition _surface;
        [SerializeField, Min(1f), Tooltip("World units across one patch of this terrain. Larger values make fewer, wider patches.")]
        private float _patchSize;
        [SerializeField, Range(0f, 1f), Tooltip("Field value a tile needs to take this terrain. Lower covers more ground; 1 switches the layer off. Not a percentage: the field clusters around its middle, so small changes here move coverage a lot.")]
        private float _threshold;
        [SerializeField, Tooltip("Offsets this layer's field from the other layers. Two layers sharing an offset and patch size sample the same field, which nests them into rings instead of scattering them independently.")]
        private int _seedOffset;

        public TerrainPatchLayer(TerrainSurfaceDefinition surface, float patchSize, float threshold,
            int seedOffset)
        {
            _surface = surface;
            _patchSize = patchSize;
            _threshold = threshold;
            _seedOffset = seedOffset;
        }

        public TerrainSurfaceDefinition Surface => _surface;
        public float PatchSize => Mathf.Max(1f, _patchSize);
        public float Threshold => Mathf.Clamp01(_threshold);
        public int SeedOffset => _seedOffset;

        /// <summary>Whether this layer reaches the given point. A layer without a surface covers nothing.</summary>
        public bool Covers(int worldSeed, float worldX, float worldZ)
        {
            return _surface != null
                   && ClusteredNoiseField.Sample(worldSeed ^ _seedOffset, PatchSize, worldX, worldZ)
                   >= Threshold;
        }
    }
}
